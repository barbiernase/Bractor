using DomainEditor;
using FluentAssertions;

namespace Infrastructure.Pruefstand.Tests;

/// <summary>
/// Dogfood für den Domänen-Editor (Stufe 1): das Modell → C# in der kanonischen Gestalt. Store-frei,
/// rein textuell — genau das, was der Prüfstand testen darf. Beweist die FORM (Records, partial-
/// Klassen, OneOf-Signaturen, Saga-DSL), nicht die Logik (die ist Stufe 3). Determinismus ist Teil
/// des Vertrags: gleiches Modell → byte-gleicher Output.
/// </summary>
public sealed class DomainEditorScaffolderTests
{
    // ── Das Konto-Modell (kanonisch), als Fixture ──────────────────────────────────────────────
    private static Aggregat KontoAggregat() => new()
    {
        Name = "Konto",
        Namespace = "Domain.Konto",
        Doku = "Ziel-Aggregat: Saldo, reservierter Teilbetrag, Gesperrt-Schalter.",
        Felder =
        [
            new() { Name = "Saldo", Typ = "decimal" },
            new() { Name = "Reserviert", Typ = "decimal" },
            new() { Name = "Gesperrt", Typ = "bool" },
            new() { Name = "Verfuegbar", Typ = "decimal", Ausdruck = "Saldo - Reserviert" },
        ],
        Commands =
        [
            new()
            {
                Name = "EroeffneKonto",
                Felder = [ new() { Name = "AggregateId", Typ = "Guid" }, new() { Name = "StartSaldo", Typ = "decimal" }, new() { Name = "Gesperrt", Typ = "bool", Standard = "false" } ],
                Ergibt = [ new() { Event = "KontoEroeffnet" }, new() { Event = "KontoExistiertBereits", Guard = "State.Version > 0" } ],
            },
            new()
            {
                Name = "ReserviereBetrag",
                Felder = [ new() { Name = "AggregateId", Typ = "Guid" }, new() { Name = "Betrag", Typ = "decimal" } ],
                Ergibt = [ new() { Event = "BetragReserviert" }, new() { Event = "KontoGesperrt" }, new() { Event = "DeckungReichtNicht", Guard = "State.Verfuegbar < cmd.Betrag" }, new() { Event = "KontoNichtGefunden" } ],
            },
        ],
        Events =
        [
            new() { Name = "KontoEroeffnet", Felder = [ new() { Name = "StartSaldo", Typ = "decimal" }, new() { Name = "Gesperrt", Typ = "bool" } ] },
            new() { Name = "BetragReserviert", Felder = [ new() { Name = "Betrag", Typ = "decimal" } ] },
            new() { Name = "KontoGesperrt", Felder = [ new() { Name = "AggregateId", Typ = "Guid" } ], Transient = true },
            new() { Name = "DeckungReichtNicht", Felder = [ new() { Name = "Verfuegbar", Typ = "decimal" }, new() { Name = "Angefordert", Typ = "decimal" } ], Transient = true },
            new() { Name = "KontoNichtGefunden", Felder = [ new() { Name = "AggregateId", Typ = "Guid" } ], Transient = true },
            new() { Name = "KontoExistiertBereits", Felder = [ new() { Name = "AggregateId", Typ = "Guid" } ], Transient = true },
        ],
    };

    private static string Inhalt(IReadOnlyList<GenerierteDatei> dateien, string pfad) =>
        dateien.Single(d => d.Pfad == pfad).Inhalt.Replace("\r\n", "\n").TrimEnd('\n');

    [Fact]
    public void Erzeugt_die_fuenf_kanonischen_Aggregat_Dateien()
    {
        var dateien = Scaffolder.Generiere(new EditorModell { Aggregate = [KontoAggregat()] });

        dateien.Select(d => d.Pfad).Should().BeEquivalentTo(
            "Konto/Konto.cs", "Konto/Commands.cs", "Konto/Events.cs", "Konto/Decider.cs", "Konto/Applier.cs");
    }

    [Fact]
    public void State_Datei_matcht_die_kanonische_Form()
    {
        var dateien = Scaffolder.Generiere(new EditorModell { Aggregate = [KontoAggregat()] });

        Inhalt(dateien, "Konto/Konto.cs").Should().Be(
            """
            using Abstractions;

            namespace Domain.Konto;

            /// <summary>
            /// Ziel-Aggregat: Saldo, reservierter Teilbetrag, Gesperrt-Schalter.
            /// </summary>
            public partial class Konto : IState
            {
                public decimal Saldo { get; set; }
                public decimal Reserviert { get; set; }
                public bool Gesperrt { get; set; }

                public decimal Verfuegbar => Saldo - Reserviert;
            }
            """);
    }

    [Fact]
    public void Commands_Datei_matcht_die_kanonische_Form()
    {
        var dateien = Scaffolder.Generiere(new EditorModell { Aggregate = [KontoAggregat()] });

        Inhalt(dateien, "Konto/Commands.cs").Should().Be(
            """
            using Abstractions;

            namespace Domain.Konto;

            public record EroeffneKonto(Guid AggregateId, decimal StartSaldo, bool Gesperrt = false) : ICommand;
            public record ReserviereBetrag(Guid AggregateId, decimal Betrag) : ICommand;
            """);
    }

    [Fact]
    public void Events_Datei_trennt_persistent_und_transient()
    {
        var dateien = Scaffolder.Generiere(new EditorModell { Aggregate = [KontoAggregat()] });

        Inhalt(dateien, "Konto/Events.cs").Should().Be(
            """
            using Abstractions;

            namespace Domain.Konto;

            public record KontoEroeffnet(decimal StartSaldo, bool Gesperrt) : IEvent;
            public record BetragReserviert(decimal Betrag) : IEvent;

            public record KontoGesperrt(Guid AggregateId) : ITransientEvent;
            public record DeckungReichtNicht(decimal Verfuegbar, decimal Angefordert) : ITransientEvent;
            public record KontoNichtGefunden(Guid AggregateId) : ITransientEvent;
            public record KontoExistiertBereits(Guid AggregateId) : ITransientEvent;
            """);
    }

    [Fact]
    public void Decider_traegt_die_OneOf_Signaturen_und_Platzhalter()
    {
        var dateien = Scaffolder.Generiere(new EditorModell { Aggregate = [KontoAggregat()] });
        var decider = Inhalt(dateien, "Konto/Decider.cs");

        decider.Should().Contain("public partial class Decider : IDecider<Konto>");
        decider.Should().Contain("public IEnumerable<OneOf<KontoEroeffnet, KontoExistiertBereits>> Decide(EroeffneKonto cmd)");
        decider.Should().Contain("public IEnumerable<OneOf<BetragReserviert, KontoGesperrt, DeckungReichtNicht, KontoNichtGefunden>> Decide(ReserviereBetrag cmd)");
        decider.Should().Contain("throw new NotImplementedException(\"TODO: Entscheidungslogik (Stufe 3).\");");
    }

    [Fact]
    public void Applier_hat_ein_Apply_je_persistentem_Event_und_keins_fuer_Ablehnungen()
    {
        var dateien = Scaffolder.Generiere(new EditorModell { Aggregate = [KontoAggregat()] });
        var applier = Inhalt(dateien, "Konto/Applier.cs");

        applier.Should().Contain("public void Apply(KontoEroeffnet evt)");
        applier.Should().Contain("public void Apply(BetragReserviert evt)");
        // Ablehnungen (ITransientEvent) haben KEIN Apply.
        applier.Should().NotContain("Apply(KontoGesperrt");
        applier.Should().NotContain("Apply(DeckungReichtNicht");
    }

    [Fact]
    public void Vorhandener_Rumpf_ersetzt_den_Platzhalter()
    {
        var agg = KontoAggregat() with
        {
            Commands =
            [
                new()
                {
                    Name = "GebeReservierungFrei",
                    Felder = [ new() { Name = "AggregateId", Typ = "Guid" }, new() { Name = "Betrag", Typ = "decimal" } ],
                    Ergibt = [ new() { Event = "ReservierungFreigegeben" } ],
                    Rumpf = "yield return new ReservierungFreigegeben(cmd.Betrag);",
                },
            ],
        };

        var decider = Inhalt(Scaffolder.Generiere(new EditorModell { Aggregate = [agg] }), "Konto/Decider.cs");
        decider.Should().Contain("            yield return new ReservierungFreigegeben(cmd.Betrag);");
        decider.Should().NotContain("NotImplementedException");
    }

    [Fact]
    public void Ist_deterministisch()
    {
        var modell = new EditorModell { Aggregate = [KontoAggregat()] };
        var a = Scaffolder.Generiere(modell);
        var b = Scaffolder.Generiere(modell);

        a.Should().BeEquivalentTo(b, o => o.WithStrictOrdering());
    }

    [Fact]
    public void JSON_Roundtrip_erhaelt_das_Modell()
    {
        var modell = new EditorModell { Aggregate = [KontoAggregat()] };
        var wieder = EditorModell.AusJson(modell.AlsJson());

        Scaffolder.Generiere(wieder).Should().BeEquivalentTo(Scaffolder.Generiere(modell), o => o.WithStrictOrdering());
    }

    // ── Saga / Prozess (Willkommensbonus-Form: Join + Kompensation, aggregat-fremde Commands) ──
    [Fact]
    public void Saga_erzeugt_die_Prozess_DSL_mit_Join_und_Kompensation()
    {
        var saga = new Saga
        {
            Name = "WillkommensbonusProzess",
            Namespace = "Domain.Willkommensbonus",
            TriggerEvent = "WillkommensbonusBeauftragt",
            ExtraUsings = ["Domain.Konto"],
            Schritte =
            [
                new()
                {
                    Wenn = ["WillkommensbonusBeauftragt"],
                    Sende = "ReserviereBetrag", SendeArgumente = ["t.PromoKonto", "t.Betrag"],
                    Kompensation = "GebeReservierungFrei", KompensationArgumente = ["t.PromoKonto", "t.Betrag"],
                },
                new()
                {
                    Wenn = ["WillkommensbonusBeauftragt", "BetragReserviert"],
                    Sende = "SchreibeGut", SendeArgumente = ["t.NeuesKonto", "t.Betrag"],
                    Kompensation = "StorniereGutschrift", KompensationArgumente = ["t.NeuesKonto", "t.Betrag"],
                },
                new()
                {
                    Wenn = ["WillkommensbonusBeauftragt", "BetragReserviert", "Gutgeschrieben"],
                    Sende = "BucheReservierung", SendeArgumente = ["t.PromoKonto", "t.Betrag"],
                },
            ],
        };

        var datei = Inhalt(Scaffolder.Generiere(new EditorModell { Sagas = [saga] }), "Willkommensbonus/WillkommensbonusProzess.cs");

        datei.Should().Be(
            """
            using Abstractions;
            using Domain.Konto;

            namespace Domain.Willkommensbonus;

            public sealed class WillkommensbonusProzess : IProzessDefinition
            {
                public ProzessRegeln Regeln => Prozess<WillkommensbonusBeauftragt>.Definiere(p =>
                {
                    p.Auf<WillkommensbonusBeauftragt>()
                        .Sende<ReserviereBetrag>(t => new ReserviereBetrag(t.PromoKonto, t.Betrag))
                        .RückgängigDurch<GebeReservierungFrei>(t => new GebeReservierungFrei(t.PromoKonto, t.Betrag));

                    p.Auf<WillkommensbonusBeauftragt>().Und<BetragReserviert>()
                        .Sende<SchreibeGut>((t, r) => new SchreibeGut(t.NeuesKonto, t.Betrag))
                        .RückgängigDurch<StorniereGutschrift>((t, r) => new StorniereGutschrift(t.NeuesKonto, t.Betrag));

                    p.Auf<WillkommensbonusBeauftragt>().Und<BetragReserviert>().Und<Gutgeschrieben>()
                        .Sende<BucheReservierung>((t, r, g) => new BucheReservierung(t.PromoKonto, t.Betrag));
                });
            }
            """);
    }

    // ── Validator (Guardrails auf der FORM) ──────────────────────────────────────────────────────
    [Fact]
    public void Validator_ist_still_bei_gesundem_Modell()
    {
        Validator.Prüfe(new EditorModell { Aggregate = [KontoAggregat()] })
            .Where(b => b.IstFehler).Should().BeEmpty();
    }

    [Fact]
    public void Validator_meldet_unrouted_Saga_Command()
    {
        var saga = new Saga
        {
            Name = "KaputterProzess",
            Namespace = "Domain.Kaputt",
            TriggerEvent = "Losgetreten",
            Schritte = [ new() { Wenn = ["Losgetreten"], Sende = "NiemandEntscheidetDas" } ],
        };

        Validator.Prüfe(new EditorModell { Sagas = [saga] })
            .Should().Contain(b => b.Code == "EDIT-UNROUTED-SAGA-CMD" && b.IstFehler);
    }

    [Fact]
    public void Validator_meldet_zu_viele_OneOf_Ausgaenge()
    {
        var agg = new Aggregat
        {
            Name = "Zuviel",
            Namespace = "Domain.Zuviel",
            Events = [ new() { Name = "E1" }, new() { Name = "E2" }, new() { Name = "E3" }, new() { Name = "E4" }, new() { Name = "E5" }, new() { Name = "E6" } ],
            Commands =
            [
                new()
                {
                    Name = "MachWas",
                    Felder = [ new() { Name = "AggregateId", Typ = "Guid" } ],
                    Ergibt = [ new() { Event = "E1" }, new() { Event = "E2" }, new() { Event = "E3" }, new() { Event = "E4" }, new() { Event = "E5" }, new() { Event = "E6" } ],
                },
            ],
        };

        Validator.Prüfe(new EditorModell { Aggregate = [agg] })
            .Should().Contain(b => b.Code == "EDIT-ONEOF-5" && b.IstFehler);
    }
}
