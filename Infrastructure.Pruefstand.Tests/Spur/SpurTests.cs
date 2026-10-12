using System;
using System.Linq;
using System.Threading.Tasks;
using Abstractions;
using FluentAssertions;
using Infrastructure.Aggregate;
using Infrastructure.Pruefstand.Funktionen;
using Xunit;

namespace Infrastructure.Pruefstand.SpurPruefung;

/// <summary>
/// Werkbank P4.3: die Spur (Ursache, Wirker) reist fluss-lokal wie der Akteur und landet an jedem geschriebenen Eintrag — über Funktionen
/// und Commands hinweg eine lückenlose Ursache-Kette bis zur Wurzel. Store-frei: das Log im Speicher stempelt wie der echte Store.
/// </summary>
public class SpurTests
{
    [Fact]
    public void Von_setzt_erbt_und_stellt_wieder_her()
    {
        Spur.Aktuell.Should().BeNull();
        using (Spur.Von("a:1", "prozess:P#R"))
        {
            using (Spur.Von(null, "funktion:F"))
                Spur.Aktuell.Should().Be(new Spur.Kopf("a:1", "funktion:F"), "eine fehlende Ursache erbt die äußere");
            Spur.Aktuell!.Wirker.Should().Be("prozess:P#R");
        }
        Spur.Aktuell.Should().BeNull();
    }

    [Fact]
    public void Ursache_ist_Stream_und_Version()
    {
        var id = Guid.NewGuid();
        var u = Spur.Ursache(id, 4);
        u.Should().Be(id + ":4");
        Spur.ZerlegeUrsache(u, out var s, out var v).Should().BeTrue();
        (s, v).Should().Be((id, 4L));
        Spur.ZerlegeUrsache("kaputt", out _, out _).Should().BeFalse();
    }

    [Fact]
    public void Die_Live_Huellen_des_Aggregats_tragen_die_Spur()
    {
        var env = EventEnvelopeFactory.BuildPerEvent(new IEvent[] { new Fertig("v", "k") }, Guid.NewGuid(), "Bildakte", 0, "c", "x", "system",
            spur: new Spur.Kopf("q:1", "decide:Bildakte/MeldeFertig")).Single();
        (env.Ursache, env.Wirker).Should().Be(("q:1", "decide:Bildakte/MeldeFertig"));
    }

    [Fact]
    public async Task Ein_Vorgang_hat_eine_lueckenlose_Ursache_Kette_ueber_Funktionen_und_Commands()
    {
        var w = new FunktionsWelt();
        await w.StarteAsync();
        (await w.TreibeAsync()).Erfolg.Should().BeTrue();

        var alle = w.Log.AlleStreamIds().SelectMany(w.Log.Stream).ToList();
        var wurzel = Spur.Ursache(w.AuslöserStream, 1);
        var vor = alle.Single(e => e.Payload is Vorverarbeitet);
        var meta = alle.Single(e => e.Payload is MetaGelesen);
        (vor.Wirker, vor.Ursache).Should().Be(("funktion:IVorverarbeitung", wurzel));
        (meta.Wirker, meta.Ursache).Should().Be(("funktion:IMetadaten", wurzel));

        var fertig = alle.Single(e => e.Payload is Fertig);
        fertig.Wirker.Should().Be("decide:Bildakte/MeldeFertig");
        fertig.Ursache.Should().BeOneOf(Spur.Ursache(vor.AggregateId, 1), Spur.Ursache(meta.AggregateId, 1));

        // Die Kette läuft vom Ergebnis bis zur Quelle zurück, jeder Schritt ist ein Eintrag im Log.
        var schritt = fertig;
        for (var i = 0; i < 5 && schritt.Ursache is { } u; i++)
        {
            Spur.ZerlegeUrsache(u, out var s, out var v).Should().BeTrue();
            schritt = alle.Single(e => e.AggregateId == s && e.AggregateVersion == v);
        }
        schritt.Payload.Should().BeOfType<BildEingegangen>("die Kette endet an der Wurzel");
    }

    [Fact]
    public void Benannt_mit_falscher_Namenszahl_ist_ein_Boot_Fehler()
    {
        var regeln = new BildAufbereitung().Regeln;
        regeln.Invoking(r => r.Benannt("BildAufbereitung", "nur-einer")).Should().Throw<InvalidOperationException>().WithMessage("*4 Regeln*");
    }
}
