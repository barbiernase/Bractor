using Abstractions;
using Core;
using Infrastructure.PubSub;

namespace Infrastructure.Projections;

/// <summary>
/// Die Schleife je (Emittent, Stream) (docs/04 §4.1): Reaktionen und der Fluss-Event-Pfad. Projektionen laufen im Batch-Konsum
/// (<see cref="BatchLeser"/>); ein Emittent bleibt je Stream (E8), weil sein Cursor erst nach bestätigtem Emit vorrückt — ein Cursor je
/// Partition ließe einen unbekannten Emit die ganze Partition halten.
///
/// Signal und Poll lesen ab dem Cursor (G-2). Die Commands eines Tails gehen parallel hinaus (<see cref="EmitAusgaenge"/>) und werden
/// gemeinsam abgewartet; der Cursor rückt nur vor, wenn jeder zugestellt oder abgelehnt ist. Bleibt einer unbekannt, meldet die Weckung
/// „nicht aufgeholt“ — der Poll hält seine Hochwassermarke und weckt erneut, der Tail geht mit denselben CommandIds noch einmal hinaus (die
/// Inbox des Ziels verwirft, was schon gewirkt hat). Ohne Cursor liest der Adapter bewusst ab 0 (at-least-once, nur für idempotente Effekte).
///
/// Der Adapter besitzt den Write-Scope: er erzeugt pro Event einen <see cref="ProjectionWriter"/> mit der kausalen Position und reicht die
/// gesammelten Ergebnisse an den optionalen <see cref="IReadModelDepsSink"/>.
/// </summary>
public sealed class EmittentenAdapter
{
    private readonly IEventStoreRepository _eventStore;
    private readonly IEmittentenCursor? _emittentenCursor;
    private readonly string _konsumentId;
    private readonly Func<EventEnvelope, ProjectionWriter, EmitAusgaenge, Task> _dispatch;
    private readonly IReadModelDepsSink? _depsSink;

    /// <param name="emittentenCursor">Durabler Fortschritt OHNE Reset, vorgerückt nach bestätigtem Emit; null → ab 0.</param>
    public EmittentenAdapter(
        IEventStoreRepository eventStore,
        IEmittentenCursor? emittentenCursor,
        string konsumentId,
        Func<EventEnvelope, ProjectionWriter, EmitAusgaenge, Task> dispatch,
        IReadModelDepsSink? depsSink = null)
    {
        _eventStore = eventStore;
        _emittentenCursor = emittentenCursor;
        _konsumentId = konsumentId;
        _dispatch = dispatch;
        _depsSink = depsSink;
    }

    /// <summary>Partition des Emittenten-Cursors: pro (Konsument, Stream) — sonst kollidierten zwei Konsumenten desselben Streams.</summary>
    private string Partition(Guid streamId) => $"{_konsumentId}:{streamId}";

    /// <summary>
    /// Eine Weckung (Signal oder Poll — gleich behandelt): ab dem Cursor lesen, anwenden, vorrücken. Liefert, ob der Stream bestätigt aufgeholt
    /// ist; false heißt: ein ausgegebener Command blieb unbekannt, der Cursor steht noch vor seinem Event.
    /// </summary>
    public async Task<bool> WakeAsync(Guid streamId, CancellationToken ct = default)
    {
        // Ab dem Cursor (Position = last+1 → applied = Position-1; unset 0 → applied -1 → ab 0). Der Cursor steht nie hinter einem
        //   unbestätigten Command — er rückt erst nach dem bestätigten Emit vor (G-2). Ohne Cursor bewusst ab 0.
        var applied = _emittentenCursor is null ? -1 : (int)(await _emittentenCursor.LadeAsync(Partition(streamId), ct)) - 1;

        var events = await _eventStore.ReadStreamAsync(streamId, applied + 1, ct);
        var pendingDeps = new List<(EventEnvelope Event, ProjectionWriter Writer)>();
        var ausgaenge = new EmitAusgaenge(ct);              // die Commands dieser Weckung
        var last = applied;
        foreach (var e in events)
        {
            if (e.AggregateVersion <= applied) continue;   // Pre-Dispatch-Guard

            var writer = new ProjectionWriter(e.AggregateId, e.AggregateVersion);
            await _dispatch(e, writer, ausgaenge);          // Commands gestartet
            if (_depsSink is not null && writer.HasResults)
                pendingDeps.Add((e, writer));
            last = e.AggregateVersion;
        }

        // Die Commands des Tails gemeinsam abwarten (parallel gestartet).
        if (!await ausgaenge.AlleEntschiedenAsync())
            return false;                                   // Cursor bleibt vor dem Tail; die nächste Weckung sendet ihn erneut

        if (_emittentenCursor is not null && last > applied)
            // Nach bestätigtem Emit: Position = last+1. Scheitert das Schreiben, sendet die nächste Weckung den Tail noch einmal
            //   (dieselben CommandIds, die Inbox des Ziels verwirft sie).
            await _emittentenCursor.SchreibeAsync(Partition(streamId), last + 1, ct);

        if (_depsSink is not null)
            foreach (var (evt, writer) in pendingDeps)
                await _depsSink.PublishAsync(_konsumentId, evt, writer.GetResults(), ct);
        return true;
    }
}
