using DomainEditor;
using FluentAssertions;
using Record = DomainEditor.Record;

namespace Infrastructure.Pruefstand.Tests;

/// <summary>
/// Akteure im Domänen-Editor (<c>docs/konzept-akteure.md</c>): ein Akteur ist ein Baustein mit EINER Kante — Akteur → was er darf
/// (<c>IDarf&lt;T&gt;</c>). Er ersetzt für diese Nachrichten die anonyme Außenwelt, liegt wie sie außerhalb jedes Moduls und wird als
/// <c>public sealed record X : IAkteur, IDarf&lt;…&gt;;</c> geschrieben.
/// </summary>
public sealed class DomainEditorAkteurTests
{
    private static Record Cmd(string name, string ns) =>
        new() { Name = name, Kind = RecordArt.Command, Namespace = ns, Felder = [new() { Name = "AggregateId", Typ = "Guid" }] };

    private static EditorModell Shop(params Akteur[] akteure) => new()
    {
        Records =
        [
            Cmd("Bestelle", "Shop.Bestellung"), Cmd("Storniere", "Shop.Bestellung"),
            new() { Name = "Bestellt", Kind = RecordArt.Event, Namespace = "Shop.Bestellung" },
            new() { Name = "Bestellungen", Kind = RecordArt.Query, Namespace = "Shop.Lesen" },
        ],
        Aggregate = [new() { Name = "Bestellung", Namespace = "Shop.Bestellung" }],
        Decider =
        [
            new() { Aggregat = "Bestellung", Command = "Bestelle", Ergibt = [new() { Event = "Bestellt" }] },
            new() { Aggregat = "Bestellung", Command = "Storniere", Ergibt = [] },
        ],
        Akteure = akteure,
        Rahmen = new Rahmen { Verzeichnisse = new Dictionary<string, string> { ["Shop"] = "Shop" } },
    };

    private static Akteur Kunde => new() { Name = "Kunde", Namespace = "Shop.Akteure", Darf = ["Bestelle", "Bestellungen"] };

    [Fact]
    public void Akteur_ist_Baustein_und_ersetzt_die_Aussenwelt_fuer_das_was_er_darf()
    {
        var fluss = Fluss.Aus(Shop(Kunde));

        fluss.KnotenVon("akt:Kunde")!.Art.Should().Be(Grammatik.Akteur);
        fluss.Aus("akt:Kunde").Select(k => k.Nachricht).Should().BeEquivalentTo(["Bestelle", "Bestellungen"]);
        // Was niemand darf, bleibt bei der anonymen Außenwelt (unbekannter Akteur).
        fluss.Aus(Fluss.AussenId).Select(k => k.Nachricht).Should().BeEquivalentTo(["Storniere"]);
    }

    [Fact]
    public void Grammatik_erlaubt_dem_Akteur_nur_Hineingehendes()
    {
        Grammatik.ErzeugungVon(Grammatik.Akteur, Grammatik.Command).Should().NotBeNull();
        Grammatik.ErzeugungVon(Grammatik.Akteur, Grammatik.Query).Should().NotBeNull();
        Grammatik.ErzeugungVon(Grammatik.Akteur, Grammatik.Trigger).Should().NotBeNull();
        Grammatik.ErzeugungVon(Grammatik.Akteur, Grammatik.Event).Should().BeNull("Fakten entstehen nur im Aggregat");

        var falsch = Shop(Kunde with { Darf = ["Bestellt"] });
        Validator.PruefeGrammatik(falsch).Should().Contain(b => b.Code == "GR-AKTEUR" && b.IstFehler);
    }

    [Fact]
    public void Ohne_Akteure_keine_Akteur_Warnung_mit_Akteuren_fuer_jede_Luecke()
    {
        Validator.PruefeGrammatik(Shop()).Should().NotContain(b => b.Code == "GR-AKTEUR-FEHLT", "ohne Akteure ist der Pfad offen — wie am Tor");

        var befunde = Validator.PruefeGrammatik(Shop(Kunde)).Where(b => b.Code == "GR-AKTEUR-FEHLT").ToList();
        befunde.Should().ContainSingle().Which.Meldung.Should().Contain("Storniere");
    }

    [Fact]
    public void Akteur_liegt_ausserhalb_jedes_Moduls_und_ist_benannter_Partner()
    {
        var module = Module.Ableiten(Shop(Kunde));

        module.Should().NotContain(m => m.Namespace == "Shop.Akteure", "Akteure sind die Außenwelt — mit Namen");
        var bestellung = module.Single(m => m.Namespace == "Shop.Bestellung");
        bestellung.Eingaenge.Single(p => p.Nachricht == "Bestelle").Partner.Should().Equal($"{Module.AkteurPraefix}Kunde");
        bestellung.Eingaenge.Single(p => p.Nachricht == "Storniere").Partner.Should().Equal(Module.Aussenwelt);
    }

    [Fact]
    public void Scaffolder_schreibt_den_Akteur_als_Record_mit_Basisliste()
    {
        var datei = Scaffolder.Generiere(Shop(Kunde)).Single(d => d.Pfad.EndsWith("Akteure.cs"));

        datei.Pfad.Should().Be("Shop/Akteure/Akteure.cs");
        datei.Art.Should().Be(DateiArt.Typen);
        datei.Inhalt.Should().Contain("namespace Shop.Akteure;")
            .And.Contain("using Shop.Bestellung;").And.Contain("using Shop.Lesen;")
            .And.Contain("public sealed record Kunde : IAkteur, IDarf<Bestelle>, IDarf<Bestellungen>;");
    }

    [Fact]
    public void Herkunft_merkt_Rechte_Aenderungen_und_der_Abgleich_erkennt_die_Akteur_Basen()
    {
        var gelesen = Herkunft.Stempeln(Shop(Kunde with { Datei = "Shop/Akteure/Akteure.cs" }));
        Herkunft.Geaenderte(gelesen).Should().BeEmpty();

        var entzogen = gelesen with { Akteure = [gelesen.Akteure[0] with { Darf = ["Bestelle"] }] };
        Herkunft.Geaenderte(entzogen).Should().Equal("akteur Shop.Akteure.Kunde");

        Scaffolder.IstAkteurBasis("IAkteur").Should().BeTrue();
        Scaffolder.IstAkteurBasis("IDarf<Shop.Bestellung.Bestelle>").Should().BeTrue();
        Scaffolder.IstAkteurBasis("IEquatable<Kunde>").Should().BeFalse("fremde Basen bleiben beim Abgleich stehen");
    }

    [Fact]
    public void Akteure_reisen_verlustfrei_durch_JSON()
    {
        var m = Shop(Kunde);
        EditorModell.AusJson(m.AlsJson()).Akteure.Should().BeEquivalentTo(m.Akteure);
    }

    // ── Dienste als Akteure (die KI): Vertrag mit IAkteur, Pipeline-Handle im Auftrag (Parameter) ──

    private static EditorModell MitKi(IReadOnlyList<string> sendet, IReadOnlyList<string> kiDarf) => Shop(new Akteur
    {
        Name = "IKi", Namespace = "Shop.Ki", Darf = kiDarf, Dienst = true,
    }) with
    {
        Lesen = new Leseseite
        {
            Pipelines = [new() { Name = "Bewerter", Namespace = "Shop.Ki", Handles =
                [new() { Eingang = "Bestellt", Ausgaenge = sendet, Faehigkeiten = [new() { Typ = "IKi", Name = "ki" }] }] }],
        },
    };

    [Fact]
    public void Handle_im_Auftrag_sendet_nur_was_der_Dienst_darf()
    {
        Validator.PruefeGrammatik(MitKi(["Bestelle"], ["Bestelle"])).Should().NotContain(b => b.Code == "GR-AUFTRAG");
        Validator.PruefeGrammatik(MitKi(["Storniere"], ["Bestelle"]))
            .Should().ContainSingle(b => b.Code == "GR-AUFTRAG").Which.Meldung.Should().Contain("Storniere").And.Contain("IKi");
    }

    [Fact]
    public void Dienst_Akteur_wird_als_Vertrag_geschrieben_ein_bestehender_bleibt_dem_Code()
    {
        var neu = Scaffolder.Generiere(MitKi(["Bestelle"], ["Bestelle"])).Single(d => d.Pfad.EndsWith("Akteure.cs") && d.Inhalt.Contains("IKi"));
        neu.Inhalt.Should().Contain("public interface IKi : IAkteur, IDarf<Bestelle> { }");

        var bestehend = MitKi(["Bestelle"], ["Bestelle"]) is var m ? m with { Akteure = [m.Akteure[0] with { Datei = "Shop/Ki/IKi.cs" }] } : null;
        Scaffolder.Generiere(bestehend!).Should().NotContain(d => d.Pfad == "Shop/Ki/IKi.cs",
            "ein bestehender Dienst-Vertrag trägt Methoden — nur seine Basisliste gleicht der Abgleich ab");
    }
}

