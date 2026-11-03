using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Abstractions;
using FluentAssertions;
using Infrastructure.Funktionen;
using Infrastructure.Umgebung;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Infrastructure.Pruefstand.Umgebung;

/// <summary>
/// Umgebungen (docs/06 §6.5), store-frei: Laden der Datei, Platzhalter, strikte Bindung, das Siegel als reiner
/// Vergleich, die Knoten-Konfig gegen die generierte Tabelle und die Arbeit der Akteure als eigene Instanzen.
/// </summary>
[Trait("Kategorie", "Garantie")]
public class UmgebungLaderTests
{
    /// <summary>Eine Umgebung als Ordner (<c>umgebungen/&lt;name&gt;/umgebung.json</c>) mit ihren Instanz-Dateien (<c>graphen/&lt;instanz&gt;.json</c>).</summary>
    private static (ConfigurationManager Konfig, string Verz) Datei(string name, string json, params (string Instanz, string Json)[] instanzen)
    {
        var verz = Directory.CreateTempSubdirectory("umgebung-").FullName;
        var ordner = Path.Combine(verz, "umgebungen", name);
        Directory.CreateDirectory(Path.Combine(ordner, "graphen"));
        File.WriteAllText(Path.Combine(ordner, "umgebung.json"), json);
        foreach (var (instanz, inhalt) in instanzen) File.WriteAllText(Path.Combine(ordner, "graphen", instanz + ".json"), inhalt);
        Environment.SetEnvironmentVariable("CQRS_UMGEBUNGEN", Path.Combine(verz, "umgebungen"));
        return (new ConfigurationManager(), verz);
    }

    [Fact]
    public void Name_kommt_aus_dem_Argument_oder_der_Variablen()
    {
        UmgebungLader.NameAus(["--umgebung", "sim-a"]).Should().Be("sim-a");
        UmgebungLader.NameAus(["--umgebung=prod"]).Should().Be("prod");
    }

    [Fact]
    public void Laedt_bindet_und_ersetzt_Platzhalter()
    {
        Environment.SetEnvironmentVariable("CQRS_TEST_PW", "geheim");
        var (k, verz) = Datei("sim-t", """
            { "Umgebung": { "Name": "sim-t", "Art": "Sim" },
              "EventStore": { "Verbindung": "Host=db;Password=${env:CQRS_TEST_PW}", "Datenbank": "cqrs_sim_${branch}" },
              "Cluster": { "Art": "Consul", "Name": "c-${branch}", "Rolle": "Mitglied" } }
            """, ("linie1", """
            { "Graph": "Bildaufbereitung", "Akteur": "KameraSystem",
              "Verkleinern": { "Parallel": 4, "Technik": { "IImageResizer": "ImageResizer" }, "AusgabePfad": "/daten/${branch}", "Endungen": [ "png", "tif" ] } }
            """));
        try
        {
            var u = UmgebungLader.Lade(k, "sim-t", [], branch: "Feature/Werkbank-P2");
            u.Umgebung.Art.Should().Be(UmgebungsArt.Sim);
            u.EventStore.Datenbank.Should().Be("cqrs_sim_feature_werkbank_p2", "${branch} wird DB-tauglich");
            UmgebungLader.Verbindung(u.EventStore).Should().Contain("Password=geheim").And.Contain("Database=cqrs_sim_feature_werkbank_p2");
            u.Cluster.Art.Should().Be(ClusterArt.Consul);
            u.Cluster.Rolle.Should().Be(KnotenRolle.Mitglied);
            var knoten = u.Graphen["linie1"].Knoten["Verkleinern"];
            u.Graphen["linie1"].Graph.Should().Be("Bildaufbereitung", "die Instanz heißt wie ihre Datei");
            u.Graphen["linie1"].Akteur.Should().Be("KameraSystem");
            knoten.Parallel.Should().Be(4);
            knoten.Technik.Should().Equal(new Dictionary<string, string> { ["IImageResizer"] = "ImageResizer" });
            knoten.Werte.Should().Contain("AusgabePfad", "/daten/feature_werkbank_p2", "Platzhalter gelten auch in den Instanzen")
                .And.Contain("Endungen:0", "png").And.Contain("Endungen:1", "tif");
        }
        finally { Directory.Delete(verz, true); }
    }

    [Fact]
    public void Unbekannter_Abschnitt_ist_ein_Fehler()
    {
        var (k, verz) = Datei("sim-u", """{ "Umgebung": { "Name": "sim-u" }, "Datenbank": {} }""");
        try { FluentActions.Invoking(() => UmgebungLader.Lade(k, "sim-u", [])).Should().Throw<InvalidOperationException>().WithMessage("*Datenbank*"); }
        finally { Directory.Delete(verz, true); }
    }

    [Fact]
    public void Unbekanntes_Feld_ist_ein_Fehler()
    {
        var (k, verz) = Datei("sim-f", """{ "Umgebung": { "Name": "sim-f" }, "Cluster": { "Nmae": "x" } }""");
        try { FluentActions.Invoking(() => UmgebungLader.Lade(k, "sim-f", [])).Should().Throw<InvalidOperationException>().WithMessage("*Nmae*"); }
        finally { Directory.Delete(verz, true); }
    }

    [Fact]
    public void Fehlende_Variable_ist_ein_Fehler()
    {
        var (k, verz) = Datei("sim-v", """{ "Umgebung": { "Name": "sim-v" }, "EventStore": { "Verbindung": "Password=${env:CQRS_GIBT_ES_NICHT}" } }""");
        try { FluentActions.Invoking(() => UmgebungLader.Lade(k, "sim-v", [])).Should().Throw<InvalidOperationException>().WithMessage("*CQRS_GIBT_ES_NICHT*"); }
        finally { Directory.Delete(verz, true); }
    }

    // ── Siegel ───────────────────────────────────────────────────────────────────────────────────────────────────────

    [Fact]
    public void Siegel_passt_nur_zur_eigenen_Umgebung()
    {
        var prod = new UmgebungsSiegel.Siegel("prod", "Prod", "cqrs");
        UmgebungsSiegel.Pruefe(null, prod).Should().BeNull("eine leere DB bekommt das Siegel");
        UmgebungsSiegel.Pruefe(prod, prod).Should().BeNull();
        UmgebungsSiegel.Pruefe(prod, new("sim-main", "Sim", "cqrs-sim")).Should().Contain("Prod-Umgebung");
        UmgebungsSiegel.Pruefe(new("sim-a", "Sim", "c"), new("sim-b", "Sim", "c")).Should().Contain("sim-a");
        UmgebungsSiegel.Pruefe(prod, prod with { Cluster = "anders" }).Should().Contain("Cluster");
    }

    // ── Knoten einer Instanz: ihre Rahmenwerte; draußen läuft nur die Arbeit eines Akteurs ────────────────────────────────
    // Die Bausteine der Beispieldomäne: Bildverkleinerung (Klasse mit Technik im Host), RangeAufloesung (Klasse mit Lese-Fähigkeit).

    private static FunktionsBaustein Baustein(string name) => Infrastructure.Komposition.Anwendung.Funktionen.Bausteine.Single(b => b.Name == name);

    private static BausteinBindung Bindung(string name, KnotenKonfig? k) => FunktionsBausteinWiring.BindungVon(Baustein(name), k);

    [Fact]
    public void Ein_Knoten_bindet_seine_Rahmenwerte()
        => Bindung("Bildverkleinerung", new() { Wiederholungen = 1, Parallel = 4, Warteschlange = 8, Vorwaermen = true })
            .Should().Be(new BausteinBindung("Bildverkleinerung", 1, 4, 8, true));

    [Fact]
    public void Ohne_Eintrag_gelten_die_Standards()
        => Bindung("Bildverkleinerung", null).Should().Be(new BausteinBindung("Bildverkleinerung"));

    [Fact]
    public void Die_Arbeit_eines_Akteurs_laeuft_draussen_eine_Funktion_im_Host()
    {
        FunktionsBausteinWiring.Baustein("Bildverkleinerung")!.Draussen.Should().BeFalse("eine Funktion ist eine Klasse im Host");
        FunktionsBausteinWiring.Baustein("RangeAufloesung")!.Draussen.Should().BeFalse();
        var k = FunktionsBausteinWiring.Baustein("Klassifizierer")!;
        k.Draussen.Should().BeTrue("die Arbeit des Klassifizierers rechnet eine Sitzung, die ihn verkörpert");
        k.Eingaenge.Select(e => e.Typ).Should().Equal(typeof(Domain.ImagePair.PaarZuBeurteilen));
        FunktionsBausteinWiring.Baustein("Inspekteur").Should().BeNull("ein Akteur ohne Arbeit ist kein Baustein");
    }

    // ── Instanzen: geprüft gegen die Graphen ───────────────────────────────────────────────────────────────────────────

    private static IReadOnlyList<GraphPlan> Graphen => Infrastructure.Komposition.Anwendung.Funktionen.Graphen;

    [Fact]
    public void Eine_Instanz_eines_unbekannten_Graphen_oder_mit_fremdem_Knoten_ist_ein_Boot_Fehler()
    {
        FluentActions.Invoking(() => GraphInstanzen.Aus(new Dictionary<string, GraphInstanzKonfig> { ["x"] = new() { Graph = "GibtsNicht" } }, Graphen))
            .Should().Throw<BootFehler>().WithMessage("*GibtsNicht*gibt es nicht*");
        FluentActions.Invoking(() => GraphInstanzen.Aus(new Dictionary<string, GraphInstanzKonfig>
            {
                ["x"] = new() { Graph = "Bildaufbereitung", Knoten = { ["Vorschau"] = new() } },
            }, Graphen))
            .Should().Throw<BootFehler>().WithMessage("*'Vorschau' ist kein Knoten von Bildaufbereitung*Einlesen*");
    }

    [Fact]
    public void Ein_Graph_mit_Command_Kopf_laeuft_nur_einmal_ein_Graph_mit_Quellkopf_beliebig_oft()
    {
        FluentActions.Invoking(() => GraphInstanzen.Aus(new Dictionary<string, GraphInstanzKonfig>
            {
                ["a"] = new() { Graph = "Teilmeldung" }, ["b"] = new() { Graph = "Teilmeldung" },
            }, Graphen))
            .Should().Throw<BootFehler>().WithMessage("*Teilmeldung hat einen Command-Kopf*genau einmal*a, b*");
        GraphInstanzen.Aus(new Dictionary<string, GraphInstanzKonfig>
            {
                ["linie1"] = new() { Graph = "Linienimport" }, ["linie2"] = new() { Graph = "Linienimport" },
            }, Graphen).Alle.Select(i => i.Name).Should().Equal("linie1", "linie2");
    }

    [Fact]
    public void Der_Akteur_einer_Instanz_darf_was_sie_verlaesst()
    {
        var rechte = Infrastructure.Komposition.Anwendung.Akteure.Rechte;
        GraphInstanzen.Aus(new Dictionary<string, GraphInstanzKonfig> { ["linie1"] = new() { Graph = "Linienimport", Akteur = "KameraSystem" } },
            Graphen, rechte).Alle.Single(i => i.Name == "linie1").Akteur.Should().Be("KameraSystem");
        FluentActions.Invoking(() => GraphInstanzen.Aus(new Dictionary<string, GraphInstanzKonfig>
            {
                ["linie1"] = new() { Graph = "Linienimport", Akteur = "Inspekteur" },
            }, Graphen, rechte))
            .Should().Throw<BootFehler>().WithMessage("*Inspekteur darf NimmBildAuf nicht*");
    }

    [Fact]
    public void Hand_nennt_Akteure_mit_Arbeit_und_bindet_den_Hand_Ausfuehrer_einmal()
    {
        var s = new ServiceCollection();
        s.BindeGraphen(new UmgebungKonfig
        {
            Umgebung = { Hand = ["Klassifizierer", "TrainingsSystem"] },
            Graphen = { ["teile"] = new() { Graph = "Teilmeldung" } },
        });
        s.Count(d => d.ServiceType == typeof(HandAusfuehrer)).Should().Be(1);
        s.Single(d => d.ServiceType == typeof(HandAkteure)).ImplementationInstance.Should().BeOfType<HandAkteure>()
            .Which.Namen.Should().BeEquivalentTo("Klassifizierer", "TrainingsSystem");
        s.Single(d => d.ServiceType == typeof(GraphInstanzen)).ImplementationInstance.Should().BeOfType<GraphInstanzen>()
            .Which.Alle.Select(i => i.Name).Should().Equal("teile", "Klassifizierer", "TrainingsSystem");
    }

    [Fact]
    public void Hand_mit_einem_Akteur_ohne_Arbeit_ist_ein_Boot_Fehler()
        => FluentActions.Invoking(() => new ServiceCollection().BindeGraphen(new UmgebungKonfig { Umgebung = { Hand = ["Inspekteur"] } }))
            .Should().Throw<BootFehler>().WithMessage("*'Inspekteur'*kein Akteur, der Arbeit hört*");

    [Fact]
    public void Ohne_Hand_bleibt_der_Hand_Ausfuehrer_weg()
    {
        var s = new ServiceCollection();
        s.BindeGraphen(new UmgebungKonfig());
        s.Should().NotContain(d => d.ServiceType == typeof(HandAusfuehrer));
    }

    [Fact]
    public void Die_Technik_eines_Knotens_braucht_ihre_Werte_und_nennt_nur_Bekanntes()
    {
        GraphInstanzen Mit(KnotenKonfig verkleinern) =>
            GraphInstanzen.Aus(new Dictionary<string, GraphInstanzKonfig> { ["linie1"] = new() { Graph = "Bildaufbereitung", Knoten = { ["Verkleinern"] = verkleinern } } }, Graphen);
        FluentActions.Invoking(() => new ServiceCollection().BindeTechnik(Mit(new KnotenKonfig())))
            .Should().Throw<BootFehler>().WithMessage("*linie1/Verkleinern:AusgabePfad fehlt*");
        FluentActions.Invoking(() => new ServiceCollection().BindeTechnik(Mit(new KnotenKonfig { Werte = { ["AusgabePfad"] = "/x", ["Breite"] = "256" } })))
            .Should().Throw<BootFehler>().WithMessage("*linie1/Verkleinern: unbekannte Felder Breite*AusgabePfad*");
        FluentActions.Invoking(() => new ServiceCollection().BindeTechnik(Mit(new KnotenKonfig { Werte = { ["AusgabePfad"] = "/x" }, Technik = { ["IImageResizer"] = "Gibtsnicht" } })))
            .Should().Throw<BootFehler>().WithMessage("*Technik:IImageResizer = 'Gibtsnicht' gibt es nicht*");
    }

    [Theory]
    [InlineData("sim-main")]
    [InlineData("prod")]
    [InlineData("multinode")]
    public void Die_Umgebungen_des_Bestands_binden_streng(string name)
    {
        // Die echten Dateien unter umgebungen/ (mit Identitäten, JSON-Eingang) — ein Tippfehler bräche erst den Host-Start.
        var verz = AppContext.BaseDirectory;
        while (verz != null && !Directory.Exists(Path.Combine(verz, "umgebungen"))) verz = Path.GetDirectoryName(verz);
        Environment.SetEnvironmentVariable("CQRS_UMGEBUNGEN", Path.Combine(verz!, "umgebungen"));
        var u = UmgebungLader.Lade(new ConfigurationManager(), name, [], branch: "main");
        u.Umgebung.Name.Should().Be(name);
        GraphInstanzen.Aus(u.Graphen, Graphen, Infrastructure.Komposition.Anwendung.Akteure.Rechte).Alle.Select(i => i.Graph.Name)
            .Should().BeEquivalentTo(["Linienimport", "Datensatzpflege", "Teilmeldung", "Klassifizierer", "TrainingsSystem"],
                "je Graph eine Instanz, dazu je Akteur mit Arbeit eine");
        if (name == "sim-main")
        {
            u.Transport.JsonEingang.Should().BeTrue("Sim: die Werkbank handelt als Akteur");
            foreach (var k in new[] { "Einlesen", "Verkleinern", "Kontrast" })
                u.Graphen["linienimport"].Knoten["Aufbereitung." + k].Wiederholungen.Should().Be(1);
            u.Graphen["linienimport"].Knoten["Aufbereitung.Verkleinern"].Werte["AusgabePfad"].Should().EndWith("/cqrs-sim/main/preprocessed");
            u.Graphen["linienimport"].Knoten["Aufbereitung.Kontrast"].Werte["AusgabePfad"].Should().EndWith("/cqrs-sim/main/preprocessed");
            u.Graphen["linienimport"].Knoten["Eingang"].Werte["Pfad"].Should().EndWith("/cqrs-sim/main/DateiErkannt", "der Wächter ist der Kopf");
        }
        else u.Transport.JsonEingang.Should().BeFalse("Prod: kein JSON-Eingang");
        if (name != "sim-main")
            u.Graphen["linienimport"].Rechner.Should().NotBeNullOrEmpty("eine Quelle läuft im Cluster auf ihrem Rechner (Cluster:Rechner des Hosts)");
    }

    [Fact]
    public void Die_generierte_Tabelle_kennt_die_Funktionen_des_Bestands_alle_im_Host()
    {
        var b = GeneratedFunktionsBausteine.Alle.ToDictionary(x => x.Name);
        b.Keys.Should().Contain(["Bildeinlesen", "Bildverkleinerung", "Kontrastausgleich", "Bildeingang", "RangeAufloesung", "MitgliederEinfrieren"]);
        b.Keys.Should().NotContain(["Klassifizierer", "TrainingsSystem"], "die Arbeit eines Akteurs ist keine generierte Funktion");
        b.Values.Should().OnlyContain(x => !x.Draussen, "eine Funktion ist eine Klasse im Host");
        b["RangeAufloesung"].Liest.Should().BeTrue("mit Lese-Fähigkeit");
    }
}
