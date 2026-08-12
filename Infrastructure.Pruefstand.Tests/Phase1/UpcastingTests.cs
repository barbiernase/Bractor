using System;
using System.Linq;
using Abstractions;
using Domain.Konto;
using Domain.Lager;
using FluentAssertions;
using Infrastructure.Mapping;
using Infrastructure.Serialization;

namespace Infrastructure.Pruefstand.Phase1;

/// <summary>
/// Typisiertes, generiertes Event-Upcasting (Schema-Evolution — docs: Upcasting). Der Generator
/// <c>EventUpcastingGenerator</c> leitet aus den <see cref="IUpcast{TAlt, TNeu}"/>-Typkanten die
/// frühere/aktuelle Version, die Kette, den Speicher-Diskriminator UND die Marten-Registrierung ab;
/// der Fachcode kennt weder String noch JSON.
///
/// Echtes Beispiel: <c>LagerEingerichtet</c> ist real evolviert (Feld <c>Bestand</c> → <c>AnfangsBestand</c>,
/// der klassische STILLE Korruptionsfall). Nicht-evolvierte Events (z. B. <c>KontoEroeffnet</c>) bleiben
/// exakt beim Basisnamen. Den End-to-End-Durchlauf durch echtes Marten deckt
/// <c>Infrastructure.Integration.Tests/LagerEvolutionRoundtripPostgresTests</c> ab.
/// </summary>
public class UpcastingTests
{
    [Fact]
    public void Nicht_evolviertes_Event_behaelt_den_Basis_Diskriminator_der_Registry()
    {
        // KontoEroeffnet hat keine IUpcast → Diskriminator == snake_case der Registry (keine Verschiebung).
        var snake = GeneratedTypeRegistry.PersistableEvents.First(e => e.Item1 == typeof(KontoEroeffnet)).Item2;
        GeneratedEventUpcasting.Diskriminator(new KontoEroeffnet(100m, false)).Should().Be(snake).And.Be("konto_eroeffnet");
    }

    [Fact]
    public void Evolviertes_Event_rueckt_auf_den_verschobenen_Diskriminator_und_registriert_die_fruehere_Version()
    {
        // Die aktuelle Gestalt schreibt neu unter _v2 …
        GeneratedEventUpcasting.Diskriminator(new LagerEingerichtet(1)).Should().Be("lager_eingerichtet_v2");

        // … und die frühere Version bleibt am Basisnamen registriert (den die alten Bytes tragen).
        var reg = GeneratedEventUpcasting.Registrierungen.ToDictionary(r => r.Typ, r => r.Diskriminator);
        reg.Should().ContainKey(typeof(LagerEingerichtet_V1));
        reg[typeof(LagerEingerichtet_V1)].Should().Be("lager_eingerichtet");
        reg[typeof(LagerEingerichtet)].Should().Be("lager_eingerichtet_v2");
    }

    [Fact]
    public void Alte_Bytes_werden_ueber_die_Kette_in_die_heutige_Gestalt_gewandelt()
    {
        // Alte Byte-Form: das UMBENANNTE Feld (Bestand). Ohne Upcasting fiele AnfangsBestand lautlos auf 0.
        var materialisiert = GeneratedEventUpcasting.Materialisiere("lager_eingerichtet", "{\"Bestand\":42}");

        materialisiert.Should().ContainSingle();
        materialisiert[0].Should().BeOfType<LagerEingerichtet>()
            .Which.AnfangsBestand.Should().Be(42, "die Wandlung übernimmt Bestand → AnfangsBestand");
    }

    [Fact]
    public void Aufwerten_faehrt_eine_frueher_deserialisierte_Version_zur_heutigen_Gestalt()
    {
        // Seam-Eintritt: Marten hat bereits in den früheren Typ deserialisiert; Aufwerten fährt die Kette.
        var frueher = new LagerEingerichtet_V1(Bestand: 7);

        var aufgewertet = GeneratedEventUpcasting.Aufwerten(frueher);

        aufgewertet.Should().ContainSingle();
        aufgewertet[0].Should().BeOfType<LagerEingerichtet>().Which.AnfangsBestand.Should().Be(7);
    }

    [Fact]
    public void Aufwerten_reicht_ein_aktuelles_Event_unveraendert_durch()
    {
        var evt = new KontoEroeffnet(50m, true);
        var aufgewertet = GeneratedEventUpcasting.Aufwerten(evt);

        aufgewertet.Should().ContainSingle();
        aufgewertet[0].Should().BeSameAs(evt);
    }

    [Fact]
    public void Unbekannter_Diskriminator_bricht_laut_ab_statt_still_zu_verschlucken()
    {
        // Der stille `continue`-Drop des alten Read-Pfads wird zur lauten Ausnahme.
        var akt = () => GeneratedEventUpcasting.Materialisiere("gibts_nicht", "{}");
        akt.Should().Throw<NotSupportedException>();
    }
}
