using Abstractions;
using Domain.ImagePair;

namespace Domain.Projections;

/// <summary>Einzelnes ImagePair abfragen.</summary>
public record GetImagePair(Guid PairId) : IQuery;

/// <summary>Filterbare Suche über alle ImagePairs.</summary>
public record SucheImagePairs(ImagePairFilter Filter) : IQuery;

/// <summary>Übersichts-Statistik.</summary>
public record GetImagePairStatistik() : IQuery;

/// <summary>Nächste Bilder die der Mensch labeln soll.</summary>
public record GetUnklassifizierteImagePairs(int MaxAnzahl = 20) : IQuery;

/// <summary>
/// Zeitlich aggregierter Produktionsverlauf (Buckets).
/// </summary>
public record GetProduktionsVerlauf(
    DateTimeOffset Von,
    DateTimeOffset Bis,
    int BucketMinuten = 5
) : IQuery;

/// <summary>
/// Liste aller Tage mit Produktion.
/// </summary>
public record GetProduktionsTage() : IQuery;

/// <summary>
/// Leichtgewichtiger Strip: ein Punkt pro Teil (nur Zeitpunkt + Label).
/// Für die Barcode-Visualisierung — zeigt Produktionsausfälle als Lücken.
/// </summary>
public record GetProduktionsStrip(
    DateTimeOffset Von,
    DateTimeOffset Bis
) : IQuery;

/// <summary>
/// Filter-Objekt — alle Felder Nullable, nur gesetzte Felder filtern.
/// Von/Bis beziehen sich auf ProduziertAm (Produktionszeitpunkt = fachliche Sortierung).
/// </summary>
public record ImagePairFilter(
    DateTimeOffset? Von = null,
    DateTimeOffset? Bis = null,
    Klassifikation? KiKlassifikation = null,
    /// <summary>Mensch-Label auf irgendeiner Ebene (Kamera erkennbar?).</summary>
    Klassifikation? MenschLabel = null,
    /// <summary>Physisches Produkt-Label.</summary>
    Klassifikation? ProduktLabel = null,
    bool? NurKomplette = null,
    bool? HatKiKlassifikation = null,
    bool? HatMenschLabel = null,
    /// <summary>Nur Paare die noch nicht inspiziert wurden.</summary>
    bool? NurNichtInspizierte = null,
    int Seite = 1,
    int SeitenGroesse = 50
);