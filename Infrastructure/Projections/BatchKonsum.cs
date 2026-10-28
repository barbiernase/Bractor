using Abstractions;
using Infrastructure.Wecken;
using Marten;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Proto;
using Proto.Cluster;

namespace Infrastructure.Projections;

/// <summary>
/// Der Takt des Batch-Konsums (docs/04 §4.1), ein generischer Dienst für alle Projektionen und Funktionen mit Log-Eingängen:
/// <list type="bullet">
///   <item><b>Beim Start</b> prüft er den Weckplan (<see cref="Wecker.Pruefe"/>): jeder registrierte Konsument steht mit jedem Typ, den er hört,
///     im Plan — sonst weckte ihn ein Commit nie, nur der Takt.</item>
///   <item><b>Takt</b> (Sicherheit): alle 30 s weckt er jede Partition — das Netz für verlorene Weckrufe (der Leser liest ab seiner durablen
///     Position). Schnell weckt der <see cref="Wecker"/> am Commit.</item>
/// </list>
/// Kennt keinen Domänentyp; die Kinds bringt der Generator als <see cref="IClusterKindContributor"/> bei.
/// </summary>
public sealed class BatchKonsumStartupService : IHostedService
{
    private readonly IEnumerable<BatchRegistrierung> _registrierungen;
    private readonly Konsum _konsum;
    private readonly Wecker _wecker;
    private readonly TimeSpan _pollIntervall;

    private readonly List<Task> _pollSchleifen = new();
    private CancellationTokenSource? _stopp;

    public BatchKonsumStartupService(IEnumerable<BatchRegistrierung> registrierungen, Konsum konsum, Wecker wecker)
        : this(registrierungen, konsum, wecker, Konsum.PollIntervall) { }

    internal BatchKonsumStartupService(IEnumerable<BatchRegistrierung> registrierungen, Konsum konsum, Wecker wecker, TimeSpan pollIntervall)
    {
        _registrierungen = registrierungen;
        _konsum = konsum;
        _wecker = wecker;
        _pollIntervall = pollIntervall;
    }

    public Task StartAsync(CancellationToken ct)
    {
        Wecker.Pruefe(_wecker.Plan, _registrierungen);
        _stopp = new CancellationTokenSource();
        Console.WriteLine();
        Console.WriteLine($"=== Batch-Konsum (generiert, {_konsum.Partitionen} Partitionen, Seite {_konsum.BatchGroesse}) ===");
        foreach (var reg in _registrierungen)
        {
            _pollSchleifen.Add(TaktAsync(reg.KindName, reg.Partitionen(_konsum), _stopp.Token));
            Console.WriteLine($"  ✓ {reg.ProjektionsId}: Kind '{reg.KindName}', {reg.EventTypes.Count} Typen im Weckplan (+ Takt {_pollIntervall.TotalSeconds:0}s je Partition)");
        }
        Console.WriteLine();
        return Task.CompletedTask;
    }

    private async Task TaktAsync(string kind, int partitionen, CancellationToken ct)
    {
        // Der erste Takt kurz nach dem Start (Cluster steht): holt nach, was während „unten“ geschah, und startet einen Neuaufbau.
        var erster = true;
        while (!ct.IsCancellationRequested)
        {
            try { await Task.Delay(erster ? TimeSpan.FromSeconds(2) : _pollIntervall, ct); }
            catch (OperationCanceledException) { break; }
            erster = false;
            for (var p = 0; p < partitionen; p++) _wecker.Wecke(kind, p.ToString());
        }
    }

    public async Task StopAsync(CancellationToken ct)
    {
        _stopp?.Cancel();
        foreach (var schleife in _pollSchleifen)
        {
            try { await schleife; } catch { /* egal */ }
        }
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
            var anzahl = reg.Partitionen(konsum);
            var positionen = new long[anzahl];
            foreach (var p in partitionen.Where(p => p.Partition < anzahl)) positionen[p.Partition] = p.Position;
            var typen = BatchLeser.TypenAus(reg.EventTypes)?.ToArray();
            await using var conn = marten.Storage.Database.CreateConnection();
            await conn.OpenAsync(ct);
            await using var cmd = new Npgsql.NpgsqlCommand(Abfrage(schema, typen is not null), conn);
            foreach (var w in typen is null
                         ? new object[] { positionen.Min(), positionen, anzahl, ZaehlGrenze }
                         : new object[] { positionen.Min(), positionen, anzahl, ZaehlGrenze, typen })
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
