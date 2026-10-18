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

/// <summary>
/// Rückstand einer Projektion: Events ihrer Typen jenseits der Positionen ihrer Partitionen (gezählt höchstens bis
/// <see cref="KonsumRueckstand.ZaehlGrenze"/>; <paramref name="Gekappt"/> = es sind mehr) und geparkte Streams.
/// </summary>
public sealed record KonsumStand(string ProjektionsId, long Rueckstand, int Geparkt, bool Gekappt = false);

/// <summary>
/// Liest den Rückstand aus dem Log und den Partitions-Checkpoints: je Projektion eine Abfrage — Events ihrer Typen, deren Sequenz über der
/// Position ihrer Partition liegt. Gezählt wird nur über die ersten <see cref="ZaehlGrenze"/> Events hinter der kleinsten Position: steht eine
/// Partition weit zurück (oder hat noch keinen Checkpoint), läse ein volles <c>count(*)</c> bei jedem Abruf von <c>/health</c> das ganze Log.
/// </summary>
public sealed class KonsumRueckstand(IDocumentStore marten, IEnumerable<BatchRegistrierung> registrierungen, Konsum konsum) : IKonsumRueckstand
{
    /// <summary>So viele Events hinter der kleinsten Position sieht sich die Zählung höchstens an.</summary>
    public const int ZaehlGrenze = 1_000;

    /// <summary>
    /// Die Zählung (eine Quelle für den Store und den Plan-Test): unter den ersten $4 Events hinter $1 die, die hinter der Position ihrer
    /// Partition liegen ($2 Positionen, $3 Partitionen), optional nur die Typen $5; dazu, wie viele Events angesehen wurden.
    /// </summary>
    public static string Abfrage(string schema, bool mitTypen)
        => $"select count(*) filter (where seq_id > ($2::bigint[])[{Infrastructure.Persistence.PartitionsAusdruck.Sql("stream_id")} % $3 + 1]"
           + (mitTypen ? " and type = any($5)" : "") + "), count(*) "
           + $"from (select seq_id, stream_id, type from {schema}.mt_events where seq_id > $1 order by seq_id limit $4) x";

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
            await using var conn = marten.Storage.Database.CreateConnection();
            await conn.OpenAsync(ct);
            await using var cmd = new Npgsql.NpgsqlCommand(Abfrage(schema, typen is not null), conn);
            foreach (var w in typen is null
                         ? new object[] { positionen.Min(), positionen, konsum.Partitionen, ZaehlGrenze }
                         : new object[] { positionen.Min(), positionen, konsum.Partitionen, ZaehlGrenze, typen })
                cmd.Parameters.Add(new Npgsql.NpgsqlParameter { Value = w });
            long zahl, angesehen;
            await using (var r = await cmd.ExecuteReaderAsync(ct))
            {
                await r.ReadAsync(ct);
                zahl = r.GetInt64(0);
                angesehen = r.GetInt64(1);
            }
            ergebnis.Add(new KonsumStand(reg.ProjektionsId, zahl, partitionen.Sum(p => p.Geparkt.Count), Gekappt: angesehen >= ZaehlGrenze));
        }
        return ergebnis;
    }
}
