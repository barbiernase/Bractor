using System.Collections.Concurrent;

namespace Client.Infrastructure.Connection;

/// <summary>
/// Verfolgt gesendete Client-Commands, bis eine korrelierte Server-Antwort (ein Event oder ein
/// CommandFailed mit derselben CorrelationId) eintrifft — die Grundlage des T2b-Retry-Loops.
///
/// Der Loop sendet bei Stille erneut; weil der Client seit T2b eine DETERMINISTISCHE CommandId
/// wiederverwendet, dedupliziert das Aggregat einen Doppel-Send idempotent (kein Doppel-Effekt).
/// Trifft nichts ein, meldet der Loop bewusst „unbestätigt" (Ausgang unbekannt, sicher wiederholbar),
/// NICHT „fehlgeschlagen".
///
/// Registrieren MUSS vor dem Senden geschehen, damit ein sehr früher Ack nicht verloren geht.
/// Rein und ohne Timer — die Wartelogik (Timeout) liegt beim Aufrufer, damit sie testbar bleibt.
/// </summary>
public sealed class PendingCommandTracker
{
    private readonly ConcurrentDictionary<string, TaskCompletionSource<bool>> _pending = new();

    /// <summary>Vor dem Senden registrieren. Mehrfach-Registrieren derselben CorrelationId ist folgenlos.</summary>
    public void Register(string correlationId)
        => _pending.TryAdd(correlationId,
            new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously));

    /// <summary>Eine korrelierte Antwort ist eingetroffen → die Wartenden freigeben. Unbekannte Id: no-op.</summary>
    public void Ack(string correlationId)
    {
        if (_pending.TryGetValue(correlationId, out var tcs))
            tcs.TrySetResult(true);
    }

    /// <summary>Aufräumen (nach Abschluss/Aufgabe des Loops).</summary>
    public void Forget(string correlationId) => _pending.TryRemove(correlationId, out _);

    public bool IsPending(string correlationId) => _pending.ContainsKey(correlationId);

    public int PendingCount => _pending.Count;

    /// <summary>
    /// Wartet bis zum Ack ODER Timeout. Liefert true = geackt (Antwort kam), false = Timeout.
    /// Ist die CorrelationId nicht (mehr) registriert, gilt sie als geackt.
    /// </summary>
    public async Task<bool> WaitAsync(string correlationId, TimeSpan timeout, CancellationToken ct)
    {
        if (!_pending.TryGetValue(correlationId, out var tcs))
            return true;

        using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        var completed = await Task.WhenAny(tcs.Task, Task.Delay(timeout, cts.Token)).ConfigureAwait(false);
        if (completed == tcs.Task)
        {
            cts.Cancel();          // den Delay abräumen
            return true;
        }
        return false;              // Timeout
    }
}
