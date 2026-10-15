namespace Abstractions;

/// <summary>
/// Fortschritt eines EMITTIERENDEN Konsumenten (Reaktion, Fluss-Event-Pfad): der durable Effekt ist das ausgegebene Command (idempotent
/// am Empfänger), NICHT ein co-committetes Read-Model. Vertrag: <b>durabel, at-least-once ab Cursor, kein Reset.</b>
/// <list type="bullet">
///   <item>Jede Weckung (Signal wie Poll) liest ab dem Cursor; es wird nie erneut ab 0 ausgegeben.</item>
///   <item>Der Cursor rückt erst vor, wenn jeder Command des Tails zugestellt oder abgelehnt ist. Er steht also nie hinter einem
///     unbestätigten Command; geht das Schreiben verloren, gibt die nächste Weckung den Tail mit denselben CommandIds erneut aus.</item>
///   <item>Kein <c>Reset</c>: Replay eines Emittenten bewegt echtes Geld. Der Emittenten-Pfad nimmt keinen <see cref="IProjectionTracker"/> —
///     blindes Replayen ist strukturell unmöglich (Achse B).</item>
/// </list>
/// <paramref name="partition"/> = die Konsumenten-Instanz (<c>{SubscriberId}:{StreamId}</c>); geschrieben nur vom Single-Writer-Adapter dieses Streams.
/// </summary>
public interface IEmittentenCursor
{
    /// <summary>Zuletzt fortgeschriebene Position dieser Partition, oder 0 wenn noch nichts.</summary>
    Task<long> LadeAsync(string partition, CancellationToken ct);

    /// <summary>Schreibt die Position <paramref name="bis"/> für diese Partition fort — erst nach bestätigtem Emit.</summary>
    Task SchreibeAsync(string partition, long bis, CancellationToken ct);
}
