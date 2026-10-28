using Abstractions;
using Core;
using Domain.ImagePair;
using FluentAssertions;
using Infrastructure.Persistence;
using Infrastructure.Projections;
using Marten;
using Microsoft.Extensions.Logging.Abstractions;

namespace Infrastructure.Integration.Tests.Garantien;

/// <summary>
/// Der allgemeine Store-Test (Plan Pub/Sub-Neustrukturierung §2.4, §4.4 Nr. 2; <see cref="Storetest"/>) für die drei Treiber, die das Framework
/// mitliefert: den Marten-Store (Klasse 1, <see cref="MartenStore"/>), das Datei-Ziel (Klasse 2, <see cref="DateiZiel"/>) und die Datei-Senke
/// (Klasse 3, <see cref="DateiSink"/>) — je gegen ein echtes Log in einem eigenen Schema und den echten Batch-Leser. Batch-Abbruch,
/// Wiederholung, zweite Aktivierung mit älterem Stand und Neuaufbau über Generationen.
/// </summary>
[Trait("Kategorie", "Garantie")]
public class StoretestPostgresTests
{
    [Fact]
    public async Task Der_Marten_Store_besteht_den_Store_Test_als_Klasse_1()
    {
        using var store = GarantiePostgresTests.Fixture.Neu("storetest_marten_" + Guid.NewGuid().ToString("N")[..8]);
        (await Storetest.PruefeAsync(new MartenTreiber(store), Lauf(store))).Should().BeEmpty();
    }

    [Fact]
    public async Task Das_Datei_Ziel_besteht_den_Store_Test_als_Klasse_2()
    {
        using var store = GarantiePostgresTests.Fixture.Neu("storetest_datei_" + Guid.NewGuid().ToString("N")[..8]);
        var wurzel = Path.Combine(Path.GetTempPath(), "storetest-datei-" + Guid.NewGuid().ToString("N"));
        try
        {
            (await Storetest.PruefeAsync(new DateiTreiber(wurzel), Lauf(store))).Should().BeEmpty();
        }
        finally { try { Directory.Delete(wurzel, recursive: true); } catch { /* egal */ } }
    }

    [Fact]
    public async Task Die_Datei_Senke_besteht_den_Store_Test_als_Klasse_3()
    {
        using var store = GarantiePostgresTests.Fixture.Neu("storetest_senke_" + Guid.NewGuid().ToString("N")[..8]);
        var wurzel = Path.Combine(Path.GetTempPath(), "storetest-senke-" + Guid.NewGuid().ToString("N"));
        try
        {
            (await Storetest.PruefeAsync(new SenkenTreiber(wurzel), Lauf(store))).Should().BeEmpty();
        }
        finally { try { Directory.Delete(wurzel, recursive: true); } catch { /* egal */ } }
    }

    private static Storetest.Lauf Lauf(IDocumentStore store) => new(
        new MartenEventStore(store, new NoopFactory(), NullLogger<MartenEventStore>.Instance),
        new FortschrittBeimFramework(store),
        () => new ImagePairInspiziert());

    private sealed class NoopFactory : IAggregateHandlerFactory
    {
        public IAggregateHandler CreateHandler(IState state) => throw new NotSupportedException();
    }

    // ── Klasse 1: Marten — je Stream ein Dokument, Lesen-Ändern-Schreiben in der Transaktion des Fortschritts ──

    public sealed record StoretestZaehler : IReadModel
    {
        public Guid Id { get; init; }
        public int Eintraege { get; init; }
        public int Stand { get; init; }
    }

    private sealed class MartenZiel(IDocumentStore store) : MartenStore(store, typeof(StoretestZaehler))
    {
        public Task Wirke(Herkunft h) => EnqueueAnlegenOderAendern<StoretestZaehler>(h.Stream,
            alt => new StoretestZaehler { Id = h.Stream, Eintraege = (alt?.Eintraege ?? 0) + 1, Stand = h.Version });
    }

    private sealed class MartenTreiber(IDocumentStore store) : IStoretestTreiber
    {
        public IProjektionsZiel NeuesZiel() => new MartenZiel(store);
        public Task WirkeAsync(IProjektionsZiel ziel, Herkunft h) => ((MartenZiel)ziel).Wirke(h);

        public async Task<IReadOnlyDictionary<Guid, (int Eintraege, int Stand)>> LiesAsync(IReadOnlyCollection<Guid> streams)
        {
            await using var s = store.QuerySession();
            var zaehler = await s.LoadManyAsync<StoretestZaehler>(streams.ToArray());
            return zaehler.ToDictionary(z => z.Id, z => (z.Eintraege, z.Stand));
        }
    }

    // ── Klasse 2: Dateien — je Event eine Datei unter Stream und Version, der Stand je Stream bedingt ──

    private sealed class DateiZielFuerTest(string wurzel) : DateiZiel(wurzel)
    {
        public async Task Wirke(Herkunft h)
        {
            await LegeAb($"{h.Stream:N}/eintraege/{h.Version}", BitConverter.GetBytes(h.Version));
            await LegeAbWennNeuer($"{h.Stream:N}/stand", BitConverter.GetBytes(h.Version), h);
        }

        public (int Eintraege, int Stand) Lies(Guid stream)
            => (Liste($"{stream:N}/eintraege").Count, Lies($"{stream:N}/stand") is { } b ? BitConverter.ToInt32(b) : 0);
    }

    private sealed class DateiTreiber(string wurzel) : IStoretestTreiber
    {
        public IProjektionsZiel NeuesZiel() => new DateiZielFuerTest(wurzel);
        public Task WirkeAsync(IProjektionsZiel ziel, Herkunft h) => ((DateiZielFuerTest)ziel).Wirke(h);

        public Task<IReadOnlyDictionary<Guid, (int Eintraege, int Stand)>> LiesAsync(IReadOnlyCollection<Guid> streams)
        {
            var leser = new DateiZielFuerTest(wurzel);
            return Task.FromResult<IReadOnlyDictionary<Guid, (int, int)>>(streams.ToDictionary(s => s, leser.Lies));
        }
    }

    // ── Klasse 3: Senke — je Event eine angehängte Zeile, ohne Bedingung ──

    private sealed class SenkeFuerTest(string wurzel) : DateiSink(wurzel)
    {
        public Task Wirke(Herkunft h) => HaengeAn($"{h.Stream:N}", h.Version.ToString(System.Globalization.CultureInfo.InvariantCulture));

        public (int Eintraege, int Stand) Lies(Guid stream)
        {
            var zeilen = Zeilen($"{stream:N}");
            return (zeilen.Count, zeilen.Select(int.Parse).DefaultIfEmpty(0).Max());
        }
    }

    private sealed class SenkenTreiber(string wurzel) : IStoretestTreiber
    {
        public IProjektionsZiel NeuesZiel() => new SenkeFuerTest(wurzel);
        public Task WirkeAsync(IProjektionsZiel ziel, Herkunft h) => ((SenkeFuerTest)ziel).Wirke(h);

        public Task<IReadOnlyDictionary<Guid, (int Eintraege, int Stand)>> LiesAsync(IReadOnlyCollection<Guid> streams)
        {
            var leser = new SenkeFuerTest(wurzel);
            return Task.FromResult<IReadOnlyDictionary<Guid, (int, int)>>(streams.ToDictionary(s => s, leser.Lies));
        }
    }
}
