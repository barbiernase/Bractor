namespace Infrastructure.Einblick;

/// <summary>
/// „Es gibt Neues im Log“ — vom Appender nach jedem Commit geweckt (<c>MartenEventStore</c>, Group-Commit), von der Einblick-SSE
/// (<c>/einblick/neu</c>) erwartet. Nur ein Weckruf ohne Inhalt (wie das Signal im Kern, Invariante 2): wer geweckt wird, liest selbst
/// die höchste Sequenz; ein verlorener Weckruf kostet nur Latenz, die SSE prüft ohnehin periodisch.
/// </summary>
public static class SpurSignal
{
    private static TaskCompletionSource _tcs = new(TaskCreationOptions.RunContinuationsAsynchronously);

    /// <summary>Weckt alle Wartenden.</summary>
    public static void Melde() => Interlocked.Exchange(ref _tcs, new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously)).TrySetResult();

    /// <summary>Wartet auf den nächsten Weckruf, höchstens <paramref name="hoechstens"/>.</summary>
    public static async Task WarteAsync(TimeSpan hoechstens, CancellationToken ct)
    {
        try { await Volatile.Read(ref _tcs).Task.WaitAsync(hoechstens, ct); }
        catch (TimeoutException) { }
    }
}
