using Abstractions;
using Infrastructure.Lebenszyklus;
using Microsoft.Extensions.Logging;
using Proto;
using Proto.Cluster;

namespace Infrastructure.Projections;

/// <summary>
/// Der Batch-Leser als VIRTUELLER CLUSTER-ACTOR je (Projektion, Partition) (docs/04 §4.1): Kind <c>batch-&lt;Projektion&gt;</c>, Identität =
/// Partition. Der Cluster hält genau eine Aktivierung je Partition; fällt das bei einer Netzstörung kurz aus, verliert die zweite Aktivierung
/// ihren Commit an der Version des Partitions-Checkpoints (G-4).
///
/// <b>Weckung, zusammengefasst</b> (Rahmen, <see cref="RahmenActor.Wecke"/>): <see cref="Wake"/> (Signal oder Poll) wird sofort quittiert.
/// Läuft schon ein Durchlauf, merkt sich der Actor „noch einmal“ — beliebig viele Weckungen während eines Durchlaufs ergeben genau einen
/// weiteren. Der Durchlauf läuft neben der Mailbox, damit Weckungen ihn nicht stauen. Scheitert er (Datenbank weg), heilt die nächste Weckung,
/// spätestens der Poll. Der Fähigkeits-Bereich (Store-Instanz, Co-Commit-Puffer) gehört der Aktivierung: beim Stopp bricht der Durchlauf ab, der
/// Bereich wird erst frei, wenn er geendet hat.
///
/// Kein Leerlauf-Stopp: es gibt nur Projektionen × Partitionen dieser Actors, und der Poll weckt jede Partition ohnehin alle 30 s.
/// </summary>
public sealed class BatchLeserActor : RahmenActor
{
    private readonly Func<int, IFaehigkeiten, BatchLeser> _fabrik;
    private readonly ILogger? _logger;
    private BatchLeser? _leser;

    /// <param name="fabrik">Partition und Fähigkeits-Bereich der Aktivierung → Leser (eine Store-Instanz je Aktivierung, Co-Commit-Puffer).</param>
    /// <param name="faehigkeiten">öffnet den Bereich der Aktivierung.</param>
    public BatchLeserActor(Func<int, IFaehigkeiten, BatchLeser> fabrik, IFaehigkeitsFabrik faehigkeiten, ILogger? logger = null)
        : base(logger, faehigkeiten: faehigkeiten)
    {
        _fabrik = fabrik;
        _logger = logger;
    }

    protected override Task BeginneAsync(IContext context)
    {
        if (int.TryParse(context.ClusterIdentity()?.Identity, out var partition))
            _leser = _fabrik(partition, Bereich);
        return Task.CompletedTask;
    }

    protected override Task BehandleAsync(IContext context)
    {
        if (context.Message is Wake)
        {
            context.Respond(new WakeAck());
            if (_leser is { } leser) Wecke(context, stopp => LaufAsync(leser, stopp));
        }
        return Task.CompletedTask;
    }

    private async Task LaufAsync(BatchLeser leser, CancellationToken stopp)
    {
        try
        {
            await leser.LaufAsync(stopp);
        }
        catch (OperationCanceledException) when (stopp.IsCancellationRequested) { }
        catch (Exception ex)
        {
            _logger?.LogWarning(ex, "[Konsum] Durchlauf gescheitert — die nächste Weckung (spätestens der Poll) versucht es erneut");
        }
    }
}

/// <summary>
/// Ein Batch-Konsument (vom <c>PullPathGenerator</c> je replaybarer Projektion registriert): Cluster-Kind, Projektions-Id und die
/// abonnierten Event-Typen (für die Signale und den Typfilter). Mit <paramref name="EineAdresse"/> (eine Funktion mit <c>[EineAdresse]</c>, Plan §2.9)
/// liest EIN Actor (Identität 0) alle Partitionen — geweckt, gepollt und gezählt wird dann nur er.
/// </summary>
public sealed record BatchRegistrierung(string KindName, string ProjektionsId, IReadOnlyList<Type> EventTypes, bool EineAdresse = false)
{
    /// <summary>In wie viele Partitionen dieser Konsument liest.</summary>
    public int Partitionen(Konsum konsum) => EineAdresse ? 1 : konsum.Partitionen;
}
