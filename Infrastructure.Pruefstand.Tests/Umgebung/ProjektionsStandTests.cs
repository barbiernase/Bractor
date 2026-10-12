using Abstractions;
using FluentAssertions;
using Infrastructure.Projections;

namespace Infrastructure.Pruefstand.Tests.Umgebung;

/// <summary>Der Projektions-Stand (Plan E9): Hash = Klasse + Store, geändert = anderer oder fehlender gespeicherter Stand.</summary>
public class ProjektionsStandTests
{
    private sealed class StoreA;
    private sealed class StoreB;

    private static readonly ProjektionsStand P1 = new("pull-P1", "p1", "k1", [typeof(StoreA)]);
    private static readonly ProjektionsStand P2 = new("pull-P2", "p2", "k2", [typeof(StoreB)]);

    [Fact]
    public void Store_Aenderung_aendert_den_Hash_nur_der_Projektionen_auf_diesem_Store()
    {
        var vorher = ProjektionsStandDienst.Hashes([P1, P2], [new StoreStand(typeof(StoreA), "a"), new StoreStand(typeof(StoreB), "b")]);
        var nachher = ProjektionsStandDienst.Hashes([P1, P2], [new StoreStand(typeof(StoreA), "a2"), new StoreStand(typeof(StoreB), "b")]);
        nachher["p1"].Should().NotBe(vorher["p1"]);
        nachher["p2"].Should().Be(vorher["p2"]);
        ProjektionsStandDienst.Geaendert(nachher, vorher).Should().Equal("p1");
    }

    [Fact]
    public void Fehlender_Stand_gilt_als_geaendert_gleicher_als_unveraendert()
    {
        var h = ProjektionsStandDienst.Hashes([P1, P2], [new StoreStand(typeof(StoreA), "a"), new StoreStand(typeof(StoreB), "b")]);
        ProjektionsStandDienst.Geaendert(h, new Dictionary<string, string>()).Should().Equal("p1", "p2");
        ProjektionsStandDienst.Geaendert(h, new Dictionary<string, string>(h)).Should().BeEmpty();
    }

    [Fact]
    public void Kodehash_ist_stabil_und_kombiniert_geordnet()
    {
        Kodehash.Hash("x").Should().HaveLength(16).And.Be(Kodehash.Hash("x"));
        Kodehash.Kombiniere("a", "b").Should().NotBe(Kodehash.Kombiniere("b", "a"));
    }
}
