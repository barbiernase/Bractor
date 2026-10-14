using System.Collections.Generic;
using System.Linq;
using DomainEditor;
using FluentAssertions;
using Xunit;
using Record = DomainEditor.Record;

namespace Infrastructure.Pruefstand.Tests;

/// <summary>
/// Wohin eine NEUE Karte geschrieben wird (docs/07, <see cref="Strukturort"/>): das Projekt folgt aus der Rolle, der Bereich aus dem Namespace der
/// Karte, die Datei aus der Tabelle — der Server entscheidet, nicht der Editor. Namespaces außerhalb der Rollen-Projekte bleiben unverändert.
/// </summary>
public class DomainEditorStrukturortTests
{
    private static Record Rec(string name, string kind, string ns) => new() { Name = name, Kind = kind, Namespace = ns, Felder = [new Feld { Name = "Pfad", Typ = "string" }] };

    private static EditorModell Modell(string ns) => new()
    {
        Rahmen = new Rahmen
        {
            Verzeichnisse = new Dictionary<string, string>
            {
                ["Domain"] = "Domain", ["Domain.Projections"] = "Domain.Projections", ["Domain.Pipeline"] = "Domain.Pipeline",
                ["Domain.Akteure"] = "Domain.Akteure", ["Domain.Infrastructure"] = "Domain.Infrastructure",
            },
        },
        Records =
        [
            Rec("Pruefe", RecordArt.Auftrag, ns) with { Funktion = "IPruefung" },
            Rec("Geprueft", RecordArt.Event, ns),
            Rec("Gebucht", RecordArt.Event, ns),
            Rec("Zeige", RecordArt.Query, ns),
        ],
        Funktionen = [new() { Name = "IPruefung", Namespace = ns, Auftrag = "Pruefe", Ergebnisse = ["Geprueft"] }],
        Akteure = [new() { Name = "Pruefer", Namespace = ns, Art = "Mensch", Darf = ["Zeige"] }],
    };

    [Fact]
    public void Neue_Karten_landen_im_Projekt_ihrer_Rolle_im_selben_Bereich()
    {
        var pfade = Scaffolder.Generiere(Modell("Domain.Ausleihe")).Select(d => d.Pfad).ToList();
        pfade.Should().Contain("Domain.Pipeline/Ausleihe/Funktionen.cs", "die Funktion gehört in die Abläufe, Sammeldatei Funktionen.cs");
        pfade.Should().Contain("Domain.Pipeline/Ausleihe/Auftraege.cs");
        pfade.Should().Contain("Domain.Pipeline/Ausleihe/Events.cs", "ein Ergebnis-Event gehört zur Funktion");
        pfade.Should().Contain("Domain/Ausleihe/Events.cs", "ein gewöhnliches Event bleibt auf der Schreibseite");
        pfade.Should().Contain("Domain.Projections/Ausleihe/Queries.cs");
        pfade.Should().Contain("Domain.Akteure/Akteure.cs", "Akteure haben keinen Bereichs-Ordner");

        var funktion = Scaffolder.Generiere(Modell("Domain.Ausleihe")).Single(d => d.Pfad == "Domain.Pipeline/Ausleihe/Funktionen.cs");
        funktion.Inhalt.Should().Contain("namespace Domain.Pipeline.Ausleihe");
    }

    [Fact]
    public void Der_Bereich_bleibt_auch_aus_einem_anderen_Rollen_Projekt()
    {
        Strukturort.Ziel("Domain.Pipeline.Ausleihe", Abstractions.Projektstruktur.Rolle.Schreibseite).Should().Be("Domain.Ausleihe");
        Strukturort.Ziel("Domain.Projections.Ausleihe", Abstractions.Projektstruktur.Rolle.Akteure).Should().Be("Domain.Akteure");
        Strukturort.Ziel("Leihwesen.Ausleihe", Abstractions.Projektstruktur.Rolle.Ablaeufe).Should().BeNull("fremde Namespaces bleiben unverändert");
    }

    [Fact]
    public void Bestehendes_und_Fremdes_bleibt_wo_es_ist()
    {
        var m = Modell("Leihwesen.Ausleihe");
        Strukturort.Normalisiere(m).Should().BeEquivalentTo(m);
        var bestehend = Modell("Domain.Ausleihe") with
        {
            Funktionen = [Modell("Domain.Ausleihe").Funktionen[0] with { Datei = "Domain/Ausleihe/Alt.cs" }],
        };
        Strukturort.Normalisiere(bestehend).Funktionen[0].Namespace.Should().Be("Domain.Ausleihe", "eine Karte mit Datei zieht nicht um");
    }
}
