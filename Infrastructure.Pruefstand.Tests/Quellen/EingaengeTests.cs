using System;
using System.Collections.Generic;
using System.Linq;
using Abstractions;
using FluentAssertions;
using Infrastructure.Funktionen;
using Infrastructure.Umgebung;
using Microsoft.Extensions.DependencyInjection;
using Xunit;
using A = Infrastructure.Komposition.Anwendung;
using QuellEingaenge = Infrastructure.Quellen.Eingaenge;

namespace Infrastructure.Pruefstand.Quellen;

/// <summary>
/// Die Boot-Prüfung der Eingänge aus Quellen (docs/04 §4.7, <c>Eingaenge.Aus</c>) gegen die Beispielanwendung: ein Eingang steht bei einem Client, den es
/// gibt und der seinen Akteur verkörpert; seine Art ist eine Quellart; für jede ihrer Meldungen macht eine Übersetzung der Akteur-Seite eine Nachricht,
/// die der Akteur darf; im Cluster nennt er seinen Rechner (eine Quelle lauscht genau einmal), und ein Rechner ist genau ein Name. Jeder Fehler ist ein
/// Boot-Fehler mit dem Eingang als Karte.
/// </summary>
public class EingaengeTests
{
    private static EingangKonfig Linie(string akteur = "KameraSystem", string art = "Dateiwaechter", string? rechner = null) => new()
    {
        Art = art, Akteur = akteur, Rechner = rechner, Werte = { ["Pfad"] = "/bilder/linie1", ["Muster:0"] = "*.tiff" },
    };

    private static QuellEingaenge Aus(string client, EingangKonfig e, bool einzelknoten = true, string name = "linie1") =>
        QuellEingaenge.Aus(new Dictionary<string, ClientKonfig> { [client] = new() { Eingaenge = { [name] = e } } }, A.Funktionen.Bausteine,
            new Uebersetzungen(A.Funktionen.Uebersetzungen), A.Akteure.Rechte, A.Akteure.ClientVertraege, einzelknoten);

    private static void Scheitert(Func<QuellEingaenge> aus, string muster, string karte = "eingang:Linienrechner/linie1") =>
        FluentActions.Invoking(aus).Should().Throw<BootFehler>().WithMessage(muster).Which.Karte.Should().Be(karte);

    [Fact]
    public void Ein_Eingang_des_Linienrechners_fuer_das_Kamerasystem_bindet_Art_Uebersetzung_Werte_und_Kontext()
    {
        var e = Aus("Linienrechner", Linie()).Alle.Should().ContainSingle().Subject;
        e.Ort.Should().Be("Linienrechner/linie1", "Schlüssel seiner Ids und seiner Spur");
        e.Art.Name.Should().Be("Dateiwaechter");
        e.Art.Lausche.Should().NotBeNull("eine Quellart lauscht");
        e.Akteur.Should().Be("KameraSystem");
        e.Uebersetzungen.Keys.Should().Equal(typeof(DateiGefunden));
        e.Uebersetzungen[typeof(DateiGefunden)].Select(u => (u.Name, u.Aus)).Should().Equal(("DateiGefunden_LiesBild_1", typeof(Bildvorschau.Katalog.LiesBild)));
        e.Kontext.Should().Match<KnotenKontext>(k => k.Instanz == "eingang:Linienrechner" && k.Knoten == "linie1", "Zustand und Technik-Wahl je Eingang");
        e.Kontext.Konfig.Werte.Should().Contain("Pfad", "/bilder/linie1").And.Contain("Muster:0", "*.tiff");
        e.Grenze.Should().Be(EingangKonfig.StandardOffen, "ohne Offen gilt der Standard");
        (e with { Offen = 3 }).Grenze.Should().Be(3);
    }

    [Fact]
    public void Drei_Verzeichnisse_sind_drei_Eingaenge_eines_Clients()
    {
        var clients = new Dictionary<string, ClientKonfig>
        {
            ["Linienrechner"] = new() { Eingaenge = { ["linie3"] = Linie("KameraSystem", rechner: "r"), ["linie1"] = Linie(rechner: "r"), ["linie2"] = Linie(rechner: "r") } },
        };
        QuellEingaenge.Aus(clients, A.Funktionen.Bausteine, new Uebersetzungen(A.Funktionen.Uebersetzungen), A.Akteure.Rechte, A.Akteure.ClientVertraege, false)
            .Alle.Select(e => e.Ort).Should().Equal(["Linienrechner/linie1", "Linienrechner/linie2", "Linienrechner/linie3"], "geordnet, je Eingang ein Läufer");
    }

    [Fact]
    public void Einen_Client_den_es_nicht_gibt_kennt_der_Start_nicht()
        => Scheitert(() => Aus("Kamerarechner", Linie()), "*Clients:Kamerarechner:Eingaenge:linie1*einen Client Kamerarechner gibt es nicht*",
            "eingang:Kamerarechner/linie1");

    [Fact]
    public void Den_Akteur_muss_es_geben_und_der_Client_muss_ihn_verkoerpern()
    {
        Scheitert(() => Aus("Linienrechner", Linie("Kamera")), "*Akteur „Kamera“ gibt es nicht*");
        Scheitert(() => Aus("Linienrechner", Linie("Inspekteur")), "*der Client Linienrechner verkörpert Inspekteur nicht*IVerkoerpert<Inspekteur>*");
    }

    [Fact]
    public void Die_Art_ist_eine_Quellart()
    {
        Scheitert(() => Aus("Linienrechner", Linie(art: "Bildeinlesen")), "*Art „Bildeinlesen“ ist keine Quellart*Dateiwaechter*");
        Scheitert(() => Aus("Linienrechner", Linie(art: "Kamera")), "*Art „Kamera“ ist keine Quellart*");
    }

    [Fact]
    public void Ohne_Uebersetzung_auf_eine_Nachricht_die_der_Akteur_darf_passt_die_Quellart_nicht()
    {
        // Der Taktgeber meldet Takt — keine Übersetzung der Akteur-Seite macht daraus etwas, das das Kamerasystem darf.
        Scheitert(() => Aus("Linienrechner", Linie(art: "Taktgeber")), "*keine Übersetzung macht aus Takt eine Nachricht, die KameraSystem darf*");
        // DateiGefunden_LiesBild_1 gibt es — aber LiesBild darf nur das Kamerasystem, nicht der Inspekteur, den der Arbeitsplatz verkörpert.
        Scheitert(() => Aus("Arbeitsplatz", Linie("Inspekteur")), "*keine Übersetzung macht aus DateiGefunden eine Nachricht, die Inspekteur darf*",
            "eingang:Arbeitsplatz/linie1");
    }

    [Fact]
    public void Im_Cluster_nennt_ein_Eingang_seinen_Rechner_und_ein_Rechner_ist_genau_ein_Name()
    {
        Scheitert(() => Aus("Linienrechner", Linie(), einzelknoten: false), "*im Cluster nennt ein Eingang seinen Rechner*eine Quelle lauscht genau einmal*");
        Aus("Linienrechner", Linie(), einzelknoten: true).Alle.Single().Rechner.Should().BeNull("im Einzelknoten-Betrieb ist der eine Host der Rechner");
        Aus("Linienrechner", Linie(rechner: "linie-1"), einzelknoten: false).Alle.Single().Rechner.Should().Be("linie-1");
        foreach (var falsch in new[] { "", "a,b", "linie-*", " linie-1" })
            Scheitert(() => Aus("Linienrechner", Linie(rechner: falsch)), $"*Rechner „{falsch}“ ist kein Name*");
    }

    [Fact]
    public void Die_Werte_eines_Eingangs_bindet_die_Technik_mit_dem_Eingang_als_Karte()
    {
        var gut = Aus("Linienrechner", Linie());
        new ServiceCollection().BindeTechnik(GraphInstanzen.Keine, gut)
            .Should().Contain(d => d.ServiceType == typeof(TechnikWahl), "die Werte der Quellart binden wie die eines Knotens");
        var falsch = Linie();
        falsch.Werte["Pfadd"] = "/x";
        FluentActions.Invoking(() => new ServiceCollection().BindeTechnik(GraphInstanzen.Keine, Aus("Linienrechner", falsch)))
            .Should().Throw<BootFehler>().WithMessage("*eingang:Linienrechner/linie1: unbekannte Felder Pfadd*")
            .Which.Karte.Should().Be("eingang:Linienrechner/linie1");
    }
}
