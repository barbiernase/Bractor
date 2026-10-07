using System.Collections.Generic;
using System.Linq;
using DomainEditor;
using FluentAssertions;
using Record = DomainEditor.Record;

namespace Infrastructure.Pruefstand.Tests;

/// <summary>
/// Der Aufruf-Knoten im Editor-Modell: eine Katalog-Funktion (Auftrag → Ergebnis-Events) und ein Prozess-Schritt, der sie mit
/// <c>Rufe&lt;F&gt;</c> (+ Zeitlimit) ruft — Scaffolder, Validator, Nachrichtenfluss und Grammatik behandeln ihn wie einen Command-Aufruf.
/// </summary>
public class DomainEditorFunktionTests
{
    private const string Ns = "Bild.Eingang";

    private static Record Rec(string name, string kind, params Feld[] felder) => new() { Name = name, Kind = kind, Namespace = Ns, Felder = felder };

    private static EditorModell Modell(string rufe = "IVorverarbeitung", string? ausdruck = null) => new()
    {
        Records =
        [
            Rec("BildEingegangen", RecordArt.Event, new Feld { Name = "Pfad", Typ = "string" }),
            Rec("VorverarbeitungsAuftrag", RecordArt.Auftrag, new Feld { Name = "Pfad", Typ = "string" }, new Feld { Name = "Zielhoehe", Typ = "int" })
                with { Funktion = "IVorverarbeitung" },
            Rec("Vorverarbeitet", RecordArt.Event, new Feld { Name = "Vorschau", Typ = "string" }),
            Rec("Unlesbar", RecordArt.Event, new Feld { Name = "Grund", Typ = "string" }),
            Rec("MeldeBild", RecordArt.Command, new Feld { Name = "AggregateId", Typ = "Guid" }, new Feld { Name = "Vorschau", Typ = "string" }),
            Rec("BildGemeldet", RecordArt.Event, new Feld { Name = "Vorschau", Typ = "string" }),
        ],
        Aggregate = [new() { Name = "Bildakte", Namespace = Ns }],
        Decider = [new() { Aggregat = "Bildakte", Command = "MeldeBild", Ergibt = [new() { Event = "BildGemeldet" }] }],
        Applier = [new() { Aggregat = "Bildakte", Event = "BildGemeldet" }],
        Funktionen = [new() { Name = "IVorverarbeitung", Namespace = Ns, Auftrag = "VorverarbeitungsAuftrag", Ergebnisse = ["Vorverarbeitet", "Unlesbar"] }],
        Sagas =
        [
            new()
            {
                Name = "BildAufbereitung", Namespace = Ns, TriggerEvent = "BildEingegangen",
                Schritte =
                [
                    new() { Wenn = ["BildEingegangen"], Rufe = rufe, SendeAusdruck = ausdruck, Zeitlimit = "TimeSpan.FromSeconds(30)" },
                    new() { Wenn = ["Vorverarbeitet"], Sende = "MeldeBild" },
                ],
            },
        ],
    };

    private static string Datei(EditorModell m, string endung) => Scaffolder.Generiere(m).Single(d => d.Pfad.EndsWith(endung)).Inhalt;

    [Fact]
    public void Der_Scaffolder_schreibt_Rufe_mit_Zeitlimit_die_Funktions_Schnittstelle_und_den_Auftrag()
    {
        var m = Modell(ausdruck: "b => new VorverarbeitungsAuftrag(b.Pfad, 512)");

        var prozess = Datei(m, "BildAufbereitung.cs");
        prozess.Should().Contain(".Rufe<IVorverarbeitung>(b => new VorverarbeitungsAuftrag(b.Pfad, 512))")
               .And.Contain(".Zeitlimit(TimeSpan.FromSeconds(30));")
               .And.Contain(".Sende<MeldeBild>(");

        Datei(m, "IVorverarbeitung.cs").Should()
            .Contain("public interface IVorverarbeitung : IFunktion")
            .And.Contain("Task<OneOf<Vorverarbeitet, Unlesbar>> RufeAsync(VorverarbeitungsAuftrag auftrag, IAusfuehrung x);");

        Datei(m, "Auftraege.cs").Should().Contain("public record VorverarbeitungsAuftrag(string Pfad, int Zielhoehe) : IAuftrag<IVorverarbeitung>;");
    }

    [Fact]
    public void Ohne_Ausdruck_baut_der_Stub_den_Auftrag_der_gerufenen_Funktion()
    {
        Datei(Modell(), "BildAufbereitung.cs").Should().Contain(".Rufe<IVorverarbeitung>(t => new VorverarbeitungsAuftrag(");
    }

    [Fact]
    public void Ein_gueltiger_Funktionsaufruf_verletzt_weder_Validator_noch_Grammatik()
    {
        Validator.Prüfe(Modell()).Where(b => b.IstFehler).Should().BeEmpty();
    }

    [Fact]
    public void Eine_unbekannte_Funktion_und_ein_falscher_Auftrag_werden_gemeldet()
    {
        Validator.Prüfe(Modell(rufe: "IGibtEsNicht")).Select(b => b.Code).Should().Contain("EDIT-SAGA-FUNKTION");

        var falsch = Modell() with { Funktionen = [new() { Name = "IVorverarbeitung", Namespace = Ns, Auftrag = "BildEingegangen", Ergebnisse = ["Vorverarbeitet"] }] };
        Validator.Prüfe(falsch).Select(b => b.Code).Should().Contain("EDIT-FUNKTION-AUFTRAG");
    }

    [Fact]
    public void Zeitlimit_hinter_einem_Count_Join_wird_gemeldet()
    {
        var m = Modell();
        var s = m.Sagas[0];
        var mitSammel = m with { Sagas = [s with { Schritte = [s.Schritte[0], s.Schritte[1] with { SammelEvent = "Unlesbar", Zeitlimit = "TimeSpan.FromSeconds(1)" }] }] };
        Validator.Prüfe(mitSammel).Select(b => b.Code).Should().Contain("EDIT-SAGA-ZEITLIMIT");
    }

    [Fact]
    public void Der_Nachrichtenfluss_fuehrt_vom_Prozess_ueber_den_Auftrag_zur_Funktion_und_zu_ihren_Ergebnissen()
    {
        var fluss = Fluss.Aus(Modell());
        fluss.Kanten.Should().Contain(k => k.Von == "saga:BildAufbereitung" && k.Nach == "msg:VorverarbeitungsAuftrag" && k.Sorte == Grammatik.Auftrag);
        fluss.Kanten.Should().Contain(k => k.Von == "msg:VorverarbeitungsAuftrag" && k.Nach == "fn:IVorverarbeitung");
        fluss.Kanten.Should().Contain(k => k.Von == "fn:IVorverarbeitung" && k.Nach == "msg:Vorverarbeitet");
        fluss.Kanten.Should().Contain(k => k.Von == "fn:IVorverarbeitung" && k.Nach == "msg:Unlesbar");
        Grammatik.KonsumVon(Grammatik.Auftrag, Grammatik.Funktion)!.Kardinalitaet.Should().Be(Grammatik.GenauEins);
        Grammatik.ErzeugungVon(Grammatik.Funktion, Grammatik.Event).Should().NotBeNull();
        Grammatik.ErzeugungVon(Grammatik.Prozess, Grammatik.Auftrag).Should().NotBeNull();
    }

    [Fact]
    public void Ein_Aufruf_mit_zugeordneten_Argumenten_baut_den_Auftrag_positionsweise()
    {
        // Wie der Editor ihn anlegt (Ablauf-Kette, „Feld ← Quelle“): kein gelesener Ausdruck, sondern Argumente je Auftragsfeld.
        var m = Modell();
        m = m with { Sagas = [m.Sagas[0] with { Schritte = [m.Sagas[0].Schritte[0] with { SendeArgumente = ["t.Pfad", "512"] }, m.Sagas[0].Schritte[1]] }] };
        Datei(m, "BildAufbereitung.cs").Should().Contain(".Rufe<IVorverarbeitung>(t => new VorverarbeitungsAuftrag(t.Pfad, 512))");
    }

    [Fact]
    public void Ohne_Zuordnung_wird_der_Auftrag_mit_default_gebaut_nicht_mit_einem_TODO()
    {
        Datei(Modell(), "BildAufbereitung.cs").Should().Contain(".Rufe<IVorverarbeitung>(t => new VorverarbeitungsAuftrag(default, default))");
    }

    [Fact]
    public void Der_Herkunfts_Stempel_sieht_eine_Aenderung_an_Rufe_oder_Zeitlimit()
    {
        var s = Modell().Sagas[0];
        var stempel = Herkunft.Von(s);
        Herkunft.Von(s with { Schritte = [s.Schritte[0] with { Zeitlimit = "TimeSpan.FromMinutes(1)" }, s.Schritte[1]] }).Should().NotBe(stempel);
        Herkunft.Von(s with { Schritte = [s.Schritte[0] with { Rufe = "IAndere" }, s.Schritte[1]] }).Should().NotBe(stempel);
    }
}
