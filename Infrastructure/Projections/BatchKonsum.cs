using Abstractions;
using Marten;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Proto;
using Proto.Cluster;

namespace Infrastructure.Projections;

/// <summary>
/// Die Weckung des Batch-Konsums (docs/04 §4.1), ein generischer Dienst für alle replaybaren Projektionen:
/// <list type="bullet">
///   <item><b>Signal</b> (schnell): ein <see cref="SignalReceiverActor"/> je Projektion abonniert die Signale ihrer Event-Typen und weckt die
///     Partition des Streams. Je Partition ist höchstens eine Weckung unterwegs; was währenddessen kommt, wird zu einer weiteren.</item>
///   <item><b>Poll</b> (Sicherheit): alle 30 s weckt er jede Partition — das Netz für verlorene Signale (der Leser liest ab seiner durablen
///     Position). Kein Heil-Poll je Stream mehr.</item>
/// </list>
/// Kennt keinen Domänentyp; die Kinds bringt der Generator als <see cref="IClusterKindContributor"/> bei.
/// </summary>
public sealed class BatchKonsumStartupService : IHostedService
{
    private readonly ActorSystem _system;
    private readonly IEnumerable<BatchRegistrierung> _registrierungen;
    private readonly Konsum _konsum;
    private readonly ILogger<BatchKonsumStartupService>? _logger;
    private readonly TimeSpan _pollIntervall;

    private readonly List<PID> _pids = new();
    private readonly List<Task> _pollSchleifen = new();
    private CancellationTokenSource? _stopp;

    public BatchKonsumStartupService(
        ActorSystem system, IEnumerable<BatchRegistrierung> registrierungen, Konsum konsum,
        ILogger<BatchKonsumStartupService>? logger = null)
        : this(system, registrierungen, konsum, logger, Konsum.PollIntervall) { }

    internal BatchKonsumStartupService(
        ActorSystem system, IEnumerable<BatchRegistrierung> registrierungen, Konsum konsum,
        ILogger<BatchKonsumStartupService>? logger, TimeSpan pollIntervall)
    {
        _system = system;
        _registrierungen = registrierungen;
        _konsum = konsum;
        _logger = logger;
        _pollIntervall = pollIntervall;
    }

    public async Task StartAsync(CancellationToken ct)
    {
        _stopp = new CancellationTokenSource();
        Console.WriteLine();
        Console.WriteLine($"=== Batch-Konsum (generiert, {_konsum.Partitionen} Partitionen, Seite {_konsum.BatchGroesse}) ===");

        foreach (var reg in _registrierungen)
        {
            var weckung = new Weckung(_system, reg.KindName, _logger);
            var signalTypen = SignalReceiver.SignalTypesFor(reg.EventTypes);
            var receiver = new SignalReceiver((streamId, _) =>
            {
                weckung.Wecke(Partitionierung.Von(streamId, _konsum.Partitionen));
                return Task.CompletedTask;
            });
            _pids.Add(_system.Root.Spawn(Props.FromProducer(() => new SignalReceiverActor(reg.KindName, signalTypen, receiver))));
            _pollSchleifen.Add(PollSchleifeAsync(weckung, _stopp.Token));
            Console.WriteLine($"  ✓ {reg.ProjektionsId}: Kind '{reg.KindName}', {signalTypen.Count} Signal-Typen → Partition (+ Poll {_pollIntervall.TotalSeconds:0}s je Partition)");
        }
        Console.WriteLine();
        await Task.CompletedTask;
    }

    private async Task PollSchleifeAsync(Weckung weckung, CancellationToken ct)
    {
        // Der erste Poll kurz nach dem Start (Cluster steht): holt nach, was während „unten“ geschah, und startet einen Neuaufbau.
        var erster = true;
        while (!ct.IsCancellationRequested)
        {
            try { await Task.Delay(erster ? TimeSpan.FromSeconds(2) : _pollIntervall, ct); }
            catch (OperationCanceledException) { break; }
            erster = false;
            for (var p = 0; p < _konsum.Partitionen; p++) weckung.Wecke(p);
        }
    }

    public async Task StopAsync(CancellationToken ct)
    {
        _stopp?.Cancel();
        foreach (var schleife in _pollSchleifen)
        {
            try { await schleife; } catch { /* egal */ }
        }
        foreach (var pid in _pids)
            await _system.Root.StopAsync(pid);
        _pids.Clear();
    }

    /// <summary>
    /// Weckt die Partitionen einer Projektion, zusammengefasst (<see cref="Weckungen{TKey}"/>): je Partition höchstens eine Weckung unterwegs
    /// und eine hinterher.
    /// </summary>
    private sealed class Weckung(ActorSystem system, string kind, ILogger? logger)
    {
        private readonly Weckungen<int> _weckungen = new(async partition =>
        {
            try
            {
                using var frist = new CancellationTokenSource(TimeSpan.FromSeconds(10));
                await system.Cluster().RequestAsync<WakeAck>(ClusterIdentity.Create(partition.ToString(), kind), new Wake(), frist.Token);
            }
            catch (Exception ex)
            {
                logger?.LogDebug(ex, "[Konsum] Weckung {Kind}/{Partition} verloren — der Poll heilt", kind, partition);
            }
        });

        public void Wecke(int partition) => _weckungen.Wecke(partition);
    }
}

/// <summary>Der Rückstand des Batch-Konsums für <c>/health</c> und <c>/monitoring/metrics</c>.</summary>
public interface IKonsumRueckstand
{
    /// <summary>Je Projektion: relevante Events jenseits der Position (Summe über die Partitionen) und geparkte Streams.</summary>
    Task<IReadOnlyList<KonsumStand>> ErfasseAsync(CancellationToken ct);
}

/// <summary>Rückstand einer Projektion: Events jenseits der Positionen ihrer Partitionen und geparkte Streams.</summary>
public sealed record KonsumStand(string ProjektionsId, long Rueckstand, int Geparkt);

/// <summary>
/// Liest den Rückstand aus dem Log und den Partitions-Checkpoints: je Projektion eine Abfrage — Events ihrer Typen, deren Sequenz über der
/// Position ihrer Partition liegt.
/// </summary>
public sealed class KonsumRueckstand(IDocumentStore marten, IEnumerable<BatchRegistrierung> registrierungen, Konsum konsum) : IKonsumRueckstand
{
    public async Task<IReadOnlyList<KonsumStand>> ErfasseAsync(CancellationToken ct)
    {
        var ergebnis = new List<KonsumStand>();
        await Infrastructure.Persistence.MartenEventStore.EventTabellenAsync(marten, ct);
        await using var s = marten.QuerySession();
        var schema = marten.Options.Events.DatabaseSchemaName;
        foreach (var reg in registrierungen)
        {
            var partitionen = await s.Query<PartitionsCheckpoint>().Where(p => p.ProjektionsId == reg.ProjektionsId).ToListAsync(ct);
            var positionen = new long[konsum.Partitionen];
            foreach (var p in partitionen.Where(p => p.Partition < konsum.Partitionen)) positionen[p.Partition] = p.Position;
            var typen = BatchLeser.TypenAus(reg.EventTypes)?.ToArray();
            var sql = $"select count(*) from {schema}.mt_events where seq_id > ? and seq_id > (?::bigint[])[{Infrastructure.Persistence.PartitionsAusdruck.Sql("stream_id")} % ? + 1]"
                      + (typen is null ? "" : " and type = any(?)");
            object[] parameter = typen is null
                ? [positionen.Min(), positionen, konsum.Partitionen]
                : [positionen.Min(), positionen, konsum.Partitionen, typen];
            var zahl = (await s.QueryAsync<long>(sql, ct, parameter)).FirstOrDefault();
            ergebnis.Add(new KonsumStand(reg.ProjektionsId, zahl, partitionen.Sum(p => p.Geparkt.Count)));
        }
        return ergebnis;
    }
}
