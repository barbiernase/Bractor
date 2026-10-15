using Abstractions;
using Core;
using Infrastructure.PubSub;

namespace Infrastructure.Projections;

/// <summary>
/// Die eine, store-agnostische Verarbeitungsschleife (docs/04 §4.1). Sie besitzt die
/// <b>Policy</b> — Marke lesen, ab Marke+1 geordnet lesen, Pre-Dispatch-Guard,
/// dispatchen, Marke vorrücken — und <b>keine Transaktion</b> (docs/04 §4.1).
///
/// Der <see cref="IProjectionTracker"/> ist optional:
/// <list type="bullet">
///   <item>vorhanden → Marke wird gelesen/vorgerückt; ob der Store sie mit seinem
///     Effekt gemeinsam gültig macht (Co-Commit), ist allein Store-Sache → dann
///     exactly-once-wirksam.</item>
///   <item>null → es wird stets ab 0 gelesen, der Guard ist wirkungslos; die
///     Verarbeitung läuft trotzdem, ist aber nur gültig, wenn der Effekt idempotent
///     ist. Das Framework verlangt das NICHT — es ist die Wahl des Entwicklers.</item>
/// </list>
///
/// Ein EMITTENT (Reaktion, Fluss-Event-Pfad) liest bei Signal UND Poll ab seinem Cursor (G-2). Seine Commands gehen je Tail parallel
/// hinaus (<see cref="EmitAusgaenge"/>) und werden gemeinsam abgewartet; der Cursor rückt nur vor, wenn jeder zugestellt oder abgelehnt
/// ist. Bleibt einer unbekannt, meldet die Weckung „nicht aufgeholt“ — der Poll hält seine Hochwassermarke und weckt erneut, der Tail
/// geht mit denselben CommandIds noch einmal hinaus (die Inbox des Ziels verwirft, was schon gewirkt hat).
///
/// Der Adapter besitzt den Write-Scope: er erzeugt pro Event einen
/// <see cref="ProjectionWriter"/> mit der kausalen Position und reicht die
/// gesammelten Ergebnisse an den optionalen <see cref="IReadModelDepsSink"/> —
/// so fällt der (verlierbare) Versions-Index aus derselben Quelle wie die Marke,
/// auch auf dem Pull-Pfad.
/// </summary>
public sealed class ProjectionAdapter
{
    private readonly IEventStoreRepository _eventStore;
    private readonly IProjectionTracker? _tracker;
    private readonly IEmittentenCursor? _emittentenCursor;
    private readonly string _projectionId;
    private readonly Func<EventEnvelope, ProjectionWriter, EmitAusgaenge, Task> _dispatch;
    private readonly IReadModelDepsSink? _depsSink;
    private readonly Func<Task>? _crashAfterEffectBeforeMark;

    /// <param name="tracker">
    /// REPLAYBARE Marke (Achse B, Projektion): co-committbar + Reset-fähig; null → tracker-los.
    /// </param>
    /// <param name="emittentenCursor">
    /// EMITTENTEN-Cursor (Achse B, Reaktion/Pipeline-Event): durabler Fortschritt OHNE Reset, vorgerückt nach bestätigtem Emit.
    /// Schließt <paramref name="tracker"/> aus (ein Konsument ist replaybar ODER emittierend) —
    /// das ist der Compile-Zeit-Schnitt: der Cursor-Typ trägt kein Reset, blindes Replayen eines
    /// geld-bewegenden Emittenten ist damit strukturell unmöglich.
    /// </param>
    public ProjectionAdapter(
        IEventStoreRepository eventStore,
        IProjectionTracker? tracker,
        IEmittentenCursor? emittentenCursor,
        string projectionId,
        Func<EventEnvelope, ProjectionWriter, EmitAusgaenge, Task> dispatch,
        IReadModelDepsSink? depsSink = null,
        Func<Task>? crashAfterEffectBeforeMark = null)
    {
        if (tracker is not null && emittentenCursor is not null)
            throw new InvalidOperationException(
                $"{projectionId}: Tracker (replaybar) UND Emittenten-Cursor (emittierend) gesetzt — " +
                "ein Konsument ist genau eine Achsen-B-Ausprägung.");

        _eventStore = eventStore;
        _tracker = tracker;
        _emittentenCursor = emittentenCursor;
        _projectionId = projectionId;
        _dispatch = dispatch;
        _depsSink = depsSink;
        _crashAfterEffectBeforeMark = crashAfterEffectBeforeMark;
    }

    /// <summary>Partition des Emittenten-Cursors: pro (Konsument, Stream) — sonst kollidierten zwei
    /// Konsumenten desselben Streams.</summary>
    private string Partition(Guid streamId) => $"{_projectionId}:{streamId}";

    /// <summary>Wie oft ein Stapel nach einem Schreibkonflikt sofort wiederholt wird, bevor der Poll übernimmt.</summary>
    private const int KonfliktVersuche = 5;

    /// <summary>
    /// Eine Weckung (Signal oder Poll — gleich behandelt): ab der Marke bzw. dem Cursor lesen, anwenden, vorrücken. Liefert, ob der Stream
    /// bestätigt aufgeholt ist; false heißt: ein ausgegebener Command blieb unbekannt, der Cursor steht noch vor seinem Event.
    /// </summary>
    public async Task<bool> WakeAsync(Guid streamId, CancellationToken ct = default)
    {
        for (var versuch = 1; ; versuch++)
        {
            try
            {
                return await WakeEinmalAsync(streamId, ct);
            }
            catch (SchreibKonfliktException) when (versuch < KonfliktVersuche)
            {
                // Ein geteiltes Dokument wurde von einem anderen Stream zwischenzeitlich geändert. Nichts aus dem
                // Stapel ist durabel (Effekte und Marke fielen gemeinsam zurück) → frisch ab der Marke wiederholen;
                // kurz und gestreut warten, damit zwei Konkurrenten nicht im Gleichtakt erneut kollidieren.
                // Nach dem letzten Versuch fliegt die Ausnahme: kein WakeAck, der Poll weckt erneut.
                await Task.Delay(Random.Shared.Next(5, 25 * versuch), ct);
            }
        }
    }

    private async Task<bool> WakeEinmalAsync(Guid streamId, CancellationToken ct)
    {
        int applied;
        if (_tracker is not null)
            applied = await _tracker.LastProcessedVersionAsync(_projectionId, streamId, ct);
        else if (_emittentenCursor is not null)
            // Emittent: ab dem Cursor (Position = last+1 → applied = Position-1; unset 0 → applied -1 → ab 0). Der Cursor steht nie
            //   hinter einem unbestätigten Command — er rückt erst nach dem bestätigten Emit vor (G-2).
            applied = (int)(await _emittentenCursor.LadeAsync(Partition(streamId), ct)) - 1;
        else
            // Weder Marke noch Cursor: bewusst ab 0 (at-least-once, nur für idempotente Effekte gültig).
            applied = -1;

        var events = await _eventStore.ReadStreamAsync(streamId, applied + 1, ct);

        // ★ Audit-Fix H2: Die Deps (der verlierbare Versions-Index) werden während der Schleife nur GEPUFFERT
        //   und erst NACH dem durablen Co-Commit publiziert (siehe unten). Sonst meldete der Index eine
        //   Zwischen-Version, deren Effekt (noch) nicht committet ist → ein Client, der „warte bis Read-Model
        //   >= v" prüft, läse das leere/alte Read-Model als „konsistent".
        var pendingDeps = new List<(EventEnvelope Event, ProjectionWriter Writer)>();

        var ausgaenge = new EmitAusgaenge(ct);              // die Commands dieser Weckung (nur Emittenten geben welche aus)
        var last = applied;
        foreach (var e in events)
        {
            if (e.AggregateVersion <= applied) continue;   // Pre-Dispatch-Guard

            var writer = new ProjectionWriter(e.AggregateId, e.AggregateVersion);
            await _dispatch(e, writer, ausgaenge);          // Effekt (Store staged/schreibt) bzw. Commands gestartet

            if (_depsSink is not null && writer.HasResults)
                pendingDeps.Add((e, writer));               // sammeln, NICHT hier publizieren (H2)

            if (_crashAfterEffectBeforeMark != null)        // Prüfstand-Absturzpunkt
                await _crashAfterEffectBeforeMark();

            last = e.AggregateVersion;
        }

        // Die Commands des Tails gemeinsam abwarten (parallel gestartet). Eine Projektion gibt keine aus (GR-AUS-PROJEKTION) — dann sofort.
        if (!await ausgaenge.AlleEntschiedenAsync())
            return false;                                   // Cursor bleibt vor dem Tail; die nächste Weckung sendet ihn erneut

        if (_tracker is not null && last > applied)
            await _tracker.MarkProcessedAsync(_projectionId, streamId, last, ct);  // Commit-Punkt (durabel)
        else if (_emittentenCursor is not null && last > applied)
            // Nach bestätigtem Emit: Position = last+1. Scheitert das Schreiben, sendet die nächste Weckung den Tail noch einmal
            //   (dieselben CommandIds, die Inbox des Ziels verwirft sie).
            await _emittentenCursor.SchreibeAsync(Partition(streamId), last + 1, ct);

        // ★ H2: Deps ERST jetzt — nach dem durablen Co-Commit — best-effort veröffentlichen. Der Index rückt
        //   damit nie über Uncommittetes vor. Ein Verlust hier ist folgenlos: der nächste Wake liefert sie neu.
        if (_depsSink is not null)
            foreach (var (evt, writer) in pendingDeps)
                await _depsSink.PublishAsync(_projectionId, evt, writer.GetResults(), ct);
        return true;
    }
}
