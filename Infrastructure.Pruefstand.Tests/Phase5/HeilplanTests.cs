using FluentAssertions;
using Infrastructure.Prozess;

namespace Infrastructure.Pruefstand.Phase5;

/// <summary>
/// Der Heil-Plan des Prozess-Backstops (PF-4): eine Heil-Weckung nach <see cref="Heilplan.Anfang"/> Stille, danach mit verdoppeltem Abstand
/// bis <see cref="Heilplan.Hoechstens"/>; Aktivität setzt zurück, beendete Vorgänge fallen beim Abgleich heraus.
/// </summary>
public class HeilplanTests
{
    private static readonly DateTimeOffset T0 = new(2026, 10, 15, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Ein_stiller_Vorgang_wird_mit_wachsendem_Abstand_geheilt()
    {
        var plan = new Heilplan();
        var k = Guid.NewGuid();
        plan.Abgleich(new[] { k }, T0);

        plan.Faellige(T0 + TimeSpan.FromSeconds(14)).Should().BeEmpty();
        var t = T0 + Heilplan.Anfang;
        plan.Faellige(t).Should().Equal(k);
        plan.Faellige(t + TimeSpan.FromSeconds(29)).Should().BeEmpty("der Abstand hat sich auf 30 s verdoppelt");
        plan.Faellige(t + TimeSpan.FromSeconds(30)).Should().Equal(k);
    }

    [Fact]
    public void Der_Abstand_waechst_hoechstens_bis_zur_Obergrenze()
    {
        var plan = new Heilplan();
        var k = Guid.NewGuid();
        plan.Abgleich(new[] { k }, T0);
        var t = T0;
        for (var i = 0; i < 20; i++)
        {
            t += TimeSpan.FromDays(1);
            plan.Faellige(t).Should().Equal(k);
        }
        plan.Faellige(t + Heilplan.Hoechstens - TimeSpan.FromSeconds(1)).Should().BeEmpty();
        plan.Faellige(t + Heilplan.Hoechstens).Should().Equal(k);
    }

    [Fact]
    public void Aktivitaet_setzt_zurueck_und_beendete_fallen_heraus()
    {
        var plan = new Heilplan();
        var (aktiv, beendet) = (Guid.NewGuid(), Guid.NewGuid());
        plan.Abgleich(new[] { aktiv, beendet }, T0);
        plan.Faellige(T0 + TimeSpan.FromMinutes(10));   // beide einmal geheilt, Abstand jetzt 30 s

        plan.Aktiv(aktiv, T0 + TimeSpan.FromMinutes(10));
        plan.Abgleich(new[] { aktiv }, T0 + TimeSpan.FromMinutes(10));

        plan.Anzahl.Should().Be(1);
        plan.Faellige(T0 + TimeSpan.FromMinutes(10) + Heilplan.Anfang).Should().Equal(aktiv);
    }
}
