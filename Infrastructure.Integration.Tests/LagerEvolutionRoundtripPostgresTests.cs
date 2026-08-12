using System;
using System.Linq;
using System.Threading.Tasks;
using Abstractions;
using Domain.Lager;
using FluentAssertions;
using Infrastructure.Persistence;
using Marten;
using Microsoft.Extensions.Logging.Abstractions;

namespace Infrastructure.Integration.Tests;

/// <summary>
/// Ebene 2 (echtes Marten) — der VOLLE Produktions-Seam für eine ECHTE Event-Evolution, end-to-end:
/// <c>LagerEingerichtet</c> ist real umbenannt (<c>Bestand</c> → <c>AnfangsBestand</c>), und die generierte
/// Kette läuft durch den echten <see cref="MartenEventStore"/> + die echte Registrierung
/// (<see cref="MartenEventTypeRegistration"/> → <c>GeneratedEventUpcasting.Registrierungen</c>).
///
/// Bewiesen:
/// 1. Alte Bytes (frühere Gestalt unter Basis-Diskriminator <c>lager_eingerichtet</c>) werden beim LESEN
///    typisiert auf die heutige Gestalt aufgewertet — KEINE Migration, das alte Feld geht nicht verloren.
/// 2. Neue Writes tragen den verschobenen Diskriminator <c>lager_eingerichtet_v2</c> und werden direkt gelesen.
/// 3. Ein Stored-Event → genau ein materialisiertes Event (SubIndex 0) im 1:1-Fall.
///
/// Braucht laufendes Postgres auf localhost:5432 (docker-compose.infrastructure.yml).
/// </summary>
public class LagerEvolutionRoundtripPostgresTests : IClassFixture<LagerEvolutionRoundtripPostgresTests.Fixture>
{
    private readonly Fixture _fx;
    public LagerEvolutionRoundtripPostgresTests(Fixture fx) => _fx = fx;

    [Fact]
    public async Task Alte_Bytes_werden_beim_Lesen_durch_den_echten_Seam_aufgewertet()
    {
        var es = new MartenEventStore(_fx.Store, new NoopFactory(), NullLogger<MartenEventStore>.Instance);
        var id = Guid.NewGuid();

        // Simuliere ALTE Bytes: schreibe die FRÜHERE Gestalt (Plain-Record) — Marten legt sie unter dem
        // Basis-Diskriminator "lager_eingerichtet" ab (via GeneratedEventUpcasting.Registrierungen).
        await using (var s = _fx.Store.LightweightSession())
        {
            s.Events.StartStream(id, new LagerEingerichtet_V1(Bestand: 42));
            await s.SaveChangesAsync();
        }

        // Lesen über den ECHTEN Produktions-Read-Pfad → Aufwertung auf die heutige Gestalt.
        var events = await es.ReadStreamAsync(id, fromVersion: 0, default);

        events.Should().ContainSingle();
        events[0].SubIndex.Should().Be(0, "1:1-Wandlung → ein Stored-Event, ein materialisiertes Event");
        events[0].Payload.Should().BeOfType<LagerEingerichtet>()
            .Which.AnfangsBestand.Should().Be(42, "das alte Feld 'Bestand' wurde typisiert auf 'AnfangsBestand' gewandelt");
    }

    [Fact]
    public async Task Neue_Writes_tragen_v2_und_werden_direkt_gelesen()
    {
        var es = new MartenEventStore(_fx.Store, new NoopFactory(), NullLogger<MartenEventStore>.Instance);
        var id = Guid.NewGuid();

        // Append über den echten Schreibpfad: die AKTUELLE Gestalt → Marten stempelt "lager_eingerichtet_v2".
        await es.AppendEventsAsync(id, expectedVersion: 0, new IEvent[] { new LagerEingerichtet(AnfangsBestand: 7) });

        // Speicher-Diskriminator ist verschoben …
        await using (var s = _fx.Store.QuerySession())
        {
            var raw = await s.Events.QueryAllRawEvents().Where(e => e.StreamId == id).ToListAsync();
            raw.Should().ContainSingle();
            raw[0].EventTypeName.Should().Be("lager_eingerichtet_v2");
        }

        // … und der Read-Pfad liefert die heutige Gestalt direkt.
        var events = await es.ReadStreamAsync(id, fromVersion: 0, default);
        events.Should().ContainSingle();
        events[0].Payload.Should().BeOfType<LagerEingerichtet>().Which.AnfangsBestand.Should().Be(7);
    }

    private sealed class NoopFactory : IAggregateHandlerFactory
    {
        public IAggregateHandler CreateHandler(IState state)
            => throw new NotSupportedException("Test nutzt nur Append/ReadStream.");
    }

    public sealed class Fixture : IDisposable
    {
        public IDocumentStore Store { get; }

        public Fixture()
        {
            Store = DocumentStore.For(opts =>
            {
                opts.Connection(
                    "Host=localhost;Port=5432;Database=cqrs_events;Username=postgres;Password=postgres");
                opts.DatabaseSchemaName = "lager_evolution_it";
                opts.Events.DatabaseSchemaName = "lager_evolution_it";
                opts.AutoCreateSchemaObjects = JasperFx.AutoCreate.All;

                opts.Events.MetadataConfig.CorrelationIdEnabled = true;
                opts.Events.MetadataConfig.CausationIdEnabled = true;
                opts.Events.MetadataConfig.HeadersEnabled = true;

                // ★ Die ECHTE Produktions-Registrierung: aktuelle Events an ihrem (ggf. verschobenen)
                //   Diskriminator, frühere Versionen am Basisnamen — aus GeneratedEventUpcasting.Registrierungen.
                MartenEventTypeRegistration.RegisterEventTypes(opts);
            });
        }

        public void Dispose() => Store.Dispose();
    }
}
