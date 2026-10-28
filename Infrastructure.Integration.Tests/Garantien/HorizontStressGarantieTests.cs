using System.Collections.Concurrent;
using Abstractions;
using Domain.ImagePair;
using FluentAssertions;
using Infrastructure.Persistence;
using Infrastructure.Projections;
using Marten;
using Microsoft.Extensions.Logging.Abstractions;
using Npgsql;
using IEvent = Abstractions.IEvent;

namespace Infrastructure.Integration.Tests.Garantien;

/// <summary>
/// G-11 (docs/13): im Störungslauf übersprang eine Projektion zwei Events eines Streams — die Partition stand am Kopf, der Guard dahinter. Dieser
/// Test stellt die Lage gegen echtes Postgres nach, ohne Host: viele Schreiber wie im Betrieb (Group-Commit mit parallelem Drain, Bündel mehrerer
/// Teile, neue und bestehende Streams), Batch-Leser einer Projektion je Partition mit langsamen Stellen (vor dem Anwenden, vor dem Commit — wie
/// ein Leser, der im Versions-Index hängt) und Neustarts (frische Aktivierung vom durablen Stand). Danach muss jedes Event genau einmal angewandt
/// sein: je Stream so viele Historien-Einträge, wie Events geschrieben wurden.
/// </summary>
[Trait("Kategorie", "Garantie")]
[Trait("Kategorie", "Last")]   // Minuten unter Last: in der Abnahme (scripts/tests.sh voll), nicht in jedem Teilschritt
public class HorizontStressGarantieTests : IClassFixture<HorizontStressGarantieTests.Fixture>
{
    private readonly Fixture _fx;
    public HorizontStressGarantieTests(Fixture fx) => _fx = fx;

    private const int Schreiber = 16, StreamsJeSchreiber = 25, Partitionen = 4;
    private static readonly TimeSpan Dauer = TimeSpan.FromSeconds(40);

    [Fact]
    public async Task Unter_vielen_Schreibern_und_langsamen_Lesern_wirkt_jedes_Event_genau_einmal()
    {
        var es = new MartenEventStore(_fx.Store, new KeineFabrik(), NullLogger<MartenEventStore>.Instance);
        await using var appender = new BatchingEventAppender(es, new MartenEventBatchWriter(_fx.Store, NullLogger<MartenEventBatchWriter>.Instance),
            NullLogger<BatchingEventAppender>.Instance, maxBatch: 256, drainParallelism: 4);
        var konsum = new Konsum(Partitionen, 200);
        const string proj = "it-horizont-stress";
        await BatchHilfe.StarteAmKopfAsync(_fx.Store, proj, konsum);

        var geschrieben = new ConcurrentDictionary<Guid, int>();
        using var schreibStopp = new CancellationTokenSource(Dauer);
        using var leseStopp = new CancellationTokenSource();

        var schreiber = Enumerable.Range(0, Schreiber).Select(w => Task.Run(async () =>
        {
            var zufall = new Random(w);
            var streams = Enumerable.Range(0, StreamsJeSchreiber).Select(_ => Guid.NewGuid()).ToArray();
            var version = new int[streams.Length];
            while (!schreibStopp.IsCancellationRequested)
            {
                var i = zufall.Next(streams.Length);
                var n = 1 + zufall.Next(3);
                var evs = Enumerable.Range(0, n).Select(_ => (IEvent)new ImagePairInspiziert()).ToList();
                try
                {
                    if (w % 4 == 0 && version[i] > 0)
                        // ein Bündel wie beim heißen Aggregat: mehrere Teile, eine Transaktion, am Group-Commit vorbei
                        await es.AppendTeileAsync(streams[i], version[i], [
                            new AppendTeil(evs.Take(1).ToList(), Guid.NewGuid().ToString(), Guid.NewGuid().ToString(), null, null),
                            new AppendTeil(evs.Skip(1).ToList(), Guid.NewGuid().ToString(), Guid.NewGuid().ToString(), null, null)], "ImagePair");
                    else
                        await appender.AppendEventsAsync(streams[i], version[i], evs, Guid.NewGuid().ToString(), Guid.NewGuid().ToString(), "ImagePair");
                    version[i] += n;
                    geschrieben.AddOrUpdate(streams[i], n, (_, alt) => alt + n);
                }
                catch (ConcurrencyException) { throw; }   // jeder Stream hat genau einen Schreiber
                await Task.Delay(zufall.Next(2, 8));   // Last wie ein belebter Betrieb, die Leser holen auf
            }
        })).ToList();

        var laeufe = new ConcurrentDictionary<int, int>();
        var leser = Enumerable.Range(0, Partitionen).Select(p => Task.Run(async () =>
        {
            var zufall = new Random(1000 + p);
            // Je Aktivierung ein eigener Co-Commit-Store (er puffert die Effekte eines Batches) — wie im Host.
            BatchLeser Neu() => BatchHilfe.HistorieLeser(es, new Domain.Infrastructure.ImagePair.ImagePairHistorieStore(_fx.Store), proj, p, konsum,
                vorher: async _ => { if (zufall.Next(200) == 0) await Task.Delay(zufall.Next(50, 400)); },
                absturz: async () => { if (zufall.Next(30) == 0) await Task.Delay(zufall.Next(50, 600)); });
            var l = Neu();
            while (!leseStopp.IsCancellationRequested)
            {
                try { await l.LaufAsync(); } catch (Exception) when (!leseStopp.IsCancellationRequested) { l = Neu(); }
                laeufe.AddOrUpdate(p, 1, (_, a) => a + 1);
                if (zufall.Next(20) == 0) l = Neu();                       // Neustart: frische Aktivierung vom durablen Stand
                await Task.Delay(zufall.Next(30));
            }
        })).ToList();

        await Task.WhenAll(schreiber);
        // Ausklingen: bis alle Partitionen am Kopf stehen (höchstens 120 s), dann noch eine Sekunde.
        var kopf = await BatchHilfe.KopfAsync(_fx.Store);
        var bis = DateTime.UtcNow + TimeSpan.FromSeconds(120);
        while (DateTime.UtcNow < bis)
        {
            var stände = await Task.WhenAll(Enumerable.Range(0, Partitionen).Select(p => BatchHilfe.PartitionAsync(_fx.Store, proj, p)));
            if (stände.All(s => s!.Position >= kopf)) break;
            await Task.Delay(200);
        }
        await Task.Delay(1_000);
        leseStopp.Cancel();
        await Task.WhenAll(leser);

        var historie = new Domain.Infrastructure.ImagePair.ImagePairHistorieStore(_fx.Store);
        var fehlend = new List<string>();
        foreach (var (stream, n) in geschrieben)
        {
            var eintraege = (await historie.GetByPairIdAsync(new(stream)))?.Eintraege.Count ?? 0;
            if (eintraege != n)
                fehlend.Add($"{stream}: {eintraege} statt {n} (Guard {await BatchHilfe.GuardAsync(_fx.Store, proj, stream)})");
        }
        geschrieben.Values.Sum().Should().BeGreaterThan(10_000, "der Test muss Last erzeugen");
        var stand = await Task.WhenAll(Enumerable.Range(0, Partitionen).Select(p => BatchHilfe.PartitionAsync(_fx.Store, proj, p)));
        stand.Should().OnlyContain(s => s!.Position >= kopf, "alle Partitionen haben den Kopf erreicht (sonst misst der Test nur Rückstand)");
        fehlend.Should().BeEmpty($"jedes Event genau einmal ({geschrieben.Values.Sum()} Events in {geschrieben.Count} Streams, Kopf {kopf}; "
            + $"{fehlend.Count} Streams abweichend: {string.Join("; ", fehlend.Take(10))})");
    }

    private sealed class KeineFabrik : IAggregateHandlerFactory
    {
        public IAggregateHandler CreateHandler(IState state) => throw new NotSupportedException();
    }

    public sealed class Fixture : IAsyncLifetime
    {
        public const string Schema = "horizont_stress_it";
        private const string Verbindung = "Host=localhost;Port=5432;Database=cqrs_events;Username=postgres;Password=postgres";

        public IDocumentStore Store { get; } = DocumentStore.For(opts =>
        {
            opts.Connection(Verbindung + ";Max Auto Prepare=20;Maximum Pool Size=200");
            opts.Events.AppendMode = JasperFx.Events.EventAppendMode.Quick;
            opts.Events.MetadataConfig.CorrelationIdEnabled = true;
            opts.Events.MetadataConfig.CausationIdEnabled = true;
            opts.Events.MetadataConfig.HeadersEnabled = true;
            opts.DatabaseSchemaName = Schema;
            opts.Events.DatabaseSchemaName = Schema;
            opts.AutoCreateSchemaObjects = JasperFx.AutoCreate.All;
            Core.KonsumSchema.Registriere(opts);
            Domain.Infrastructure.Generated.GeneratedProjectionServices.RegistriereSchema(opts);
        });

        public async Task InitializeAsync()
        {
            await using (var c = new NpgsqlConnection(Verbindung))
            {
                await c.OpenAsync();
                await using var cmd = new NpgsqlCommand($"drop schema if exists {Schema} cascade", c);
                await cmd.ExecuteNonQueryAsync();
            }
            await Store.Storage.ApplyAllConfiguredChangesToDatabaseAsync();
            await Store.Storage.Database.EnsureStorageExistsAsync(typeof(JasperFx.Events.IEvent));
        }

        public Task DisposeAsync() { Store.Dispose(); return Task.CompletedTask; }
    }
}
