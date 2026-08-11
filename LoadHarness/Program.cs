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
using System.Text.Json;
using System.Text.Json.Serialization.Metadata;
using Abstractions;
using Domain.Konto;
using Domain.Pipeline.Benchmark;        // BenchPing (No-Op-Trigger)
using Domain.Pipeline.Infrastructure;   // AddDomainPipelineServices
using Infrastructure.Aggregate;         // AggregateRehydrator
using Infrastructure.Extensions;        // AddCqrsFramework
using Infrastructure.Pipeline;          // GeneratedPipelines
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Proto.Cluster;

string mode     = ArgStr("--mode", "aggregate");
int concurrency = ArgInt("--concurrency", 64);
LogLevel level  = ParseLevel(ArgStr("--log", "warning"));

// Serializer-Mikrobench: beantwortet, ob (a) der Source-Gen-Pfad überhaupt für Events genutzt wird
// und (b) ob er schneller ist als Reflection — und rechnet den Amdahl-Anteil an der Append-Wall-Clock aus.
// Läuft OHNE Cluster/DB (früher Ausstieg vor dem Host-Build).
if (mode == "serbench") { SerBench(ArgInt("--iters", 500_000)); return 0; }

var watchDir = Path.Combine(Path.GetTempPath(), "loadtest-" + Guid.NewGuid().ToString("N"));
Directory.CreateDirectory(watchDir);

var builder = Host.CreateApplicationBuilder();
builder.Logging.ClearProviders();
builder.Logging.AddSimpleConsole(o => { o.SingleLine = true; o.TimestampFormat = "HH:mm:ss.fff "; });
builder.Logging.SetMinimumLevel(level);

builder.Services.AddCqrsFramework(opts =>
{
    opts.EnableGrpc = false;
    // ── Cluster-Anbindung: aus Config/Env, sonst isolierter Eigen-Cluster (natives Last-Test-Verhalten) ──
    //   Für ein echtes Multi-Node-Deployment als JOINENDER Member: Cluster__Name=cqrs-cluster +
    //   Consul__Address + Cluster__AdvertisedHost setzen → der Harness tritt dem laufenden Cluster bei
    //   und beweist mit seinem Exactly-once-Rehydrations-Check den cross-node Dispatch.
    opts.ClusterName = builder.Configuration.GetValue<string>("Cluster:Name")
        ?? ("loadtest-" + Guid.NewGuid().ToString("N")[..8]);
    opts.ConsulAddress = builder.Configuration.GetValue<string>("Consul:Address") ?? opts.ConsulAddress;
    opts.AdvertisedHost = builder.Configuration.GetValue<string>("Cluster:AdvertisedHost") ?? opts.AdvertisedHost;
    if (builder.Configuration.GetConnectionString("EventStore") is { Length: > 0 } es)
        opts.EventStoreConnectionString = es;
    if (builder.Configuration.GetValue<string>("EventStore:Schema") is { Length: > 0 } schema)
        opts.EventStoreSchema = schema;
    if (builder.Configuration.GetValue<string>("Redis:Endpoint") is { Length: > 0 } redis)
        opts.RedisConnectionString = redis;
    if (builder.Configuration.GetValue<int?>("Redis:Database") is { } redisDb)
        opts.RedisDatabase = redisDb;
    opts.SnapshotThreshold = 200;
    // A/B-Schalter für die Group-Commit-Messung (Default AN):
    //   BRACTOR_BATCHING=0        → Batching aus (Ein-Append-pro-Command)
    //   BRACTOR_BATCH_LINGER=<ms> → optionales Linger-Fenster
    opts.AppendBatching = Environment.GetEnvironmentVariable("BRACTOR_BATCHING") != "0";
    if (int.TryParse(Environment.GetEnvironmentVariable("BRACTOR_BATCH_LINGER"), out var linger))
        opts.AppendBatchLingerMs = linger;
    if (int.TryParse(Environment.GetEnvironmentVariable("BRACTOR_BATCH_MAX"), out var bmax) && bmax > 0)
        opts.AppendBatchMaxSize = bmax;
    if (int.TryParse(Environment.GetEnvironmentVariable("BRACTOR_DRAIN_PAR"), out var dpar) && dpar > 0)
        opts.AppendDrainParallelism = dpar;
    // A/B-Schalter für die Serializer-Messung:
    //   BRACTOR_SOURCEGEN_JSON=1 → STJ-Source-Gen-Kontext für Events in Martens Serializer
    opts.UseGeneratedJsonSerializer = Environment.GetEnvironmentVariable("BRACTOR_SOURCEGEN_JSON") == "1";
    // A/B-Schalter für die Redis-Messung:
    //   BRACTOR_VERSION_TRACKING=0 → Version-Index AUS (No-op) → misst den synchronen Redis-Anteil
    opts.EnableVersionTracking = Environment.GetEnvironmentVariable("BRACTOR_VERSION_TRACKING") != "0";
    Console.WriteLine($"[LoadHarness] AppendBatching={opts.AppendBatching} Linger={opts.AppendBatchLingerMs}ms "
        + $"DrainPar={opts.AppendDrainParallelism} "
        + $"SourceGenJson={opts.UseGeneratedJsonSerializer} VersionTracking={opts.EnableVersionTracking}");
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
        var env = new CommandEnvelope { AggregateId = id, AggregateType = "Konto", Modus = new CommandModus.Emittiert(), Payload = payload };
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
    Infrastructure.Persistence.BatchWriterStats.Reset();
    var sw = Stopwatch.StartNew();
    await Parallel.ForEachAsync(ids, new ParallelOptions { MaxDegreeOfParallelism = concurrency },
        async (id, _) =>
        {
            if (!await Send(id, new EroeffneKonto(id, Start))) return;
            for (var k = 0; k < creditsPer; k++) await Send(id, new SchreibeGut(id, Credit));
        });
    sw.Stop();

    Report("AGGREGATE-REPORT (Command→Event, mit Persistenz)", ok, fail, sw, latencies);

    // ── Group-Commit-Profil: wo geht die Zeit im DB-Schreibpfad hin? ──
    var bs = Infrastructure.Persistence.BatchWriterStats.Snapshot();
    if (bs.Batches > 0)
    {
        double wallMs = sw.Elapsed.TotalMilliseconds;
        Console.WriteLine($"Group-Commit-Profil: {bs.Batches} Commits, {bs.Events} Events, "
            + $"Ø Batch {bs.AvgBatch:0.0} Events/Commit, Ø Commit {bs.AvgCommitMs:0.00} ms");
        Console.WriteLine($"  Commit-Zeit gesamt (seriell): {bs.TotalCommitMs:0} ms = {bs.TotalCommitMs / wallMs * 100:0}% der Wall-Clock "
            + $"→ Event-Ceiling des seriellen Drains ≈ {bs.Events / (bs.TotalCommitMs / 1000.0):0} Events/s "
            + $"(≈ {(ok + fail) / (bs.TotalCommitMs / 1000.0):0} Commands/s)");
    }

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
                    new CommandEnvelope { AggregateId = probeId, AggregateType = "Konto", Modus = new CommandModus.Emittiert(), Payload = new EroeffneKonto(probeId, 0m) },
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

// ── Serializer-Mikrobench ────────────────────────────────────────────────────
void SerBench(int iters)
{
    // (1) Martens tatsächlicher Default-Serializer (der Baseline-Arm des A/B).
    try
    {
        using var store = Marten.DocumentStore.For(o =>
            o.Connection("Host=localhost;Database=cqrs_events;Username=postgres;Password=postgres"));
        Console.WriteLine($"[Marten] Default-Serializer: {store.Options.Serializer().GetType().Name}");
    }
    catch (Exception ex) { Console.WriteLine($"[Marten] Default-Serializer unbekannt: {ex.GetType().Name}"); }

    // (2) BEWEIS, dass der Source-Gen-Resolver die Event-Typen bedient (und Nicht-Events NICHT).
    var ctx = Infrastructure.Serialization.EventJsonSerializerContext.Default;
    var combined = new JsonSerializerOptions
    {
        TypeInfoResolver = JsonTypeInfoResolver.Combine(ctx, new DefaultJsonTypeInfoResolver())
    };
    var tiKonto = ctx.GetTypeInfo(typeof(BetragReserviert));
    var tiBild  = ctx.GetTypeInfo(typeof(Domain.ImagePair.BildVerfuegbar));
    var tiDict  = ctx.GetTypeInfo(typeof(Dictionary<string, object>));
    Console.WriteLine($"[Beweis] SourceGen-Kontext liefert TypeInfo für  BetragReserviert={tiKonto != null}  "
        + $"BildVerfuegbar={tiBild != null}  Dictionary<string,object>={tiDict != null}");
    Console.WriteLine($"[Beweis] → Combine gibt das erste Nicht-null zurück: Events ⇒ Source-Gen, Dictionary/Doku ⇒ Reflection.");

    // (3) Rohe Serialisierungsgeschwindigkeit: Reflection vs. Source-Gen-über-Options (== Martens Weg
    //     flag-on) vs. Source-Gen-direkt (der GeneratedEventJson-Switch). Kleines Konto-Event (das der
    //     Aggregate-Bench real schreibt) + grosses verschachteltes Event.
    var reflOpts = new JsonSerializerOptions { TypeInfoResolver = new DefaultJsonTypeInfoResolver() };

    IEvent klein = new BetragReserviert(49.99m);
    IEvent gross = new Domain.ImagePair.BildVerfuegbar(
        Domain.ImagePair.BildVersion.Dc2,
        new Domain.ImagePair.BildMeta("bild.png", 123456, 1920, 1080, DateTimeOffset.UnixEpoch),
        "/pfad/bild.png",
        Enumerable.Range(0, 8).Select(i => new Domain.ImagePair.RegionBewertung(
            i, new Domain.ImagePair.RegionPosition(i * 12.5, 0, 12.5, 100),
            Domain.ImagePair.Klassifikation.KeineAnomalie, null)).ToList<Domain.ImagePair.RegionBewertung>());

    double NsPerOp(Action a)
    {
        for (int i = 0; i < 2000; i++) a();               // Warmup (JIT + JsonTypeInfo-Cache)
        var sw = Stopwatch.StartNew();
        for (int i = 0; i < iters; i++) a();
        sw.Stop();
        return sw.Elapsed.TotalMilliseconds * 1_000_000.0 / iters;
    }

    void Row(string label, IEvent e)
    {
        var r  = NsPerOp(() => JsonSerializer.Serialize(e, e.GetType(), reflOpts));
        var so = NsPerOp(() => JsonSerializer.Serialize(e, e.GetType(), combined));
        var sd = NsPerOp(() => Infrastructure.Serialization.GeneratedEventJson.Serialize(e));
        Console.WriteLine($"{label,-16} reflection={r,7:0.0} ns   sourcegen(options)={so,7:0.0} ns   "
            + $"sourcegen(switch)={sd,7:0.0} ns   speedup(switch/refl)={r / sd,4:0.00}x");
    }

    Console.WriteLine($"\n=== Roh-Serialisierung ({iters} Iterationen, ns/op) ===");
    Row("BetragReserviert", klein);
    Row("BildVerfuegbar", gross);

    // (4) Amdahl: der Aggregate-Bench schrieb 20500 kleine Events in ~6.46 s Wall-Clock.
    //     Wie viel davon ist Serialisierung — und was spart Source-Gen davon END-TO-END?
    var kleinRefl = NsPerOp(() => JsonSerializer.Serialize(klein, klein.GetType(), reflOpts));
    var kleinSg   = NsPerOp(() => Infrastructure.Serialization.GeneratedEventJson.Serialize(klein));
    const int events = 20500; const double wallMs = 6460;
    double serReflMs = kleinRefl * events / 1_000_000.0;
    double serSgMs   = kleinSg   * events / 1_000_000.0;
    Console.WriteLine($"\n=== Amdahl (20500 kleine Events, Wall {wallMs:0} ms) ===");
    Console.WriteLine($"Serialisierung gesamt: reflection {serReflMs:0.0} ms ({serReflMs / wallMs * 100:0.00}% der Wall-Clock), "
        + $"source-gen {serSgMs:0.0} ms ({serSgMs / wallMs * 100:0.00}%)");
    Console.WriteLine($"Ersparnis durch Source-Gen END-TO-END: {(serReflMs - serSgMs):0.0} ms = "
        + $"{(serReflMs - serSgMs) / wallMs * 100:0.000}% der Append-Wall-Clock.");
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
