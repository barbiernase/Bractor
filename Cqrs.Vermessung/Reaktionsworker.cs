using Client.Infrastructure.Reaktionen;

namespace Cqrs.Vermessung;

/// <summary>
/// Der Client, der auf die Fakten einer Anwendung reagiert, neben ihrem Host, solange ein Lauf dauert (Schritt 6: ein Decide gibt nur Fakten aus, die
/// Folgen stößt ein Client an). Beispieldomäne: in S3 meldet er jedes fertige Teil am Sammelvorgang, in S4 löst er die Range auf und schließt das
/// Einfrieren ab. Prüfdomäne (S5): er gibt jeden Auftrag, jede Verteilung und jeden unterschrittenen Posten an den Kopf seines Graphen. Sein Stand
/// gilt für den Lauf (eine frische Datenbank je Wiederholung).
/// </summary>
internal sealed class Reaktionsworker : IAsyncDisposable
{
    public const string Token = "vermessung-reaktionsworker";
    private readonly CancellationTokenSource _ende = new();
    private readonly Task _lauf;

    private Reaktionsworker(Func<CancellationToken, Task> lauf) => _lauf = lauf(_ende.Token);

    public static Reaktionsworker Beispiel(int grpcPort) => new(ct => Domain.Client.Worker.Reaktionslauf.LaufeAsync($"http://localhost:{grpcPort}", Token,
        new ReaktionsStandImSpeicher(), "vermessung-reaktionsworker", ct, TimeSpan.FromMilliseconds(200)));

    /// <summary>Die Prüfdomäne läuft ohne Tor: kein Token.</summary>
    public static Reaktionsworker Pruefdomaene(int grpcPort) => new(ct => global::Pruefdomaene.Client.Worker.Reaktionslauf.LaufeAsync(
        $"http://localhost:{grpcPort}", null, new ReaktionsStandImSpeicher(), "vermessung-reaktionsworker", ct, TimeSpan.FromMilliseconds(200)));

    public async ValueTask DisposeAsync()
    {
        await _ende.CancelAsync();
        try { await _lauf; } catch { /* beendet */ }
        _ende.Dispose();
    }
}
