using System;
using System.Collections.Generic;
using Abstractions;
using FluentAssertions;
using Infrastructure.Funktionen;
using Infrastructure.Umgebung;
using Xunit;

namespace Infrastructure.Pruefstand.Funktionen;

/// <summary>
/// Der Rechner einer Instanz bzw. eines Eingangs (docs/04 §4.4, §4.7): was einen Rechner nennt, bedient nur der Host mit diesem Namen; im Cluster nennt
/// eine Instanz aus der Umgebung mit Zustand ihren Rechner (Boot-Fehler sonst; die Pflicht eines Eingangs prüft <c>Eingaenge.Aus</c>, siehe
/// <c>EingaengeTests</c>); ein Rechner ist genau ein Name.
/// </summary>
public class RechnerTests
{
    private sealed record Start(int N) : ICommand;

    private static readonly GraphPlan Plan = new("G", typeof(object), [new KnotenPlan("A", "A", [new OperationPlan(0, typeof(Start), [], [])])],
        new Dictionary<Type, string[]> { [typeof(Start)] = ["A"] }, []);

    private static GraphInstanz Instanz(string name, string? rechner = null) => new(name, Plan, new Dictionary<string, KnotenKonfig>(), rechner);

    [Fact]
    public void Eine_festgelegte_Instanz_bedient_nur_ihr_Rechner_eine_freie_jeder_im_Einzelknoten_der_eine()
    {
        var gpu = Instanz("modell", "gpu-1");
        var frei = Instanz("rechnung");
        new EigenerRechner("gpu-1", Einzelknoten: false).Bedient(gpu).Should().BeTrue();
        new EigenerRechner("halle-3", Einzelknoten: false).Bedient(gpu).Should().BeFalse("ein anderer Rechner hat die GPU nicht");
        new EigenerRechner(null, Einzelknoten: false).Bedient(gpu).Should().BeFalse("ohne Namen nur, was keinen Rechner nennt");
        new EigenerRechner("halle-3", Einzelknoten: false).Bedient(frei).Should().BeTrue("ohne Rechner auf jedem Host");
        new EigenerRechner(null, Einzelknoten: true).Bedient(gpu).Should().BeTrue("im Einzelknoten-Betrieb ist der eine Host jeder Rechner");
        // Dieselbe Regel für den Rechner eines Eingangs (sein Läufer lauscht nur dort).
        new EigenerRechner("linie-1", Einzelknoten: false).Bedient("linie-1").Should().BeTrue();
        new EigenerRechner("linie-2", Einzelknoten: false).Bedient("linie-1").Should().BeFalse("eine Quelle lauscht nur auf ihrem Rechner");
        new EigenerRechner("linie-2", Einzelknoten: false).Bedient((string?)null).Should().BeTrue();
    }

    [Fact]
    public void Im_Cluster_nennt_eine_Instanz_mit_Zustand_aus_der_Umgebung_ihren_Rechner()
    {
        var instanzen = new GraphInstanzen([Instanz("modell"), Instanz("rechnung")], ausUmgebung: true);
        bool MitZustand(GraphInstanz i) => i.Name == "modell";
        FluentActions.Invoking(() => new EigenerRechner("gpu-1", Einzelknoten: false).Pruefe(instanzen, MitZustand))
            .Should().Throw<BootFehler>().WithMessage("*Instanz modell*Zustand*nennt keinen Rechner*");
        new EigenerRechner(null, Einzelknoten: true).Pruefe(instanzen, MitZustand);   // Einzelknoten: der eine Host
        new EigenerRechner(null, Einzelknoten: false).Pruefe(new GraphInstanzen(instanzen.Alle), MitZustand);   // je Graph eine (Prüfstände)
        new EigenerRechner("gpu-1", Einzelknoten: false).Pruefe(new GraphInstanzen([Instanz("modell", "gpu-1"), Instanz("rechnung")], ausUmgebung: true), MitZustand);
    }

    [Theory]
    [InlineData("")]
    [InlineData("gpu-1,gpu-2")]
    [InlineData("gpu-*")]
    [InlineData(" gpu-1")]
    public void Ein_Rechner_ist_genau_ein_Name(string rechner)
    {
        var konfig = new Dictionary<string, GraphInstanzKonfig> { ["modell"] = new() { Graph = "G", Rechner = rechner } };
        FluentActions.Invoking(() => GraphInstanzen.Aus(konfig, [Plan]))
            .Should().Throw<BootFehler>().WithMessage("*Instanz modell: Rechner*kein Name*");
    }
}
