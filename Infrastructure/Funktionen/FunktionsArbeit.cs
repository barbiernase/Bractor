using System.Collections.Concurrent;
using Abstractions;
using Microsoft.Extensions.Logging;

namespace Infrastructure.Funktionen;

/// <summary>Was mit einem Eingang geschah.</summary>
public enum EingangsAusgang
{
    /// <summary>Gewirkt: die Ausgabe ist festgehalten (bzw. gleichbleibend) und jeder Command entschieden (zugestellt oder abgelehnt).</summary>
    Fertig,
    /// <summary>Noch nicht: ein Command blieb „unbekannt“, ein anderer Lauf hält gerade fest, oder draußen kam noch keine Antwort — später wiederholen.</summary>
    Spaeter,
    /// <summary>Die Funktion warf (bzw. draußen meldete einen Fehler) — zählt gegen die Wiederholungen, danach wird der Stream geparkt.</summary>
    Fehler,
}

/// <summary>Ein Eingang wird draußen gerechnet (Laufort Hand, Worker, Client): anbieten und auf die Antwort warten.</summary>
public delegate Task BieteAn(EingangAnbieten angebot, CancellationToken ct);

/// <summary>
/// Die Arbeit an EINEM Eingang (Plan Pub/Sub-Neustrukturierung §2.2, §4.2) — neben der Mailbox des Funktions-Actors, ohne dessen Zustand:
/// <list type="number">
/// <item><b>Gleichbleibend</b> (R12) und ohne Events in der Ausgabe: rechnen und senden — dieselbe Rechnung ergibt beim Wiederholen dieselben
///   Commands mit denselben Ids, die Inbox des Ziels verwirft das Zweite.</item>
/// <item><b>Sonst festhalten</b> (A1): steht unter „F, X“ schon ein vollständiger Lauf, nur senden; sonst rechnen (ein Strom überspringt, was
///   schon festgehalten ist — Fortschritt), festhalten (ein Strom stückweise), dann aus dem Festgehaltenen senden. Hat ein anderer Lauf zuerst
///   festgehalten, gilt dessen Eintrag.</item>
/// <item><b>Draußen</b> (Laufort Hand/Worker/Client): anbieten (Vermittlung mit Lease), auf die Antwort warten — wer antwortet, hält fest; die
///   erste Antwort gewinnt am Schlüssel „F, X“. Gesendet wird immer hier: die Garantie bleibt beim Actor, der Worker ist nur die Hand.</item>
/// </list>
/// Commands gehen mit abgeleiteter Command-Id (Funktion, Eingang, Stelle, Typ, Ziel) über den einen Emit-Weg hinaus; Hinweise schickt nur der
/// gewinnende Lauf (höchstens einmal).
/// </summary>
public sealed class FunktionsArbeit
{
    /// <summary>Wie viele Ausgaben eines Stroms auf einmal festgehalten werden (danach gesendet, dann geht der Strom weiter).</summary>
    public const int StromStueck = 100;

    /// <summary>Wie lange eine Funktion draußen auf eine Antwort wartet, bevor sie nachsieht und neu anbietet.</summary>
    public static readonly TimeSpan AntwortGeduld = TimeSpan.FromSeconds(30);

    private readonly FunktionsBaustein _b;
    private readonly Func<object>? _funktion;
    private readonly IAusgabeAblage _ablage;
    private readonly Func<ICommand, Guid, Guid, CancellationToken, Task<EmitAusgang>> _sende;
    private readonly Action<IEvent, EventEnvelope>? _hinweis;
    private readonly IFaehigkeitsFabrik? _faehigkeiten;
    private readonly BieteAn? _bieteAn;
    private readonly ILogger? _logger;
    private readonly int _stueck;
    private readonly TimeSpan _geduld;
    private readonly ConcurrentDictionary<Guid, Antworten> _antworten = new();
    /// <summary>Wie viele festgehaltene Ausgaben eines Eingangs draußen schon gesendet sind (nur ein Merker gegen Doppelsenden; nach einem
    /// Neustart sendet der Actor alles noch einmal — dieselben Ids, die Inbox verwirft es).</summary>
    private readonly ConcurrentDictionary<Guid, int> _gesendet = new();

    /// <summary>Die Meldungen der Sitzung zu einem Eingang draußen: ein Stück (Strom), das Ende oder ein Fehler — ein Kanal, ein Leser.</summary>
    private sealed class Antworten
    {
        private readonly System.Threading.Channels.Channel<string?> _kanal =
            System.Threading.Channels.Channel.CreateUnbounded<string?>(new() { SingleReader = true });
        private string? _fehler;

        public string? Fehler => Volatile.Read(ref _fehler);

        public void Melde(string? fehler)
        {
            if (fehler is not null) Interlocked.CompareExchange(ref _fehler, fehler, null);
            _kanal.Writer.TryWrite(fehler);
        }

        /// <summary>Wartet auf die nächste Meldung, höchstens <paramref name="geduld"/>; liefert, ob eine kam (alle wartenden zählen als eine).</summary>
        public async Task<bool> WarteAsync(TimeSpan geduld, CancellationToken ct)
        {
            try { await _kanal.Reader.WaitToReadAsync(ct).AsTask().WaitAsync(geduld, ct); }
            catch (TimeoutException) { return false; }
            while (_kanal.Reader.TryRead(out _)) { }
            return true;
        }
    }

    /// <param name="funktion">Baut bzw. liefert die Umsetzung im Host; null = die Funktion rechnet draußen (dann <paramref name="bieteAn"/>).</param>
    /// <param name="sende">Command, Command-Id, Korrelation → Ausgang (live der <c>CommandEmitter</c>, eine Frage mit Pflicht-Frist).</param>
    /// <param name="hinweis">Einen Hinweis melden (verlierbar); null = Hinweise verfallen.</param>
    public FunktionsArbeit(FunktionsBaustein baustein, Func<object>? funktion, IAusgabeAblage ablage,
        Func<ICommand, Guid, Guid, CancellationToken, Task<EmitAusgang>> sende, Action<IEvent, EventEnvelope>? hinweis = null,
        IFaehigkeitsFabrik? faehigkeiten = null, BieteAn? bieteAn = null, ILogger? logger = null, int stueck = StromStueck, TimeSpan? antwortGeduld = null)
    {
        if (funktion is null && bieteAn is null)
            throw new ArgumentException($"{baustein.Name}: weder eine Umsetzung im Host noch ein Weg nach draußen.");
        _b = baustein;
        _funktion = funktion;
        _ablage = ablage;
        _sende = sende;
        _hinweis = hinweis;
        _faehigkeiten = faehigkeiten;
        _bieteAn = funktion is null ? bieteAn : null;
        _logger = logger;
        _stueck = Math.Max(1, stueck);
        _geduld = antwortGeduld ?? AntwortGeduld;
    }

    /// <summary>Rechnet die Funktion draußen?</summary>
    public bool Draussen => _funktion is null;

    /// <summary>Darf die Ausgabe ohne Festhalten gesendet werden (R12: gleichbleibend, im Host, keine Events in der Ausgabe)?</summary>
    public bool OhneFesthalten(FunktionsEingang eingang) =>
        _b.Gleichbleibend && !Draussen && !eingang.MitEvents;

    /// <summary>
    /// Die Antwort für den Eingang mit diesem Schlüssel liegt vor (Meldung der Sitzung; <paramref name="fehler"/> = draußen gescheitert,
    /// <paramref name="teil"/> = ein Stück eines Stroms, es kommt noch mehr).
    /// </summary>
    public void Beantwortet(Guid schluessel, string? fehler, bool teil = false)
    {
        if (_antworten.TryGetValue(schluessel, out var a)) a.Melde(fehler);
    }

    public static FunktionsEingabe EingabeVon(FunktionsBaustein b, EventEnvelope e) =>
        new(b.Name, e.AggregateId, e.AggregateVersion, e.CorrelationId, e.UserId);

    public async Task<EingangsAusgang> VerarbeiteAsync(EventEnvelope e, CancellationToken ct)
    {
        var eingang = _b.Eingang(e.Payload.GetType())
            ?? throw new InvalidOperationException($"{_b.Name} hört {e.Payload.GetType().Name} nicht.");
        var eingabe = EingabeVon(_b, e);
        // Spur: Ursache ist der Eingang, Wirker die Funktion; der Akteur der Kette reist mit (docs/konzept-akteure.md §5.1).
        using var spur = Spur.Von(Spur.Ursache(e.AggregateId, e.AggregateVersion), _b.Wirker);
        using var akteur = Infrastructure.Akteure.AkteurHerkunft.Aus(e.UserId);

        if (OhneFesthalten(eingang))
        {
            var ausgaben = new List<IPipelineOutput>();
            await foreach (var a in Rechne(e, ct)) ausgaben.Add(a);
            foreach (var h in ausgaben.OfType<ITransientEvent>()) _hinweis?.Invoke(h, e);
            return await SendeAsync(eingabe, ausgaben.Where(a => a is not ITransientEvent).ToList(), 0, ct);
        }

        var fest = await _ablage.LiesAsync(eingabe, ct);
        if (!fest.Abgeschlossen)
        {
            if (Draussen) return await DraussenAsync(e, eingabe, ct);
            var lauf = await RechneUndHalteFestAsync(e, eingabe, eingang, fest, ct);
            if (lauf is null) return EingangsAusgang.Spaeter;   // ein anderer Lauf hält gerade fest — später aus seinem Eintrag senden
            return lauf.Value;
        }
        return await SendeAsync(eingabe, fest.Ausgaben, 0, ct);
    }

    /// <summary>Die Funktion rechnen (im Host; Lese-Fähigkeiten je Aufruf aus einem eigenen Bereich).</summary>
    private async IAsyncEnumerable<IPipelineOutput> Rechne(EventEnvelope e, [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken ct)
    {
        var eingang = _b.Eingang(e.Payload.GetType())!;
        using var bereich = eingang.Faehigkeiten.Count > 0
            ? (_faehigkeiten ?? throw new InvalidOperationException($"{_b.Name} liest Fähigkeiten, aber es gibt keine Fähigkeits-Fabrik.")).Oeffne()
            : null;
        var herkunft = new Herkunft(e.AggregateId, e.AggregateVersion, e.CreatedAtUtc, ImAuftrag.IstAkteur(e.UserId) ? e.UserId : null, _b.Wirker);
        await foreach (var a in _b.Rufe(_funktion!(), e.Payload, herkunft, bereich, ct).WithCancellation(ct)) yield return a;
    }

    /// <summary>
    /// Rechnen und festhalten. Ein Strom überspringt, was schon festgehalten ist (Fortschritt), hält stückweise fest und sendet jedes Stück, bevor
    /// er weiterrechnet. Null: ein anderer Lauf hat zwischendurch festgehalten und ist noch nicht fertig.
    /// </summary>
    private async Task<EingangsAusgang?> RechneUndHalteFestAsync(EventEnvelope e, FunktionsEingabe eingabe, FunktionsEingang eingang, Festgehalten fest,
        CancellationToken ct)
    {
        var schon = fest.Ausgaben.Count;    // Fortschritt: so viele Ausgaben sind festgehalten
        var entschieden = true;
        // Was ein abgebrochener Lauf schon festgehalten hatte, ist vielleicht nie gesendet worden — einmal senden (dieselben Ids, die Inbox verwirft).
        if (schon > 0) entschieden &= await SendeAsync(eingabe, fest.Ausgaben, 0, ct) == EingangsAusgang.Fertig;
        var stueck = new List<IPipelineOutput>();
        var hinweise = new List<IEvent>();
        var gezaehlt = 0;
        await foreach (var a in Rechne(e, ct))
        {
            if (a is ITransientEvent h)
            {
                if (gezaehlt >= schon) hinweise.Add(h);   // Hinweise aus dem schon festgehaltenen Teil gingen schon (oder verfielen)
                continue;
            }
            if (gezaehlt++ < schon) continue;
            stueck.Add(a);
            if (!eingang.Strom || stueck.Count < _stueck) continue;
            if (!await _ablage.HalteFestAsync(eingabe, schon, stueck, abgeschlossen: false, ct)) return await NachKonfliktAsync(eingabe, ct);
            // Das Festgehaltene senden, bevor der Strom weiterrechnet — der Rückstau eines großen Imports liegt im Log, nicht im Speicher.
            entschieden &= await SendeAsync(eingabe, stueck, schon, ct) == EingangsAusgang.Fertig;
            schon += stueck.Count;
            stueck = new List<IPipelineOutput>();
            foreach (var hw in hinweise) _hinweis?.Invoke(hw, e);
            hinweise.Clear();
        }
        if (!await _ablage.HalteFestAsync(eingabe, schon, stueck, abgeschlossen: true, ct)) return await NachKonfliktAsync(eingabe, ct);
        foreach (var hw in hinweise) _hinweis?.Invoke(hw, e);
        entschieden &= await SendeAsync(eingabe, stueck, schon, ct) == EingangsAusgang.Fertig;
        return entschieden ? EingangsAusgang.Fertig : EingangsAusgang.Spaeter;
    }

    /// <summary>Ein anderer Lauf hat zuerst festgehalten: ist er fertig, aus seinem Eintrag senden; sonst später.</summary>
    private async Task<EingangsAusgang?> NachKonfliktAsync(FunktionsEingabe eingabe, CancellationToken ct)
    {
        var fest = await _ablage.LiesAsync(eingabe, ct);
        _logger?.LogInformation("[Funktion] {Funktion}: Eingang {Stream}:{Version} hat ein anderer Lauf zuerst festgehalten{Fertig}",
            eingabe.Funktion, eingabe.Stream, eingabe.Version, fest.Abgeschlossen ? " — sein Eintrag gilt" : " (noch nicht fertig)");
        return fest.Abgeschlossen ? await SendeAsync(eingabe, fest.Ausgaben, 0, ct) : null;
    }

    /// <summary>
    /// Draußen: anbieten und auf die Antwort warten; jedes festgehaltene Stück (Strom) sofort senden, bevor das nächste kommt. Kommt in der Geduld
    /// nichts, heißt der Eingang „später“ — der Actor bietet ihn nach dem Backoff erneut an (beim Vermittler, der die Lease eines laufenden Workers
    /// kennt, ein Nichts), und die Arbeit sendet weiter, was inzwischen festgehalten ist.
    /// </summary>
    private async Task<EingangsAusgang> DraussenAsync(EventEnvelope e, FunktionsEingabe eingabe, CancellationToken ct)
    {
        var antworten = _antworten.GetOrAdd(eingabe.Schluessel, _ => new Antworten());
        try
        {
            await _bieteAn!(new EingangAnbieten(eingabe.Schluessel, _b.Name, e.Payload, e.AggregateId, e.AggregateVersion, e.CreatedAtUtc,
                e.CorrelationId, ImAuftrag.IstAkteur(e.UserId) ? e.UserId : null, Spur.Aktuell?.Ursache, Spur.Aktuell?.Wirker), ct);
            while (true)
            {
                var gemeldet = await antworten.WarteAsync(_geduld, ct);   // die Meldung der Sitzung ist verlierbar: ohne sie nachsehen
                if (antworten.Fehler is { } fehler)
                {
                    _logger?.LogWarning("[Funktion] {Funktion}: draußen gescheitert für {Stream}:{Version}: {Fehler}", _b.Name, e.AggregateId, e.AggregateVersion, fehler);
                    return EingangsAusgang.Fehler;
                }
                var fest = await _ablage.LiesAsync(eingabe, ct);
                var schon = _gesendet.GetValueOrDefault(eingabe.Schluessel);
                if (fest.Ausgaben.Count > schon)
                {
                    if (await SendeAsync(eingabe, fest.Ausgaben.Skip(schon).ToList(), schon, ct) != EingangsAusgang.Fertig) return EingangsAusgang.Spaeter;
                    _gesendet[eingabe.Schluessel] = fest.Ausgaben.Count;
                }
                if (fest.Abgeschlossen)
                {
                    _gesendet.TryRemove(eingabe.Schluessel, out _);
                    return EingangsAusgang.Fertig;
                }
                if (!gemeldet) return EingangsAusgang.Spaeter;
            }
        }
        finally
        {
            _antworten.TryRemove(eingabe.Schluessel, out _);
        }
    }

    /// <summary>
    /// Sendet die Commands unter <paramref name="ausgaben"/> (ab der Stelle <paramref name="ab"/>) gemeinsam hinaus — je mit der abgeleiteten Id
    /// ihrer Stelle; Fertig, wenn jeder entschieden ist (zugestellt oder abgelehnt).
    /// </summary>
    private async Task<EingangsAusgang> SendeAsync(FunktionsEingabe eingabe, IReadOnlyList<IPipelineOutput> ausgaben, int ab, CancellationToken ct)
    {
        var korrelation = Guid.TryParse(eingabe.Korrelation, out var k) ? k : eingabe.Stream;
        var sends = new List<Task<EmitAusgang>>();
        for (var i = 0; i < ausgaben.Count; i++)
            if (ausgaben[i] is ICommand c)
                sends.Add(_sende(c, FunktionsIds.Befehl(eingabe.Funktion, eingabe.Stream, eingabe.Version, ab + i, c), korrelation, ct));
        if (sends.Count == 0) return EingangsAusgang.Fertig;
        var ausgaenge = await Task.WhenAll(sends);
        return ausgaenge.All(a => a.IstEntschieden()) ? EingangsAusgang.Fertig : EingangsAusgang.Spaeter;
    }
}
