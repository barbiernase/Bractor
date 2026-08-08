// ══════════════════════════════════════════════════════════════════════════════
// Load-Harness — zwei Modi, jeweils Cluster EINMAL gebootet (wie Produktion, kein
// Cold-Boot-Flake, Readiness-Barriere).
//
//   --mode aggregate  (Default): Command→Event→Persistenz. Miss Schreibpfad + Exactly-once.
//       dotnet run --project LoadHarness -- --mode aggregate --accounts 500 --credits 40 --concurrency 128
//
//   --mode pipeline: eingehende Pipeline-Trigger → Ack, OHNE Persistenz (No-Op-Pipeline).
//       dotnet run --project LoadHarness -- --mode pipeline --messages 50000 --concurrency 128
//       ⚠ Eine Pipeline = EIN Actor = serielle Mailbox → gemessen wird der Durchsatz EINER Pipeline.
//         Sender-Concurrency erhöht nur die In-Flight-Requests, nicht die Parallelität im Actor.
//
//   --log warning (Default) = saubere Messung; --log debug = Proto/Consul-Logs sichtbar.
// ══════════════════════════════════════════════════════════════════════════════
using System.Collections.Concurrent;
using System.Diagnostics;
using Abstractions;
using Domain.Konto;
using Domain.Pipeline.Benchmark;        // BenchPing (No-Op-Trigger)
using Domain.Pipeline.Infrastructure;   // AddDomainPipelineServices
using Infrastructure.Aggregate;         // AggregateRehydrator
using Infrastructure.Extensions;        // AddCqrsFramework
using Infrastructure.Pipeline;          // GeneratedPipelines
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Proto.Cluster;

string mode     = ArgStr("--mode", "aggregate");
int concurrency = ArgInt("--concurrency", 64);
LogLevel level  = ParseLevel(ArgStr("--log", "warning"));

var watchDir = Path.Combine(Path.GetTempPath(), "loadtest-" + Guid.NewGuid().ToString("N"));
Directory.CreateDirectory(watchDir);

var builder = Host.CreateApplicationBuilder();
builder.Logging.ClearProviders();
builder.Logging.AddSimpleConsole(o => { o.SingleLine = true; o.TimestampFormat = "HH:mm:ss.fff "; });
builder.Logging.SetMinimumLevel(level);

builder.Services.AddCqrsFramework(opts =>
{
    opts.EnableGrpc = false;
    opts.ClusterName = "loadtest-" + Guid.NewGuid().ToString("N")[..8];
    opts.SnapshotThreshold = 200;
});
builder.Services.AddDomainPipelineServices(watchPath: watchDir, preprocessedPath: null);
GeneratedPipelines.RegisterAllPipelines(builder.Services);

using var host = builder.Build();
Proto.Log.SetLoggerFactory(host.Services.GetRequiredService<ILoggerFactory>());
await host.StartAsync();

var system  = host.Services.GetRequiredService<Proto.ActorSystem>();
var cluster = system.Cluster();

int exit;
try
{
    exit = mode.ToLowerInvariant() switch
    {
        "pipeline" => await RunPipeline(),
        _          => await RunAggregate(),
    };
}
finally
{
    await host.StopAsync();
    try { Directory.Delete(watchDir, recursive: true); } catch { /* egal */ }
}
return exit;

// ══════════════════════════════════════════════════════════════════════════════
// MODUS PIPELINE — eingehende Trigger ohne Persistenz
// ══════════════════════════════════════════════════════════════════════════════
async Task<int> RunPipeline()
{
    int messages = ArgInt("--messages", 50000);
    var identity = ClusterIdentity.Create("benchmark", "Pipeline-benchmark");

    if (!await WaitRoutableCore()) return 2;

    var latencies = new ConcurrentBag<double>();
    long ok = 0, fail = 0;

    Console.WriteLine($"Pipeline-Last: {messages} Trigger an EINE Pipeline (serielle Mailbox), Concurrency {concurrency}");
    var sw = Stopwatch.StartNew();

    await Parallel.ForEachAsync(Enumerable.Range(0, messages),
        new ParallelOptions { MaxDegreeOfParallelism = concurrency },
        async (i, _) =>
        {
            var t = Stopwatch.StartNew();
            try
            {
                using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(15));
                var ack = await cluster.RequestAsync<PipelineAck>(identity, new BenchPing(i), cts.Token);
                t.Stop();
                if (ack is null) { Interlocked.Increment(ref fail); return; }
                latencies.Add(t.Elapsed.TotalMilliseconds);
                Interlocked.Increment(ref ok);
            }
            catch { Interlocked.Increment(ref fail); }
        });

    sw.Stop();
    Report("PIPELINE-REPORT (Trigger→Ack, ohne Persistenz)", ok, fail, sw, latencies);
    Console.WriteLine("Hinweis: EIN Pipeline-Actor, serielle Verarbeitung — das ist der Durchsatz EINER Pipeline.");
    Console.WriteLine("============================================");
    return fail == 0 ? 0 : 1;
}

// ══════════════════════════════════════════════════════════════════════════════
// MODUS AGGREGATE — Command→Event→Persistenz + Exactly-once-Prüfung
// ══════════════════════════════════════════════════════════════════════════════
async Task<int> RunAggregate()
{
    int accounts   = ArgInt("--accounts", 200);
    int creditsPer = ArgInt("--credits", 20);
    const decimal Start = 1000m, Credit = 10m;
    decimal expected = Start + creditsPer * Credit;
    int total = accounts * (1 + creditsPer);

    if (!await WaitRoutableCore()) return 2;

    var ids = Enumerable.Range(0, accounts).Select(_ => Guid.NewGuid()).ToArray();
    var latencies = new ConcurrentBag<double>();
    long ok = 0, fail = 0;

    async Task<bool> Send(Guid id, ICommand payload)
    {
        var env = new CommandEnvelope { AggregateId = id, AggregateType = "Konto", Payload = payload };
        var t = Stopwatch.StartNew();
        try
        {
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(15));
            var r = await cluster.RequestAsync<CommandResult>(ClusterIdentity.Create(id.ToString(), "Konto"), env, cts.Token);
            t.Stop();
            if (r is null) { Interlocked.Increment(ref fail); return false; }
            latencies.Add(t.Elapsed.TotalMilliseconds);
            Interlocked.Increment(ref ok);
            return true;
        }
        catch { Interlocked.Increment(ref fail); return false; }
    }

    Console.WriteLine($"Aggregate-Last: {accounts} Konten × (1 + {creditsPer}) = {total} Commands, Concurrency {concurrency}");
    var sw = Stopwatch.StartNew();
    await Parallel.ForEachAsync(ids, new ParallelOptions { MaxDegreeOfParallelism = concurrency },
        async (id, _) =>
        {
            if (!await Send(id, new EroeffneKonto(id, Start))) return;
            for (var k = 0; k < creditsPer; k++) await Send(id, new SchreibeGut(id, Credit));
        });
    sw.Stop();

    Report("AGGREGATE-REPORT (Command→Event, mit Persistenz)", ok, fail, sw, latencies);

    var store   = host.Services.GetRequiredService<IEventStoreRepository>();
    var factory = host.Services.GetRequiredService<IAggregateHandlerFactory>();
    Console.WriteLine("Prüfe Korrektheit (Rehydration der Salden)…");
    int wrong = 0;
    foreach (var id in ids)
    {
        var st = (await AggregateRehydrator.LoadAsync<Konto>(id, null, store, factory, null, default)).State;
        if (st.Saldo != expected) { wrong++; if (wrong <= 5) Console.WriteLine($"  ✗ {id}: {st.Saldo} != {expected}"); }
    }
    Console.WriteLine($"Salden geprüft:      {accounts}, korrekt {accounts - wrong}, falsch {wrong}");
    Console.WriteLine(wrong == 0 ? $"✓ Exactly-once hält: alle {accounts} Salden == {expected}." : $"✗ {wrong} falsch!");
    Console.WriteLine("============================================");
    return wrong == 0 && fail == 0 ? 0 : 1;
}

// ── Readiness-Barriere: wartet bis der Ziel-Actor einen Request platzieren kann ──
async Task<bool> WaitRoutableCore()
{
    Console.WriteLine("Warte auf Cluster-Routbarkeit…");
    var sw = Stopwatch.StartNew();
    var probeId = Guid.NewGuid();
    while (true)
    {
        try
        {
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
            object? ack = mode.Equals("pipeline", StringComparison.OrdinalIgnoreCase)
                ? await cluster.RequestAsync<PipelineAck>(
                    ClusterIdentity.Create("benchmark", "Pipeline-benchmark"), new BenchPing(-1), cts.Token)
                : await cluster.RequestAsync<CommandResult>(
                    ClusterIdentity.Create(probeId.ToString(), "Konto"),
                    new CommandEnvelope { AggregateId = probeId, AggregateType = "Konto", Payload = new EroeffneKonto(probeId, 0m) },
                    cts.Token);
            if (ack != null) break;
        }
        catch (OperationCanceledException) { }
        if (sw.Elapsed > TimeSpan.FromSeconds(60))
        {
            Console.WriteLine("✗ Cluster wurde in 60s nicht routbar — Abbruch.");
            return false;
        }
    }
    Console.WriteLine($"✓ Cluster routbar nach {sw.ElapsedMilliseconds} ms.");
    return true;
}

void Report(string title, long ok, long fail, Stopwatch sw, ConcurrentBag<double> latencies)
{
    var lat = latencies.OrderBy(x => x).ToArray();
    double Pct(double q) => lat.Length == 0 ? 0 : lat[Math.Min(lat.Length - 1, (int)(q * lat.Length))];
    double secs = Math.Max(sw.Elapsed.TotalSeconds, 1e-6);
    Console.WriteLine();
    Console.WriteLine("================ " + title + " ================");
    Console.WriteLine($"Nachrichten:         {ok + fail}  (ok {ok}, fehlgeschlagen {fail})");
    Console.WriteLine($"Wall-Clock:          {secs:0.00} s");
    Console.WriteLine($"Durchsatz:           {ok / secs:0} msg/s");
    Console.WriteLine($"Latenz p50/p95/p99:  {Pct(0.50):0.0} / {Pct(0.95):0.0} / {Pct(0.99):0.0} ms   (max {(lat.Length > 0 ? lat[^1] : 0):0.0})");
}

// ── Arg-Helpers ──────────────────────────────────────────────────────────────
int ArgInt(string name, int dflt)
{
    var i = Array.IndexOf(args, name);
    return i >= 0 && i + 1 < args.Length && int.TryParse(args[i + 1], out var v) ? v : dflt;
}
string ArgStr(string name, string dflt)
{
    var i = Array.IndexOf(args, name);
    return i >= 0 && i + 1 < args.Length ? args[i + 1] : dflt;
}
static LogLevel ParseLevel(string s) => s.ToLowerInvariant() switch
{
    "trace" => LogLevel.Trace,
    "debug" => LogLevel.Debug,
    "info" or "information" => LogLevel.Information,
    "warn" or "warning" => LogLevel.Warning,
    "error" => LogLevel.Error,
    _ => LogLevel.Warning,
};
