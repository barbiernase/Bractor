// ── Domain.Client/ImagePair/Handlers/ChartRefreshHandler.cs ──

using Client.Infrastructure.Abstractions;
using Client.Infrastructure.Connection;
using Domain.Projections;

namespace Domain.Client.ImagePair;

/// <summary>
/// Orchestriert Chart-Daten: Tagesliste laden, ersten Tag auto-selektieren.
///
/// Symmetrisch zum ListRefreshHandler:
///   ListRefreshHandler  → ConnectionEstablished → SucheImagePairs
///   ChartRefreshHandler → ConnectionEstablished → GetProduktionsTage
///
/// VORHER: MainStage.OnInitialized() lud Daten, OnChartChanged() orchestrierte.
///         Das war eine Verletzung des Handler-Prinzips (View darf nicht orchestrieren).
///
/// Sync-Handler: IEnumerable Handle → läuft NACH allen Stores.
/// Liest: ChartStore (GewaehlterTag — um doppelte Selektion zu vermeiden)
/// </summary>
public partial class ChartRefreshHandler
{
    private readonly ChartStore _chart;

    public ChartRefreshHandler(ChartStore chart)
    {
        _chart = chart;
    }

    // ═══════════════════════════════════════════════════
    // HANDLE — Connection
    // ═══════════════════════════════════════════════════

    /// <summary>
    /// Bei Verbindungsaufbau: Tagesliste laden.
    /// Die Antwort (ProduktionsTageAntwort) wird vom ChartStore gehandlet
    /// und von diesem Handler für Auto-Selektion abgefangen.
    /// </summary>
    IEnumerable<object> Handle(ConnectionEstablished evt, MessageContext ctx)
    {
        yield return new GetProduktionsTage();
    }

    // ═══════════════════════════════════════════════════
    // HANDLE — Query-Responses
    // ═══════════════════════════════════════════════════

    /// <summary>
    /// Wenn Tagesliste geladen und noch kein Tag gewählt:
    /// Ersten Tag automatisch selektieren + Strip laden.
    ///
    /// Läuft NACH ChartStore.Handle(ProduktionsTageAntwort) —
    /// Store hat ProduktionsTage bereits gesetzt.
    /// </summary>
    IEnumerable<object> Handle(ProduktionsTageAntwort antwort, MessageContext ctx)
    {
        if (_chart.GewaehlterTag != null) yield break;
        if (antwort.Tage.Count == 0) yield break;

        var tag = antwort.Tage[0].Datum;
        yield return new TagGewaehlt(tag);

        var von = new DateTimeOffset(tag.Date, TimeSpan.Zero);
        yield return new GetProduktionsStrip(von, von.AddDays(1));
    }
}
