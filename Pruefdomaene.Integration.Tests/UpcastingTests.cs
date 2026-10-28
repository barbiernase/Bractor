using Abstractions;
using FluentAssertions;
using Infrastructure.Persistence;
using Marten;
using Microsoft.Extensions.Logging.Abstractions;
using Pruefdomaene.Posten;
using Anwendung = global::Infrastructure.Komposition.Anwendung;

namespace Pruefdomaene.Integration.Tests;

/// <summary>
/// Typisiertes, generiertes Event-Upcasting (docs/13 T-1; <c>IUpcast</c> bleibt Backend-Primitive): <c>PostenEroeffnet</c> ist evolviert
/// (Feld <c>Bestand</c> → <c>AnfangsBestand</c>, der klassische STILLE Korruptionsfall). Die generierte Tabelle der Komposition
/// (Diskriminator, Kette, Materialisieren) und der volle Seam durch den echten <see cref="MartenEventStore"/>: alte Bytes werden beim
/// Lesen aufgewertet, neue Writes tragen den verschobenen Diskriminator.
/// </summary>
public class UpcastingTests : IClassFixture<UpcastingTests.Fixture>
{
    private readonly Fixture _fx;
    public UpcastingTests(Fixture fx) => _fx = fx;

    [Fact]
    public void Nicht_evolviertes_Event_behaelt_den_Basis_Diskriminator()
    {
        var snake = Anwendung.Typen.PersistableEvents.First(e => e.Type == typeof(PostenReserviert)).SnakeCaseName;
        Anwendung.Upcasting.Diskriminator(new PostenReserviert(1, 0, Beleg.Ohne)).Should().Be(snake).And.Be("posten_reserviert");
    }

    [Fact]
    public void Evolviertes_Event_rueckt_auf_den_verschobenen_Diskriminator_und_registriert_die_fruehere_Version()
    {
        Anwendung.Upcasting.Diskriminator(new PostenEroeffnet(1)).Should().Be("posten_eroeffnet_v2");
        var reg = Anwendung.Upcasting.Registrierungen.ToDictionary(r => r.Typ, r => r.Diskriminator);
        reg[typeof(PostenEroeffnet_V1)].Should().Be("posten_eroeffnet", "die alten Bytes tragen den Basisnamen");
        reg[typeof(PostenEroeffnet)].Should().Be("posten_eroeffnet_v2");
    }

    [Fact]
    public void Alte_Bytes_werden_ueber_die_Kette_gewandelt_und_Unbekanntes_bricht_laut_ab()
    {
        Anwendung.Upcasting.Materialisiere("posten_eroeffnet", "{\"Bestand\":42}").Should().ContainSingle()
            .Which.Should().BeOfType<PostenEroeffnet>().Which.AnfangsBestand.Should().Be(42, "die Wandlung übernimmt Bestand → AnfangsBestand");
        Anwendung.Upcasting.Aufwerten(new PostenEroeffnet_V1(7)).Should().ContainSingle()
            .Which.Should().BeOfType<PostenEroeffnet>().Which.AnfangsBestand.Should().Be(7);
        var aktuell = new PostenReserviert(3, 0, Beleg.Ohne);
        Anwendung.Upcasting.Aufwerten(aktuell).Should().ContainSingle().Which.Should().BeSameAs(aktuell);
        var akt = () => Anwendung.Upcasting.Materialisiere("gibts_nicht", "{}");
        akt.Should().Throw<NotSupportedException>();
    }

    [Fact]
    public async Task Alte_Bytes_werden_beim_Lesen_durch_den_echten_Seam_aufgewertet()
    {
        var es = new MartenEventStore(_fx.Store, new Pruefdomaene.AggregateHandlerFactory(), NullLogger<MartenEventStore>.Instance);
        var id = Guid.NewGuid();

        // ALTE Bytes: die frühere Gestalt (Plain-Record) — Marten legt sie unter dem Basis-Diskriminator ab (Registrierung der Komposition).
        await using (var s = _fx.Store.LightweightSession())
        {
            s.Events.StartStream(id, new PostenEroeffnet_V1(Bestand: 42));
            await s.SaveChangesAsync();
        }

        var events = await es.ReadStreamAsync(id, fromVersion: 0, default);
        events.Should().ContainSingle();
        events[0].SubIndex.Should().Be(0, "1:1-Wandlung → ein gespeichertes, ein materialisiertes Event");
        events[0].Payload.Should().BeOfType<PostenEroeffnet>().Which.AnfangsBestand.Should().Be(42);
    }

    [Fact]
    public async Task Neue_Writes_tragen_v2_und_werden_direkt_gelesen()
    {
        var es = new MartenEventStore(_fx.Store, new Pruefdomaene.AggregateHandlerFactory(), NullLogger<MartenEventStore>.Instance);
        var id = Guid.NewGuid();

        await es.AppendEventsAsync(id, expectedVersion: 0, new IEvent[] { new PostenEroeffnet(AnfangsBestand: 7) });

        await using (var s = _fx.Store.QuerySession())
        {
            var roh = await s.Events.QueryAllRawEvents().Where(e => e.StreamId == id).ToListAsync();
            roh.Should().ContainSingle().Which.EventTypeName.Should().Be("posten_eroeffnet_v2");
        }
        (await es.ReadStreamAsync(id, 0, default)).Should().ContainSingle()
            .Which.Payload.Should().BeOfType<PostenEroeffnet>().Which.AnfangsBestand.Should().Be(7);
    }

    public sealed class Fixture : IDisposable
    {
        public IDocumentStore Store { get; } = Pruefstand.Store("pruef_upcasting");
        public void Dispose() => Store.Dispose();
    }
}
