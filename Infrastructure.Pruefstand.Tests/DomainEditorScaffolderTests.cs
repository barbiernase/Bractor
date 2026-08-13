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
    private static EditorModell KontoModell() => new()
    {
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
                Decider =
                [
                    new() { Command = "EroeffneKonto", Ergibt = [ new() { Event = "KontoEroeffnet" }, new() { Event = "KontoExistiertBereits", Guard = "State.Version > 0" } ] },
                    new() { Command = "ReserviereBetrag", Ergibt = [ new() { Event = "BetragReserviert" }, new() { Event = "KontoGesperrt" }, new() { Event = "DeckungReichtNicht", Guard = "State.Verfuegbar < cmd.Betrag" }, new() { Event = "KontoNichtGefunden" } ] },
                ],
                Applier =
                [
                    new() { Event = "KontoEroeffnet" },
                    new() { Event = "BetragReserviert" },
                ],
            },
        ],
    };

    private static string Inhalt(IReadOnlyList<GenerierteDatei> dateien, string pfad) =>
        dateien.Single(d => d.Pfad == pfad).Inhalt.Replace("\r\n", "\n").TrimEnd('\n');

    [Fact]
    public void Erzeugt_die_kanonischen_Aggregat_Dateien()
    {
        var dateien = Scaffolder.Generiere(KontoModell());
        dateien.Select(d => d.Pfad).Should().BeEquivalentTo(
            "Konto/Konto.cs", "Konto/Commands.cs", "Konto/Events.cs", "Konto/Decider.cs", "Konto/Applier.cs");
    }

    [Fact]
    public void Commands_Datei_matcht_die_kanonische_Form()
    {
        Inhalt(Scaffolder.Generiere(KontoModell()), "Konto/Commands.cs").Should().Be(
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
        Inhalt(Scaffolder.Generiere(KontoModell()), "Konto/Events.cs").Should().Be(
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
        Inhalt(Scaffolder.Generiere(KontoModell()), "Konto/Konto.cs").Should().Be(
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
        var decider = Inhalt(Scaffolder.Generiere(KontoModell()), "Konto/Decider.cs");
        decider.Should().Contain("public partial class Decider : IDecider<Konto>");
        decider.Should().Contain("public IEnumerable<OneOf<KontoEroeffnet, KontoExistiertBereits>> Decide(EroeffneKonto cmd)");
        decider.Should().Contain("public IEnumerable<OneOf<BetragReserviert, KontoGesperrt, DeckungReichtNicht, KontoNichtGefunden>> Decide(ReserviereBetrag cmd)");
        decider.Should().Contain("throw new NotImplementedException(\"TODO: Entscheidungslogik.\");");
    }

    [Fact]
    public void Applier_hat_ein_Apply_je_persistentem_Event()
    {
        var applier = Inhalt(Scaffolder.Generiere(KontoModell()), "Konto/Applier.cs");
        applier.Should().Contain("public void Apply(KontoEroeffnet evt)");
        applier.Should().Contain("public void Apply(BetragReserviert evt)");
    }

    [Fact]
    public void Vorhandener_Rumpf_ersetzt_den_Platzhalter()
    {
        var m = KontoModell();
        var agg = m.Aggregate[0] with
        {
            Decider = [ m.Aggregate[0].Decider[0] with { Rumpf = "yield return new KontoEroeffnet(cmd.StartSaldo, cmd.Gesperrt);" }, m.Aggregate[0].Decider[1] ],
            Applier = [ m.Aggregate[0].Applier[0] with { Rumpf = "this.State.Saldo = evt.StartSaldo;" }, m.Aggregate[0].Applier[1] ],
        };
        var dateien = Scaffolder.Generiere(m with { Aggregate = [agg] });
        Inhalt(dateien, "Konto/Decider.cs").Should().Contain("            yield return new KontoEroeffnet(cmd.StartSaldo, cmd.Gesperrt);");
        Inhalt(dateien, "Konto/Applier.cs").Should().Contain("            this.State.Saldo = evt.StartSaldo;");
    }

    // ── Komposition: Records referenzieren andere Records/Enums als Feldtyp (ImagePair-Art) ──
    [Fact]
    public void Records_komponieren_ueber_Enums_und_Value_Objects()
    {
        var m = new EditorModell
        {
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

        Inhalt(dateien, "ImagePair/Enums.cs").Should().Be(
            """
            namespace Domain.ImagePair;

            public enum BildVersion { Dc0, Dc2 }
            """);
        Inhalt(dateien, "ImagePair/ValueObjects.cs").Should().Be(
            """
            namespace Domain.ImagePair;

            public record BildMeta(string OriginalDateiname, DateTimeOffset ErstelltAm);
            """);
        // Composition: das Command-Feld hat den Typ eines anderen Records/Enums.
        Inhalt(dateien, "ImagePair/Commands.cs").Should().Contain("public record MeldeBildVerfuegbar(Guid AggregateId, BildVersion Version, BildMeta Meta) : ICommand;");
        Inhalt(dateien, "ImagePair/Events.cs").Should().Contain("public record BildVerfuegbar(BildVersion Version, IReadOnlyList<BildMeta> Regionen) : IEvent;");
    }

    [Fact]
    public void Erzeugungs_Command_bekommt_ICreationCommand()
    {
        var m = new EditorModell
        {
            Records = [ new() { Name = "ErstelleX", Kind = RecordArt.Command, Namespace = "Domain.X", IstErzeugung = true, Felder = [ new() { Name = "AggregateId", Typ = "Guid" } ] } ],
        };
        Inhalt(Scaffolder.Generiere(m), "X/Commands.cs").Should().Contain("public record ErstelleX(Guid AggregateId) : ICommand, ICreationCommand;");
    }

    [Fact]
    public void Ist_deterministisch_und_JSON_roundtrip_stabil()
    {
        var m = KontoModell();
        Scaffolder.Generiere(m).Should().BeEquivalentTo(Scaffolder.Generiere(m), o => o.WithStrictOrdering());
        Scaffolder.Generiere(EditorModell.AusJson(m.AlsJson())).Should().BeEquivalentTo(Scaffolder.Generiere(m), o => o.WithStrictOrdering());
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
            Sagas = [ new() { Name = "Kaputt", Namespace = "Domain.Kaputt", TriggerEvent = "Los", Schritte = [ new() { Wenn = ["Los"], Sende = "NiemandDecided" } ] } ],
        };
        Validator.Prüfe(m).Should().Contain(b => b.Code == "EDIT-UNROUTED-SAGA-CMD" && b.IstFehler);
    }

    [Fact]
    public void Validator_meldet_Decide_auf_unbekanntes_Event()
    {
        var m = KontoModell();
        var agg = m.Aggregate[0] with { Decider = [ m.Aggregate[0].Decider[0] with { Ergibt = [ new() { Event = "GibtsNicht" } ] }, m.Aggregate[0].Decider[1] ] };
        Validator.Prüfe(m with { Aggregate = [agg] }).Should().Contain(b => b.Code == "EDIT-EVENT-FEHLT" && b.IstFehler);
    }
}
