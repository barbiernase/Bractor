using System;
using System.Linq;
using System.Threading.Tasks;
using Abstractions;
using FluentAssertions;
using Infrastructure.Aggregate;
using Xunit;

namespace Infrastructure.Pruefstand.SpurPruefung;

/// <summary>
/// Werkbank P4.3: die Spur (Ursache, Wirker) reist fluss-lokal wie der Akteur und landet an jedem geschriebenen Eintrag. Die Kette über Funktionen
/// und Commands hinweg prüft die Prüfdomäne gegen echtes Marten (der Treiber der Messung folgt ihr: S3 ordnet jedes TeilVerbucht über die Ursache zu).
/// </summary>
public class SpurTests
{
    private sealed record Fertig : IEvent;

    [Fact]
    public void Von_setzt_erbt_und_stellt_wieder_her()
    {
        Spur.Aktuell.Should().BeNull();
        using (Spur.Von("a:1", "funktion:G"))
        {
            using (Spur.Von(null, "funktion:F"))
                Spur.Aktuell.Should().Be(new Spur.Kopf("a:1", "funktion:F"), "eine fehlende Ursache erbt die äußere");
            Spur.Aktuell!.Wirker.Should().Be("funktion:G");
        }
        Spur.Aktuell.Should().BeNull();
    }

    [Fact]
    public void Der_Absender_gehoert_zum_Wirker()
    {
        using (Spur.Von("a:1", "decide:A/C", "funktion:G"))
        {
            Spur.Aktuell!.Ueber.Should().Be("funktion:G");
            using (Spur.Von("b:2", null))
                Spur.Aktuell!.Ueber.Should().Be("funktion:G", "ohne neuen Wirker bleibt der Absender");
            using (Spur.Von(null, "funktion:F"))
                Spur.Aktuell!.Ueber.Should().BeNull("ein neuer Wirker ohne Absender trägt keinen fremden");
        }
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
        var env = EventEnvelopeFactory.BuildPerEvent(new IEvent[] { new Fertig() }, Guid.NewGuid(), "Bildakte", 0, "c", "x", "system",
            spur: new Spur.Kopf("q:1", "decide:Bildakte/MeldeFertig")).Single();
        (env.Ursache, env.Wirker).Should().Be(("q:1", "decide:Bildakte/MeldeFertig"));
    }
}
