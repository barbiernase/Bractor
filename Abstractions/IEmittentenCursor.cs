namespace Abstractions;

/// <summary>
/// Marken-Interface für EMITTIERENDE Konsumenten (Reaktion / Pipeline-Event / Prozess): der durable
/// Effekt ist das emittierte Command (idempotent am Empfänger), NICHT ein co-committetes Read-Model.
/// Trägt daher NUR den Cursor-Fortschritt und bewusst KEIN <c>Reset</c> — Replay eines Emittenten
/// bewegt echtes Geld. Der Compile-Zeit-Schnitt gegen <see cref="IReplaybarerTracker"/> (P4) macht
/// blindes Replayen unmöglich (Achse B).
///
/// Charakter: best-effort/Cache (v. a. beim Prozess — außerhalb der Entscheidungs-Transaktion;
/// Verlust/Inkonsistenz heilt der Voll-Fold). <paramref name="partition"/> = die Konsumenten-Instanz
/// (Ein-Strom: <c>StreamId</c>; Prozess: <c>Korrelation</c>).
/// </summary>
public interface IEmittentenCursor
{
    /// <summary>Zuletzt fortgeschriebene Position dieser Partition, oder 0 wenn noch nichts.</summary>
    Task<long> LadeAsync(string partition, CancellationToken ct);

    /// <summary>Schreibt die Position <paramref name="bis"/> für diese Partition fort (best-effort).</summary>
    Task SchreibeAsync(string partition, long bis, CancellationToken ct);
}
