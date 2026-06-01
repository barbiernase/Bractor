using System.Collections.Immutable;
using Domain.ImagePair;
using Domain.Projections;

namespace Domain.Client.ImagePair;

/// <summary>
/// Immutable Record — ersetzt den mutable ObservableObject ImagePairEntry.
///
/// Mutation = Replacement:
///   map.Update(id, e => e with { PhysischesProduktLabel = label });
///
/// Die TrackedMap sieht die Zuweisung und erzeugt ein Replaced-Delta.
/// Kein RenderGeneration++, kein direktes PropertyChanged auf dem Entry.
///
/// Regionen: ImmutableArray mit fixer Länge 8.
///   - ImmutableArray ist ein struct (kein Heap-Objekt pro Region-Array)
///   - with-Semantik für Region-Updates via WithRegion()
///
/// BildSrc: Client-seitig gesetzt durch FileLoaded (FileBridge).
///   Nicht Teil der Server-Antwort — wird nach dem ersten FileLoaded befüllt.
///
/// Historie: NICHT Teil des Records — lebt im HistorieStore separat.
/// </summary>
public record ImagePairRecord(
    Guid Id,
    string? PairKey,
    DateTimeOffset ProduziertAm,
    DateTimeOffset AufgenommenAm,
    bool IstKomplett,
    string? UrsprungsPfad,

    // DC0 — Kamerabild 0
    string? Dc0Pfad,
    Klassifikation? Dc0KiBildKlassifikation,
    Klassifikation? Dc0MenschBildLabel,
    string? Dc0BildSrc,                             // client-seitig: aus FileLoaded
    ImmutableArray<Klassifikation?> Dc0KiRegionen,  // Länge 8
    ImmutableArray<Klassifikation?> Dc0MenschRegionen,

    // DC2 — Kamerabild 2
    string? Dc2Pfad,
    Klassifikation? Dc2KiBildKlassifikation,
    Klassifikation? Dc2MenschBildLabel,
    string? Dc2BildSrc,                             // client-seitig: aus FileLoaded
    ImmutableArray<Klassifikation?> Dc2KiRegionen,
    ImmutableArray<Klassifikation?> Dc2MenschRegionen,

    // Paar-Ebene
    Klassifikation? KiBildpaarKlassifikation,
    Klassifikation? MenschBildpaarLabel,
    Klassifikation? PhysischesProduktLabel,

    // Inspektion
    bool IstInspiziert
)
{
    // ═══════════════════════════════════════════════════
    // BERECHNETE PROPERTIES (kein State, keine Mutation)
    // ═══════════════════════════════════════════════════

    public bool HatKiKlassifikation =>
        Dc0KiBildKlassifikation != null || Dc2KiBildKlassifikation != null;

    public bool HatMenschLabel =>
        Dc0MenschBildLabel != null || Dc2MenschBildLabel != null;

    public bool IstVollstaendigKlassifiziert =>
        HatKiKlassifikation && HatMenschLabel && PhysischesProduktLabel != null;

    // ═══════════════════════════════════════════════════
    // REGION-UPDATES — with-Syntax für Einzelregion
    // ═══════════════════════════════════════════════════

    /// <summary>
    /// Gibt einen neuen Record zurück mit einem aktualisierten DC0-Regions-Label.
    /// Verwendung: map.Update(id, e => e.WithDc0KiRegion(index, label))
    /// </summary>
    public ImagePairRecord WithDc0KiRegion(int index, Klassifikation? label) =>
        this with { Dc0KiRegionen = Dc0KiRegionen.SetItem(index, label) };

    public ImagePairRecord WithDc2KiRegion(int index, Klassifikation? label) =>
        this with { Dc2KiRegionen = Dc2KiRegionen.SetItem(index, label) };

    public ImagePairRecord WithDc0MenschRegion(int index, Klassifikation? label) =>
        this with { Dc0MenschRegionen = Dc0MenschRegionen.SetItem(index, label) };

    public ImagePairRecord WithDc2MenschRegion(int index, Klassifikation? label) =>
        this with { Dc2MenschRegionen = Dc2MenschRegionen.SetItem(index, label) };

    // ═══════════════════════════════════════════════════
    // FACTORY — aus Server-Antwort
    // ═══════════════════════════════════════════════════

    /// <summary>
    /// Erzeugt einen Record aus einer Server-Antwort.
    /// BildSrc bleibt null — wird nachträglich via FileLoaded gesetzt.
    /// Regionen werden auf Länge 8 aufgefüllt / abgeschnitten.
    /// </summary>
    public static ImagePairRecord FromAntwort(ImagePairAntwort a) => new(
        Id:                       a.Id,
        PairKey:                  a.PairKey,
        ProduziertAm:             a.ProduziertAm,
        AufgenommenAm:            a.AufgenommenAm,
        IstKomplett:              a.IstKomplett,
        UrsprungsPfad:            a.UrsprungsPfad,

        Dc0Pfad:                  a.Dc0Pfad,
        Dc0KiBildKlassifikation:  a.Dc0KiBildKlassifikation,
        Dc0MenschBildLabel:       a.Dc0MenschBildLabel,
        Dc0BildSrc:               null,
        Dc0KiRegionen:            ToRegionArray(a.Dc0KiRegionen),
        Dc0MenschRegionen:        ToRegionArray(a.Dc0MenschRegionen),

        Dc2Pfad:                  a.Dc2Pfad,
        Dc2KiBildKlassifikation:  a.Dc2KiBildKlassifikation,
        Dc2MenschBildLabel:       a.Dc2MenschBildLabel,
        Dc2BildSrc:               null,
        Dc2KiRegionen:            ToRegionArray(a.Dc2KiRegionen),
        Dc2MenschRegionen:        ToRegionArray(a.Dc2MenschRegionen),

        KiBildpaarKlassifikation: a.KiBildpaarKlassifikation,
        MenschBildpaarLabel:      a.MenschBildpaarLabel,
        PhysischesProduktLabel:   a.PhysischesProduktLabel,
        IstInspiziert:            a.IstInspiziert
    );

    /// <summary>
    /// Leerer Record mit nur der Id — für Erstellen-Events bevor die
    /// vollständige Antwort vom Server kommt.
    /// </summary>
    public static ImagePairRecord Leer(Guid id) => new(
        Id:                       id,
        PairKey:                  null,
        ProduziertAm:             default,
        AufgenommenAm:            default,
        IstKomplett:              false,
        UrsprungsPfad:            null,
        Dc0Pfad:                  null,
        Dc0KiBildKlassifikation:  null,
        Dc0MenschBildLabel:       null,
        Dc0BildSrc:               null,
        Dc0KiRegionen:            EmptyRegionen,
        Dc0MenschRegionen:        EmptyRegionen,
        Dc2Pfad:                  null,
        Dc2KiBildKlassifikation:  null,
        Dc2MenschBildLabel:       null,
        Dc2BildSrc:               null,
        Dc2KiRegionen:            EmptyRegionen,
        Dc2MenschRegionen:        EmptyRegionen,
        KiBildpaarKlassifikation: null,
        MenschBildpaarLabel:      null,
        PhysischesProduktLabel:   null,
        IstInspiziert:            false
    );

    // ═══════════════════════════════════════════════════
    // HILFSMETHODEN
    // ═══════════════════════════════════════════════════

    /// <summary>8 leere Regionen — Länge entspricht Kamera-Spezifikation.</summary>
    public static readonly ImmutableArray<Klassifikation?> EmptyRegionen =
        ImmutableArray.Create<Klassifikation?>(null, null, null, null, null, null, null, null);

    private static ImmutableArray<Klassifikation?> ToRegionArray(
        IReadOnlyList<Klassifikation?> source)
    {
        var builder = ImmutableArray.CreateBuilder<Klassifikation?>(8);
        for (int i = 0; i < 8; i++)
            builder.Add(i < source.Count ? source[i] : null);
        return builder.MoveToImmutable();
    }
}
