using Abstractions;
using Domain.ImagePair;
using FluentAssertions;
using Infrastructure.Aggregate;
using Infrastructure.Mapping;
using Infrastructure.Serialization;

namespace Infrastructure.Pruefstand.Phase1;

/// <summary>
/// STJ-Source-Gen-Serialisierung (Schritt 1 der COPY-Vorarbeit): der generierte, reflection-freie
/// <see cref="GeneratedEventJson"/>-Dispatch über den <see cref="EventJsonSerializerContext"/>.
///
/// Zwei Achsen:
/// (1) VOLLSTÄNDIGKEIT — der generierte Switch deckt GENAU die persistierbaren Events ab
///     (== <c>GeneratedTypeRegistry.PersistableEventTypes</c>). Kein Event fällt durch, keins zu viel.
///     Zusammen mit dem Compile-Zeit-Bruch (fehlt ein Manifest-Eintrag → keine <c>Default.X</c>-Property)
///     ist Drift damit doppelt abgefangen.
/// (2) KORREKTHEIT — repräsentative Round-Trips über die echten Event-Formen (verschachtelte Records,
///     nullable Enums, <c>IReadOnlyList</c>, prozess-interne Marke) gehen verlustfrei durch.
/// </summary>
public class EventJsonSerializationTests
{
    [Fact]
    public void Abgedeckte_Typen_sind_exakt_die_persistierbaren_Events()
    {
        var abgedeckt = GeneratedEventJson.AbgedeckteTypen.ToHashSet();
        var persistierbar = GeneratedTypeRegistry.PersistableEventTypes.ToHashSet();

        // Symmetrische Differenz muss leer sein — sonst ist entweder das Manifest oder die
        // Generator-Filterlogik aus dem Tritt mit der Marten-Persistenzmenge.
        abgedeckt.Should().BeEquivalentTo(persistierbar,
            "der Source-Gen-Kontext muss genau die persistierbaren Events abdecken");
    }

    [Fact]
    public void Diskriminator_ist_der_snake_case_der_Registry()
    {
        // Der COPY-`type`-Spaltenwert muss deckungsgleich mit Martens MapEventType-Namen sein.
        var (_, snake) = GeneratedTypeRegistry.PersistableEvents
            .First(e => e.Type == typeof(ImagePairErstellt));

        GeneratedEventJson.Diskriminator(new ImagePairErstellt(
            Guid.NewGuid(), "k", DateTimeOffset.UnixEpoch, DateTimeOffset.UnixEpoch, "p"))
            .Should().Be(snake).And.Be("image_pair_erstellt");
    }

    [Fact]
    public void RoundTrip_einfaches_Event()
    {
        var e = new ImagePairErstellt(
            Guid.NewGuid(), "pair-42",
            new DateTimeOffset(2026, 8, 10, 12, 0, 0, TimeSpan.Zero),
            new DateTimeOffset(2026, 8, 9, 8, 30, 0, TimeSpan.Zero),
            "/ursprung/pfad.png");

        RoundTrip(e).Should().Be(e);
    }

    [Fact]
    public void RoundTrip_verschachteltes_Event_mit_Liste_und_nullable_Enums()
    {
        var e = new BildVerfuegbar(
            BildVersion.Dc2,
            new BildMeta("bild.png", 123456, 1920, 1080,
                new DateTimeOffset(2026, 1, 2, 3, 4, 5, TimeSpan.Zero)),
            "/pfad/bild.png",
            new List<RegionBewertung>
            {
                new(0, new RegionPosition(0, 0, 12.5, 100), Klassifikation.KeineAnomalie, null),
                new(1, new RegionPosition(12.5, 0, 12.5, 100), null, Klassifikation.Anomalie),
            });

        var back = (BildVerfuegbar)RoundTrip(e);

        // Tiefer Struktur-Vergleich: Record-`.Be` verglich die IReadOnlyList per Referenz (immer ungleich
        // nach Round-Trip) — nicht die Serialisierung ist schuld, sondern die Record-Gleichheit über Listen.
        back.Should().BeEquivalentTo(e);                       // Meta + Liste + verschachtelte Records tief
    }

    [Fact]
    public void RoundTrip_prozessinterne_Marke()
    {
        var e = new KommandoVerarbeitet(Guid.NewGuid());
        RoundTrip(e).Should().Be(e);
    }

    /// <summary>Serialisiert über den Dispatch, liest über den Diskriminator zurück — der Weg,
    /// den der künftige COPY-Writer/-Reader nimmt.</summary>
    private static IEvent RoundTrip(IEvent e)
    {
        var json = GeneratedEventJson.Serialize(e);
        var typ = GeneratedEventJson.Diskriminator(e);
        return GeneratedEventJson.Deserialize(typ, json);
    }
}
