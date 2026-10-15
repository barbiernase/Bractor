using Abstractions;
using Microsoft.Extensions.Logging;
using Proto;
using Proto.Cluster;

namespace Infrastructure.Projections;

/// <summary>
/// Der Batch-Leser als VIRTUELLER CLUSTER-ACTOR je (Projektion, Partition) (docs/04 §4.1): Kind <c>batch-&lt;Projektion&gt;</c>, Identität =
/// Partition. Der Cluster hält genau eine Aktivierung je Partition; fällt das bei einer Netzstörung kurz aus, verliert die zweite Aktivierung
/// ihren Commit an der Version des Partitions-Checkpoints (G-4).
///
/// <b>Weckung, zusammengefasst:</b> <see cref="Wake"/> (Signal oder Poll) wird sofort quittiert. Läuft schon ein Durchlauf, merkt sich der
/// Actor „noch einmal“ — beliebig viele Weckungen während eines Durchlaufs ergeben genau einen weiteren. Der Durchlauf läuft neben der Mailbox
/// (<c>ReenterAfter</c>), damit Weckungen ihn nicht stauen. Scheitert er (Datenbank weg), heilt die nächste Weckung, spätestens der Poll.
///
/// Kein Leerlauf-Stopp: es gibt nur Projektionen × Partitionen dieser Actors, und der Poll weckt jede Partition ohnehin alle 30 s.
/// </summary>
public sealed class BatchLeserActor : IActor
{
    private readonly Func<int, (BatchLeser Leser, IDisposable? Bereich)> _fabrik;
    private readonly ILogger? _logger;

    private BatchLeser? _leser;
    private IDisposable? _bereich;
    private bool _laeuft;
    private bool _nochmal;
    private readonly CancellationTokenSource _stopp = new();

    /// <param name="fabrik">Partition → Leser samt seinem Fähigkeits-Bereich (eine Store-Instanz je Aktivierung, Co-Commit-Puffer).</param>
    public BatchLeserActor(Func<int, (BatchLeser, IDisposable?)> fabrik, ILogger? logger = null)
    {
        _fabrik = fabrik;
        _logger = logger;
    }

    public Task ReceiveAsync(IContext context)
    {
        switch (context.Message)
        {
            case Started:
                if (int.TryParse(context.ClusterIdentity()?.Identity, out var partition))
                    (_leser, _bereich) = _fabrik(partition);
                break;

            case Wake:
                context.Respond(new WakeAck());
                Starte(context);
                break;

            case Stopping:
                _stopp.Cancel();
                break;

            case Stopped:
                _bereich?.Dispose();
                break;
        }
        return Task.CompletedTask;
    }

    private void Starte(IContext context)
    {
        if (_leser is null) return;
        if (_laeuft) { _nochmal = true; return; }
        _laeuft = true;
        _nochmal = false;
        context.ReenterAfter(LaufAsync(_leser), _ =>
        {
            _laeuft = false;
            if (_nochmal && !_stopp.IsCancellationRequested) Starte(context);
            return Task.CompletedTask;
        });
    }

    private async Task LaufAsync(BatchLeser leser)
    {
        try
        {
            await leser.LaufAsync(_stopp.Token);
        }
        catch (OperationCanceledException) when (_stopp.IsCancellationRequested) { }
        catch (Exception ex)
        {
            _logger?.LogWarning(ex, "[Konsum] Durchlauf gescheitert — die nächste Weckung (spätestens der Poll) versucht es erneut");
        }
    }
}

/// <summary>
/// Ein Batch-Konsument (vom <c>PullPathGenerator</c> je replaybarer Projektion registriert): Cluster-Kind, Projektions-Id und die
/// abonnierten Event-Typen (für die Signale und den Typfilter).
/// </summary>
public sealed record BatchRegistrierung(string KindName, string ProjektionsId, IReadOnlyList<Type> EventTypes);
