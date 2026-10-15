using Abstractions;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Pruefdomaene.Posten;
using static Pruefdomaene.Integration.Tests.Pruefstand;
using PostenState = Pruefdomaene.Posten.Posten;

namespace Pruefdomaene.Integration.Tests.Garantien;

/// <summary>
/// Garantien der Schreibseite (docs/11 §11.2, §11.3) unter Störung, im echten Cluster gegen echtes Marten: doppelte Zustellung desselben
/// Commands, eine alte CommandId jenseits der Inbox-Grenze, ein Versionskonflikt durch einen zweiten Schreiber, ein mehrdeutiger Commit
/// (committet, Quittung verloren). Beweiskraft: der Zustand aus dem Log (Voll-Fold) und die Events im Stream.
/// </summary>
[Trait("Kategorie", "Garantie")]
public class SchreibGarantieTests
{
    private static async Task<Guid> PostenAsync(IHost host, int bestand = 100)
    {
        var p = Guid.NewGuid();
        (await FrageAsync(host, new EroeffnePosten(p, bestand), Guid.NewGuid()))!.Success.Should().BeTrue();
        return p;
    }

    private static async Task<int> ReserviertImLog(IHost host, Guid p)
        => (await host.Services.GetRequiredService<IEventStoreRepository>().ReadStreamAsync(p, 0, default)).Count(e => e.Payload is PostenReserviert);

    [Fact]
    public async Task Doppelt_und_gleichzeitig_zugestellter_Command_wirkt_genau_einmal()
    {
        using var host = Cluster("garantie-doppelt");
        await host.StartAsync();
        try
        {
            var p = await PostenAsync(host);
            var id = Guid.NewGuid();
            var erste = await FrageAsync(host, new ReservierePosten(p, 5), id);
            var wiederholt = await Task.WhenAll(Enumerable.Range(0, 5).Select(_ => FrageAsync(host, new ReservierePosten(p, 5), id)));

            erste!.Success.Should().BeTrue();
            wiederholt.Should().OnlyContain(r => r != null && r.Success, "eine Wiederholung bekommt idempotent dasselbe Ergebnis");
            (await Zustand<PostenState>(host.Services, p)).Reserviert.Should().Be(5);
            (await ReserviertImLog(host, p)).Should().Be(1);
        }
        finally { await host.StopAsync(); }
    }

    [Fact(Skip = "G-2 offen (docs/13)")]
    public async Task Eine_alte_CommandId_jenseits_der_Inbox_Grenze_wirkt_nicht_erneut()
    {
        using var host = Cluster("garantie-inbox", inboxGrenze: 3);
        await host.StartAsync();
        try
        {
            var p = await PostenAsync(host);
            var alt = Guid.NewGuid();
            (await FrageAsync(host, new ReservierePosten(p, 1), alt))!.Success.Should().BeTrue();
            for (var i = 0; i < 3; i++) (await FrageAsync(host, new ReservierePosten(p, 1), Guid.NewGuid()))!.Success.Should().BeTrue();

            await FrageAsync(host, new ReservierePosten(p, 1), alt);   // ein Emittent gibt die alte CommandId erneut aus

            (await Zustand<PostenState>(host.Services, p)).Reserviert.Should().Be(4, "die alte CommandId hat schon gewirkt");
            (await ReserviertImLog(host, p)).Should().Be(4);
        }
        finally { await host.StopAsync(); }
    }

    [Fact(Skip = "G-3 offen (docs/13)")]
    public async Task Nach_einem_Versionskonflikt_durch_einen_zweiten_Schreiber_heilt_der_Actor_ohne_Neustart()
    {
        using var host = Cluster("garantie-konflikt");
        await host.StartAsync();
        try
        {
            var p = await PostenAsync(host);
            (await FrageAsync(host, new ReservierePosten(p, 1), Guid.NewGuid()))!.Success.Should().BeTrue();   // der Actor ist aktiv, Stand v4

            // Ein zweiter Schreiber (zweite Aktivierung, anderer Knoten) hängt an denselben Stream an.
            var store = host.Services.GetRequiredService<IEventStoreRepository>();
            var version = (await store.ReadStreamAsync(p, 0, default)).Max(e => e.AggregateVersion);
            await store.AppendEventsAsync(p, version, new IEvent[] { new PostenZugebucht(10) });

            await FrageAsync(host, new ReservierePosten(p, 1), Guid.NewGuid());          // trifft den veralteten Stand — darf scheitern
            var danach = await FrageAsync(host, new ReservierePosten(p, 1), Guid.NewGuid());

            danach!.Success.Should().BeTrue("der Actor rehydriert nach dem Konflikt frisch und entscheidet auf dem aktuellen Stand");
            (await Zustand<PostenState>(host.Services, p)).Bestand.Should().Be(110);
        }
        finally { await host.StopAsync(); }
    }

    [Fact(Skip = "G-3 offen (docs/13)")]
    public async Task Ein_mehrdeutiger_Commit_wird_als_Erfolg_quittiert_und_wirkt_genau_einmal()
    {
        var störung = new MehrdeutigerCommit();
        using var host = Cluster("garantie-mehrdeutig", dienste: s => Dekoriere<IEventStoreRepository>(s, (_, innen) => störung.Um(innen)));
        await host.StartAsync();
        try
        {
            var p = await PostenAsync(host);
            störung.Scharf = p;
            var id = Guid.NewGuid();

            var ergebnis = await FrageAsync(host, new ReservierePosten(p, 5), id);

            störung.Ausgeloest.Should().BeTrue("der Commit ist durch, nur die Quittung ging verloren");
            ergebnis!.Success.Should().BeTrue("was im Log steht, hat gewirkt (P10) — kein falsches Negativ");
            (await FrageAsync(host, new ReservierePosten(p, 5), id))!.Success.Should().BeTrue();
            (await ReserviertImLog(host, p)).Should().Be(1);
        }
        finally { await host.StopAsync(); }
    }

    /// <summary>Störungs-Seam: der nächste Append auf den scharfen Stream committet — dann geht die Quittung verloren (Ausnahme).</summary>
    private sealed class MehrdeutigerCommit
    {
        public Guid? Scharf { get; set; }
        public bool Ausgeloest { get; private set; }

        public IEventStoreRepository Um(IEventStoreRepository innen) => new Laeufer(this, innen);

        private sealed class Laeufer(MehrdeutigerCommit s, IEventStoreRepository innen) : IEventStoreRepository
        {
            public async Task AppendEventsAsync(Guid aggregateId, int expectedVersion, IReadOnlyList<IEvent> events, string? correlationId = null,
                string? causationId = null, string? aggregateType = null, string? akteur = null)
            {
                await innen.AppendEventsAsync(aggregateId, expectedVersion, events, correlationId, causationId, aggregateType, akteur);
                if (s.Scharf == aggregateId && !s.Ausgeloest)
                {
                    s.Ausgeloest = true;
                    throw new TimeoutException("Verbindung nach dem Commit verloren — Quittung fehlt");
                }
            }

            public Task<IReadOnlyList<EventEnvelope>> ReadStreamAsync(Guid streamId, int fromVersion, CancellationToken ct) => innen.ReadStreamAsync(streamId, fromVersion, ct);
            public Task<StreamChanges> ReadChangedStreamsAsync(long after, CancellationToken ct) => innen.ReadChangedStreamsAsync(after, ct);
            public Task<TState?> LoadStateAsync<TState>(Guid id) where TState : class, IState, new() => innen.LoadStateAsync<TState>(id);
        }
    }
}
