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
        Grammatik.ErzeugungVon(Grammatik.Akteur, Grammatik.Event).Should().BeNull("Fakten entstehen nur im Aggregat (Build: CQRS058)");
    }

    [Fact]
    public void Mit_Akteuren_bleibt_jede_Luecke_ohne_Akteur()
    {
        // GR-HERKUNFT: was weder ein IDarf noch eine Kette hat, steht im Rahmen „ohne Akteur“ (Sicht) und wird am Tor abgewiesen.
        var a = AkteurAnteile.Aus(Shop(Kunde));
        a.Eingaenge.Where(e => a.AkteureVon(e.Nachricht).Count == 0).Select(e => e.Nachricht).Should().Equal("Storniere");
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

        var mensch = Scaffolder.Generiere(Shop(Kunde with { Art = "Mensch" })).Single(d => d.Pfad.EndsWith("Akteure.cs"));
        mensch.Inhalt.Should().Contain("public sealed record Kunde : IMensch, IDarf<Bestelle>, IDarf<Bestellungen>;");
    }

    [Fact]
    public void Herkunft_merkt_Rechte_Aenderungen_und_der_Abgleich_erkennt_die_Akteur_Basen()
    {
        var gelesen = Herkunft.Stempeln(Shop(Kunde with { Datei = "Shop/Akteure/Akteure.cs" }));
        Herkunft.Geaenderte(gelesen).Should().BeEmpty();

        var entzogen = gelesen with { Akteure = [gelesen.Akteure[0] with { Darf = ["Bestelle"] }] };
        Herkunft.Geaenderte(entzogen).Should().Equal("akteur Shop.Akteure.Kunde");

        Scaffolder.IstAkteurBasis("IAkteur").Should().BeTrue();
        Scaffolder.IstAkteurBasis("IKi").Should().BeTrue("die Art ist Teil der Akteur-Basisliste");
        Herkunft.Geaenderte(gelesen with { Akteure = [gelesen.Akteure[0] with { Art = "Mensch" }] }).Should().Equal("akteur Shop.Akteure.Kunde");
        Scaffolder.IstAkteurBasis("IDarf<Shop.Bestellung.Bestelle>").Should().BeTrue();
        Scaffolder.IstAkteurBasis("IEquatable<Kunde>").Should().BeFalse("fremde Basen bleiben beim Abgleich stehen");
    }

    [Fact]
    public void Akteure_reisen_verlustfrei_durch_JSON()
    {
        var m = Shop(Kunde);
        EditorModell.AusJson(m.AlsJson()).Akteure.Should().BeEquivalentTo(m.Akteure);
    }

    // ── Der Dienst eines Akteurs (IAkteurDienst<A>): kein Akteur; ein Handle mit ihm als Parameter entscheidet im Auftrag von A ──

    private static EditorModell MitKi(IReadOnlyList<string> sendet, IReadOnlyList<string> kiDarf) => Shop(Kunde, new Akteur
    {
        Name = "Gutachter", Namespace = "Shop.Akteure", Art = "Ki", Darf = kiDarf, Dienste = ["IGutachten"],
    }) with
    {
        Lesen = new Leseseite
        {
            Pipelines = [new() { Name = "Bewerter", Namespace = "Shop.Ki", Handles =
                [new() { Eingang = "Bestellt", Ausgaenge = sendet, Faehigkeiten = [new() { Typ = "IGutachten", Name = "ki" }] }] }],
        },
    };

    [Fact]
    public void Ein_Dienst_wird_nie_als_Akteur_geschrieben()
        => Scaffolder.Generiere(MitKi(["Storniere"], ["Storniere"])).Should()
            .NotContain(d => d.Inhalt.Contains("interface IGutachten"), "der Dienst gehört dem Code; geschrieben wird nur der Akteur-Record");

    // ── Anteile: direkt (IDarf) und über die Kette; Akteur-Wechsel über den Dienst ──

    private static EditorModell Kette(bool mitDienst) => Shop(Kunde, new Akteur { Name = "Gutachter", Namespace = "Shop.Akteure", Art = "Ki", Dienste = ["IGutachten"] }) with
    {
        Lesen = new Leseseite
        {
            Pipelines = [new() { Name = "Folge", Namespace = "Shop.Bestellung", Handles =
                [new() { Eingang = "Bestellt", Ausgaenge = ["Storniere"], Faehigkeiten = mitDienst ? [new() { Typ = "IGutachten", Name = "g" }] : [] }] }],
        },
    };

    [Fact]
    public void Ketten_Command_traegt_den_Akteur_des_ausloesenden_Events()
    {
        var a = AkteurAnteile.Aus(Kette(mitDienst: false));
        a.Direkt["Bestelle"].Should().Equal("Kunde");
        a.Kette["Bestellt"].Should().Equal("Kunde");
        a.Kette["Storniere"].Should().Equal(new[] { "Kunde" }, "die Pipeline erzeugt ihn aus Bestellt → Kunde");
        a.Direkt.Should().NotContainKey("Storniere", "Ketten-Commands darf niemand direkt");
        a.Eingaenge.Should().OnlyContain(e => a.AkteureVon(e.Nachricht).Count > 0, "jeder Eingang kommt von einem Akteur (GR-HERKUNFT)");
    }

    [Fact]
    public void Der_Dienst_wechselt_den_Akteur_mitten_in_der_Kette()
        => AkteurAnteile.Aus(Kette(mitDienst: true)).Kette["Storniere"].Should().Equal("Gutachter");

    [Fact]
    public void Ein_Command_kann_von_zwei_Akteuren_kommen()
    {
        var m = Shop(Kunde, new Akteur { Name = "Haendler", Namespace = "Shop.Akteure", Art = "Mensch", Darf = ["Bestelle"] });
        AkteurAnteile.Aus(m).AkteureVon("Bestellt").Should().Equal("Haendler", "Kunde");
    }

    // ── Vertrag (docs/konzept-akteure.md §3): Event → Akteur → Command, die Ausgänge gehören dem Akteur ──

    private static Akteur Kasse => new()
    {
        Name = "Kasse", Namespace = "Shop.Akteure",
        Vertrag = [new() { Eingang = "Bestellt", Ausgaenge = ["Storniere"] }, new() { Eingang = "Bestellt2" }],
    };

    [Fact]
    public void Vertrag_ist_Zusage_Kante_mit_Akteur_Wechsel()
    {
        var m = Shop(Kunde, Kasse with { Vertrag = [Kasse.Vertrag[0]] });
        var fluss = Fluss.Aus(m);
        fluss.Ein("akt:Kasse").Should().ContainSingle(k => k.Nachricht == "Bestellt" && k.Handle == "Bestellt");
        fluss.Aus("akt:Kasse").Should().ContainSingle(k => k.Nachricht == "Storniere" && k.Handle == "Bestellt");
        fluss.Aus(Fluss.AussenId).Should().BeEmpty("Storniere hat jetzt einen Erzeuger");

        var anteile = AkteurAnteile.Aus(m);
        anteile.Kette["Storniere"].Should().Equal("Kasse");   // Wechsel: nicht der Kunde, dessen Bestelle das Event auslöste
        anteile.Direkt.ContainsKey("Storniere").Should().BeFalse("eine Zusage ist kein IDarf");
        anteile.Eingaenge.Should().OnlyContain(e => anteile.AkteureVon(e.Nachricht).Count > 0, "jeder Eingang kommt von einem Akteur (GR-HERKUNFT)");
    }

    // ── Clients (docs/konzept-akteure.md §4): ein Vertrag je Client, n:m zu Akteuren ──

    private static DomainEditor.Client Laden => new()
    {
        Name = "ILaden", Namespace = "Shop.Clients", Traegt = ["IKasse"], Sendet = ["Bestelle"], Fragt = ["Bestellungen"], Kenntnis = ["Bestellt2"],
    };

    private static EditorModell MitClients(params DomainEditor.Client[] clients) =>
        Shop(Kunde, Kasse with { Vertrag = [Kasse.Vertrag[0]] }) with { Clients = clients };

    [Fact]
    public void Scaffolder_schreibt_den_Client_als_Interface_mit_seinem_Rand()
    {
        var text = Scaffolder.ClientInterface(Laden);
        text.Should().Contain("public interface ILaden : IClientVertrag,")
            .And.Contain("ITraegt<IKasse>").And.Contain("ISendet<Bestelle>").And.Contain("IFragt<Bestellungen>")
            .And.Contain("IHoert<Bestellt2>").And.NotContain("void Auf");
        Scaffolder.IstClientBasis("ISendet<Bestelle>", new HashSet<string>()).Should().BeTrue();
        Scaffolder.IstClientBasis("ITraegt<IKasse>", new HashSet<string>()).Should().BeTrue();
        Scaffolder.IstClientBasis("IHoert<Bestellt2>", new HashSet<string>()).Should().BeTrue();
        Scaffolder.IstClientBasis("IDisposable", new HashSet<string> { "IKasse" }).Should().BeFalse("fremde Basistypen gehören dem Code");
        var dateien = Scaffolder.Generiere(MitClients(Laden));
        dateien.Should().Contain(d => d.Inhalt.Contains("public interface ILaden : IClientVertrag"));
    }

    [Fact]
    public void Herkunfts_Stempel_erkennt_einen_im_Editor_geaenderten_Client()
    {
        var m = Herkunft.Stempeln(MitClients(Laden));
        Herkunft.Geaenderte(m).Should().BeEmpty();
        var geaendert = m with { Clients = [m.Clients[0] with { Sendet = [] }] };
        Herkunft.Geaenderte(geaendert).Should().Contain("client Shop.Clients.ILaden");
    }
}
