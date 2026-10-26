using Abstractions;
using Infrastructure.Lebenszyklus;
using Infrastructure.Projections;   // Wake, WakeAck, Konsum, BatchLeser.TypenAus
using Microsoft.Extensions.Logging;
using Proto;
using Proto.Cluster;

namespace Infrastructure.Funktionen;

/// <summary>
/// Der FUNKTIONS-ACTOR je (Funktion, Partition) (Plan Pub/Sub-Neustrukturierung §2.2, Phase 3): ein virtueller Cluster-Actor, Kind
/// <c>funktion-&lt;F&gt;</c>, Identität = Partition. Er trägt, was eine Funktion nie selbst trägt: Position und Fortschritt (der Stand beim Framework),
/// Gleichzeitigkeit (Slots; Eingänge eines Streams nacheinander), Wiederholung mit Backoff, Parken, das Festhalten der Ausgabe und das Senden aus
/// dem Festgehaltenen (<see cref="FunktionsArbeit"/>). Rechnet die Funktion draußen, bietet er den Eingang über den Vermittler an — die Garantie
/// bleibt hier.
/// <para><b>Wecken</b> (bis zum Wecker am Flush, Plan Phase 6): wie ein Partitions-Leser einer Projektion — das Signal eines Aggregat-Commits weckt die
/// Partition schnell, der Takt (alle 30 s je Partition) holt alles andere nach (Events aus Funktionen, Quellen und Uhr wecken heute niemanden). Jede
/// Weckung führt zu höchstens einer Lese-Runde mehr („läuft → noch einmal“).</para>
/// <para><b>Lebenszyklus</b> (Rahmen): Lesen und Arbeiten laufen neben der Mailbox (<see cref="RahmenActor.Neben"/>); der Zustand
/// (<see cref="FunktionsLauf"/>) wird nur in der Mailbox geändert. Beim Stopp bricht die Arbeit ab — was nicht fertig wurde, rechnet die nächste
/// Aktivierung erneut (wiederholungsfest über das Festgehaltene bzw. abgeleitete Command-Ids); der Stand wird zuletzt noch geschrieben. Kein
/// Leerlauf-Stopp: es gibt nur Funktionen × Partitionen dieser Actors, der Takt weckt sie ohnehin.</para>
/// </summary>
public sealed class FunktionsActor : RahmenActor
{
    /// <summary>Wie oft der Stand höchstens geschrieben wird (er ist wiederholungsfest; beim Stopp wird er immer geschrieben).</summary>
    public static readonly TimeSpan StandAlle = TimeSpan.FromSeconds(1);

    private sealed record Gelesen(PartitionsCheckpoint? Stand, PartitionsSeite Seite);
    private sealed record Erneut(PartitionsEvent Eingang);

    private readonly FunktionsBaustein _b;
    private readonly FunktionsArbeit _arbeit;
    private readonly IEventStoreRepository _log;
    private readonly IFunktionsStand _staende;
    private readonly IDeadLetterSink? _deadLetter;
    private readonly Konsum _konsum;
    private readonly int _wiederholungen;
    private readonly IReadOnlyCollection<string>? _typen;
    private readonly ILogger? _logger;
    private readonly Func<DateTimeOffset> _jetzt;
    private readonly int _slots;
    private readonly Dictionary<long, int> _versuche = new();
    private FunktionsLauf? _lauf;
    private int _partition;
    private bool _liest, _nochmal, _schreibt, _geparktGeaendert;
    private DateTimeOffset _letzterStand = DateTimeOffset.MinValue, _naechsterParkVersuch = DateTimeOffset.MinValue;

    /// <param name="wiederholungen">Wie oft ein werfender Eingang nach Backoff erneut versucht wird, bevor sein Stream geparkt wird.</param>
    public FunktionsActor(FunktionsBaustein baustein, FunktionsArbeit arbeit, IEventStoreRepository log, IFunktionsStand staende, Konsum konsum,
        int slots, int wiederholungen, IDeadLetterSink? deadLetter = null, ILogger? logger = null, Func<DateTimeOffset>? jetzt = null,
        IReadOnlyCollection<string>? typen = null)
        : base(logger)
    {
        _b = baustein;
        _arbeit = arbeit;
        _log = log;
        _staende = staende;
        _konsum = konsum;
        _slots = Math.Max(1, slots);
        _wiederholungen = Math.Max(0, wiederholungen);
        _deadLetter = deadLetter;
        _logger = logger;
        _jetzt = jetzt ?? (() => DateTimeOffset.UtcNow);
        _typen = typen;
    }

    /// <summary>Die Speicher-Diskriminatoren der Eingänge aus dem Log (samt Upcasting-Vorgängern) — der Typfilter beim Lesen.</summary>
    public static IReadOnlyCollection<string>? TypenVon(FunktionsBaustein b) => BatchLeser.TypenAus(b.LogEingaenge.Select(e => e.Typ));

    protected override Task BeginneAsync(IContext context)
    {
        _partition = int.TryParse(context.ClusterIdentity()?.Identity, out var p) ? p : 0;
        _lauf = new FunktionsLauf(_slots);
        return Task.CompletedTask;
    }

    protected override Task BehandleAsync(IContext context)
    {
        switch (context.Message)
        {
            case Wake:
                context.Respond(new WakeAck());
                Lies(context);
                break;
            case EingangBeantwortet b:
                _arbeit.Beantwortet(b.Schluessel, b.Fehler, b.Teil);
                if (context.Sender is not null) context.Respond(new WakeAck());
                break;
            case Erneut e:
                Starte(context, e.Eingang);
                break;
        }
        return Task.CompletedTask;
    }

    // ── Lesen: eine Seite neben der Mailbox, aufnehmen in der Mailbox ─────────────────────────────────────────────────────

    private void Lies(IContext context)
    {
        if (Stopp.IsCancellationRequested || _lauf is null) return;
        if (_liest) { _nochmal = true; return; }
        if (_lauf.Stand is not null && _lauf.FreieSlots <= 0) return;   // Rückstau im Log: erst lesen, wenn ein Slot frei ist
        _liest = true;
        _nochmal = false;
        var lauf = _lauf;
        var brauchtStand = lauf.Stand is null;
        var lese = lauf.Position;
        var ausser = lauf.Ausser;
        Neben(context, ct => LiesAsync(brauchtStand, lese, ausser, ct), t => NachLesen(context, t));
    }

    private async Task<Gelesen> LiesAsync(bool brauchtStand, long lese, IReadOnlyCollection<long> ausser, CancellationToken ct)
    {
        var stand = brauchtStand ? await _staende.LadeAsync(_b.StandId, _partition, ct) : null;
        var seite = await _log.ReadPartitionAsync(stand?.Position ?? lese, _partition, _konsum.Partitionen, _typen,
            stand is null ? ausser : Array.Empty<long>(), _konsum.BatchGroesse, ct);
        return new Gelesen(stand, seite);
    }

    private Task NachLesen(IContext context, Task lesen)
    {
        _liest = false;
        if (lesen is Task<Gelesen> { IsCompletedSuccessfully: true } g && _lauf is { } lauf)
        {
            if (g.Result.Stand is { } stand) lauf.Setze(stand);
            if (lauf.Stand is not null)
            {
                foreach (var e in lauf.Nimm(g.Result.Seite)) Starte(context, e);
                VersucheGeparkte(context);
                SchreibeStand(context, sofort: false);
                // Die Seite war voll und es sind noch Slots frei: weiterlesen.
                if (g.Result.Seite.Voll && lauf.FreieSlots > 0) _nochmal = true;
            }
        }
        else if (lesen.Exception is { } ex)
            _logger?.LogWarning(ex.GetBaseException(), "[Funktion] {Funktion}/{Partition}: Lesen gescheitert — die nächste Weckung (spätestens der Takt) liest erneut",
                _b.Name, _partition);
        if (_nochmal) Lies(context);
        return Task.CompletedTask;
    }

    // ── Arbeiten: je Eingang neben der Mailbox, Ausgang in der Mailbox ────────────────────────────────────────────────────

    private void Starte(IContext context, PartitionsEvent e)
    {
        if (Stopp.IsCancellationRequested) return;
        Neben(context, ct => _arbeit.VerarbeiteAsync(e.Event, ct), t => NachArbeit(context, e, t));
    }

    private Task NachArbeit(IContext context, PartitionsEvent e, Task arbeit)
    {
        if (_lauf is not { } lauf || Stopp.IsCancellationRequested) return Task.CompletedTask;
        var ausgang = arbeit is Task<EingangsAusgang> { IsCompletedSuccessfully: true } a ? a.Result : EingangsAusgang.Fehler;
        if (ausgang == EingangsAusgang.Fertig)
        {
            _versuche.Remove(e.Sequenz);
            lauf.Fertig(e.Sequenz);
            SchreibeStand(context, sofort: false);
            Lies(context);   // ein Slot ist frei, dahinter kann ein Eingang desselben Streams warten
            return Task.CompletedTask;
        }

        // Backoff: 1 s, 2 s, 4 s … höchstens 30 s, gestreut. „Später“ (unbekannter Emit-Ausgang, Antwort draußen offen) zählt nie gegen die
        //   Wiederholungen — das Ziel ist überlastet oder der Mensch überlegt; nur ein Fehler der Funktion führt am Ende zum Parken.
        var versuch = _versuche[e.Sequenz] = _versuche.GetValueOrDefault(e.Sequenz) + 1;
        if (ausgang == EingangsAusgang.Fehler && versuch > _wiederholungen)
        {
            var grund = (arbeit.Exception?.GetBaseException() is { } ex ? $"{ex.GetType().Name}: {ex.Message}" : "draußen gescheitert");
            _versuche.Remove(e.Sequenz);
            if (lauf.Parke(e.Sequenz, grund, _jetzt()) is { } g)
            {
                _geparktGeaendert = true;
                _logger?.LogWarning("[Funktion] {Funktion}/{Partition}: Stream {Stream} geparkt ab v{Version} ({Grund}) — die übrigen laufen weiter",
                    _b.Name, _partition, g.StreamId, g.Version + 1, grund);
                if (_deadLetter is { } dl)
                {
                    var eintrag = new DeadLetter
                    {
                        Id = Guid.NewGuid(), Quelle = _b.Wirker, CommandType = "(Funktion)", AggregateId = g.StreamId,
                        Grund = $"geparkt ab v{g.Version + 1}: {grund}", Versuche = versuch, ErfasstUtc = g.Seit,
                    };
                    Neben(context, _ => dl.WriteAsync(eintrag));   // best-effort, nur Beobachtung
                }
            }
            SchreibeStand(context, sofort: true);
            Lies(context);
            return Task.CompletedTask;
        }
        if (ausgang == EingangsAusgang.Fehler)
            _logger?.LogInformation(arbeit.Exception?.GetBaseException(), "[Funktion] {Funktion}: Eingang {Stream}:{Version} gescheitert (Versuch {Versuch}) — Backoff",
                _b.Name, e.Event.AggregateId, e.Event.AggregateVersion, versuch);
        var warte = TimeSpan.FromMilliseconds(Math.Min(30_000, 1_000 * Math.Pow(2, Math.Min(versuch - 1, 5))) * (0.8 + Random.Shared.NextDouble() * 0.4));
        Neben(context, ct => Task.Delay(warte, ct), _ =>
        {
            if (!Stopp.IsCancellationRequested) context.Send(context.Self, new Erneut(e));
            return Task.CompletedTask;
        });
        return Task.CompletedTask;
    }

    // ── Geparkte Streams: je Takt ein Einzelversuch ab ihrem Stand ────────────────────────────────────────────────────────

    private void VersucheGeparkte(IContext context)
    {
        if (_lauf is not { } lauf || lauf.Geparkt.Count == 0 || _jetzt() < _naechsterParkVersuch) return;
        _naechsterParkVersuch = _jetzt() + Konsum.PollIntervall;
        foreach (var g in lauf.Geparkt.ToList())
        {
            if (!lauf.BeginneParkVersuch(g.StreamId)) continue;
            Neben(context, ct => EinzelversuchAsync(g, ct), t =>
            {
                var durch = t is Task<bool> { IsCompletedSuccessfully: true, Result: true };
                lauf.EndeParkVersuch(g.StreamId, durch);
                if (durch)
                {
                    _geparktGeaendert = true;
                    _logger?.LogInformation("[Funktion] {Funktion}/{Partition}: geparkter Stream {Stream} läuft wieder", _b.Name, _partition, g.StreamId);
                    SchreibeStand(context, sofort: true);
                }
                Lies(context);
                return Task.CompletedTask;
            });
        }
    }

    /// <summary>Die Eingänge des Streams ab seinem Stand, nacheinander; true, wenn jeder fertig wurde.</summary>
    private async Task<bool> EinzelversuchAsync(GeparkterStream g, CancellationToken ct)
    {
        foreach (var e in await _log.ReadStreamAsync(g.StreamId, g.Version + 1, ct))
        {
            if (_b.Eingang(e.Payload.GetType()) is not { Hinweis: false }) continue;
            try
            {
                if (await _arbeit.VerarbeiteAsync(e, ct) != EingangsAusgang.Fertig) return false;
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested) { throw; }
            catch (Exception ex)
            {
                _logger?.LogDebug(ex, "[Funktion] {Funktion}: geparkter Stream {Stream} scheitert weiter", _b.Name, g.StreamId);
                return false;
            }
        }
        return true;
    }

    // ── Stand: gedrosselt neben der Mailbox, beim Stopp zuletzt ──────────────────────────────────────────────────────────

    private void SchreibeStand(IContext context, bool sofort)
    {
        if (_lauf is not { } lauf || _schreibt || Stopp.IsCancellationRequested) return;
        if (!sofort && !_geparktGeaendert && _jetzt() - _letzterStand < StandAlle) return;
        if (lauf.ZuSchreiben(_geparktGeaendert) is not { } neu) return;
        _schreibt = true;
        _geparktGeaendert = false;
        _letzterStand = _jetzt();
        Neben(context, ct => _staende.SchreibeAsync(neu, ct), t =>
        {
            _schreibt = false;
            if (t is Task<int> { IsCompletedSuccessfully: true } v) lauf.Geschrieben(neu, v.Result);
            else if (t.Exception?.GetBaseException() is PartitionsKonfliktException)
            {
                // Eine zweite Aktivierung hat geschrieben: neu laden — die laufende Arbeit ist wiederholungsfest.
                _logger?.LogInformation("[Funktion] {Funktion}/{Partition}: Stand zwischenzeitlich geschrieben — neu laden", _b.Name, _partition);
                lauf.Verloren();
                Lies(context);
            }
            else _geparktGeaendert |= neu.Geparkt.Count != lauf.Geparkt.Count;
            return Task.CompletedTask;
        });
    }

    protected override async Task BeimStoppenAsync(IContext context)
    {
        if (_lauf?.ZuSchreiben(true) is not { } neu) return;
        try { await _staende.SchreibeAsync(neu, CancellationToken.None).WaitAsync(TimeSpan.FromSeconds(5)); }
        catch (Exception ex) { _logger?.LogDebug(ex, "[Funktion] {Funktion}/{Partition}: Stand beim Stopp nicht geschrieben (wiederholungsfest)", _b.Name, _partition); }
    }

    /// <summary>Die Cluster-Identität des Funktions-Actors einer Partition.</summary>
    public static ClusterIdentity Identitaet(FunktionsBaustein b, int partition) => ClusterIdentity.Create(partition.ToString(), b.KindName);
}
