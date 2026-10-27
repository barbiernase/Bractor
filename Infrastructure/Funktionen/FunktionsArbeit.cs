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

/// <summary>
/// Die Rechnung einer Funktion mit Zustand an EINEM Eingang in dieser Aktivierung (Plan §2.9, A5): sobald <c>Auf</c> auf dem Zustand gelaufen ist,
/// stehen hier seine Ausgaben — ein Wiederholen (Command „unbekannt“, Festhalten gescheitert) rechnet nicht noch einmal, sonst wirkte der Eingang
/// zweimal auf den Zustand. Der Funktions-Actor hält sie, bis der Eingang fertig ist.
/// </summary>
public sealed class EingangsRechnung
{
    /// <summary>Die Ausgaben des einen Laufs von <c>Auf</c> auf dem Zustand; null = noch nicht gerechnet.</summary>
    public IReadOnlyList<IAusgabe>? Ausgaben { get; internal set; }

    /// <summary>Nur ein Hinweis-Eingang: seine Hinweise sind schon weiter (ein Wiederholen schickt sie nicht noch einmal).</summary>
    internal bool HinweiseGesendet { get; set; }
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
    private readonly Action<HinweisZustellen>? _hinweis;
    private readonly IFaehigkeitsFabrik? _faehigkeiten;
    private readonly BieteAn? _bieteAn;
    private readonly ILogger? _logger;
    private readonly int _partitionen;
    private readonly int _stueck;
    private readonly TimeSpan _geduld;
    private readonly ConcurrentDictionary<Guid, Antworten> _antworten = new();
    /// <summary>Wie viele festgehaltene Ausgaben eines Eingangs draußen schon gesendet sind (nur ein Merker gegen Doppelsenden; nach einem
    /// Neustart sendet der Actor alles noch einmal — dieselben Ids, die Inbox verwirft es).</summary>
    private readonly ConcurrentDictionary<Guid, int> _gesendet = new();

    /// <summary>
    /// Die Meldungen der Sitzung zu einem Eingang draußen: ein Stück (Strom), das Ende oder ein Fehler — ein Kanal, ein Leser. Für einen Hinweis als
    /// Eingang trägt die Meldung die Ausgaben selbst (nichts ist festgehalten).
    /// </summary>
    private sealed class Antworten
    {
        private readonly System.Threading.Channels.Channel<string?> _kanal =
            System.Threading.Channels.Channel.CreateUnbounded<string?>(new() { SingleReader = true });
        private string? _fehler;
        private IReadOnlyList<IAusgabe>? _ausgaben;

        public string? Fehler => Volatile.Read(ref _fehler);
        public IReadOnlyList<IAusgabe>? Ausgaben => Volatile.Read(ref _ausgaben);

        public void Melde(string? fehler, IReadOnlyList<IAusgabe>? ausgaben = null)
        {
            if (fehler is not null) Interlocked.CompareExchange(ref _fehler, fehler, null);
            if (ausgaben is not null) Interlocked.CompareExchange(ref _ausgaben, ausgaben, null);
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
    /// <param name="hinweis">Einen Hinweis weitergeben (verlierbar: das Hinweis-Netz und die Clients); null = Hinweise verfallen.</param>
    public FunktionsArbeit(FunktionsBaustein baustein, Func<object>? funktion, IAusgabeAblage ablage,
        Func<ICommand, Guid, Guid, CancellationToken, Task<EmitAusgang>> sende, Action<HinweisZustellen>? hinweis = null,
        IFaehigkeitsFabrik? faehigkeiten = null, BieteAn? bieteAn = null, ILogger? logger = null, int stueck = StromStueck, TimeSpan? antwortGeduld = null,
        int partitionen = 0)
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
        _partitionen = partitionen > 0 ? partitionen : Infrastructure.Projections.Konsum.Standard.Partitionen;
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
    public void Beantwortet(Guid schluessel, string? fehler, bool teil = false, IReadOnlyList<IAusgabe>? ausgaben = null)
    {
        if (_antworten.TryGetValue(schluessel, out var a)) a.Melde(fehler, ausgaben);
    }

    public static FunktionsEingabe EingabeVon(FunktionsBaustein b, EventEnvelope e) =>
        new(b.Name, e.AggregateId, e.AggregateVersion, e.CorrelationId, e.UserId);

    /// <param name="rechnung">Nur eine Funktion mit Zustand: die Rechnung dieses Eingangs in dieser Aktivierung (<see cref="EingangsRechnung"/>).</param>
    public async Task<EingangsAusgang> VerarbeiteAsync(EventEnvelope e, CancellationToken ct, EingangsRechnung? rechnung = null)
    {
        var eingang = _b.Eingang(e.Payload.GetType())
            ?? throw new InvalidOperationException($"{_b.Name} hört {e.Payload.GetType().Name} nicht.");
        var eingabe = EingabeVon(_b, e);
        // Spur: Ursache ist der Eingang, Wirker die Funktion; der Akteur der Kette reist mit (docs/konzept-akteure.md §5.1).
        using var spur = Spur.Von(Spur.Ursache(e.AggregateId, e.AggregateVersion), _b.Wirker);
        using var akteur = Infrastructure.Akteure.AkteurHerkunft.Aus(e.UserId);

        if (rechnung is not null) return await MitZustandAsync(e, eingabe, rechnung, ct);

        if (OhneFesthalten(eingang))
        {
            var ausgaben = new List<IAusgabe>();
            await foreach (var a in Rechne(e, ct)) ausgaben.Add(a);
            MeldeHinweise(ausgaben, e);
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

    /// <summary>
    /// Eine Funktion mit Zustand (Plan §2.9, A5): <c>Auf</c> läuft je Eingang und Aktivierung GENAU EINMAL auf dem Zustand — auch wenn die Ausgabe
    /// schon festgehalten ist (dann rechnet die Aktivierung nach einem Absturz den Eingang auf dem gesicherten Zustand nach, gesendet wird das
    /// Festgehaltene, Hinweise nicht noch einmal). Sonst wird der Rest der Ausgabe festgehalten (was ein abgebrochener Lauf schon festhielt, gilt)
    /// und gesendet; die Hinweise schickt nur, wer den Lauf abschließt.
    /// </summary>
    private async Task<EingangsAusgang> MitZustandAsync(EventEnvelope e, FunktionsEingabe eingabe, EingangsRechnung rechnung, CancellationToken ct)
    {
        var fest = await _ablage.LiesAsync(eingabe, ct);
        if (rechnung.Ausgaben is null)
        {
            var liste = new List<IAusgabe>();
            await foreach (var a in Rechne(e, ct)) liste.Add(a);
            rechnung.Ausgaben = liste;   // ab hier wirkt der Eingang auf den Zustand — ein Wiederholen rechnet nicht noch einmal
        }
        if (fest.Abgeschlossen) return await SendeAsync(eingabe, fest.Ausgaben, 0, ct);
        // Nichts ausgegeben (die Funktion hat nur gelernt): nichts festzuhalten, nichts zu senden — nach einem Absturz rechnet die Aktivierung den
        //   Eingang ohnehin auf dem gesicherten Zustand nach. (Mit einem Hinweis hielte sie fest: sonst ginge er beim Nachrechnen ein zweites Mal hinaus.)
        if (rechnung.Ausgaben.Count == 0 && fest.Ausgaben.Count == 0) return EingangsAusgang.Fertig;
        var ohneHinweise = rechnung.Ausgaben.Where(a => a is not ITransientEvent).ToList();
        var schon = fest.Ausgaben.Count;
        if (!await _ablage.HalteFestAsync(eingabe, schon, ohneHinweise.Skip(schon).ToList(), abgeschlossen: true, ct))
            return await NachKonfliktAsync(eingabe, ct) ?? EingangsAusgang.Spaeter;
        MeldeHinweise(rechnung.Ausgaben, e);
        var gesendet = await SendeAsync(eingabe, fest.Ausgaben, 0, ct);   // was ein abgebrochener Lauf festhielt, ist vielleicht nie gesendet worden
        var rest = await SendeAsync(eingabe, ohneHinweise.Skip(schon).ToList(), schon, ct);
        return gesendet == EingangsAusgang.Fertig && rest == EingangsAusgang.Fertig ? EingangsAusgang.Fertig : EingangsAusgang.Spaeter;
    }

    /// <summary>Die Funktion zu einem Log-Eingang rechnen.</summary>
    private IAsyncEnumerable<IAusgabe> Rechne(EventEnvelope e, CancellationToken ct) =>
        Rechne(e.Payload, new Herkunft(e.AggregateId, e.AggregateVersion, e.CreatedAtUtc, ImAuftrag.IstAkteur(e.UserId) ? e.UserId : null, _b.Wirker), ct);

    /// <summary>Die Funktion rechnen (im Host; Lese-Fähigkeiten je Aufruf aus einem eigenen Bereich).</summary>
    private async IAsyncEnumerable<IAusgabe> Rechne(IEvent eingabe, Herkunft herkunft, [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken ct)
    {
        var eingang = _b.Eingang(eingabe.GetType())!;
        using var bereich = eingang.Faehigkeiten.Count > 0
            ? (_faehigkeiten ?? throw new InvalidOperationException($"{_b.Name} liest Fähigkeiten, aber es gibt keine Fähigkeits-Fabrik.")).Oeffne()
            : null;
        await foreach (var a in _b.Rufe(_funktion!(), eingabe, herkunft, bereich, ct).WithCancellation(ct)) yield return a;
    }

    /// <summary>
    /// Die Hinweise einer Ausgabe zum Log-Eingang <paramref name="e"/> weitergeben — je mit der Kennung aus Funktion, Eingang und ihrer Stelle unter
    /// den Hinweisen (A7; dasselbe Nachrechnen ergibt dieselbe Kennung). Der Herkunfts-Stream ist der des Eingangs (er bestimmt die Adresse, A6).
    /// </summary>
    private void MeldeHinweise(IEnumerable<IAusgabe> ausgaben, EventEnvelope e, int ab = 0)
    {
        if (_hinweis is null) return;
        var nr = ab;
        foreach (var h in ausgaben.OfType<ITransientEvent>())
            _hinweis(new HinweisZustellen(FunktionsIds.Hinweis(_b.Name, e.AggregateId, e.AggregateVersion, nr++), h, e.AggregateId, e.AggregateVersion,
                e.CreatedAtUtc, e.CorrelationId, ImAuftrag.IstAkteur(e.UserId) ? e.UserId : null, Spur.Ursache(e.AggregateId, e.AggregateVersion)));
    }

    /// <summary>
    /// Ein HINWEIS-Eingang (Plan §2.9, E7, A7): höchstens einmal — keine Position, nichts festgehalten. Die Rechnung hält die Ausgabe des einen Laufs
    /// (ein Wiederholen nach „unbekannt“ rechnet nicht noch einmal und schickt die Hinweise nicht noch einmal); Commands gehen mit der Id aus Funktion,
    /// Hinweis-Kennung, Stelle, Typ und Ziel in den Aggregat-Mechanismus, Hinweise mit einer abgeleiteten Kennung weiter ins Netz.
    /// </summary>
    public async Task<EingangsAusgang> HinweisAsync(HinweisZustellen h, EingangsRechnung rechnung, CancellationToken ct)
    {
        if (_b.Eingang(h.Hinweis.GetType()) is not { Hinweis: true })
            throw new InvalidOperationException($"{_b.Name} hört den Hinweis {h.Hinweis.GetType().Name} nicht.");
        using var spur = Spur.Von(h.Ursache ?? Spur.Ursache(h.Stream, h.Version), _b.Wirker);
        using var akteur = Infrastructure.Akteure.AkteurHerkunft.Aus(h.Akteur);
        if (rechnung.Ausgaben is null)
        {
            if (Draussen)
            {
                if (await HinweisDraussenAsync(h, ct) is not { } antwort) return EingangsAusgang.Spaeter;
                rechnung.Ausgaben = antwort;
            }
            else
            {
                // Je Hinweis eine eigene Ableitung neuer Ids (zwei Hinweise aus demselben Eingang bekommen verschiedene).
                var herkunft = new Herkunft(h.Stream, h.Version, h.Zeit, ImAuftrag.IstAkteur(h.Akteur) ? h.Akteur : null, $"{_b.Wirker}/hinweis:{h.Kennung:N}");
                var liste = new List<IAusgabe>();
                await foreach (var a in Rechne(h.Hinweis, herkunft, ct)) liste.Add(a);
                rechnung.Ausgaben = liste;
            }
        }
        if (!rechnung.HinweiseGesendet)
        {
            rechnung.HinweiseGesendet = true;
            var nr = 0;
            foreach (var w in rechnung.Ausgaben.OfType<ITransientEvent>())
                _hinweis?.Invoke(new HinweisZustellen(FunktionsIds.Hinweis(_b.Name, h.Kennung, nr++), w, h.Stream, h.Version, h.Zeit, h.Korrelation,
                    h.Akteur, h.Ursache));
        }
        var korrelation = Guid.TryParse(h.Korrelation, out var k) ? k : h.Stream;
        var sends = new List<Task<EmitAusgang>>();
        var stelle = 0;
        foreach (var c in rechnung.Ausgaben.OfType<ICommand>())
            sends.Add(_sende(c, FunktionsIds.BefehlAusHinweis(_b.Name, h.Kennung, stelle++, c), korrelation, ct));
        if (sends.Count == 0) return EingangsAusgang.Fertig;
        var ausgaenge = await Task.WhenAll(sends);
        return ausgaenge.All(a => a.IstEntschieden()) ? EingangsAusgang.Fertig : EingangsAusgang.Spaeter;
    }

    /// <summary>
    /// Ein Hinweis-Eingang draußen (Plan §2.9, E9): derselbe Weg wie ein Log-Eingang — anbieten beim Vermittler (die Adresse bindet ihn an den Träger
    /// dieses Actors, A8), die Sitzung reicht ihn als <c>ArbeitsAuftrag</c> weiter —, nur dass die Antwort nicht festgehalten wird: sie kommt mit ihren
    /// Ausgaben zurück. Kommt in der Geduld keine, verfällt der Hinweis (höchstens einmal; ein zweites Anbieten nähme der Vermittler nach „erledigt“
    /// ohnehin nicht mehr an).
    /// </summary>
    private async Task<IReadOnlyList<IAusgabe>?> HinweisDraussenAsync(HinweisZustellen h, CancellationToken ct)
    {
        var schluessel = FunktionsIds.HinweisEingang(_b.Name, h.Kennung);
        var antworten = _antworten.GetOrAdd(schluessel, _ => new Antworten());
        try
        {
            await _bieteAn!(new EingangAnbieten(schluessel, _b.Name, h.Hinweis, h.Stream, h.Version, h.Zeit, h.Korrelation,
                ImAuftrag.IstAkteur(h.Akteur) ? h.Akteur : null, h.Ursache, _b.Wirker, _b.Adresse(h.Stream, _partitionen)), ct);
            while (antworten.Ausgaben is null && antworten.Fehler is null)
                if (!await antworten.WarteAsync(_geduld, ct))
                    throw new TimeoutException($"{_b.Name}: auf den Hinweis {h.Hinweis.GetType().Name} {h.Kennung} kam draußen in {_geduld.TotalSeconds:0} s keine Antwort");
            if (antworten.Fehler is { } fehler) throw new InvalidOperationException($"{_b.Name}: draußen gescheitert am Hinweis {h.Kennung}: {fehler}");
            return antworten.Ausgaben;
        }
        finally
        {
            _antworten.TryRemove(schluessel, out _);
        }
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
        var stueck = new List<IAusgabe>();
        var hinweise = new List<IAusgabe>();
        var gezaehlt = 0;
        var hinweisNr = 0;     // die Stelle der Hinweise unter den Hinweisen (ihre Kennung, A7)
        var ersterHinweis = 0;
        await foreach (var a in Rechne(e, ct))
        {
            if (a is ITransientEvent h)
            {
                if (gezaehlt < schon) ersterHinweis = ++hinweisNr;   // Hinweise aus dem schon festgehaltenen Teil gingen schon (oder verfielen)
                else { hinweise.Add(h); hinweisNr++; }
                continue;
            }
            if (gezaehlt++ < schon) continue;
            stueck.Add(a);
            if (!eingang.Strom || stueck.Count < _stueck) continue;
            if (!await _ablage.HalteFestAsync(eingabe, schon, stueck, abgeschlossen: false, ct)) return await NachKonfliktAsync(eingabe, ct);
            // Das Festgehaltene senden, bevor der Strom weiterrechnet — der Rückstau eines großen Imports liegt im Log, nicht im Speicher.
            entschieden &= await SendeAsync(eingabe, stueck, schon, ct) == EingangsAusgang.Fertig;
            schon += stueck.Count;
            stueck = new List<IAusgabe>();
            MeldeHinweise(hinweise, e, ersterHinweis);
            ersterHinweis = hinweisNr;
            hinweise.Clear();
        }
        if (!await _ablage.HalteFestAsync(eingabe, schon, stueck, abgeschlossen: true, ct)) return await NachKonfliktAsync(eingabe, ct);
        MeldeHinweise(hinweise, e, ersterHinweis);
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
                e.CorrelationId, ImAuftrag.IstAkteur(e.UserId) ? e.UserId : null, Spur.Aktuell?.Ursache, Spur.Aktuell?.Wirker,
                _b.Adresse(e.AggregateId, _partitionen)), ct);
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
    private async Task<EingangsAusgang> SendeAsync(FunktionsEingabe eingabe, IReadOnlyList<IAusgabe> ausgaben, int ab, CancellationToken ct)
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
