using DomainEditor;
using FluentAssertions;
using Record = DomainEditor.Record;

namespace Infrastructure.Pruefstand.Tests;

/// <summary>
/// Die Kompositions-Sprache des Domänen-Editors (<c>docs/konzept-editor-komposition.md</c>): Phase 1 Grammatik als EINE Quelle
/// (<see cref="Grammatik"/> → <see cref="Validator"/> nennt die verletzte Regel) und Phase 2 Kapselung (<see cref="Module"/>:
/// Namespace = Modul, Ports abgeleitet aus dem Nachrichtenfluss, offene Ports beim Entwurf von oben nach unten).
/// </summary>
public sealed class DomainEditorKompositionTests
{
    private static Record Cmd(string name, string ns, params Feld[] mehr) =>
        new() { Name = name, Kind = RecordArt.Command, Namespace = ns, Felder = [new() { Name = "AggregateId", Typ = "Guid" }, .. mehr] };
    private static Record Evt(string name, string ns) => new() { Name = name, Kind = RecordArt.Event, Namespace = ns };

    /// <summary>Zwei Geschwister-Module unter „Shop": Bestellung (Aggregat) → Bestellt → Versand (Reaktion → Command → Aggregat).</summary>
    private static EditorModell Shop() => new()
    {
        Records =
        [
            Cmd("Bestelle", "Shop.Bestellung"), Evt("Bestellt", "Shop.Bestellung"),
            Cmd("Versende", "Shop.Versand"), Evt("Versendet", "Shop.Versand"),
            new() { Name = "Adresse", Kind = RecordArt.ValueObject, Namespace = "Shop.Versand.Typen" },
        ],
        Aggregate = [new() { Name = "Bestellung", Namespace = "Shop.Bestellung" }, new() { Name = "Versand", Namespace = "Shop.Versand" }],
        Decider =
        [
            new() { Aggregat = "Bestellung", Command = "Bestelle", Ergibt = [new() { Event = "Bestellt" }] },
            new() { Aggregat = "Versand", Command = "Versende", Ergibt = [new() { Event = "Versendet" }] },
        ],
        Applier = [new() { Aggregat = "Bestellung", Event = "Bestellt" }, new() { Aggregat = "Versand", Event = "Versendet" }],
        Lesen = new Leseseite
        {
            Konsumenten = [new() { Name = "VersandStarter", Namespace = "Shop.Versand", Handles = [new() { Eingang = "Bestellt", Ausgaenge = ["Versende"] }] }],
        },
    };

    private static IEnumerable<string> Grammatikcodes(EditorModell m) => Validator.PruefeGrammatik(m).Select(b => b.Code);

    [Fact]
    public void Grammatik_ist_in_sich_geschlossen_und_jede_Regel_hat_ein_Build_Gegenstueck()
    {
        var ids = Grammatik.Regeln.Select(r => r.Id).ToList();
        ids.Should().OnlyHaveUniqueItems();
        Grammatik.Konsume.Select(k => k.Regel).Concat(Grammatik.Erzeugungen.Select(e => e.Regel)).Should().OnlyContain(r => ids.Contains(r));
        Grammatik.Regeln.Should().OnlyContain(r => r.Build.Count > 0, "jede Regel nennt ihr Gegenstück im Build — und sei es „offen“");
        var sorten = Grammatik.Sorten.Select(s => s.Id).ToHashSet();
        var bausteine = Grammatik.Bausteine.Select(b => b.Id).ToHashSet();
        Grammatik.Konsume.Should().OnlyContain(k => sorten.Contains(k.Sorte) && bausteine.Contains(k.Baustein));
        Grammatik.Erzeugungen.Should().OnlyContain(e => sorten.Contains(e.Sorte) && bausteine.Contains(e.Baustein));
        // Die Tabelle aus §3: Command genau ein Aggregat, Trigger genau eine Pipeline, Query genau ein Reader, Event beliebig viele.
        Grammatik.KonsumVon(Grammatik.Command, Grammatik.Aggregat)!.Kardinalitaet.Should().Be(Grammatik.GenauEins);
        Grammatik.KonsumVon(Grammatik.Trigger, Grammatik.Pipeline)!.Kardinalitaet.Should().Be(Grammatik.GenauEins);
        Grammatik.KonsumVon(Grammatik.Query, Grammatik.Reader)!.Kardinalitaet.Should().Be(Grammatik.GenauEins);
        Grammatik.KonsumVon(Grammatik.Event, Grammatik.Projektion)!.Kardinalitaet.Should().Be(Grammatik.Beliebig);
        Grammatik.KonsumVon(Grammatik.Command, Grammatik.Pipeline).Should().BeNull();
        Grammatik.ErzeugungVon(Grammatik.Pipeline, Grammatik.Event).Should().BeNull("Fakten entstehen nur im Aggregat");
        // Fürs Board: die Editor-Port-Abbildung reist mit (das JS kodiert nichts hart).
        var json = System.Text.Json.JsonSerializer.Serialize(Grammatik.AlsJson(), EditorModell.JsonOptionen);
        json.Should().Contain("\"portBaustein\"").And.Contain("\"recordSorte\"").And.Contain("CQRS010");
    }

    [Fact]
    public void Gueltiges_Modell_verletzt_keine_Regel()
    {
        Validator.PruefeGrammatik(Shop()).Where(b => b.IstFehler).Should().BeEmpty();
        Validator.Prüfe(Shop()).Where(b => b.IstFehler).Should().BeEmpty();
    }

    [Fact]
    public void Validator_nennt_die_verletzte_Regel_beim_Namen()
    {
        // Ein zweites Aggregat entscheidet denselben Command → Kardinalität verletzt (Build: CQRS010).
        var m = Shop();
        m = m with
        {
            Aggregate = [.. m.Aggregate, new() { Name = "Doppelt", Namespace = "Shop.Bestellung" }],
            Decider = [.. m.Decider, new() { Aggregat = "Doppelt", Command = "Bestelle", Ergibt = [new() { Event = "Bestellt" }] }],
        };
        var b = Validator.PruefeGrammatik(m).Single(x => x.Code == "GR-COMMAND");
        b.IstFehler.Should().BeTrue();
        b.Meldung.Should().Contain("Command → genau ein Aggregat").And.Contain("CQRS010").And.Contain("Doppelt");
    }

    [Fact]
    public void Pipeline_Zusatzregeln_Selbst_Event_Garantie_Frist_Zustand()
    {
        const string ns = "Werk";
        var m = new EditorModell
        {
            Records =
            [
                Cmd("Starte", ns), Cmd("Timeout", ns, new Feld { Name = "Grund", Typ = "string" }), Evt("Gestartet", ns),
                new() { Name = "Anstoss", Kind = RecordArt.Trigger, Namespace = ns },
                new() { Name = "Tick", Kind = RecordArt.Selbst, Namespace = ns, OhneParameterliste = true },
            ],
            Aggregate = [new() { Name = "Werk", Namespace = ns }],
            Decider =
            [
                new() { Aggregat = "Werk", Command = "Starte", Ergibt = [new() { Event = "Gestartet" }] },
                new() { Aggregat = "Werk", Command = "Timeout", Ergibt = [new() { Event = "Gestartet" }] },
            ],
            Lesen = new Leseseite
            {
                Pipelines =
                [
                    new()
                    {
                        Name = "WerkPipeline", Namespace = ns, Zustand = ["_seen"],
                        Handles =
                        [
                            new() { Eingang = "Anstoss", Ausgaenge = ["Starte"] },                                     // Garantie: ab Trigger
                            new() { Eingang = "Gestartet", Ausgaenge = ["Selbst<Tick>", "Frist<Timeout>", "Gestartet"] }, // Selbst aus Event, Frist ohne (Guid), Event
                            new() { Eingang = "Tick" },
                        ],
                    },
                ],
            },
            Ingress = [new() { Trigger = "Anstoss", Modus = "webhook", Ort = "/anstoss" }],
        };
        var codes = Grammatikcodes(m).ToList();
        codes.Should().Contain(["GR-GARANTIE", "GR-SELBST-OHNE-EVENT", "GR-FRIST-CTOR", "GR-KEIN-EVENT-AUS-PIPELINE", "GR-ZUSTAND"]);
        Validator.PruefeGrammatik(m).Single(b => b.Code == "GR-ZUSTAND").Schweregrad.Should().Be("info", "Regel Z ist in dieser Runde nur ein Hinweis");
    }

    [Fact]
    public void Trigger_und_Query_haben_genau_einen_Konsumenten_und_Kreise_brauchen_ein_Aggregat()
    {
        const string ns = "Kette";
        var m = new EditorModell
        {
            Records =
            [
                new() { Name = "T1", Kind = RecordArt.Trigger, Namespace = ns }, new() { Name = "T2", Kind = RecordArt.Trigger, Namespace = ns },
                new() { Name = "Frage", Kind = RecordArt.Query, Namespace = ns }, new() { Name = "Antwort", Kind = RecordArt.Antwort, Namespace = ns },
            ],
            Lesen = new Leseseite
            {
                Reader =
                [
                    new() { Name = "R1", Namespace = ns, Projektion = "P", Handles = [new() { Eingang = "Frage", Ausgaenge = ["Antwort"] }] },
                    new() { Name = "R2", Namespace = ns, Projektion = "P", Handles = [new() { Eingang = "Frage", Ausgaenge = ["Antwort"] }] },
                ],
                Pipelines =
                [
                    new() { Name = "A", Namespace = ns, Handles = [new() { Eingang = "T1", Ausgaenge = ["T2"] }] },
                    new() { Name = "B", Namespace = ns, Handles = [new() { Eingang = "T2", Ausgaenge = ["T1"] }] },
                    new() { Name = "C", Namespace = ns, Handles = [new() { Eingang = "T2" }] },
                ],
            },
        };
        var codes = Grammatikcodes(m).ToList();
        codes.Should().Contain(["GR-QUERY", "GR-TRIGGER", "GR-ZYKLUS"]);
        Validator.PruefeGrammatik(m).Single(b => b.Code == "GR-ZYKLUS").Meldung.Should().Contain("A").And.Contain("B");
    }

    [Fact]
    public void Module_sind_Namespaces_mit_abgeleiteten_Ports()
    {
        var module = Module.Ableiten(Shop()).ToDictionary(x => x.Namespace);
        module.Keys.Should().BeEquivalentTo(["Shop", "Shop.Bestellung", "Shop.Versand", "Shop.Versand.Typen"]);
        module["Shop"].Kinder.Should().BeEquivalentTo(["Shop.Bestellung", "Shop.Versand"]);
        module["Shop.Versand"].Kinder.Should().BeEquivalentTo(["Shop.Versand.Typen"]);
        module["Shop.Versand.Typen"].Inhalt.Should().ContainKey(RecordArt.ValueObject);

        // Bestellung: Command von außen (Client) hinein, Event an Versand hinaus.
        var b = module["Shop.Bestellung"];
        b.Eingaenge.Should().ContainSingle(p => p.Sorte == Grammatik.Command && p.Nachricht == "Bestelle" && p.Partner.Contains(Module.Aussenwelt) && !p.Offen);
        b.Ausgaenge.Should().ContainSingle(p => p.Sorte == Grammatik.Event && p.Nachricht == "Bestellt" && p.Partner.Contains("Shop.Versand"));
        // Versand: das Event hinein; sein eigener Command läuft intern (Reaktion → Aggregat im selben Modul) → kein Port.
        var v = module["Shop.Versand"];
        v.Eingaenge.Select(p => p.Nachricht).Should().BeEquivalentTo(["Bestellt"]);
        v.Ausgaenge.Should().BeEmpty();
        // Die Eltern-Ebene sieht nur, was über IHRE Grenze läuft: Bestellt bleibt innen (Geschwister-Kante).
        var shop = module["Shop"];
        shop.Eingaenge.Select(p => p.Nachricht).Should().BeEquivalentTo(["Bestelle"]);
        shop.Ausgaenge.Should().BeEmpty();
    }

    [Fact]
    public void Entwurf_von_oben_nach_unten_zeigt_offene_Ports()
    {
        // Ein neues Modul, nur skizziert: „nimmt Auftrag an“ (Command ohne Decider), „meldet erledigt“ (Event ohne Erzeuger).
        var m = Shop();
        m = m with { Records = [.. m.Records, Cmd("NimmAuftragAn", "Shop.Mahnung"), Evt("AuftragErledigt", "Shop.Mahnung")] };
        var mahnung = Module.Ableiten(m).Single(x => x.Namespace == "Shop.Mahnung");
        mahnung.Eingaenge.Should().ContainSingle(p => p.Nachricht == "NimmAuftragAn" && p.Offen);
        mahnung.Ausgaenge.Should().ContainSingle(p => p.Nachricht == "AuftragErledigt" && p.Offen);
        var befunde = Validator.PruefeGrammatik(m).ToList();
        befunde.Should().ContainSingle(x => x.Code == "GR-MODUL-EINGANG-OFFEN" && x.Meldung.Contains("NimmAuftragAn") && x.Meldung.Contains("Shop.Mahnung"));
        befunde.Should().ContainSingle(x => x.Code == "GR-MODUL-AUSGANG-OFFEN" && x.Meldung.Contains("AuftragErledigt"));

        // Füllen: ein Aggregat im Modul entscheidet den Command und erzeugt das Event → beide Ports geschlossen.
        m = m with
        {
            Aggregate = [.. m.Aggregate, new() { Name = "Mahnvorgang", Namespace = "Shop.Mahnung" }],
            Decider = [.. m.Decider, new() { Aggregat = "Mahnvorgang", Command = "NimmAuftragAn", Ergibt = [new() { Event = "AuftragErledigt" }] }],
        };
        mahnung = Module.Ableiten(m).Single(x => x.Namespace == "Shop.Mahnung");
        mahnung.Eingaenge.Concat(mahnung.Ausgaenge).Should().OnlyContain(p => !p.Offen);
        Validator.PruefeGrammatik(m).Should().NotContain(x => x.Code.StartsWith("GR-MODUL-"));
    }

    [Fact]
    public void Faehigkeit_ueber_Modulgrenze_ist_ein_Port_und_Frist_fliesst_als_Frist()
    {
        var m = Shop();
        m = m with
        {
            Records = [.. m.Records, Evt("Ueberfaellig", "Shop.Mahnung")],
            Lesen = m.Lesen! with
            {
                Stores = [new() { Name = "IBuch", Namespace = "Shop.Lesen", Fns = [new() { Name = "IFinde", Methode = "FindeAsync", Lesen = true }] }],
                Pipelines = [new() { Name = "Mahner", Namespace = "Shop.Mahnung", Handles = [new() { Eingang = "Bestellt", Ausgaenge = ["Frist<Versende>"], Faehigkeiten = [new() { Typ = "IFinde", Name = "finde" }] }] }],
            },
        };
        var mo = Module.Ableiten(m).ToDictionary(x => x.Namespace);
        mo["Shop.Lesen"].Ausgaenge.Should().ContainSingle(p => p.Sorte == Grammatik.Faehigkeit && p.Nachricht == "IFinde" && p.Partner.Contains("Shop.Mahnung"));
        mo["Shop.Mahnung"].Eingaenge.Should().Contain(p => p.Sorte == Grammatik.Faehigkeit && p.Nachricht == "IFinde");
        mo["Shop.Mahnung"].Ausgaenge.Should().Contain(p => p.Sorte == Grammatik.Frist && p.Nachricht == "Versende" && p.Partner.Contains("Shop.Versand"));
        Validator.PruefeGrammatik(m).Should().Contain(b => b.Code == "GR-FRIST-CTOR" || b.Code == "GR-MODUL-AUSGANG-OFFEN");
        Validator.PruefeGrammatik(m).Where(b => b.IstFehler).Should().BeEmpty();
    }
}
