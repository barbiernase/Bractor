using Abstractions;
using Infrastructure.Lebenszyklus;
using Infrastructure.Projections;   // Wake, WakeAck, Konsum, BatchLeser.TypenAus
using Microsoft.Extensions.Logging;
using Proto;
using Proto.Cluster;

namespace Infrastructure.Funktionen;

/// <summary>
/// Der FUNKTIONS-ACTOR je Adresse (Plan Pub/Sub-Neustrukturierung §2.2, §2.9): ein virtueller Cluster-Actor, Kind <c>funktion-&lt;F&gt;</c>, Identität =
/// Partition (bzw. die eine Adresse einer Funktion mit <c>[EineAdresse]</c>, A6). Er trägt, was eine Funktion nie selbst trägt: Position und
/// Fortschritt (der Stand beim Framework), Gleichzeitigkeit (Slots; Eingänge eines Streams nacheinander), Wiederholung mit Backoff, Parken, das
/// Festhalten der Ausgabe und das Senden aus dem Festgehaltenen (<see cref="FunktionsArbeit"/>). Rechnet die Funktion draußen, bietet er den Eingang
/// über den Vermittler an — die Garantie bleibt hier.
/// <para><b>Wecken</b> (bis zum Wecker am Flush, Plan Phase 6): wie ein Partitions-Leser einer Projektion — das Signal eines Aggregat-Commits weckt die
/// Partition schnell, der Takt (alle 30 s je Partition) holt alles andere nach. Jede Weckung führt zu höchstens einer Lese-Runde mehr.</para>
/// <para><b>Lebenszyklus</b> (Rahmen): beim Aktivieren baut der Actor die Umsetzung im Host samt ihren Diensten in einem eigenen Bereich
/// (<see cref="FunktionsAktivierung"/>), beim Stoppen, Passivieren und vor einem Neustart gibt er sie frei. Lesen und Arbeiten laufen neben der
/// Mailbox (<see cref="RahmenActor.Neben"/>); der Lauf (<see cref="FunktionsLauf"/>) wird nur in der Mailbox geändert. Beim Stopp bricht die Arbeit
/// ab — was nicht fertig wurde, rechnet die nächste Aktivierung erneut (wiederholungsfest über das Festgehaltene bzw. abgeleitete Command-Ids); der
/// Stand wird zuletzt noch geschrieben.</para>
/// <para><b>Hinweise</b> (Plan §2.9, E7, A7): ein Hinweis kommt direkt aus dem Hinweis-Netz (<see cref="HinweisZustellen"/>, verlierbar) — ohne
/// Position, ohne Festhalten, höchstens einmal. Bis zur Slot-Grenze laufen Hinweise neben den Log-Eingängen; was nicht gleich starten kann, wartet in
/// einer gedeckelten Schlange (läuft sie über, fallen die ältesten weg). Bleibt ein Command „unbekannt“, sendet der Actor nach Backoff dieselben
/// Commands mit denselben Ids erneut. Eine Funktion, die nur Hinweise hört, hat keinen Log-Leser, keine Position und keinen Takt.</para>
/// <para><b>Zustand</b> (<c>IZustand&lt;T&gt;</c>, Plan §2.9, A5) — nach dem Vorbild des Aggregat-Actors: geladen beim Aktivieren (zusammen mit dem
/// Stand, bevor das erste <c>Auf</c> läuft); immer nur EIN Eingang zugleich (ein Schreiber je Adresse); gesichert nur in einem RUHEPUNKT — kein
/// Eingang in Arbeit, also enthält der Zustand genau die Log-Eingänge bis zur Position — in EINER Transaktion mit dem Stand, höchstens so oft wie
/// die Umgebung sagt und beim Stoppen. Während einer Sicherung startet nichts. Verliert die Sicherung gegen eine zweite Aktivierung, verwirft der
/// Actor Zustand und Lauf und lädt beides neu.</para>
/// </summary>
public sealed class FunktionsActor : RahmenActor
{
    /// <summary>Wie oft der Stand höchstens geschrieben wird (er ist wiederholungsfest; beim Stopp wird er immer geschrieben).</summary>
    public static readonly TimeSpan StandAlle = TimeSpan.FromSeconds(1);

    /// <summary>Wie oft ein Laden nachfasst, wenn Stand und Zustand gerade von verschiedenen Sicherungen stammen (zwischen zwei Lesevorgängen geschrieben).</summary>
    private const int LadeVersuche = 5;

    private sealed record Gelesen(PartitionsCheckpoint? Stand, PartitionsSeite Seite);
    private sealed record Geladen(PartitionsCheckpoint? Stand, GeladenerZustand Zustand);
    private sealed record Erneut(PartitionsEvent Eingang);
    private sealed record HinweisErneut(HinweisZustellen Hinweis, int Versuch);

    /// <summary>So viele Hinweise warten höchstens auf einen freien Slot; darüber fallen die ältesten weg (höchstens einmal, Inv. 6).</summary>
    public const int HinweiseHoechstens = 10_000;

    private readonly FunktionsBaustein _b;
    private readonly FunktionsArbeit _arbeit;
    private readonly FunktionsAktivierung? _aktivierung;
    private readonly ZustandsSicherung? _sicherung;
    private readonly IEventStoreRepository _log;
    private readonly IFunktionsStand _staende;
    private readonly IDeadLetterSink? _deadLetter;
    private readonly Konsum _konsum;
    private readonly int _partitionen;
    private readonly int _wiederholungen;
    private readonly IReadOnlyCollection<string>? _typen;
    private readonly ILogger? _logger;
    private readonly Func<DateTimeOffset> _jetzt;
    private readonly int _slots;
    private readonly Dictionary<long, int> _versuche = new();
    /// <summary>Nur mit Zustand: die Rechnungen der Eingänge in Arbeit (Schlüssel „F, X“) — <c>Auf</c> läuft je Eingang und Aktivierung einmal.</summary>
    private readonly Dictionary<Guid, EingangsRechnung> _rechnungen = new();
    /// <summary>Hinweise, die auf einen Slot warten, und die Rechnungen der Hinweise in Arbeit oder im Backoff (Schlüssel = Kennung).</summary>
    private readonly Queue<HinweisZustellen> _hinweise = new();
    private readonly Dictionary<Guid, EingangsRechnung> _hinweisRechnungen = new();
    private int _hinweiseInArbeit;
    private FunktionsLauf? _lauf;
    private int _partition;
    private string _adresse = "0";
    private bool _liest, _nochmal, _schreibt, _geparktGeaendert;
    private DateTimeOffset _letzterStand = DateTimeOffset.MinValue, _naechsterParkVersuch = DateTimeOffset.MinValue;

    // ── Zustand (nur mit IZustand<T>) ──
    private bool _zustandBereit, _laedt, _zustandGeaendert;
    private int _zustandVersion, _parkInArbeit;
    private long _zustandPosition;
    private string? _zustandVerweis;

    /// <param name="wiederholungen">Wie oft ein werfender Eingang nach Backoff erneut versucht wird, bevor sein Stream geparkt wird.</param>
    /// <param name="aktivierung">Die Umsetzung im Host und ihre Dienste je Aktivierung; null = die Funktion rechnet draußen.</param>
    /// <param name="sicherung">Nur mit Zustand: Laden und Sichern (mit der Position, A5).</param>
    public FunktionsActor(FunktionsBaustein baustein, FunktionsArbeit arbeit, IEventStoreRepository log, IFunktionsStand staende, Konsum konsum,
        int slots, int wiederholungen, IDeadLetterSink? deadLetter = null, ILogger? logger = null, Func<DateTimeOffset>? jetzt = null,
        IReadOnlyCollection<string>? typen = null, FunktionsAktivierung? aktivierung = null, ZustandsSicherung? sicherung = null)
        : base(logger)
    {
        _b = baustein;
        _arbeit = arbeit;
        _aktivierung = aktivierung;
        _sicherung = aktivierung is { MitZustand: true } ? sicherung : null;
        _log = log;
        _staende = staende;
        _konsum = konsum;
        _partitionen = baustein.Partitionen(konsum.Partitionen);
        // Mit Zustand ein Eingang zugleich: der Zustand kennt nur einen Schreiber (und gesichert wird zwischen zwei Eingängen).
        _slots = _sicherung is null ? Math.Max(1, slots) : 1;
        _wiederholungen = Math.Max(0, wiederholungen);
        _deadLetter = deadLetter;
        _logger = logger;
        _jetzt = jetzt ?? (() => DateTimeOffset.UtcNow);
        _typen = typen;
        if (_sicherung is not null && slots > 1)
            logger?.LogWarning("[Funktion] {Funktion} hat Zustand — sie rechnet einen Eingang zugleich (Slots {Slots} ignoriert)", baustein.Name, slots);
    }

    /// <summary>Die Speicher-Diskriminatoren der Eingänge aus dem Log (samt Upcasting-Vorgängern) — der Typfilter beim Lesen.</summary>
    public static IReadOnlyCollection<string>? TypenVon(FunktionsBaustein b) => BatchLeser.TypenAus(b.LogEingaenge.Select(e => e.Typ));

    private bool MitZustand => _sicherung is not null;

    /// <summary>Kein Eingang in Arbeit, keine Sicherung, kein Laden — nur hier enthält der Zustand genau die Log-Eingänge bis zur Position.</summary>
    private bool Ruhig => _lauf is { InArbeit: 0 } && _parkInArbeit == 0 && _hinweiseInArbeit == 0 && !_schreibt && !_laedt;

    /// <summary>
    /// Darf jetzt ein Log-Eingang starten? Ohne Zustand immer (bis zur Slot-Grenze); mit Zustand nur geladen, ohne laufende Sicherung und ohne einen
    /// Hinweis in Arbeit (ein Schreiber).
    /// </summary>
    private bool DarfStarten => !MitZustand || (_zustandBereit && !_schreibt && !_laedt && _parkInArbeit == 0 && _hinweiseInArbeit == 0);

    /// <summary>Darf jetzt ein Hinweis starten? Ohne Zustand bis zur Slot-Grenze, mit Zustand nur im Ruhepunkt.</summary>
    private bool HinweisDarfStarten => !Stopp.IsCancellationRequested && (MitZustand ? _zustandBereit && Ruhig : _hinweiseInArbeit < _slots);

    protected override async Task BeginneAsync(IContext context)
    {
        _adresse = context.ClusterIdentity()?.Identity ?? "0";
        _partition = int.TryParse(_adresse, out var p) && p < _partitionen ? p : 0;
        _lauf = new FunktionsLauf(_slots);
        // Wirft der Bau oder das Laden, startet die Supervision neu (die nächste Weckung versucht es wieder).
        if (_aktivierung is not null) await _aktivierung.BeginneAsync();
        if (MitZustand) Uebernimm(await LadeAsync(Stopp));
    }

    /// <summary>Umsetzung und Dienste dieser Aktivierung schließen (Stopp, Passivierung, Neustart) — die nächste baut sie neu.</summary>
    protected override ValueTask FreigebenAsync() => _aktivierung?.DisposeAsync() ?? ValueTask.CompletedTask;

    protected override Task BehandleAsync(IContext context)
    {
        switch (context.Message)
        {
            case Wake:
                context.Respond(new WakeAck());
                Lies(context);
                break;
            case EingangBeantwortet b:
                _arbeit.Beantwortet(b.Schluessel, b.Fehler, b.Teil, b.Befehle is null && b.Hinweise is null ? null
                    : (b.Befehle ?? Array.Empty<ICommand>()).Cast<IAusgabe>().Concat(b.Hinweise ?? Array.Empty<IEvent>()).ToList());
                if (context.Sender is not null) context.Respond(new WakeAck());
                break;
            case Erneut e:
                Starte(context, e.Eingang);
                break;
            case HinweisZustellen h:
                if (context.Sender is not null) context.Respond(new WakeAck());   // der Bote wartet nicht länger als nötig
                NimmHinweis(context, h);
                break;
            case HinweisErneut e:
                StarteHinweis(context, e.Hinweis, e.Versuch);
                break;
        }
        return Task.CompletedTask;
    }

    // ── Hinweise: ohne Position, höchstens einmal ─────────────────────────────────────────────────────────────────────────

    private void NimmHinweis(IContext context, HinweisZustellen h)
    {
        if (_b.Eingang(h.Hinweis.GetType()) is not { Hinweis: true })
        {
            _logger?.LogWarning("[Funktion] {Funktion}: Hinweis {Typ} gehört nicht zu ihren Eingängen — verworfen", _b.Name, h.Hinweis.GetType().Name);
            return;
        }
        _hinweise.Enqueue(h);
        while (_hinweise.Count > HinweiseHoechstens)
        {
            var weg = _hinweise.Dequeue();
            _logger?.LogWarning("[Funktion] {Funktion}/{Adresse}: Hinweis {Kennung} verworfen — die Schlange ist voll (höchstens einmal)", _b.Name, _adresse, weg.Kennung);
        }
        StarteHinweise(context);
    }

    private void StarteHinweise(IContext context)
    {
        while (_hinweise.Count > 0 && HinweisDarfStarten) StarteHinweis(context, _hinweise.Dequeue(), 0);
    }

    private void StarteHinweis(IContext context, HinweisZustellen h, int versuch)
    {
        if (Stopp.IsCancellationRequested) return;
        if (versuch > 0 && !HinweisDarfStarten) { _hinweise.Enqueue(h); return; }   // nach dem Backoff wieder hinten anstellen
        if (!_hinweisRechnungen.TryGetValue(h.Kennung, out var rechnung)) _hinweisRechnungen[h.Kennung] = rechnung = new EingangsRechnung();
        _hinweiseInArbeit++;
        Neben(context, ct => _arbeit.HinweisAsync(h, rechnung, ct), t => NachHinweis(context, h, versuch, t));
    }

    private Task NachHinweis(IContext context, HinweisZustellen h, int versuch, Task arbeit)
    {
        _hinweiseInArbeit--;
        if (Stopp.IsCancellationRequested) return Task.CompletedTask;
        if (MitZustand) _zustandGeaendert = true;   // Auf lief (oder lief teilweise) auf dem Zustand
        var ausgang = arbeit is Task<EingangsAusgang> { IsCompletedSuccessfully: true } a ? a.Result : EingangsAusgang.Fehler;
        switch (ausgang)
        {
            case EingangsAusgang.Fertig:
                _hinweisRechnungen.Remove(h.Kennung);
                break;
            case EingangsAusgang.Fehler:
                // Höchstens einmal: ein Hinweis, an dem die Funktion scheitert, verfällt (kein Parken — er steht in keinem Log).
                _hinweisRechnungen.Remove(h.Kennung);
                _logger?.LogWarning(arbeit.Exception?.GetBaseException(), "[Funktion] {Funktion}: Hinweis {Typ} {Kennung} gescheitert — verworfen (höchstens einmal)",
                    _b.Name, h.Hinweis.GetType().Name, h.Kennung);
                break;
            default:
                // Ein Command blieb „unbekannt“ (oder draußen kam keine Antwort): nach Backoff dieselben Commands mit denselben Ids, nicht neu rechnen.
                var warte = TimeSpan.FromMilliseconds(Math.Min(30_000, 1_000 * Math.Pow(2, Math.Min(versuch, 5))) * (0.8 + Random.Shared.NextDouble() * 0.4));
                Neben(context, ct => Task.Delay(warte, ct), _ =>
                {
                    if (!Stopp.IsCancellationRequested) context.Send(context.Self, new HinweisErneut(h, versuch + 1));
                    return Task.CompletedTask;
                });
                break;
        }
        if (MitZustand) Sichere(context, sofort: false);
        StarteHinweise(context);
        Lies(context);
        return Task.CompletedTask;
    }

    // ── Zustand laden (beim Aktivieren und nach einem verlorenen Konflikt) ──────────────────────────────────────────────

    /// <summary>
    /// Stand und Zustand derselben Sicherung: beide entstehen in einer Transaktion; liest man sie nacheinander, kann dazwischen eine zweite
    /// Aktivierung geschrieben haben — dann passen die Positionen nicht, und es wird neu gelesen.
    /// </summary>
    private async Task<Geladen> LadeAsync(CancellationToken ct)
    {
        for (var versuch = 1; ; versuch++)
        {
            var stand = _b.HoertLog ? await _staende.LadeAsync(_b.StandId, _partition, ct) : null;
            var zustand = await _sicherung!.LadeAsync(Funktionsvertrag.ZustandId(_b.Name, _adresse), ct);
            if (stand is null || zustand.Daten is null || zustand.Position == stand.Position || versuch >= LadeVersuche)
            {
                if (stand is not null && zustand.Daten is not null && zustand.Position != stand.Position)
                    throw new InvalidOperationException($"{_b.Name}/{_adresse}: Stand (Position {stand.Position}) und Zustand (Position {zustand.Position}) "
                        + $"passen nach {LadeVersuche} Versuchen nicht zusammen.");
                return new Geladen(stand, zustand);
            }
        }
    }

    /// <summary>Übernimmt Geladenes (in der Mailbox): Zustand, Position, Versionen — erst danach darf ein <c>Auf</c> laufen.</summary>
    private void Uebernimm(Geladen g)
    {
        _aktivierung!.Lade(g.Zustand.Daten);
        _zustandVersion = g.Zustand.Version;
        _zustandVerweis = g.Zustand.Verweis;
        _zustandPosition = g.Stand?.Position ?? g.Zustand.Position;
        _zustandGeaendert = false;
        _rechnungen.Clear();
        if (g.Stand is not null) _lauf!.Setze(g.Stand);
        _zustandBereit = true;
    }

    /// <summary>Nach einem verlorenen Konflikt: Zustand und Lauf der anderen Aktivierung übernehmen (was sie seit ihrer Sicherung rechnete, verfällt).</summary>
    private void LadeNeu(IContext context)
    {
        if (_laedt) return;
        _laedt = true;
        _zustandBereit = false;
        _lauf = new FunktionsLauf(_slots);
        Neben(context, LadeAsync, t =>
        {
            _laedt = false;
            if (t is Task<Geladen> { IsCompletedSuccessfully: true } g) Uebernimm(g.Result);
            else _logger?.LogWarning(t.Exception?.GetBaseException(), "[Funktion] {Funktion}/{Adresse}: Zustand nicht geladen — die nächste Weckung versucht es wieder",
                _b.Name, _adresse);
            StarteHinweise(context);
            Lies(context);
            return Task.CompletedTask;
        });
    }

    // ── Lesen: eine Seite neben der Mailbox, aufnehmen in der Mailbox ─────────────────────────────────────────────────────

    private void Lies(IContext context)
    {
        if (Stopp.IsCancellationRequested || _lauf is null) return;
        if (MitZustand && !_zustandBereit)
        {
            if (!_laedt) LadeNeu(context);
            return;
        }
        if (!_b.HoertLog) return;   // nur Hinweise: kein Log-Leser, keine Position
        if (_liest) { _nochmal = true; return; }
        if (_lauf.Stand is not null && (_lauf.FreieSlots <= 0 || !DarfStarten)) return;   // Rückstau im Log: erst lesen, wenn ein Slot frei ist
        _liest = true;
        _nochmal = false;
        var lauf = _lauf;
        var brauchtStand = lauf.Stand is null;
        var lese = lauf.Position;
        var ausser = lauf.Ausser;
        Neben(context, ct => LiesAsync(brauchtStand, lese, ausser, ct), t => NachLesen(context, lauf, t));
    }

    private async Task<Gelesen> LiesAsync(bool brauchtStand, long lese, IReadOnlyCollection<long> ausser, CancellationToken ct)
    {
        var stand = brauchtStand ? await _staende.LadeAsync(_b.StandId, _partition, ct) : null;
        var seite = await _log.ReadPartitionAsync(stand?.Position ?? lese, _partition, _partitionen, _typen,
            stand is null ? ausser : Array.Empty<long>(), _konsum.BatchGroesse, ct);
        return new Gelesen(stand, seite);
    }

    private Task NachLesen(IContext context, FunktionsLauf gelesenFuer, Task lesen)
    {
        _liest = false;
        if (!ReferenceEquals(gelesenFuer, _lauf)) { Lies(context); return Task.CompletedTask; }   // inzwischen neu geladen: die Seite gilt nicht mehr
        if (lesen is Task<Gelesen> { IsCompletedSuccessfully: true } g && _lauf is { } lauf)
        {
            if (g.Result.Stand is { } stand && !MitZustand) lauf.Setze(stand);   // mit Zustand setzt nur das Laden den Stand (A5)
            if (lauf.Stand is not null && DarfStarten)
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
        var rechnung = RechnungFuer(e.Event);
        Neben(context, ct => _arbeit.VerarbeiteAsync(e.Event, ct, rechnung), t => NachArbeit(context, e, t));
    }

    /// <summary>Mit Zustand: die Rechnung des Eingangs in dieser Aktivierung (dieselbe bei jedem Wiederholen); ohne Zustand null.</summary>
    private EingangsRechnung? RechnungFuer(EventEnvelope e)
    {
        if (!MitZustand) return null;
        var schluessel = FunktionsArbeit.EingabeVon(_b, e).Schluessel;
        if (!_rechnungen.TryGetValue(schluessel, out var r)) _rechnungen[schluessel] = r = new EingangsRechnung();
        return r;
    }

    private void Vergiss(EventEnvelope e)
    {
        if (MitZustand) _rechnungen.Remove(FunktionsArbeit.EingabeVon(_b, e).Schluessel);
    }

    private Task NachArbeit(IContext context, PartitionsEvent e, Task arbeit)
    {
        if (_lauf is not { } lauf || Stopp.IsCancellationRequested) return Task.CompletedTask;
        var ausgang = arbeit is Task<EingangsAusgang> { IsCompletedSuccessfully: true } a ? a.Result : EingangsAusgang.Fehler;
        if (ausgang == EingangsAusgang.Fertig)
        {
            _versuche.Remove(e.Sequenz);
            Vergiss(e.Event);
            lauf.Fertig(e.Sequenz);
            SchreibeStand(context, sofort: false);
            StarteHinweise(context);   // mit Zustand warten Hinweise auf den Ruhepunkt
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
            Vergiss(e.Event);
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
        {
            // Mit Zustand: die Funktion warf — ob Auf den Zustand schon teilweise änderte, weiß nur sie; der nächste Versuch rechnet neu.
            Vergiss(e.Event);
            _logger?.LogInformation(arbeit.Exception?.GetBaseException(), "[Funktion] {Funktion}: Eingang {Stream}:{Version} gescheitert (Versuch {Versuch}) — Backoff",
                _b.Name, e.Event.AggregateId, e.Event.AggregateVersion, versuch);
        }
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
        if (MitZustand && !Ruhig) return;   // mit Zustand nur im Ruhepunkt, einer zugleich (ein Schreiber)
        _naechsterParkVersuch = _jetzt() + Konsum.PollIntervall;
        foreach (var g in lauf.Geparkt.ToList())
        {
            if (!lauf.BeginneParkVersuch(g.StreamId)) continue;
            _parkInArbeit++;
            Neben(context, ct => EinzelversuchAsync(g, ct), t =>
            {
                _parkInArbeit--;
                var (durch, bis) = t is Task<(bool, int)> { IsCompletedSuccessfully: true } r ? r.Result : (false, g.Version);
                lauf.EndeParkVersuch(g.StreamId, durch, bis);
                if (durch)
                {
                    _logger?.LogInformation("[Funktion] {Funktion}/{Partition}: geparkter Stream {Stream} läuft wieder", _b.Name, _partition, g.StreamId);
                }
                if (durch || bis > g.Version) _geparktGeaendert = true;
                if (_geparktGeaendert) SchreibeStand(context, sofort: true);
                Lies(context);
                return Task.CompletedTask;
            });
            if (MitZustand) break;
        }
    }

    /// <summary>
    /// Die Eingänge des Streams ab seinem Stand, nacheinander; liefert, ob jeder fertig wurde, und die Version des letzten fertigen (der Stand des
    /// geparkten Streams rückt mit — mit Zustand wirkt so kein Eingang zweimal auf ihn).
    /// </summary>
    private async Task<(bool Durch, int Bis)> EinzelversuchAsync(GeparkterStream g, CancellationToken ct)
    {
        var bis = g.Version;
        foreach (var e in await _log.ReadStreamAsync(g.StreamId, g.Version + 1, ct))
        {
            if (_b.Eingang(e.Payload.GetType()) is not { Hinweis: false }) { bis = e.AggregateVersion; continue; }
            try
            {
                var rechnung = MitZustand ? new EingangsRechnung() : null;
                if (await _arbeit.VerarbeiteAsync(e, ct, rechnung) != EingangsAusgang.Fertig) return (false, bis);
                bis = e.AggregateVersion;
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested) { throw; }
            catch (Exception ex)
            {
                _logger?.LogDebug(ex, "[Funktion] {Funktion}: geparkter Stream {Stream} scheitert weiter", _b.Name, g.StreamId);
                return (false, bis);
            }
        }
        return (true, bis);
    }

    // ── Stand: gedrosselt neben der Mailbox, beim Stopp zuletzt ──────────────────────────────────────────────────────────

    private void SchreibeStand(IContext context, bool sofort)
    {
        if (MitZustand) { Sichere(context, sofort); return; }
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

    /// <summary>Was jetzt zu sichern wäre (nur im Ruhepunkt): Stand (mit Log-Eingängen), Position, Daten — oder null, wenn sich nichts änderte.</summary>
    private (PartitionsCheckpoint? Stand, long Position, byte[] Daten)? ZuSichern(bool erzwingen)
    {
        if (!Ruhig || !_zustandBereit || _lauf is not { } lauf) return null;
        PartitionsCheckpoint? stand = null;
        if (_b.HoertLog)
        {
            stand = lauf.ZuSchreiben(true);
            if (stand is null) return null;
        }
        var position = stand?.Position ?? 0;
        if (!erzwingen && !_zustandGeaendert && !_geparktGeaendert && position <= _zustandPosition) return null;
        return (stand, position, _aktivierung!.Sichere());
    }

    /// <summary>
    /// Sichert Zustand und Stand gemeinsam (A5) — nur im Ruhepunkt, gedrosselt (<see cref="ZustandsSicherung.Alle"/>). Während die Sicherung läuft,
    /// startet kein Eingang; danach geht das Lesen weiter.
    /// </summary>
    private void Sichere(IContext context, bool sofort)
    {
        if (Stopp.IsCancellationRequested || _sicherung is null) return;
        if (!sofort && !_geparktGeaendert && _jetzt() - _letzterStand < _sicherung.Alle) return;
        if (ZuSichern(erzwingen: false) is not { } s) return;
        _schreibt = true;
        _letzterStand = _jetzt();
        var warGeaendert = (_zustandGeaendert, _geparktGeaendert);
        _zustandGeaendert = _geparktGeaendert = false;
        var lauf = _lauf!;
        Neben(context, ct => _sicherung.SichereAsync(_b.Name, _adresse, s.Stand, s.Position, s.Daten, _zustandVersion, _zustandVerweis, ct), t =>
        {
            _schreibt = false;
            if (t is Task<(int? Stand, int Zustand, string? Verweis)> { IsCompletedSuccessfully: true } v)
            {
                if (v.Result.Stand is { } sv && s.Stand is not null) lauf.Geschrieben(s.Stand, sv);
                _zustandVersion = v.Result.Zustand;
                _zustandVerweis = v.Result.Verweis;
                _zustandPosition = s.Position;
            }
            else if (t.Exception?.GetBaseException() is PartitionsKonfliktException)
            {
                // Eine zweite Aktivierung hat gesichert: ihr Stand gilt — Zustand und Lauf verwerfen und neu laden (nichts ist gerade in Arbeit).
                _logger?.LogInformation("[Funktion] {Funktion}/{Adresse}: Zustand zwischenzeitlich gesichert — neu laden", _b.Name, _adresse);
                LadeNeu(context);
                return Task.CompletedTask;
            }
            else
            {
                (_zustandGeaendert, _geparktGeaendert) = (_zustandGeaendert || warGeaendert.Item1, _geparktGeaendert || warGeaendert.Item2);
                _logger?.LogWarning(t.Exception?.GetBaseException(), "[Funktion] {Funktion}/{Adresse}: Zustand nicht gesichert — beim nächsten Ruhepunkt wieder",
                    _b.Name, _adresse);
            }
            StarteHinweise(context);
            Lies(context);
            return Task.CompletedTask;
        });
    }

    protected override async Task BeimStoppenAsync(IContext context)
    {
        if (_sicherung is not null)
        {
            // Nur im Ruhepunkt: war beim Stopp ein Eingang mitten in Arbeit, stimmt der Zustand nicht mit der Position überein — dann gilt die
            //   letzte Sicherung, und die nächste Aktivierung rechnet ab dort nach (jeder Log-Eingang wirkt genau einmal auf den gesicherten Zustand).
            if (ZuSichern(erzwingen: false) is not { } s) return;
            try
            {
                await _sicherung.SichereAsync(_b.Name, _adresse, s.Stand, s.Position, s.Daten, _zustandVersion, _zustandVerweis, CancellationToken.None)
                    .WaitAsync(TimeSpan.FromSeconds(10));
            }
            catch (Exception ex) { _logger?.LogDebug(ex, "[Funktion] {Funktion}/{Adresse}: Zustand beim Stopp nicht gesichert (die letzte Sicherung gilt)", _b.Name, _adresse); }
            return;
        }
        if (_lauf?.ZuSchreiben(true) is not { } neu) return;
        try { await _staende.SchreibeAsync(neu, CancellationToken.None).WaitAsync(TimeSpan.FromSeconds(5)); }
        catch (Exception ex) { _logger?.LogDebug(ex, "[Funktion] {Funktion}/{Partition}: Stand beim Stopp nicht geschrieben (wiederholungsfest)", _b.Name, _partition); }
    }

    /// <summary>Die Cluster-Identität des Funktions-Actors einer Partition (bzw. der einen Adresse).</summary>
    public static ClusterIdentity Identitaet(FunktionsBaustein b, int partition) =>
        ClusterIdentity.Create(b.EineAdresse ? Funktionsvertrag.EinzigeIdentitaet : partition.ToString(), b.KindName);

    /// <summary>Die Cluster-Identität des Funktions-Actors, der Eingänge des Streams <paramref name="stream"/> rechnet (A6).</summary>
    public static ClusterIdentity IdentitaetFuer(FunktionsBaustein b, Guid stream, Konsum konsum) =>
        ClusterIdentity.Create(b.Adresse(stream, konsum.Partitionen), b.KindName);
}
