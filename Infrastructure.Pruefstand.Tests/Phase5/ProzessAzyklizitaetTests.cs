using Abstractions;
using Domain.Konto;
using Domain.Ueberweisung;
using FluentAssertions;

namespace Infrastructure.Pruefstand.Phase5;

/// <summary>
/// Azyklizitäts-Prüfung des Regel-Graphs (Spec §3): <c>Command → produziert → Event → triggert → Command</c>
/// muss ein DAG sein. Hier der pure Algorithmus-Kern (<see cref="ProzessGraph"/>); der Roslyn-Analyzer
/// (Step 7) speist die Kanten aus dem Syntaxbaum. Bewiesen: die Überweisungs-Form ist azyklisch, ein Kreis
/// wird als Zyklus gemeldet.
/// </summary>
public class ProzessAzyklizitaetTests
{
    [Fact]
    public void Ueberweisungs_Form_ist_azyklisch()
    {
        // Regeln (Bedingung → Command) wie beim Piloten.
        var regeln = new (IReadOnlyList<string> Bedingung, string Command)[]
        {
            (new[] { "UeberweisungBeauftragt" }, "ReserviereBetrag"),
            (new[] { "UeberweisungBeauftragt", "BetragReserviert" }, "SchreibeGut"),
            (new[] { "UeberweisungBeauftragt", "BetragReserviert", "Gutgeschrieben" }, "BucheReservierung"),
        };
        // Was die Decider produzieren (der Analyzer liest das aus den Decidern; hier gegeben).
        var produziert = new Dictionary<string, IReadOnlyList<string>>
        {
            ["ReserviereBetrag"] = new[] { "BetragReserviert" },
            ["SchreibeGut"] = new[] { "Gutgeschrieben" },
            ["BucheReservierung"] = new[] { "ReservierungGebucht" },
        };

        var kanten = ProzessGraph.BaueKanten(regeln, produziert);
        ProzessGraph.FindeZyklus(kanten).Should().BeEmpty("die Überweisungs-Regeln bilden einen DAG");
    }

    [Fact]
    public void Ein_Kreis_wird_als_Zyklus_gemeldet()
    {
        // A triggert cmdX (produziert B); B triggert cmdY (produziert A) → Kreis.
        var regeln = new (IReadOnlyList<string> Bedingung, string Command)[]
        {
            (new[] { "A" }, "cmdX"),
            (new[] { "B" }, "cmdY"),
        };
        var produziert = new Dictionary<string, IReadOnlyList<string>>
        {
            ["cmdX"] = new[] { "B" },
            ["cmdY"] = new[] { "A" },
        };

        var kanten = ProzessGraph.BaueKanten(regeln, produziert);
        ProzessGraph.FindeZyklus(kanten).Should().NotBeEmpty("A→cmdX→B→cmdY→A ist ein Zyklus");
    }

    [Fact]
    public void Guard_akzeptiert_die_echten_Ueberweisungs_Regeln()
    {
        // Der Boot-Guard über die ECHTEN Pilot-Regeln (ProduziertCommands per Inferenz erfasst) +
        // die Konto-Command→Event-Abbildung. Darf NICHT werfen (der Pilot ist ein DAG).
        var produziert = new Dictionary<Type, Type[]>
        {
            [typeof(ReserviereBetrag)] = new[] { typeof(BetragReserviert) },
            [typeof(SchreibeGut)] = new[] { typeof(Gutgeschrieben) },
            [typeof(BucheReservierung)] = new[] { typeof(ReservierungGebucht) },
            [typeof(GebeReservierungFrei)] = new[] { typeof(ReservierungFreigegeben) },
            [typeof(StorniereGutschrift)] = new[] { typeof(GutschriftStorniert) },
        };

        var regeln = new UeberweisungsProzess().Regeln;
        var akt = () => ProzessAzyklizität.Prüfe("UeberweisungsProzess", regeln,
            cmd => produziert.TryGetValue(cmd, out var e) ? e : Array.Empty<Type>());

        akt.Should().NotThrow("die Überweisungs-Regeln bilden einen DAG");
        // Und die Command-Typen wurden tatsächlich erfasst (sonst wäre der Check leer/wertlos).
        regeln.Regeln.SelectMany(r => r.ProduziertCommands).Should().Contain(typeof(BucheReservierung));
    }
}
