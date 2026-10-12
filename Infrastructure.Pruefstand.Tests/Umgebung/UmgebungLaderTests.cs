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
/// Umgebungen (docs/konzept-werkbank.md §4), store-frei: Laden der Datei, Platzhalter, strikte Bindung, das Siegel als reiner
/// Vergleich und der Funktions-Laufort als Konfig gegen die generierte Tabelle.
/// </summary>
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
              "VersionIndex": { "Betrieb": "Aus", "Praefix": "sim:${branch}:" },
              "Cluster": { "Art": "Consul", "Name": "c-${branch}", "Rolle": "Mitglied" },
              "Funktionen": { "IX": { "Laufort": "Extern" } } }
            """);
        try
        {
            var u = UmgebungLader.Lade(k, "sim-t", [], branch: "Feature/Werkbank-P2");
            u.Umgebung.Art.Should().Be(UmgebungsArt.Sim);
            u.EventStore.Datenbank.Should().Be("cqrs_sim_feature_werkbank_p2", "${branch} wird DB-tauglich");
            UmgebungLader.Verbindung(u.EventStore).Should().Contain("Password=geheim").And.Contain("Database=cqrs_sim_feature_werkbank_p2");
            u.VersionIndex.Betrieb.Should().Be(Betrieb.Aus);
            u.VersionIndex.Praefix.Should().Be("sim:feature_werkbank_p2:");
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

    private interface IA;   // bewusst KEIN IFunktion: sonst sähen die Generator-Tests (alle geladenen Assemblies) eine Funktion ohne Form
    private interface IB;
    private sealed class A1 : IA;
    private sealed class A2 : IA;

    private static List<string> Gebunden(Dictionary<string, FunktionsKonfig> k)
    {
        var protokoll = new List<string>();
        var namen = new Dictionary<string, Type> { ["IA"] = typeof(IA), ["IB"] = typeof(IB) };
        var impls = new[]
        {
            new FunktionsImplementierung(typeof(IA), "A1", (_, s, w) => protokoll.Add($"A1 {s}/{w}")),
            new FunktionsImplementierung(typeof(IA), "A2", (_, s, w) => protokoll.Add($"A2 {s}/{w}")),
        };
        var ext = new Dictionary<Type, Action<IServiceCollection>> { [typeof(IA)] = _ => protokoll.Add("IA extern"), [typeof(IB)] = _ => protokoll.Add("IB extern") };
        var faeh = new Dictionary<Type, Type[]> { [typeof(IB)] = [typeof(object)] };
        new ServiceCollection().BindeFunktionen(k, namen, impls, ext, faeh);
        return protokoll;
    }

    [Fact]
    public void Laufort_Host_bindet_die_genannte_Implementierung()
        => Gebunden(new() { ["IA"] = new() { Implementierung = "A2", Slots = 3, Wiederholungen = 1 } }).Should().Equal("A2 3/1");

    [Fact]
    public void Laufort_Host_mit_mehreren_Implementierungen_braucht_einen_Namen()
        => FluentActions.Invoking(() => Gebunden(new() { ["IA"] = new() })).Should().Throw<InvalidOperationException>().WithMessage("*A1, A2*");

    [Fact]
    public void Laufort_Extern_bindet_extern_ausser_mit_Lese_Faehigkeit()
    {
        Gebunden(new() { ["IA"] = new() { Laufort = FunktionsLaufort.Extern } }).Should().Equal("IA extern");
        FluentActions.Invoking(() => Gebunden(new() { ["IB"] = new() { Laufort = FunktionsLaufort.Extern } }))
            .Should().Throw<InvalidOperationException>().WithMessage("*Lese-Fähigkeit*");
    }

    [Fact]
    public void Laufort_Hand_bindet_den_Hand_Ausfuehrer_einmal_auch_mit_Lese_Faehigkeit()
    {
        // Ein Mensch antwortet — er braucht keine Stores des Hosts; der Hand-Ausführer holt die Aufträge (einer für alle Funktionen).
        var s = new ServiceCollection();
        var namen = new Dictionary<string, Type> { ["IA"] = typeof(IA), ["IB"] = typeof(IB) };
        s.BindeFunktionen(new Dictionary<string, FunktionsKonfig> { ["IA"] = new() { Laufort = FunktionsLaufort.Hand }, ["IB"] = new() { Laufort = FunktionsLaufort.Hand } },
            namen, [], new Dictionary<Type, Action<IServiceCollection>>(), new Dictionary<Type, Type[]> { [typeof(IB)] = [typeof(object)] });
        var bindungen = s.Where(d => d.ServiceType == typeof(FunktionsBindung)).Select(d => (FunktionsBindung)d.ImplementationInstance!).ToList();
        bindungen.Should().BeEquivalentTo([new FunktionsBindung(typeof(IA), 0, 0, Extern: true, Hand: true), new FunktionsBindung(typeof(IB), 0, 0, Extern: true, Hand: true)]);
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
            u.Funktionen["IBildVerkleinerung"].Worker!.Skript.Should().EndWith("funktions_worker.py");
            u.Funktionen["IBildVerkleinerung"].Worker!.Argumente.Should().Contain(a => a.EndsWith("/cqrs-sim/main/preprocessed"));
        }
        else u.Transport.JsonEingang.Should().BeFalse("Prod: kein JSON-Eingang");
    }

    [Fact]
    public void Unbekannte_Funktion_ist_ein_Fehler()
        => FluentActions.Invoking(() => Gebunden(new() { ["IC"] = new() })).Should().Throw<InvalidOperationException>().WithMessage("*IC*");

    [Fact]
    public void Die_generierte_Tabelle_kennt_alle_Funktionen_des_Bestands_mit_genau_einer_Implementierung()
    {
        GeneratedFunktionen.Namen.Keys.Should().Contain(["IBildVerkleinerung", "IHistogrammAusgleich", "IDateinameDeutung", "IRangeSuche", "IMitgliederEinfrieren"]);
        foreach (var (name, typ) in GeneratedFunktionen.Namen)
            GeneratedFunktionen.Implementierungen.Count(i => i.Funktion == typ).Should().Be(1, $"{name} hat im Bestand genau eine Implementierung");
    }
}
