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
/// Vergleich und der Funktions-Laufort als Konfig gegen die generierte Tabelle.
/// </summary>
[Trait("Kategorie", "Garantie")]
public class UmgebungLaderTests
{
    private static (ConfigurationManager Konfig, string Verz) Datei(string name, string json)
    {
        var verz = Directory.CreateTempSubdirectory("umgebung-").FullName;
        Directory.CreateDirectory(Path.Combine(verz, "umgebungen"));
        File.WriteAllText(Path.Combine(verz, "umgebungen", $"umgebung.{name}.json"), json);
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
              "Funktionen": { "IX": { "Laufort": "Extern" } } }
            """);
        try
        {
            var u = UmgebungLader.Lade(k, "sim-t", [], branch: "Feature/Werkbank-P2");
            u.Umgebung.Art.Should().Be(UmgebungsArt.Sim);
            u.EventStore.Datenbank.Should().Be("cqrs_sim_feature_werkbank_p2", "${branch} wird DB-tauglich");
            UmgebungLader.Verbindung(u.EventStore).Should().Contain("Password=geheim").And.Contain("Database=cqrs_sim_feature_werkbank_p2");
            u.Cluster.Art.Should().Be(ClusterArt.Consul);
            u.Cluster.Rolle.Should().Be(KnotenRolle.Mitglied);
            u.Funktionen["IX"].Laufort.Should().Be(FunktionsLaufort.Extern);
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

    // ── Funktions-Laufort als Konfig ─────────────────────────────────────────────────────────────────────────────────
    // Die Bausteine der Beispieldomäne: IBildaufbereitung (Schnittstelle, eine Umsetzung im Host), RangeAufloesung (Klasse mit Lese-Fähigkeit).

    private static FunktionsBaustein Baustein(string name) => Infrastructure.Komposition.Anwendung.Funktionen.Bausteine.Single(b => b.Name == name);

    private static BausteinBindung Bindung(string name, FunktionsKonfig k)
        => FunktionsBausteinWiring.BindungVon(Baustein(name), [new BausteinBindung(name, k.Laufort, k.Implementierung, Math.Max(1, k.Slots), k.Wiederholungen)]);

    [Fact]
    public void Laufort_Host_bindet_die_genannte_Umsetzung()
        => Bindung("IBildaufbereitung", new() { Implementierung = "OpenCvBildaufbereitung", Slots = 3, Wiederholungen = 1 })
            .Should().Be(new BausteinBindung("IBildaufbereitung", FunktionsLaufort.Host, "OpenCvBildaufbereitung", 3, 1));

    [Fact]
    public void Laufort_Host_mit_unbekannter_Umsetzung_ist_ein_Boot_Fehler()
        => FluentActions.Invoking(() => Bindung("IBildaufbereitung", new() { Implementierung = "Gibtsnicht" }))
            .Should().Throw<BootFehler>().WithMessage("*genau eine Umsetzung*Gibtsnicht*OpenCvBildaufbereitung*");

    [Fact]
    public void Laufort_Extern_braucht_eine_Schnittstelle_und_keine_Lese_Faehigkeit()
    {
        Bindung("IBildaufbereitung", new() { Laufort = FunktionsLaufort.Extern }).Laufort.Should().Be(FunktionsLaufort.Extern);
        FluentActions.Invoking(() => Bindung("RangeAufloesung", new() { Laufort = FunktionsLaufort.Extern }))
            .Should().Throw<BootFehler>().WithMessage("*Lese-Fähigkeit*");
    }

    [Fact]
    public void Ohne_Eintrag_laeuft_eine_Schnittstelle_mit_einer_Umsetzung_im_Host()
        => FunktionsBausteinWiring.BindungVon(Baustein("IBildaufbereitung"), []).Laufort.Should().Be(FunktionsLaufort.Host);

    [Fact]
    public void Ein_unbekannter_Funktionsname_ist_ein_Boot_Fehler_Hand_bindet_den_Hand_Ausfuehrer_einmal()
    {
        FluentActions.Invoking(() => new ServiceCollection().BindeFunktionen(new Dictionary<string, FunktionsKonfig> { ["IGibtsNicht"] = new() }))
            .Should().Throw<BootFehler>().WithMessage("*IGibtsNicht*keine Funktion dieses Namens*");
        var s = new ServiceCollection().BindeFunktionen(new Dictionary<string, FunktionsKonfig>
        {
            ["IBildaufbereitung"] = new() { Laufort = FunktionsLaufort.Hand },
            ["TeilFertigMeldung"] = new() { Laufort = FunktionsLaufort.Hand },
        });
        s.Where(d => d.ServiceType == typeof(BausteinBindung)).Select(d => ((BausteinBindung)d.ImplementationInstance!).Funktion)
            .Should().BeEquivalentTo("IBildaufbereitung", "TeilFertigMeldung");
        s.Count(d => d.ServiceType == typeof(HandAusfuehrer)).Should().Be(1);
    }

    [Theory]
    [InlineData("sim-main")]
    [InlineData("prod")]
    [InlineData("multinode")]
    public void Die_Umgebungen_des_Bestands_binden_streng(string name)
    {
        // Die echten Dateien unter umgebungen/ (mit Identitäten, JSON-Eingang, Worker) — ein Tippfehler bräche erst den Host-Start.
        var verz = AppContext.BaseDirectory;
        while (verz != null && !Directory.Exists(Path.Combine(verz, "umgebungen"))) verz = Path.GetDirectoryName(verz);
        Environment.SetEnvironmentVariable("CQRS_UMGEBUNGEN", Path.Combine(verz!, "umgebungen"));
        var u = UmgebungLader.Lade(new ConfigurationManager(), name, [], branch: "main");
        u.Umgebung.Name.Should().Be(name);
        if (name == "sim-main")
        {
            u.Transport.JsonEingang.Should().BeTrue("Sim: die Werkbank handelt als Akteur");
            u.Funktionen["IBildaufbereitung"].Worker!.Skript.Should().EndWith("funktions_worker.py");
            u.Funktionen["IBildaufbereitung"].Worker!.Argumente.Should().Contain(a => a.EndsWith("/cqrs-sim/main/preprocessed"));
        }
        else u.Transport.JsonEingang.Should().BeFalse("Prod: kein JSON-Eingang");
    }

    [Fact]
    public void Die_generierte_Tabelle_kennt_die_Funktionen_des_Bestands_mit_ihrem_Laufort_aus_der_Form()
    {
        var b = GeneratedFunktionsBausteine.Alle.ToDictionary(x => x.Name);
        b.Keys.Should().Contain(["IBildaufbereitung", "IBildpaarBeurteilung", "ITraining", "Bildeingang", "RangeAufloesung", "MitgliederEinfrieren"]);
        b["IBildaufbereitung"].Draussen.Should().BeTrue("eine Schnittstelle");
        b["IBildaufbereitung"].Umsetzungen.Select(u => u.Name).Should().Equal(["OpenCvBildaufbereitung"], "genau eine Umsetzung im Host");
        b["IBildpaarBeurteilung"].Umsetzungen.Should().BeEmpty("nur draußen getragen (ITraegt)");
        b["Bildeingang"].Gleichbleibend.Should().BeTrue("eine Klasse ohne Dienst und Lese-Fähigkeit");
        b["RangeAufloesung"].Liest.Should().BeTrue("mit Lese-Fähigkeit: läuft im Host und hält fest");
    }
}
