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
/// Die Boot-Prüfung der Eingänge aus Quellen (docs/04 §4.7, <c>Eingaenge.Aus</c>) gegen die Beispielanwendung: eine App-Instanz nennt eine App, die es gibt
/// (hier <c>KameraApp([Uebersetzt&lt;DateiGefunden_LiesBild_1&gt;] Dateiwaechter Kamera) : IApp&lt;Linienrechner&gt;</c>), ihre Werte nennen nur Quellen der
/// App, ihr Token gibt es und es nennt einen Akteur ihres Clients; je Quelle wird ein Eingang mit der Quellart, der Übersetzung aus dem Code und dem Akteur,
/// der darf, was sie gibt. Laufen tun sie im Rechnerdienst des Rechners der Instanz. Jeder Fehler ist ein Boot-Fehler mit der Instanz bzw. der Quelle
/// als Karte.
/// </summary>
public class EingaengeTests
{
    private static readonly Dictionary<string, IReadOnlyList<string>> Tokens = new()
    {
        ["kamera"] = ["KameraSystem"], ["arbeitsplatz"] = ["Inspekteur", "KIOperator"],
    };

    private static AppInstanzKonfig Linie(string app = "KameraApp", string rechner = "linie-1", string token = "kamera") => new()
    {
        App = app, Rechner = rechner, Token = token,
        Quellen = { ["Kamera"] = new QuelleKonfig { Werte = { ["Pfad"] = "/bilder/linie1", ["Muster:0"] = "*.tiff" } } },
    };

    private static QuellEingaenge Aus(AppInstanzKonfig a, string name = "linie1") =>
        QuellEingaenge.Aus(new Dictionary<string, AppInstanzKonfig> { [name] = a }, A.Akteure.AppVertraege, A.Funktionen.Bausteine,
            new Uebersetzungen(A.Funktionen.Uebersetzungen), A.Akteure.Rechte, Tokens);

    private static void Scheitert(Func<QuellEingaenge> aus, string muster, string karte = "appinstanz:linie1") =>
        FluentActions.Invoking(aus).Should().Throw<BootFehler>().WithMessage(muster).Which.Karte.Should().Be(karte);

    [Fact]
    public void Die_Quelle_einer_Kamera_App_bindet_Art_Uebersetzung_Akteur_Werte_und_Kontext()
    {
        var e = Aus(Linie()).Alle.Should().ContainSingle().Subject;
        e.Ort.Should().Be("linie1/Kamera", "Schlüssel seiner Ids und seiner Spur: Instanz und Quelle");
        e.Art.Name.Should().Be("Dateiwaechter");
        e.Art.Lausche.Should().NotBeNull("eine Quellart lauscht");
        e.Akteur.Should().Be("KameraSystem", "der Akteur des Clients, der darf, was die Übersetzung gibt");
        e.Rechner.Should().Be("linie-1", "der Rechner der Instanz");
        e.Uebersetzungen.Keys.Should().Equal(typeof(DateiGefunden));
        e.Uebersetzungen[typeof(DateiGefunden)].Select(u => (u.Name, u.Aus)).Should().Equal(("DateiGefunden_LiesBild_1", typeof(Bildvorschau.Katalog.LiesBild)));
        e.Kontext.Should().Match<KnotenKontext>(k => k.Instanz == "eingang:linie1" && k.Knoten == "Kamera", "Zustand und Technik-Wahl je Quelle");
        e.Kontext.Konfig.Werte.Should().Contain("Pfad", "/bilder/linie1").And.Contain("Muster:0", "*.tiff");
    }

    [Fact]
    public void Zwei_Instanzen_derselben_App_sind_zwei_Eingaenge()
    {
        var apps = new Dictionary<string, AppInstanzKonfig> { ["linie2"] = Linie(rechner: "linie-2"), ["linie1"] = Linie() };
        QuellEingaenge.Aus(apps, A.Akteure.AppVertraege, A.Funktionen.Bausteine, new Uebersetzungen(A.Funktionen.Uebersetzungen), A.Akteure.Rechte, Tokens)
            .Alle.Select(e => (e.Ort, e.Rechner)).Should().Equal([("linie1/Kamera", "linie-1"), ("linie2/Kamera", "linie-2")], "geordnet, je Quelle ein Läufer");
    }

    [Fact]
    public void Eine_App_die_es_nicht_gibt_kennt_der_Start_nicht()
        => Scheitert(() => Aus(Linie(app: "Kamerarechner")), "*eine App „Kamerarechner“ gibt es nicht*KameraApp*");

    [Fact]
    public void Die_Werte_nennen_nur_Quellen_der_App()
    {
        var a = Linie();
        a.Quellen["Linie"] = new QuelleKonfig();
        Scheitert(() => Aus(a), "*„Linie“ ist keine Quelle von KameraApp (Quellen: Kamera)*");
    }

    [Fact]
    public void Das_Token_gibt_es_und_es_nennt_einen_Akteur_des_Clients()
    {
        Scheitert(() => Aus(Linie(token: "fremd")), "*das Token „fremd“ gibt es nicht*");
        Scheitert(() => Aus(Linie(token: "arbeitsplatz")), "*das Token „arbeitsplatz“ nennt keinen Akteur von Linienrechner (KameraSystem)*");
        Aus(Linie(token: "")).Alle.Single().Akteur.Should().Be("KameraSystem", "ohne Token gelten alle Akteure des Clients");
    }

    [Fact]
    public void Eine_App_ohne_Quellen_gibt_keinen_Eingang()
        => QuellEingaenge.Aus(new Dictionary<string, AppInstanzKonfig> { ["arbeitsplatz"] = new() { App = "ArbeitsplatzApp", Rechner = "linie-1", Token = "arbeitsplatz" } },
                A.Akteure.AppVertraege, A.Funktionen.Bausteine, new Uebersetzungen(A.Funktionen.Uebersetzungen), A.Akteure.Rechte, Tokens)
            .Alle.Should().BeEmpty("alles, was ihr Client sendet, gibt die App selbst hinein");

    [Fact]
    public void Eine_Quelle_laeuft_auf_dem_Rechner_ihrer_Instanz_auch_ausserhalb_des_Backends()
        => Aus(Linie(rechner: "linie-9")).Alle.Single().Rechner.Should().Be("linie-9", "der Rechnerdienst dieses Rechners trägt sie");

    [Fact]
    public void Die_Werte_einer_Quelle_bindet_die_Technik_mit_der_Quelle_als_Karte()
    {
        var gut = Aus(Linie());
        new ServiceCollection().BindeTechnik(GraphInstanzen.Keine, gut)
            .Should().Contain(d => d.ServiceType == typeof(TechnikWahl), "die Werte der Quellart binden wie die eines Knotens");
        var falsch = Linie();
        falsch.Quellen["Kamera"].Werte["Pfadd"] = "/x";
        FluentActions.Invoking(() => new ServiceCollection().BindeTechnik(GraphInstanzen.Keine, Aus(falsch)))
            .Should().Throw<BootFehler>().WithMessage("*eingang:linie1/Kamera: unbekannte Felder Pfadd*")
            .Which.Karte.Should().Be("eingang:linie1/Kamera");
    }
}
