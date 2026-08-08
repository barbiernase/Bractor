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
    private readonly string _projectionId;
    private readonly Func<EventEnvelope, ProjectionWriter, Task> _dispatch;
    private readonly IReadModelDepsSink? _depsSink;
    private readonly Func<Task>? _crashAfterEffectBeforeMark;

    public ProjectionAdapter(
        IEventStoreRepository eventStore,
        IProjectionTracker? tracker,
        string projectionId,
        Func<EventEnvelope, ProjectionWriter, Task> dispatch,
        IReadModelDepsSink? depsSink = null,
        Func<Task>? crashAfterEffectBeforeMark = null)
    {
        _eventStore = eventStore;
        _tracker = tracker;
        _projectionId = projectionId;
        _dispatch = dispatch;
        _depsSink = depsSink;
        _crashAfterEffectBeforeMark = crashAfterEffectBeforeMark;
    }

    public async Task WakeAsync(Guid streamId, CancellationToken ct = default)
    {
        var applied = _tracker is null
            ? -1
            : await _tracker.LastProcessedVersionAsync(_projectionId, streamId, ct);

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

        // ★ H2: Deps ERST jetzt — nach dem durablen Co-Commit — best-effort veröffentlichen. Der Index rückt
        //   damit nie über Uncommittetes vor. Ein Verlust hier ist folgenlos: der nächste Wake liefert sie neu.
        if (_depsSink is not null)
            foreach (var (evt, writer) in pendingDeps)
                await _depsSink.PublishAsync(_projectionId, evt, writer.GetResults(), ct);
    }
}
