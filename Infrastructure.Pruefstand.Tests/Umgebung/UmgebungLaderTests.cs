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
/// Vergleich, die Knoten-Konfig gegen die generierte Tabelle, die Instanzen gegen die Graphen und die Eingänge der Clients aus Quellen.
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
              "Cluster": { "Art": "Consul", "Name": "c-${branch}", "Rolle": "Mitglied" },
              "Clients": { "Linienrechner": { "Eingaenge": { "kamera1": {
                  "Art": "Dateiwaechter", "Akteur": "KameraSystem", "Rechner": "linie-${branch}", "Offen": 50,
                  "Pfad": "/bilder/${branch}", "Muster": [ "*.tiff", "*.png" ], "Intervall": "00:00:01" } } } } }
            """, ("linie1", """
            { "Graph": "Bildaufbereitung", "Straenge": 2,
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
            u.Graphen["linie1"].Straenge.Should().Be(2);
            u.Graphen["linie1"].Knoten.Keys.Should().Equal(["Verkleinern"], "Straenge gehört der Instanz, kein Knoten");
            knoten.Parallel.Should().Be(4);
            knoten.Technik.Should().Equal(new Dictionary<string, string> { ["IImageResizer"] = "ImageResizer" });
            knoten.Werte.Should().Contain("AusgabePfad", "/daten/feature_werkbank_p2", "Platzhalter gelten auch in den Instanzen")
                .And.Contain("Endungen:0", "png").And.Contain("Endungen:1", "tif");

            // Ein Eingang des Clients: Art, Akteur, Rechner, Offen als Rahmen; alles andere sind Werte der Quellart, flach (eine Liste als Feld:0 …).
            var e = u.Clients["Linienrechner"].Eingaenge["kamera1"];
            e.Art.Should().Be("Dateiwaechter");
            e.Akteur.Should().Be("KameraSystem");
            e.Rechner.Should().Be("linie-feature_werkbank_p2", "Platzhalter gelten auch im Eingang");
            e.Offen.Should().Be(50);
            e.Werte.Should().BeEquivalentTo(new Dictionary<string, string?>
            {
                ["Pfad"] = "/bilder/feature_werkbank_p2", ["Muster:0"] = "*.tiff", ["Muster:1"] = "*.png", ["Intervall"] = "00:00:01",
            }, "Art, Akteur, Rechner und Offen sind keine Werte");
            e.AlsKnoten().Werte.Should().BeSameAs(e.Werte, "die Aktivierung liest die Werte wie am Knoten einer Instanz");
        }
        finally { Directory.Delete(verz, true); }
    }

    [Fact]
    public void Straenge_sind_eine_Zahl_ab_null()
    {
        var (k, verz) = Datei("sim-s", """{ "Umgebung": { "Name": "sim-s" } }""", ("linie1", """{ "Graph": "Bildaufbereitung", "Straenge": "viele" }"""));
        try { FluentActions.Invoking(() => UmgebungLader.Lade(k, "sim-s", [])).Should().Throw<Exception>().WithMessage("*Straenge*viele*"); }
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
    public void Offen_eines_Eingangs_ist_eine_Zahl_ab_null()
    {
        var (k, verz) = Datei("sim-o", """
            { "Umgebung": { "Name": "sim-o" },
              "Clients": { "Linienrechner": { "Eingaenge": { "kamera1": { "Art": "Dateiwaechter", "Akteur": "KameraSystem", "Offen": "viele" } } } } }
            """);
        try { FluentActions.Invoking(() => UmgebungLader.Lade(k, "sim-o", [])).Should().Throw<Exception>().WithMessage("*Linienrechner:Eingaenge:kamera1*Offen*viele*"); }
        finally { Directory.Delete(verz, true); }
    }

    [Fact]
    public void Ein_unbekanntes_Feld_eines_Clients_ausserhalb_der_Eingaenge_ist_ein_Fehler()
    {
        var (k, verz) = Datei("sim-c", """{ "Umgebung": { "Name": "sim-c" }, "Clients": { "Linienrechner": { "Tokn": "x", "Eingaenge": {} } } }""");
        try { FluentActions.Invoking(() => UmgebungLader.Lade(k, "sim-c", [])).Should().Throw<InvalidOperationException>().WithMessage("*Tokn*"); }
        finally { Directory.Delete(verz, true); }
    }

    [Fact]
    public void Ein_Akteur_an_der_Instanz_ist_kein_Rahmenfeld_mehr()
    {
        // Den Akteur einer Kette bringt ihr Auslöser mit; ein „Akteur“ in einer alten Instanz-Datei ist ein Knoten dieses Namens — den der Start ablehnt.
        var (k, verz) = Datei("sim-a", """{ "Umgebung": { "Name": "sim-a" } }""", ("bild", """{ "Graph": "Bildaufbereitung", "Akteur": "KameraSystem" }"""));
        try
        {
            var u = UmgebungLader.Lade(k, "sim-a", []);
            u.Graphen["bild"].Knoten.Keys.Should().Equal(["Akteur"]);
            FluentActions.Invoking(() => GraphInstanzen.Aus(u.Graphen, Graphen)).Should().Throw<BootFehler>().WithMessage("*'Akteur' ist kein Knoten von Bildaufbereitung*");
        }
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
    // Die Bausteine der Beispieldomäne: Bildverkleinerung (Klasse mit Technik im Host).

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
    public void Ein_Baustein_ist_eine_Funktion_ein_Akteur_ist_keiner()
    {
        FunktionsBausteinWiring.Baustein("Bildverkleinerung").Should().NotBeNull("eine Funktion ist eine Klasse im Host");
        FunktionsBausteinWiring.Baustein("Klassifizierer").Should().BeNull("der Klassifizierer reagiert als Client auf ein Fakt");
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
    public void Ein_Graph_laeuft_je_Umgebung_genau_einmal()
    {
        // Ein Graph kennt keine Quelle mehr: jeder Kopf nimmt Commands (genau ein Empfänger) — mehrere Verzeichnisse sind mehrere Eingänge eines Clients.
        FluentActions.Invoking(() => GraphInstanzen.Aus(new Dictionary<string, GraphInstanzKonfig>
            {
                ["a"] = new() { Graph = "Bildaufbereitung" }, ["b"] = new() { Graph = "Bildaufbereitung" },
            }, Graphen))
            .Should().Throw<BootFehler>().WithMessage("*Bildaufbereitung läuft je Umgebung genau einmal*a, b*");
        GraphInstanzen.Aus(new Dictionary<string, GraphInstanzKonfig> { ["bild"] = new() { Graph = "Bildaufbereitung", Straenge = 3, Rechner = "r1" } }, Graphen)
            .Alle.Should().ContainSingle().Which.Should().Match<GraphInstanz>(i => i.Name == "bild" && i.Gleichzeitig == 3 && i.Rechner == "r1");
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
        GraphInstanzen.Aus(u.Graphen, Graphen).Alle.Select(i => i.Graph.Name)
            .Should().BeEquivalentTo(["Bildaufbereitung"], "je Graph eine Instanz; Arbeit hat kein Akteur, die Worker reagieren");
        foreach (var k in new[] { "Einlesen", "Verkleinern", "Kontrast" })
            u.Graphen["bildaufbereitung"].Knoten[k].Wiederholungen.Should().Be(1);
        u.Graphen["bildaufbereitung"].Knoten.Keys.Should().BeEquivalentTo(["Einlesen", "Verkleinern", "Kontrast"], "ohne Quellknoten, ohne Präfix");

        // Der Dateiwächter ist ein Eingang des Linienrechners, der das Kamerasystem verkörpert — geprüft wie beim Start.
        var linie = u.Clients["Linienrechner"].Eingaenge["linie1"];
        linie.Art.Should().Be("Dateiwaechter");
        linie.Akteur.Should().Be("KameraSystem");
        var a = Infrastructure.Komposition.Anwendung.Aktuell;
        var eingaenge = Infrastructure.Quellen.Eingaenge.Aus(u.Clients, a.Funktionen.Bausteine, new Uebersetzungen(a.Funktionen.Uebersetzungen),
            a.Akteure.Rechte, a.Akteure.ClientVertraege, u.Cluster.Art == ClusterArt.Einzelknoten);
        eingaenge.Alle.Select(e => e.Ort).Should().Equal("Linienrechner/linie1");
        eingaenge.Alle.Single().Uebersetzungen[typeof(DateiGefunden)].Select(x => x.Name).Should().Equal("DateiGefunden_LiesBild_1");
        new ServiceCollection().BindeTechnik(GraphInstanzen.Aus(u.Graphen, Graphen), eingaenge);   // Werte und Technik binden — auch die des Eingangs

        if (name == "sim-main")
        {
            u.Transport.JsonEingang.Should().BeTrue("Sim: die Werkbank handelt als Akteur");
            u.Graphen["bildaufbereitung"].Knoten["Verkleinern"].Werte["AusgabePfad"].Should().EndWith("/cqrs-sim/main/preprocessed");
            u.Graphen["bildaufbereitung"].Knoten["Kontrast"].Werte["AusgabePfad"].Should().EndWith("/cqrs-sim/main/preprocessed");
            linie.Werte["Pfad"].Should().EndWith("/cqrs-sim/main/DateiErkannt", "der Wächter des Eingangs lauscht auf dem Verzeichnis der Kamera");
        }
        else
        {
            u.Transport.JsonEingang.Should().BeFalse("Prod: kein JSON-Eingang");
            linie.Rechner.Should().NotBeNullOrEmpty("eine Quelle lauscht im Cluster genau einmal, auf ihrem Rechner (Cluster:Rechner des Hosts)");
        }
    }

    [Fact]
    public void Die_generierte_Tabelle_kennt_die_Funktionen_des_Bestands_alle_im_Host()
    {
        var b = GeneratedFunktionsBausteine.Alle.ToDictionary(x => x.Name);
        b.Keys.Should().Contain(["Bildeinlesen", "Bildverkleinerung", "Kontrastausgleich", "Dateinamendeutung"]);
        b.Keys.Should().NotContain(["RangeAufloesung", "MitgliederEinfrieren"], "die Datensatz-Reaktionen rechnet ein Client (Schritt 6)");
        b.Keys.Should().NotContain(["Klassifizierer", "TrainingsSystem"], "ein Akteur ist keine Funktion");
        b.Values.Should().OnlyContain(x => x.Baue != null, "eine Funktion ist eine Klasse im Host");
    }
}
