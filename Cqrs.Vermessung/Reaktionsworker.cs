using Client.Infrastructure.Reaktionen;

namespace Cqrs.Vermessung;

/// <summary>
/// Der ReaktionsWorker der Beispieldomäne (Domain.Client.Worker) neben dem Host, solange ein Lauf dauert: ein Decide gibt nur Fakten aus, die
/// Folgen stößt dieser Client an — in S3 meldet er jedes fertige Teil am Sammelvorgang, in S4 löst er die Range auf und schließt das Einfrieren ab.
/// Sein Stand gilt für den Lauf (eine frische Datenbank je Wiederholung).
/// </summary>
internal sealed class Reaktionsworker : IAsyncDisposable
{
    public const string Token = "vermessung-reaktionsworker";
    private readonly CancellationTokenSource _ende = new();
    private readonly Task _lauf;

    public Reaktionsworker(int grpcPort)
        => _lauf = Domain.Client.Worker.Reaktionslauf.LaufeAsync($"http://localhost:{grpcPort}", Token, new ReaktionsStandImSpeicher(),
            "vermessung-reaktionsworker", _ende.Token, TimeSpan.FromMilliseconds(200));

    public async ValueTask DisposeAsync()
    {
        await _ende.CancelAsync();
        try { await _lauf; } catch { /* beendet */ }
        _ende.Dispose();
    }
}
