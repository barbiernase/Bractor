// ── Zielverzeichnis: Domain.Client/ImagePair/Workspace/ImagePairWorkspace.cs ──

using Client.Infrastructure.Abstractions;

namespace Domain.Client.ImagePair;

/// <summary>
/// Root-Store des ImagePair-Feature.
///
/// Hält alle Kind-Stores als public Properties.
/// Die Deklarationsreihenfolge = Subscription-Reihenfolge (depth-first).
/// Der WiringGenerator erkennt die Baum-Struktur und generiert SubscribeAll.
///
/// Subscription-Reihenfolge:
///   1. ImagePairWorkspace selbst  (kein eigener State)
///   2. ListStore                  (TrackedMap — alle Entity-Mutationen)
///   3. NavigationStore            (liest ListStore für Index-Berechnung)
///   4. ChartStore                 (Strip, Tage, Verlauf — unabhängig)
///   5. HistorieStore              (Live-Historie — unabhängig)
///   6. StatistikStore             (Zähler — unabhängig)
///   7. FeedbackStore              (Ablehnungen — unabhängig)
///
/// Die View injiziert den Workspace und erreicht alles über Properties:
///   Workspace.List.Count, Workspace.Nav.SelectedIndex, Workspace.Chart.Strip
/// </summary>
public partial class ImagePairWorkspace : StoreBase
{
    /// <summary>
    /// Aktuelle Seite der ImagePair-Records.
    /// Subscribed zuerst — alle anderen Stores können ListStore lesen.
    /// </summary>
    public ListStore List { get; }

    /// <summary>
    /// Navigation: SelectedIndex, Modus, Filter, _pendingIndex.
    /// Subscribed nach ListStore — kann ListStore.FindById/Count im Handle lesen.
    /// </summary>
    public NavigationStore Nav { get; }

    /// <summary>
    /// Chart-State: Strip, Tagesliste, Verlauf, gewählter Tag.
    /// Unabhängig von List und Nav.
    /// </summary>
    public ChartStore Chart { get; }

    /// <summary>
    /// Historie pro ImagePair: Server-Query + Live-Einträge.
    /// Unabhängig — reagiert auf Server-Events mit eigenen AggregateIds.
    /// </summary>
    public HistorieStore Historie { get; }

    /// <summary>
    /// Statistik-Zähler: Gesamt, Komplett, KI-Klassifiziert, etc.
    /// </summary>
    public StatistikStore Statistik { get; }

    /// <summary>
    /// Transientes Feedback: Ablehnungen, letzte Datei, Fehler.
    /// </summary>
    public FeedbackStore Feedback { get; }

    public ImagePairWorkspace(
        ListStore list,
        NavigationStore nav,
        ChartStore chart,
        HistorieStore historie,
        StatistikStore statistik,
        FeedbackStore feedback)
    {
        List = list;
        Nav = nav;
        Chart = chart;
        Historie = historie;
        Statistik = statistik;
        Feedback = feedback;
    }
}
