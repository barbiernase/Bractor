using Abstractions;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Pruefdomaene.Kontingent;
using Pruefdomaene.Posten;
using Proto.Cluster;
using static Pruefdomaene.Integration.Tests.Pruefstand;

namespace Pruefdomaene.Integration.Tests.Garantien;

/// <summary>
/// Die direkte Abfrage eines Aggregats (Plan Pub/Sub-Neustrukturierung §2.6, Phase 9), im echten Cluster gegen echtes Marten: die Antwort ist der
/// Stand, auf dem der nächste Command entschieden würde, und trägt seine Version als OCC-Stand. Beweiskraft: das Log — der Stand aus den Events bis
/// zur Version der Antwort, und was der nächste Command mit dieser Version entscheidet.
/// </summary>
[Trait("Kategorie", "Garantie")]
public class AggregatAbfrageGarantieTests
{
    private static async Task<(IQueryResponse Antwort, int Version)> FrageKontingentAsync(IHost host, Guid k)
    {
        var a = await host.Services.GetRequiredService<IQueryDienst>().ExecuteAsync(new HoleKontingent(new(k)));
        a.Deps.Should().ContainSingle("die Antwort zeigt genau das gefragte Aggregat");
        a.Deps![0].Id.Should().Be(k);
        a.Deps[0].AggregateType.Should().Be(nameof(Kontingent.Kontingent));
        a.NochNicht.Should().BeFalse("eine Frage an ein Aggregat ist exakt — kein Warten auf eine Projektion");
        return (a.Data, a.Deps[0].Version);
    }

    /// <summary>Die freien Plätze aus dem Log bis einschließlich <paramref name="version"/> — die Wahrheit, gegen die eine Antwort steht.</summary>
    private static async Task<int> FreiImLogAsync(IHost host, Guid k, int version)
    {
        var frei = 0;
        foreach (var e in await host.Services.GetRequiredService<IEventStoreRepository>().ReadStreamAsync(k, 0, default))
        {
            if (e.AggregateVersion > version) break;
            frei = e.Payload switch
            {
                KontingentEingerichtet x => x.Plaetze,
                KontingentBelegt x => frei - x.Plaetze,
                KontingentEntlastet x => frei + x.Plaetze,
                _ => frei,
            };
        }
        return frei;
    }

    [Fact]
    public async Task Die_Antwort_ist_der_Stand_auf_dem_der_naechste_Command_entscheidet()
    {
        using var host = Cluster("garantie-abfrage");
        await host.StartAsync();
        try
        {
            var k = Guid.NewGuid();
            var (vorher, v0) = await FrageKontingentAsync(host, k);
            vorher.Should().Be(new KontingentUnbekannt(new(k)));
            v0.Should().Be(0);
            (await FrageAsync(host, new RichteKontingentEin(new(k), 10), Guid.NewGuid(), v0))!.Success.Should().BeTrue();

            // Mit dem Stand der Antwort als erwarteter Version entscheidet das Aggregat ohne Konflikt — und auf denselben freien Plätzen.
            var (stand, v1) = await FrageKontingentAsync(host, k);
            stand.Should().Be(new KontingentStand(new(k), 10));
            v1.Should().Be(1);
            var zuViel = await FrageAsync(host, new BelegeKontingent(new(k), 11, Beleg.Ohne), Guid.NewGuid(), v1);
            zuViel!.Success.Should().BeTrue("mit der Version der Antwort gibt es keinen Versionskonflikt");
            var abgelehnt = (await host.Services.GetRequiredService<IEventStoreRepository>().ReadStreamAsync(k, v1 + 1, default)).Single().Payload;
            abgelehnt.Should().Be(new BelegungAbgelehnt(10, 11, Beleg.Ohne), "entschieden auf genau den 10 freien Plätzen, die die Antwort nannte");

            var (stand2, v2) = await FrageKontingentAsync(host, k);
            stand2.Should().Be(new KontingentStand(new(k), 10));
            v2.Should().Be(2, "die Absage ist ein Fakt im Log — eine Version weiter");
            (await FrageAsync(host, new BelegeKontingent(new(k), 10, Beleg.Ohne), Guid.NewGuid(), v2))!.Success.Should().BeTrue();
            var (stand3, v3) = await FrageKontingentAsync(host, k);
            stand3.Should().Be(new KontingentStand(new(k), 0));
            v3.Should().Be(3);

            // Eine veraltete Version (die einer früheren Antwort) ist ein Versionskonflikt — die Version der Antwort ist der OCC-Stand.
            var veraltet = await FrageAsync(host, new EntlasteKontingent(new(k), 1, Beleg.Ohne), Guid.NewGuid(), v2);
            veraltet!.Success.Should().BeFalse();
            veraltet.Abgelehnt.Should().BeFalse("kein fachliches Nein, sondern ein Konflikt");
            veraltet.ErrorMessage.Should().Contain("Concurrency conflict");
        }
        finally { await host.StopAsync(); }
    }

    [Fact]
    public async Task Unter_gleichzeitigen_Commands_entspricht_jede_Antwort_dem_Log_an_ihrer_Version()
    {
        using var host = Cluster("garantie-abfrage-last");
        await host.StartAsync();
        try
        {
            var k = Guid.NewGuid();
            (await FrageAsync(host, new RichteKontingentEin(new(k), 1000), Guid.NewGuid()))!.Success.Should().BeTrue();

            // Commands und Fragen gleichzeitig: Bündel und Commits laufen, während Fragen ankommen.
            const int n = 60;
            var commands = Enumerable.Range(0, n).Select(_ => FrageAsync(host, new BelegeKontingent(new(k), 1, Beleg.Ohne), Guid.NewGuid())).ToList();
            var fragen = Enumerable.Range(0, n).Select(_ => FrageKontingentAsync(host, k)).ToList();
            (await Task.WhenAll(commands)).Should().OnlyContain(r => r != null && r.Success);
            var antworten = await Task.WhenAll(fragen);

            foreach (var (antwort, version) in antworten)
            {
                var frei = ((KontingentStand)antwort).Frei;
                (await FreiImLogAsync(host, k, version)).Should().Be(frei, $"die Antwort auf Version {version} ist der Stand des Logs an dieser Version");
                (1000 - frei).Should().Be(version - 1, "je Version eine Belegung — keine Antwort zeigt eine Entscheidung, die (noch) nicht committet ist");
            }
            var (schluss, vEnde) = await FrageKontingentAsync(host, k);
            schluss.Should().Be(new KontingentStand(new(k), 1000 - n));
            vEnde.Should().Be(n + 1);
        }
        finally { await host.StopAsync(); }
    }

    [Fact]
    public async Task Eine_Frage_waehrend_eines_Commits_sieht_nur_den_committeten_Stand()
    {
        // Während ein Bündel committet, trägt der State seine Entscheidungen schon — sie können noch scheitern. Hier scheitert der Commit an einem
        //   zweiten Schreiber (eine Entlastung um 5, direkt ins Log): die Belegung um 3 wird auf dem neuen Stand neu entschieden. Eine Frage, die
        //   während des Commits kam, darf nicht den hinfälligen Stand (7 frei) nennen, sondern wartet bis danach.
        var commit = new AngehaltenerCommit();
        using var host = Cluster("garantie-abfrage-commit", dienste: s => Dekoriere<IEventStoreRepository>(s, (_, innen) => commit.Um(innen)));
        await host.StartAsync();
        try
        {
            var k = Guid.NewGuid();
            (await FrageAsync(host, new RichteKontingentEin(new(k), 10), Guid.NewGuid()))!.Success.Should().BeTrue();
            (await FrageKontingentAsync(host, k)).Version.Should().Be(1);
            commit.Scharf = k;

            var belegung = FrageAsync(host, new BelegeKontingent(new(k), 3, Beleg.Ohne), Guid.NewGuid());
            await commit.Laeuft.Task.WaitAsync(TimeSpan.FromSeconds(30));
            var frage = FrageKontingentAsync(host, k);
            await Task.Delay(TimeSpan.FromMilliseconds(500));
            frage.IsCompleted.Should().BeFalse("die Frage wartet, bis der Commit durch ist");

            await commit.Innen!.AppendEventsAsync(k, 1, new IEvent[] { new KontingentEntlastet(5, Beleg.Ohne) });   // der zweite Schreiber
            var nachDemCommit = System.Diagnostics.Stopwatch.StartNew();
            commit.Freigabe.SetResult();

            (await belegung)!.Success.Should().BeTrue("auf dem frischen Stand neu entschieden");
            var (antwort, version) = await frage;
            // Gleich nach dem Commit beantwortet, nicht erst, wenn Proto.Cluster die unbeantwortete Anfrage nach seiner Frist erneut zustellt.
            nachDemCommit.Elapsed.Should().BeLessThan(TimeSpan.FromSeconds(2), "die wartende Frage beantwortet der Actor gleich nach dem Commit");
            (await FreiImLogAsync(host, k, version)).Should().Be(((KontingentStand)antwort).Frei, "die Antwort ist der Stand des Logs an ihrer Version");
            antwort.Should().NotBe(new KontingentStand(new(k), 7), "7 frei war die Entscheidung des gescheiterten Commits");
            var (danach, vDanach) = await FrageKontingentAsync(host, k);
            danach.Should().Be(new KontingentStand(new(k), 12), "10 + 5 entlastet − 3 belegt");
            vDanach.Should().Be(3);
        }
        finally { await host.StopAsync(); }
    }

    [Fact]
    public async Task Eine_Frage_an_eine_fremde_Identitaet_wird_nicht_beantwortet()
    {
        // Wie ein falsch gerouteter Command (SchreibGarantieTests): die Id des Actors ist seine Cluster-Identität; eine Frage, deren Ziel eine andere
        //   ist, beantwortet er nicht — sonst nennte er den Stand eines anderen Aggregats als den des gefragten.
        using var host = Cluster("garantie-abfrage-fremd");
        await host.StartAsync();
        try
        {
            var k = Guid.NewGuid();
            (await FrageAsync(host, new RichteKontingentEin(new(k), 10), Guid.NewGuid()))!.Success.Should().BeTrue();
            var fremd = Guid.NewGuid();
            var bote = new global::Infrastructure.Lebenszyklus.Bote(host.Services.GetRequiredService<Proto.ActorSystem>().Cluster());
            var antwort = await bote.FrageAsync<global::Infrastructure.Aggregate.ActorSystem.AggregatAntwort>(
                Proto.Cluster.ClusterIdentity.Create(fremd.ToString(), nameof(Kontingent.Kontingent)),
                new global::Infrastructure.Aggregate.ActorSystem.AggregatFrage(new HoleKontingent(new(k)), k), TimeSpan.FromSeconds(30), default);

            antwort.Should().NotBeNull();
            antwort!.Antwort.Should().BeNull("die Identität des Actors ist nicht das Ziel der Frage");
            antwort.Fehler.Should().Contain(fremd.ToString());
            (await FrageKontingentAsync(host, k)).Antwort.Should().Be(new KontingentStand(new(k), 10), "an die richtige Identität antwortet er");
        }
        finally { await host.StopAsync(); }
    }

    /// <summary>Störungs-Seam: der erste Bündel-Append auf den scharfen Stream meldet sich und hält an, bis der Test ihn freigibt.</summary>
    private sealed class AngehaltenerCommit
    {
        public Guid Scharf { get; set; }
        public TaskCompletionSource Laeuft { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource Freigabe { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public IEventStoreRepository? Innen { get; private set; }
        private int _ausgeloest;

        public IEventStoreRepository Um(IEventStoreRepository innen)
        {
            Innen = innen;
            return new Laeufer(this, innen);
        }

        private sealed class Laeufer(AngehaltenerCommit s, IEventStoreRepository innen) : IEventStoreRepository
        {
            public Task AppendEventsAsync(Guid aggregateId, int expectedVersion, IReadOnlyList<IEvent> events, string? correlationId = null,
                string? causationId = null, string? aggregateType = null, string? akteur = null, IReadOnlyList<Beilage>? beilagen = null)
                => innen.AppendEventsAsync(aggregateId, expectedVersion, events, correlationId, causationId, aggregateType, akteur, beilagen);
            public async Task AppendTeileAsync(Guid aggregateId, int expectedVersion, IReadOnlyList<AppendTeil> teile, string? aggregateType)
            {
                if (aggregateId == s.Scharf && Interlocked.Exchange(ref s._ausgeloest, 1) == 0)
                {
                    s.Laeuft.SetResult();
                    await s.Freigabe.Task;
                }
                await innen.AppendTeileAsync(aggregateId, expectedVersion, teile, aggregateType);
            }
            public Task<IReadOnlyList<EventEnvelope>> ReadStreamAsync(Guid streamId, int fromVersion, CancellationToken ct) => innen.ReadStreamAsync(streamId, fromVersion, ct);
            public Task<EventEnvelope?> ReadEventAsync(Guid streamId, int version, CancellationToken ct) => innen.ReadEventAsync(streamId, version, ct);
            public Task<PartitionsSeite> ReadPartitionAsync(long nach, int partition, int partitionen, IReadOnlyCollection<string>? typen, IReadOnlyCollection<long> ausser, int limit, CancellationToken ct)
                => innen.ReadPartitionAsync(nach, partition, partitionen, typen, ausser, limit, ct);
            public Task<TState?> LoadStateAsync<TState>(Guid id) where TState : class, IState, new() => innen.LoadStateAsync<TState>(id);
            public Task<KommandoAusgang?> AusgangAsync(Guid commandId, CancellationToken ct) => innen.AusgangAsync(commandId, ct);
            public Task<IReadOnlyCollection<Guid>> StreamsVorhandenAsync(IReadOnlyCollection<Guid> streams, CancellationToken ct) => innen.StreamsVorhandenAsync(streams, ct);
        }
    }
}
