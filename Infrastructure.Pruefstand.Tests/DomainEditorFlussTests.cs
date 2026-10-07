using System.Collections.Generic;
using System.Linq;
using DomainEditor;
using FluentAssertions;
using Record = DomainEditor.Record;

namespace Infrastructure.Pruefstand.Tests;

/// <summary>
/// Pipeline als Fluss im Editor-Modell (docs/konzept-editor-pipelines.md §14): der Scaffolder schreibt die Knoten so, wie der
/// Editor sie zeichnet (vom Draht aus, Lambda-Parameter = Knoten-Namen, ∧/∨/⏳/Je ausdrücklich), und der Validator meldet, was
/// der Compiler nicht sieht (Fall außerhalb der Ausgänge, offener ⏳-Port, „∧ wartet auf einen Weg, der nicht immer liefert").
/// </summary>
public class DomainEditorFlussTests
{
    private const string Ns = "Kamera.Eingang";

    private static Record Rec(string name, string kind, params Feld[] felder) => new() { Name = name, Kind = kind, Namespace = Ns, Felder = felder };
    private static Feld F(string name, string typ) => new() { Name = name, Typ = typ };
    private static FlussDraht D(string von, string? fall = null, string port = FlussPort.Fall) => new() { Von = von, Fall = fall, Port = port };
    private static FlussEingang E(params FlussDraht[] d) => new() { Draehte = d };

    private static EditorModell Modell(params FlussSchritt[] schritte) => new()
    {
        Records =
        [
            Rec("DateiDa", RecordArt.Event, F("Pfad", "string"), F("Name", "string")),
            Rec("DeuteName", RecordArt.Auftrag, F("Name", "string")) with { Funktion = "INameDeuten" },
            Rec("Bilddatei", RecordArt.Event, F("PaarId", "Guid"), F("Pfad", "string")),
            Rec("Unbekannt", RecordArt.Event, F("Name", "string")),
            Rec("Verkleinere", RecordArt.Auftrag, F("Pfad", "string"), F("Hoehe", "int")) with { Funktion = "IVerkleinern" },
            Rec("Verkleinert", RecordArt.Event, F("Pfad", "string")),
            Rec("MeldeBild", RecordArt.Command, F("AggregateId", "Guid"), F("Vorschau", "string"), F("Daumen", "string")),
            Rec("BildGemeldet", RecordArt.Event, F("Vorschau", "string")),
        ],
        Aggregate = [new() { Name = "Bildakte", Namespace = Ns }],
        Decider = [new() { Aggregat = "Bildakte", Command = "MeldeBild", Ergibt = [new() { Event = "BildGemeldet" }] }],
        Applier = [new() { Aggregat = "Bildakte", Event = "BildGemeldet" }],
        Funktionen =
        [
            new() { Name = "INameDeuten", Namespace = Ns, Auftrag = "DeuteName", Ergebnisse = ["Bilddatei", "Unbekannt"] },
            new() { Name = "IVerkleinern", Namespace = Ns, Auftrag = "Verkleinere", Ergebnisse = ["Verkleinert"] },
        ],
        Fluesse = [new() { Name = "Bildeingang", Namespace = Ns, Knoten = schritte }],
    };

    /// <summary>Der Bildeingang so, wie er auf leerem Board gezeichnet wird: nur Zuordnungen, kein Lambda.</summary>
    private static FlussSchritt[] Bildeingang(bool unbekanntVerdrahtet = false) =>
    [
        new() { Name = "datei", Art = FlussArt.Quelle, Typ = "DateiDa" },
        new() { Name = "deuten", Art = FlussArt.Funktion, Typ = "INameDeuten", Eingaenge = [E(D("datei")) with { Argumente = ["datei.Name"] }] },
        new() { Name = "vorschau", Art = FlussArt.Funktion, Typ = "IVerkleinern", Zeitlimit = "TimeSpan.FromMinutes(5)",
                Eingaenge = [E(D("deuten", "Bilddatei")) with { Argumente = ["deuten.Pfad", "512"] }] },
        new() { Name = "daumen", Art = FlussArt.Funktion, Typ = "IVerkleinern",
                Eingaenge = [E(D("deuten", "Bilddatei")) with { Argumente = ["deuten.Pfad", "128"] }] },
        new() { Name = "melden", Art = FlussArt.Command, Typ = "MeldeBild",
                Eingaenge = [E(D("vorschau", "Verkleinert"), D("daumen", "Verkleinert"), D("deuten", "Bilddatei"))
                    with { Argumente = ["deuten.PaarId", "vorschau.Pfad", "daumen.Pfad"] }] },
        .. unbekanntVerdrahtet
            ? new FlussSchritt[] { new() { Name = "melden2", Art = FlussArt.Command, Typ = "MeldeBild",
                Eingaenge = [E(D("deuten", "Unbekannt")) with { Argumente = ["System.Guid.Empty", "\"\"", "\"\""] }] } }
            : [],
    ];

    private static string Datei(EditorModell m) => Scaffolder.Generiere(m).Single(d => d.Pfad.EndsWith("Bildeingang.cs")).Inhalt;

    [Fact]
    public void Der_Scaffolder_schreibt_den_gezeichneten_Fluss_vom_Draht_aus_mit_Knoten_Namen_als_Lambda_Parametern()
    {
        var code = Datei(Modell(Bildeingang()));

        code.Should().Contain("public sealed class Bildeingang : IPipeline")
            .And.Contain("public PipelineFluss Fluss => PipelineFluss.Definiere(p =>")
            .And.Contain("var datei = p.Quelle<DateiDa>();")
            .And.Contain("var deuten = datei.Rufe<INameDeuten>(datei => new DeuteName(datei.Name));")
            .And.Contain("var vorschau = deuten.Bei<Bilddatei>().Rufe<IVerkleinern>(deuten => new Verkleinere(deuten.Pfad, 512))")
            .And.Contain("    .Zeitlimit(TimeSpan.FromMinutes(5));")
            .And.Contain("var daumen = deuten.Bei<Bilddatei>().Rufe<IVerkleinern>(deuten => new Verkleinere(deuten.Pfad, 128));")
            .And.Contain("var melden = p.Alle(vorschau.Bei<Verkleinert>(), daumen.Bei<Verkleinert>(), deuten.Bei<Bilddatei>())"
                         + ".Sende<MeldeBild>((vorschau, daumen, deuten) => new MeldeBild(deuten.PaarId, vorschau.Pfad, daumen.Pfad));");
    }

    [Fact]
    public void Oder_Fehler_Ports_Je_und_Sammeln_werden_ausdruecklich_geschrieben()
    {
        var m = Modell(
            new() { Name = "datei", Art = FlussArt.Quelle, Typ = "DateiDa" },
            new() { Name = "teile", Art = FlussArt.Je, Typ = "string", Liste = "d => new[] { d.Pfad, d.Name }", Eingaenge = [E(D("datei"))] },
            new() { Name = "klein", Art = FlussArt.Funktion, Typ = "IVerkleinern", Zeitlimit = "TimeSpan.FromSeconds(9)",
                    Eingaenge = [new() { Je = "teile", Argumente = ["teile", "64"] }] },
            new() { Name = "melden", Art = FlussArt.Command, Typ = "MeldeBild", OhneVariable = true,
                    Eingaenge =
                    [
                        new() { Je = "teile", Sammle = true, Draehte = [D("klein", "Verkleinert")], Ausdruck = "(datei, klein) => new MeldeBild(System.Guid.Empty, datei.Pfad, klein[0].Pfad)" },
                    ] },
            new() { Name = "notfall", Art = FlussArt.Command, Typ = "MeldeBild",
                    Eingaenge =
                    [
                        E(D("klein", port: FlussPort.Zeitlimit)) with { Ausdruck = "z => new MeldeBild(System.Guid.Empty, z.Grund, \"\")" },
                        E(D("datei")) with { Ausdruck = "d => new MeldeBild(System.Guid.Empty, d.Pfad, \"\")" },
                    ] });

        var code = Datei(m);
        code.Should().Contain("var teile = datei.Je(d => new[] { d.Pfad, d.Name });")
            .And.Contain("var klein = teile.Rufe<IVerkleinern>(teile => new Verkleinere(teile, 64))")
            .And.Contain("teile.Sammle(klein.Bei<Verkleinert>()).Sende<MeldeBild>((datei, klein) => new MeldeBild(System.Guid.Empty, datei.Pfad, klein[0].Pfad));")
            .And.Contain("var notfall = klein.BeiZeitlimit().Sende<MeldeBild>(z => new MeldeBild(System.Guid.Empty, z.Grund, \"\"))")
            .And.Contain("    .Oder(datei, d => new MeldeBild(System.Guid.Empty, d.Pfad, \"\"));");
        Validator.PruefeFluesse(m).Where(b => b.Schweregrad == "error").Should().BeEmpty();
    }

    [Fact]
    public void Ein_gueltig_gezeichneter_Fluss_hat_keine_Fehler_aber_den_Hinweis_auf_den_offenen_Fall()
    {
        var befunde = Validator.PruefeFluesse(Modell(Bildeingang()));

        befunde.Where(b => b.Schweregrad == "error").Should().BeEmpty();
        // ∧ wartet auf deuten.Bilddatei — kommt „Unbekannt", wartet melden für immer (deuten hat kein Zeitlimit).
        befunde.Should().ContainSingle(b => b.Code == "GR-FLUSS-WARTET")
            .Which.Meldung.Should().Contain("deuten.Bilddatei").And.Contain("Unbekannt").And.Contain("für immer");

        Validator.PruefeFluesse(Modell(Bildeingang(unbekanntVerdrahtet: true)))
            .Should().NotContain(b => b.Code == "GR-FLUSS-WARTET", "der andere Fall ist verdrahtet");
    }

    [Fact]
    public void Der_Validator_meldet_was_der_Compiler_nicht_sieht()
    {
        var schritte = Bildeingang().ToList();
        schritte[2] = schritte[2] with { Eingaenge = [E(D("deuten", "Verkleinert")) with { Argumente = ["deuten.Pfad", "1"] }] };   // kein Ausgang von deuten
        schritte[3] = schritte[3] with { Eingaenge = [E(D("daumen", "Verkleinert"))] };                                               // Draht von sich selbst
        schritte.Add(new() { Name = "zeit", Art = FlussArt.Command, Typ = "MeldeBild", Eingaenge = [E(D("daumen", port: FlussPort.Zeitlimit))] });
        schritte.Add(new() { Name = "zweite", Art = FlussArt.Auf, Typ = "BildGemeldet" });
        var befunde = Validator.PruefeFluesse(Modell(schritte.ToArray()));

        befunde.Should().Contain(b => b.Code == "GR-FLUSS-DRAHT" && b.Meldung.Contains("'Verkleinert' ist kein Ausgang von 'deuten'"));
        befunde.Should().Contain(b => b.Code == "GR-FLUSS-DRAHT" && b.Meldung.Contains("kein früherer Knoten"));
        befunde.Should().Contain(b => b.Code == "GR-FLUSS-ZEITLIMIT");
        befunde.Should().Contain(b => b.Code == "GR-FLUSS-QUELLE");
        befunde.Should().Contain(b => b.Code == "EDIT-FLUSS-ZUORDNUNG" && b.Meldung.Contains("zeit"));
    }

    [Fact]
    public void Im_Nachrichtenfluss_ist_die_Pipeline_ein_Baustein_der_Auftraege_und_Commands_erzeugt()
    {
        var fluss = DomainEditor.Fluss.Aus(Modell(Bildeingang()));

        fluss.Ein("fl:Bildeingang").Select(k => k.Nachricht).Should().Contain(["DateiDa", "Bilddatei", "Verkleinert"]);
        fluss.Aus("fl:Bildeingang").Select(k => k.Nachricht).Should().Contain(["DeuteName", "Verkleinere", "MeldeBild"]);
        Validator.PruefeGrammatik(Modell(Bildeingang())).Should().NotContain(b => b.Schweregrad == "error" && b.Meldung.Contains("Bildeingang"));
    }

    [Fact]
    public void Der_Herkunfts_Stempel_erkennt_eine_Aenderung_am_Fluss()
    {
        var m = Herkunft.Stempeln(Modell(Bildeingang()));
        var f = m.Fluesse[0];
        Herkunft.Geaendert(f.Herkunft, Herkunft.Von(f)).Should().BeFalse();

        var geaendert = f with { Knoten = f.Knoten.Select(s => s.Name == "vorschau" ? s with { Zeitlimit = "TimeSpan.FromMinutes(1)" } : s).ToList() };
        Herkunft.Geaendert(f.Herkunft, Herkunft.Von(geaendert)).Should().BeTrue();
        Herkunft.Geaenderte(m with { Fluesse = [geaendert] }).Should().Contain($"pipeline {Ns}.Bildeingang");
    }
}
