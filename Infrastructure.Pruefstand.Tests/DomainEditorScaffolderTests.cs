using DomainEditor;
using FluentAssertions;

namespace Infrastructure.Pruefstand.Tests;

/// <summary>
/// Dogfood für den record-zentrischen Domänen-Editor: Records (Command/Event/Ablehnung/Value Object)
/// + Aggregat-Komposition (State/Decider/Applier) → C# in kanonischer Gestalt. Store-frei, rein
/// textuell. Beweist die FORM + Komposition (Feldtyp = anderer Record/Enum), nicht die Logik.
/// </summary>
public sealed class DomainEditorScaffolderTests
{
    // ── Konto als record-zentrisches Modell ────────────────────────────────────────────────────
    /// <summary>Der Code-Fakt, den der Extractor liefert: das Projekt mit Wurzel-Namespace <c>Domain</c> liegt in <c>Domain/</c>.</summary>
    private static readonly Rahmen TestRahmen = new() { Verzeichnisse = new Dictionary<string, string> { ["Domain"] = "Domain" } };

    private static EditorModell KontoModell() => new()
    {
        Rahmen = TestRahmen,
        Records =
        [
            new() { Name = "EroeffneKonto", Kind = RecordArt.Command, Namespace = "Domain.Konto",
                Felder = [ new() { Name = "AggregateId", Typ = "Guid" }, new() { Name = "StartSaldo", Typ = "decimal" }, new() { Name = "Gesperrt", Typ = "bool", Standard = "false" } ] },
            new() { Name = "ReserviereBetrag", Kind = RecordArt.Command, Namespace = "Domain.Konto",
                Felder = [ new() { Name = "AggregateId", Typ = "Guid" }, new() { Name = "Betrag", Typ = "decimal" } ] },

            new() { Name = "KontoEroeffnet", Kind = RecordArt.Event, Namespace = "Domain.Konto",
                Felder = [ new() { Name = "StartSaldo", Typ = "decimal" }, new() { Name = "Gesperrt", Typ = "bool" } ] },
            new() { Name = "BetragReserviert", Kind = RecordArt.Event, Namespace = "Domain.Konto",
                Felder = [ new() { Name = "Betrag", Typ = "decimal" } ] },

            new() { Name = "KontoExistiertBereits", Kind = RecordArt.Rejection, Namespace = "Domain.Konto", Felder = [ new() { Name = "AggregateId", Typ = "Guid" } ] },
            new() { Name = "KontoGesperrt", Kind = RecordArt.Rejection, Namespace = "Domain.Konto", Felder = [ new() { Name = "AggregateId", Typ = "Guid" } ] },
            new() { Name = "DeckungReichtNicht", Kind = RecordArt.Rejection, Namespace = "Domain.Konto", Felder = [ new() { Name = "Verfuegbar", Typ = "decimal" }, new() { Name = "Angefordert", Typ = "decimal" } ] },
            new() { Name = "KontoNichtGefunden", Kind = RecordArt.Rejection, Namespace = "Domain.Konto", Felder = [ new() { Name = "AggregateId", Typ = "Guid" } ] },
        ],
        Aggregate =
        [
            new()
            {
                Name = "Konto", Namespace = "Domain.Konto",
                State =
                [
                    new() { Name = "Saldo", Typ = "decimal" },
                    new() { Name = "Reserviert", Typ = "decimal" },
                    new() { Name = "Gesperrt", Typ = "bool" },
                    new() { Name = "Verfuegbar", Typ = "decimal", Ausdruck = "Saldo - Reserviert" },
                ],
            },
        ],
        Decider =
        [
            new() { Aggregat = "Konto", Command = "EroeffneKonto", Ergibt = [ new() { Event = "KontoEroeffnet" }, new() { Event = "KontoExistiertBereits", Guard = "State.Version > 0" } ] },
            new() { Aggregat = "Konto", Command = "ReserviereBetrag", Ergibt = [ new() { Event = "BetragReserviert" }, new() { Event = "KontoGesperrt" }, new() { Event = "DeckungReichtNicht", Guard = "State.Verfuegbar < cmd.Betrag" }, new() { Event = "KontoNichtGefunden" } ] },
        ],
        Applier =
        [
            new() { Aggregat = "Konto", Event = "KontoEroeffnet" },
            new() { Aggregat = "Konto", Event = "BetragReserviert" },
        ],
    };

    private static string Inhalt(IReadOnlyList<GenerierteDatei> dateien, string pfad) =>
        dateien.Single(d => d.Pfad == pfad).Inhalt.Replace("\r\n", "\n").TrimEnd('\n');

    [Fact]
    public void Erzeugt_die_kanonischen_Aggregat_Dateien()
    {
        var dateien = Scaffolder.Generiere(KontoModell());
        dateien.Select(d => d.Pfad).Should().BeEquivalentTo(
            "Domain/Konto/Konto.cs", "Domain/Konto/Commands.cs", "Domain/Konto/Events.cs", "Domain/Konto/Konto.Decider.cs", "Domain/Konto/Konto.Applier.cs");
    }

    [Fact]
    public void Commands_Datei_matcht_die_kanonische_Form()
    {
        Inhalt(Scaffolder.Generiere(KontoModell()), "Domain/Konto/Commands.cs").Should().Be(
            """
            using Abstractions;

            namespace Domain.Konto;

            public record EroeffneKonto(Guid AggregateId, decimal StartSaldo, bool Gesperrt = false) : ICommand;
            public record ReserviereBetrag(Guid AggregateId, decimal Betrag) : ICommand;
            """);
    }

    [Fact]
    public void Events_Datei_trennt_persistent_und_Ablehnung()
    {
        Inhalt(Scaffolder.Generiere(KontoModell()), "Domain/Konto/Events.cs").Should().Be(
            """
            using Abstractions;

            namespace Domain.Konto;

            public record KontoEroeffnet(decimal StartSaldo, bool Gesperrt) : IEvent;
            public record BetragReserviert(decimal Betrag) : IEvent;

            public record KontoExistiertBereits(Guid AggregateId) : ITransientEvent;
            public record KontoGesperrt(Guid AggregateId) : ITransientEvent;
            public record DeckungReichtNicht(decimal Verfuegbar, decimal Angefordert) : ITransientEvent;
            public record KontoNichtGefunden(Guid AggregateId) : ITransientEvent;
            """);
    }

    [Fact]
    public void State_matcht_die_kanonische_Form()
    {
        Inhalt(Scaffolder.Generiere(KontoModell()), "Domain/Konto/Konto.cs").Should().Be(
            """
            using Abstractions;

            namespace Domain.Konto;

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
    public void Decider_traegt_die_OneOf_Signaturen()
    {
        var decider = Inhalt(Scaffolder.Generiere(KontoModell()), "Domain/Konto/Konto.Decider.cs");
        decider.Should().Contain("public partial class Decider : IDecider<Konto>");
        decider.Should().Contain("public IEnumerable<OneOf<KontoEroeffnet, KontoExistiertBereits>> Decide(EroeffneKonto cmd)");
        decider.Should().Contain("public IEnumerable<OneOf<BetragReserviert, KontoGesperrt, DeckungReichtNicht, KontoNichtGefunden>> Decide(ReserviereBetrag cmd)");
        decider.Should().Contain("throw new NotImplementedException(\"TODO: Entscheidungslogik.\");");
    }

    [Fact]
    public void Applier_hat_ein_Apply_je_persistentem_Event()
    {
        var applier = Inhalt(Scaffolder.Generiere(KontoModell()), "Domain/Konto/Konto.Applier.cs");
        applier.Should().Contain("public void Apply(KontoEroeffnet evt)");
        applier.Should().Contain("public void Apply(BetragReserviert evt)");
    }

    [Fact]
    public void Vorhandener_Rumpf_ersetzt_den_Platzhalter()
    {
        var m = KontoModell();
        var m2 = m with
        {
            Decider = [ m.Decider[0] with { Rumpf = "yield return new KontoEroeffnet(cmd.StartSaldo, cmd.Gesperrt);" }, m.Decider[1] ],
            Applier = [ m.Applier[0] with { Rumpf = "this.State.Saldo = evt.StartSaldo;" }, m.Applier[1] ],
        };
        var dateien = Scaffolder.Generiere(m2);
        Inhalt(dateien, "Domain/Konto/Konto.Decider.cs").Should().Contain("            yield return new KontoEroeffnet(cmd.StartSaldo, cmd.Gesperrt);");
        Inhalt(dateien, "Domain/Konto/Konto.Applier.cs").Should().Contain("            this.State.Saldo = evt.StartSaldo;");
    }

    // ── Komposition: Records referenzieren andere Records/Enums als Feldtyp (ImagePair-Art) ──
    [Fact]
    public void Records_komponieren_ueber_Enums_und_Value_Objects()
    {
        var m = new EditorModell
        {
            Rahmen = TestRahmen,
            Enums = [ new() { Name = "BildVersion", Namespace = "Domain.ImagePair", Werte = ["Dc0", "Dc2"] } ],
            Records =
            [
                new() { Name = "BildMeta", Kind = RecordArt.ValueObject, Namespace = "Domain.ImagePair",
                    Felder = [ new() { Name = "OriginalDateiname", Typ = "string" }, new() { Name = "ErstelltAm", Typ = "DateTimeOffset" } ] },
                new() { Name = "MeldeBildVerfuegbar", Kind = RecordArt.Command, Namespace = "Domain.ImagePair",
                    Felder = [ new() { Name = "AggregateId", Typ = "Guid" }, new() { Name = "Version", Typ = "BildVersion" }, new() { Name = "Meta", Typ = "BildMeta" } ] },
                new() { Name = "BildVerfuegbar", Kind = RecordArt.Event, Namespace = "Domain.ImagePair",
                    Felder = [ new() { Name = "Version", Typ = "BildVersion" }, new() { Name = "Regionen", Typ = "IReadOnlyList<BildMeta>" } ] },
            ],
        };
        var dateien = Scaffolder.Generiere(m);

        Inhalt(dateien, "Domain/ImagePair/Enums.cs").Should().Be(
            """
            namespace Domain.ImagePair;

            public enum BildVersion { Dc0, Dc2 }
            """);
        Inhalt(dateien, "Domain/ImagePair/ValueObjects.cs").Should().Be(
            """
            namespace Domain.ImagePair;

            public record BildMeta(string OriginalDateiname, DateTimeOffset ErstelltAm);
            """);
        // Composition: das Command-Feld hat den Typ eines anderen Records/Enums.
        Inhalt(dateien, "Domain/ImagePair/Commands.cs").Should().Contain("public record MeldeBildVerfuegbar(Guid AggregateId, BildVersion Version, BildMeta Meta) : ICommand;");
        Inhalt(dateien, "Domain/ImagePair/Events.cs").Should().Contain("public record BildVerfuegbar(BildVersion Version, IReadOnlyList<BildMeta> Regionen) : IEvent;");
    }

    [Fact]
    public void Erzeugungs_Command_bekommt_ICreationCommand()
    {
        var m = new EditorModell
        {
            Rahmen = TestRahmen,
            Records = [ new() { Name = "ErstelleX", Kind = RecordArt.Command, Namespace = "Domain.X", IstErzeugung = true, Felder = [ new() { Name = "AggregateId", Typ = "Guid" } ] } ],
        };
        Inhalt(Scaffolder.Generiere(m), "Domain/X/Commands.cs").Should().Contain("public record ErstelleX(Guid AggregateId) : ICommand, ICreationCommand;");
    }

    [Fact]
    public void Ist_deterministisch_und_JSON_roundtrip_stabil()
    {
        var m = KontoModell();
        Scaffolder.Generiere(m).Should().BeEquivalentTo(Scaffolder.Generiere(m), o => o.WithStrictOrdering());
        Scaffolder.Generiere(EditorModell.AusJson(m.AlsJson())).Should().BeEquivalentTo(Scaffolder.Generiere(m), o => o.WithStrictOrdering());
    }

    // ── Round-trip-Treue: was der Extractor aus echtem Code liest, schreibt der Scaffolder verlustfrei zurück ──
    [Fact]
    public void State_Felder_behalten_Reihenfolge_Initialwert_und_NurGet()
    {
        var m = KontoModell() with
        {
            Aggregate = [ KontoModell().Aggregate[0] with { State =
            [
                new() { Name = "Offen", Typ = "HashSet<Guid>", Standard = "new()", NurGet = true },
                new() { Name = "Existiert", Typ = "bool", Ausdruck = "Version > 0" },
                new() { Name = "Saldo", Typ = "decimal", Standard = "0m" },
            ], StateZusatz = "public bool Enthält(Guid id) => Offen.Contains(id);" } ],
        };
        var state = Inhalt(Scaffolder.Generiere(m), "Domain/Konto/Konto.cs");
        state.Should().Contain("public HashSet<Guid> Offen { get; } = new();");
        state.Should().Contain("public decimal Saldo { get; set; } = 0m;");
        state.IndexOf("Existiert", StringComparison.Ordinal).Should().BeLessThan(state.IndexOf("Saldo", StringComparison.Ordinal),
            "Deklarations-Reihenfolge bleibt (sonst ist der Round-trip kein Fixpunkt)");
        state.Should().Contain("    public bool Enthält(Guid id) => Offen.Contains(id);");
    }

    [Fact]
    public void Record_mit_Handcode_Rumpf_und_Defaults()
    {
        var m = KontoModell() with
        {
            Records = [ .. KontoModell().Records, new()
            {
                Name = "Limit", Kind = RecordArt.ValueObject, Namespace = "Domain.Konto",
                Felder = [ new() { Name = "Betrag", Typ = "decimal" }, new() { Name = "Waehrung", Typ = "string?", Standard = "null" } ],
                Zusatz = "public static Limit Null { get; } = new(0m);",
            } ],
        };
        Inhalt(Scaffolder.Generiere(m), "Domain/Konto/ValueObjects.cs").Should().Contain(
            "public record Limit(decimal Betrag, string? Waehrung = null)\n{\n    public static Limit Null { get; } = new(0m);\n}");
    }

    [Fact]
    public void Parametername_und_bewusst_leerer_Rumpf()
    {
        var m = KontoModell() with
        {
            Decider = [ KontoModell().Decider[0] with { Parameter = "command", Rumpf = "yield return new KontoEroeffnet(command.StartSaldo, command.Gesperrt);" } ],
            Applier = [ new() { Aggregat = "Konto", Event = "KontoEroeffnet", Parameter = "e", Rumpf = "" } ],
        };
        var dateien = Scaffolder.Generiere(m);
        Inhalt(dateien, "Domain/Konto/Konto.Decider.cs").Should().Contain("Decide(EroeffneKonto command)");
        var applier = Inhalt(dateien, "Domain/Konto/Konto.Applier.cs");
        applier.Should().Contain("public void Apply(KontoEroeffnet e)\n        {\n        }");
        applier.Should().NotContain("NotImplementedException", "\"\" = bewusster No-op, kein Platzhalter");
    }

    [Fact]
    public void Usings_werden_aus_Typ_Referenzen_ueber_Namespace_Grenzen_abgeleitet()
    {
        var m = KontoModell() with
        {
            Records = [ .. KontoModell().Records,
                new() { Name = "Waehrung", Kind = RecordArt.ValueObject, Namespace = "Domain.Geld", Felder = [ new() { Name = "Code", Typ = "string" } ] },
                new() { Name = "SetzeWaehrung", Kind = RecordArt.Command, Namespace = "Domain.Konto",
                    Felder = [ new() { Name = "AggregateId", Typ = "Guid" }, new() { Name = "W", Typ = "List<Waehrung>" } ] } ],
        };
        Inhalt(Scaffolder.Generiere(m), "Domain/Konto/Commands.cs").Should().StartWith("using Abstractions;\nusing Domain.Geld;\n");
    }

    [Fact]
    public void Saga_schreibt_gelesene_Lambdas_verbatim_und_leitet_Ausloeser_using_ab()
    {
        var m = KontoModell() with
        {
            Records = [ .. KontoModell().Records,
                new() { Name = "TeilFertig", Kind = RecordArt.Event, Namespace = "Domain.Teil", Felder = [ new() { Name = "KontoId", Typ = "Guid" } ] } ],
            Sagas = [ new()
            {
                Name = "TeilProzess", Namespace = "Domain.Konto", TriggerEvent = "TeilFertig",
                Schritte = [ new() { Wenn = [ "TeilFertig" ], Sende = "ReserviereBetrag", SendeAusdruck = "e => new ReserviereBetrag(e.KontoId, 1m)" } ],
            } ],
        };
        var saga = Inhalt(Scaffolder.Generiere(m), "Domain/Konto/TeilProzess.cs");
        saga.Should().Contain("using Domain.Teil;", "der Auslöser-Namespace wird abgeleitet (früher CS0246)");
        saga.Should().Contain(".Sende<ReserviereBetrag>(e => new ReserviereBetrag(e.KontoId, 1m));");
    }

    // ── Validator ──────────────────────────────────────────────────────────────────────────────
    [Fact]
    public void Validator_ist_still_bei_gesundem_Modell()
    {
        Validator.Prüfe(KontoModell()).Where(b => b.IstFehler).Should().BeEmpty();
    }

    [Fact]
    public void Validator_meldet_unrouted_Saga_Command()
    {
        var m = new EditorModell
        {
            Rahmen = TestRahmen,
            Sagas = [ new() { Name = "Kaputt", Namespace = "Domain.Kaputt", TriggerEvent = "Los", Schritte = [ new() { Wenn = ["Los"], Sende = "NiemandDecided" } ] } ],
        };
        Validator.Prüfe(m).Should().Contain(b => b.Code == "EDIT-UNROUTED-SAGA-CMD" && b.IstFehler);
    }

    [Fact]
    public void Validator_meldet_Decide_auf_unbekanntes_Event()
    {
        var m = KontoModell();
        var m2 = m with { Decider = [ m.Decider[0] with { Ergibt = [ new() { Event = "GibtsNicht" } ] }, m.Decider[1] ] };
        Validator.Prüfe(m2).Should().Contain(b => b.Code == "EDIT-EVENT-FEHLT" && b.IstFehler);
    }
}
