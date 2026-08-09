using Abstractions;
using Core;

namespace Infrastructure.Projections;

/// <summary>
/// Die eine, store-agnostische Verarbeitungsschleife (Spec 7.3). Sie besitzt die
/// <b>Policy</b> — Marke lesen, ab Marke+1 geordnet lesen, Pre-Dispatch-Guard,
/// dispatchen, Marke vorrücken — und <b>keine Transaktion</b> (Spec 7.1).
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
    private readonly Func<EventEnvelope, ProjectionWriter, Task> _dispatch;
    private readonly IReadModelDepsSink? _depsSink;
    private readonly Func<Task>? _crashAfterEffectBeforeMark;

    /// <param name="tracker">
    /// REPLAYBARE Marke (Achse B, Projektion): co-committbar + Reset-fähig; null → tracker-los.
    /// </param>
    /// <param name="emittentenCursor">
    /// EMITTENTEN-Cursor (Achse B, Reaktion/Pipeline-Event): best-effort Fortschritt OHNE Reset.
    /// Schließt <paramref name="tracker"/> aus (ein Konsument ist replaybar ODER emittierend) —
    /// das ist der Compile-Zeit-Schnitt: der Cursor-Typ trägt kein Reset, blindes Replayen eines
    /// geld-bewegenden Emittenten ist damit strukturell unmöglich.
    /// </param>
    public ProjectionAdapter(
        IEventStoreRepository eventStore,
        IProjectionTracker? tracker,
        IEmittentenCursor? emittentenCursor,
        string projectionId,
        Func<EventEnvelope, ProjectionWriter, Task> dispatch,
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

    public async Task WakeAsync(Guid streamId, CancellationToken ct = default)
        => await WakeAsync(streamId, vomPoll: false, ct);

    public async Task WakeAsync(Guid streamId, bool vomPoll, CancellationToken ct = default)
    {
        int applied;
        if (_tracker is not null)
            applied = await _tracker.LastProcessedVersionAsync(_projectionId, streamId, ct);
        else if (_emittentenCursor is not null && !vomPoll)
            // Signal-Pfad: ab dem best-effort Cursor (Position = last+1 → applied = Position-1;
            // unset 0 → applied -1 → ab 0, robust gegen die Versions-Nummerierung).
            applied = (int)(await _emittentenCursor.LadeAsync(Partition(streamId), ct)) - 1;
        else
            // Tracker-los ODER Poll-Heilung eines Emittenten: bewusst ab 0 (at-least-once).
            applied = -1;

        var events = await _eventStore.ReadStreamAsync(streamId, applied + 1, ct);

        // ★ Audit-Fix H2: Die Deps (der verlierbare Versions-Index) werden während der Schleife nur GEPUFFERT
        //   und erst NACH dem durablen Co-Commit publiziert (siehe unten). Sonst meldete der Index eine
        //   Zwischen-Version, deren Effekt (noch) nicht committet ist → ein Client, der „warte bis Read-Model
        //   >= v" prüft, läse das leere/alte Read-Model als „konsistent".
        var pendingDeps = new List<(EventEnvelope Event, ProjectionWriter Writer)>();

        var last = applied;
        foreach (var e in events)
        {
            if (e.AggregateVersion <= applied) continue;   // Pre-Dispatch-Guard

            var writer = new ProjectionWriter(e.AggregateId, e.AggregateVersion);
            await _dispatch(e, writer);                     // Effekt (Store staged/schreibt)

            if (_depsSink is not null && writer.HasResults)
                pendingDeps.Add((e, writer));               // sammeln, NICHT hier publizieren (H2)

            if (_crashAfterEffectBeforeMark != null)        // Prüfstand-Absturzpunkt
                await _crashAfterEffectBeforeMark();

            last = e.AggregateVersion;
        }

        if (_tracker is not null && last > applied)
            await _tracker.MarkProcessedAsync(_projectionId, streamId, last, ct);  // Commit-Punkt (durabel)
        else if (_emittentenCursor is not null && last > applied)
            // Best-effort (kein Co-Commit): Position = last+1. Verlust heilt der Voll-Fold (Poll ab 0).
            await _emittentenCursor.SchreibeAsync(Partition(streamId), last + 1, ct);

        // ★ H2: Deps ERST jetzt — nach dem durablen Co-Commit — best-effort veröffentlichen. Der Index rückt
        //   damit nie über Uncommittetes vor. Ein Verlust hier ist folgenlos: der nächste Wake liefert sie neu.
        if (_depsSink is not null)
            foreach (var (evt, writer) in pendingDeps)
                await _depsSink.PublishAsync(_projectionId, evt, writer.GetResults(), ct);
    }
}
