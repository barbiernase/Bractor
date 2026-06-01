// ── Zielverzeichnis: Domain.Client/ImagePair/Handlers/ImagePairStatistikHandler.cs ──

using Client.Infrastructure.Abstractions;
using Domain.ImagePair;
using Domain.Projections;

namespace Domain.Client.ImagePair;

/// <summary>
/// Löst bei relevanten Events eine Statistik-Query aus.
///
/// Sync-Handler: IEnumerable Handle → läuft NACH allen Stores.
///
/// Warum Server-Query statt lokale Berechnung?
/// ListStore hält nur die aktuelle Seite (z.B. 25 von 5000 Items).
/// Lokale Zähler wären falsch. Der Server hat alle Daten.
///
/// Yieldet: GetImagePairStatistik (IQuery) → Server → ImagePairStatistikAntwort → StatistikStore
/// </summary>
public partial class ImagePairStatistikHandler
{
    // ── Lifecycle-Events ──

    IEnumerable<object> Handle(ImagePairErstellt evt, MessageContext ctx)
        => Query();

    IEnumerable<object> Handle(ImagePairKomplett evt, MessageContext ctx)
        => Query();

    // ── KI-Klassifikation ──

    IEnumerable<object> Handle(EinzelBildDurchKiKlassifiziert evt, MessageContext ctx)
        => Query();

    IEnumerable<object> Handle(BildPaarDurchKiKlassifiziert evt, MessageContext ctx)
        => Query();

    // ── Mensch-Labels ──

    IEnumerable<object> Handle(BildRegionGelabelt evt, MessageContext ctx)
        => Query();

    IEnumerable<object> Handle(EinzelBildGelabelt evt, MessageContext ctx)
        => Query();

    IEnumerable<object> Handle(BildPaarGelabelt evt, MessageContext ctx)
        => Query();

    IEnumerable<object> Handle(PhysischesProduktGelabelt evt, MessageContext ctx)
        => Query();

    // ── Query ──

    private static IEnumerable<object> Query()
    {
        yield return new GetImagePairStatistik();
    }
}
