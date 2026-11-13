using System.Collections.Generic;
using System.Linq;
using Abstractions;
using FluentAssertions;
using Infrastructure.Umgebung;
using Xunit;

namespace Infrastructure.Pruefstand.Umgebung;

/// <summary>
/// Rechner und Backend (docs/06 §6.5, <see cref="Rechnerregeln"/>): eine Regel für den Start des Hosts und die Sicht der Werkbank — der Pool nennt Namen
/// und Adressen, das Backend Rechner aus dem Pool, eine Instanz keinen oder einen des Backends; ein Host läuft nur auf einem Rechner des Backends.
/// </summary>
public class RechnerregelnTests
{
    private static readonly Dictionary<string, string> Pool = new() { ["server-1"] = "10.0.1.5", ["gpu-1"] = "10.0.2.7", ["linie-1"] = "10.0.4.21" };

    [Fact]
    public void Ein_stimmiger_Pool_hat_keine_Befunde()
        => Rechnerregeln.Befunde(Pool, ["server-1", "gpu-1"], [("bild", "gpu-1"), ("frei", null)]).Should().BeEmpty();

    [Fact]
    public void Jeder_Befund_steht_an_seiner_Karte()
    {
        var b = Rechnerregeln.Befunde(new Dictionary<string, string> { ["a b"] = "x", ["leer"] = "", ["url"] = "http://h" }, ["fremd", "fremd"],
            [("bild", "leer"), ("weg", "nirgends")]);
        b.Should().Contain(x => x.Karte == "rechner:a b" && x.Text.Contains("kein Name"));
        b.Should().Contain(x => x.Karte == "rechner:leer" && x.Text.Contains("keine Adresse"));
        b.Should().Contain(x => x.Karte == "rechner:url" && x.Text.Contains("ohne Schema und Port"));
        b.Should().Contain(x => x.Karte == Rechnerregeln.BackendKarte && x.Text.Contains("2-mal"));
        b.Should().Contain(x => x.Karte == Rechnerregeln.BackendKarte && x.Text.Contains("gibt es im Pool nicht"));
        b.Should().Contain(x => x.Karte == "instanz:bild" && x.Text.Contains("gehört nicht zum Backend"));
        b.Should().Contain(x => x.Karte == "instanz:weg" && x.Text.Contains("gibt es im Pool nicht"));
        Rechnerregeln.Befunde(Pool, [], []).Should().ContainSingle(x => x.Text.Contains("nennt keinen Rechner"));
    }

    [Fact]
    public void Der_Rechner_des_Prozesses_kommt_aus_dem_Argument_sonst_localhost()
    {
        Rechnerregeln.NameAus([Rechnerregeln.Argument, "gpu-1"]).Should().Be("gpu-1");
        Rechnerregeln.NameAus([Rechnerregeln.Argument + "=linie-1"]).Should().Be("linie-1");
        if (System.Environment.GetEnvironmentVariable(Rechnerregeln.Variable) is null)
            Rechnerregeln.NameAus([]).Should().Be(Rechnerregeln.Standard);
    }

    [Fact]
    public void Das_Backend_bestimmt_Einzelknoten_und_Adresse()
    {
        var u = new UmgebungKonfig();
        u.Einzelknoten.Should().BeTrue("ohne Abschnitt: localhost allein");
        u.GrpcAdresse().Should().Be($"http://localhost:{u.Transport.GrpcPort}");
        u.Rechner = Pool.ToDictionary(p => p.Key, p => new RechnerKonfig { Adresse = p.Value });
        u.Backend = new BackendKonfig { Rechner = ["server-1", "gpu-1"] };
        u.Einzelknoten.Should().BeFalse("zwei Rechner im Backend sind ein Cluster über Consul");
        u.Adresse("gpu-1").Should().Be("10.0.2.7");
        u.GrpcAdresse().Should().StartWith("http://10.0.1.5:");
    }

    [Fact]
    public void Ein_Host_laeuft_nur_auf_einem_Rechner_des_Backends()
    {
        var u = new UmgebungKonfig
        {
            Rechner = Pool.ToDictionary(p => p.Key, p => new RechnerKonfig { Adresse = p.Value }),
            Backend = new BackendKonfig { Rechner = ["server-1"] },
        };
        u.DieserRechner = "server-1";
        CqrsUmgebungExtensions.PruefeRechner(u);
        u.DieserRechner = "linie-1";
        FluentActions.Invoking(() => CqrsUmgebungExtensions.PruefeRechner(u)).Should().Throw<BootFehler>().WithMessage("*gehört nicht zum Backend*")
            .Which.Karte.Should().Be(Rechnerregeln.BackendKarte);
        u.DieserRechner = "unbekannt";
        FluentActions.Invoking(() => CqrsUmgebungExtensions.PruefeRechner(u)).Should().Throw<BootFehler>().WithMessage("*gibt es im Pool nicht*");
        u.Cluster.Rolle = KnotenRolle.Migrator;
        CqrsUmgebungExtensions.PruefeRechner(u);   // der Migrator eines Kaltstarts ist kein Knoten
        u.Cluster.Rolle = KnotenRolle.Allein;
        u.DieserRechner = "server-1";
        u.Graphen["bild"] = new GraphInstanzKonfig { Graph = "X", Rechner = "gpu-1" };
        FluentActions.Invoking(() => CqrsUmgebungExtensions.PruefeRechner(u)).Should().Throw<BootFehler>().WithMessage("*Instanz bild nennt gpu-1*")
            .Which.Karte.Should().Be("instanz:bild");
    }

    [Fact]
    public void Eine_App_Instanz_nennt_einen_Rechner_des_Pools_und_ihr_Port_ist_dort_frei()
    {
        string[] Befunde(params (string, string, int)[] apps) =>
            Rechnerregeln.Befunde(Pool, ["server-1"], [], apps, [5001, 5002]).Select(b => b.Karte + ": " + b.Text).ToArray();
        Befunde(("arbeitsplatz", "linie-1", 5110), ("kamera", "linie-1", 0), ("zweite", "linie-1", 0)).Should().BeEmpty("ohne Port belegt eine App keinen");
        Befunde(("kamera", "linie-9", 0)).Should().ContainSingle().Which.Should().Match("appinstanz:kamera:*gibt es im Pool nicht*");
        Befunde(("a", "linie-1", 5110), ("b", "linie-1", 5110)).Should().ContainSingle().Which.Should().Match("appinstanz:b:*Port 5110 auf linie-1 hat schon a*");
        Befunde(("a", "linie-1", 5110), ("b", "server-1", 5110)).Should().BeEmpty("je Rechner ein Port je App — auf zwei Rechnern derselbe Port");
        Befunde(("a", "server-1", 5001)).Should().ContainSingle().Which.Should().Match("*Port 5001 auf server-1 belegt der Host*");
        Befunde(("a", "linie-1", 5001)).Should().BeEmpty("auf einem Rechner ohne Host ist der Port frei");
        Befunde(("a", "linie-1", 70000)).Should().ContainSingle().Which.Should().Match("*Port 70000 ist kein Port*");
    }
}
