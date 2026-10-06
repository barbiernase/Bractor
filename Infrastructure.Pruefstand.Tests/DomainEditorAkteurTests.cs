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
    public void Ohne_Akteure_keine_Herkunfts_Warnung_mit_Akteuren_fuer_jede_Luecke()
    {
        Validator.PruefeGrammatik(Shop()).Should().NotContain(b => b.Code == "GR-HERKUNFT", "ohne Akteure ist der Pfad offen — wie am Tor");

        var befunde = Validator.PruefeGrammatik(Shop(Kunde)).Where(b => b.Code == "GR-HERKUNFT").ToList();
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
    public void Handle_im_Auftrag_sendet_nur_was_der_Akteur_darf()
    {
        Validator.PruefeGrammatik(MitKi(["Storniere"], ["Storniere"])).Should().NotContain(b => b.Code == "GR-AUFTRAG");
        Validator.PruefeGrammatik(MitKi(["Storniere"], ["Bestelle"]))
            .Should().ContainSingle(b => b.Code == "GR-AUFTRAG").Which.Meldung.Should().Contain("Storniere").And.Contain("Gutachter");
    }

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
        Validator.PruefeGrammatik(Kette(false)).Should().NotContain(b => b.Code == "GR-HERKUNFT");
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
        Validator.PruefeGrammatik(m).Should().NotContain(b => b.Code == "GR-HERKUNFT");
    }

    [Fact]
    public void Vertrag_auf_Nicht_Event_oder_mit_Nicht_Command_ist_ein_Grammatik_Fehler()
    {
        var befunde = Validator.PruefeGrammatik(Shop(Kunde, Kasse with
        {
            Vertrag = [new() { Eingang = "Bestelle" }, new() { Eingang = "Bestellt", Ausgaenge = ["Bestellt"] }, new() { Eingang = "Bestellt" }],
        })).Where(b => b.Code == "GR-VERTRAG").Select(b => b.Meldung).ToList();
        befunde.Should().Contain(x => x.Contains("Auf(Bestelle)")).And.Contain(x => x.Contains("gibt Bestellt aus")).And.Contain(x => x.Contains("zwei Zusagen"));
    }

    [Fact]
    public void Scaffolder_schreibt_den_Vertrag_neben_den_Akteur()
    {
        var a = Kasse with
        {
            Vertrag = [new() { Eingang = "Bestellt", Ausgaenge = ["Storniere", "Bestelle"], Strom = true }, new() { Eingang = "Bestellt" }],
        };
        a.VertragTyp.Should().Be("IKasse");
        var text = Scaffolder.VertragsInterface(a);
        text.Should().Contain("public interface IKasse : IAkteurVertrag<Kasse>")
            .And.Contain("IAsyncEnumerable<OneOf<Storniere, Bestelle>> Auf(Bestellt e);")
            .And.Contain("void Auf(Bestellt e);");
        Scaffolder.VertragsRueckgabe(new() { Eingang = "X", Ausgaenge = ["Y"] }).Should().Be("OneOf<Y>");
        (a with { VertragName = "IRegal" }).VertragTyp.Should().Be("IRegal");
    }

    // ── Clients (docs/konzept-akteure.md §4): ein Vertrag je Client, n:m zu Akteuren ──

    private static Client Laden => new()
    {
        Name = "ILaden", Namespace = "Shop.Clients", Traegt = ["IKasse"], Sendet = ["Bestelle"], Fragt = ["Bestellungen"], Kenntnis = ["Bestellt2"],
    };

    private static EditorModell MitClients(params Client[] clients) =>
        Shop(Kunde, Kasse with { Vertrag = [Kasse.Vertrag[0]] }) with { Clients = clients };

    [Fact]
    public void Ein_gueltiger_Client_traegt_einen_Akteur_Vertrag_und_sendet_was_ein_Akteur_darf()
    {
        var m = MitClients(Laden with { Kenntnis = [] });
        Validator.PruefeGrammatik(m).Should().NotContain(b => b.Code.StartsWith("GR-CLIENT") || b.Code == "GR-GETRAGEN");
    }

    [Fact]
    public void Client_sendet_nur_was_ein_Akteur_darf_und_fragt_nur_Queries()
    {
        var befunde = Validator.PruefeGrammatik(MitClients(Laden with { Sendet = ["Storniere"], Fragt = ["Bestelle"], Kenntnis = [] }))
            .Where(b => b.Code == "GR-CLIENT-BEFUGT").Select(b => b.Meldung).ToList();
        befunde.Should().Contain(x => x.Contains("sendet Storniere") && x.Contains("kein Akteur"))
            .And.Contain(x => x.Contains("fragt Bestelle"));
    }

    [Fact]
    public void Client_traegt_nur_Akteur_Vertraege_und_hoert_jedes_Event_einmal()
    {
        var befunde = Validator.PruefeGrammatik(MitClients(Laden with { Traegt = ["IKasse", "IGibtsNicht"], Kenntnis = ["Bestellt", "Bestelle"] }))
            .Where(b => b.Code == "GR-CLIENT").Select(b => b.Meldung).ToList();
        befunde.Should().Contain(x => x.Contains("IGibtsNicht")).And.Contain(x => x.Contains("Bestellt kommt schon aus IKasse"))
            .And.Contain(x => x.Contains("Auf(Bestelle)"));
    }

    [Fact]
    public void Eine_Zusage_ohne_Client_bricht_die_Kette_sobald_es_Clients_gibt()
    {
        Validator.PruefeGrammatik(MitClients()).Should().NotContain(b => b.Code == "GR-GETRAGEN", "ohne Clients ist es ein Entwurf ohne Software-Sicht");
        var andere = new Client { Name = "IAnzeige", Namespace = "Shop.Clients", Kenntnis = ["Bestellt"] };
        Validator.PruefeGrammatik(MitClients(andere)).Should().ContainSingle(b => b.Code == "GR-GETRAGEN")
            .Which.Meldung.Should().Contain("IKasse.Auf(Bestellt) → Storniere");
        Validator.PruefeGrammatik(MitClients(andere, Laden with { Kenntnis = [] })).Should().NotContain(b => b.Code == "GR-GETRAGEN");
    }

    [Fact]
    public void Scaffolder_schreibt_den_Client_als_Interface_mit_seinem_Rand()
    {
        var text = Scaffolder.ClientInterface(Laden);
        text.Should().Contain("public interface ILaden : IClientVertrag,")
            .And.Contain("IKasse").And.Contain("ISendet<Bestelle>").And.Contain("IFragt<Bestellungen>")
            .And.Contain("void Auf(Bestellt2 e);");
        Scaffolder.IstClientBasis("ISendet<Bestelle>", new HashSet<string>()).Should().BeTrue();
        Scaffolder.IstClientBasis("IKasse", new HashSet<string> { "IKasse" }).Should().BeTrue();
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
