// ── Domain.Client/ImagePair/Workspace/ImagePairWorkspace.cs ──

using Client.Infrastructure.Abstractions;
using Domain.Projections;

namespace Domain.Client.ImagePair;

/// <summary>
/// Root-Store des ImagePair-Feature.
///
/// Hält alle Kind-Stores als public Properties.
/// Die Deklarationsreihenfolge = Subscription-Reihenfolge (depth-first).
/// Der WiringGenerator erkennt die Baum-Struktur und generiert SubscribeAll.
///
/// Shared Selectors:
///   Abgeleitete Properties die von mehreren Sections benötigt werden.
///   Statt Duplikation in HeaderSection + StageSection + HistorieSection
///   liegt die Berechnung einmal hier.
/// </summary>
public partial class ImagePairWorkspace : StoreBase
{
    // ═══════════════════════════════════════════════════
    // Kind-Stores (Subscription-Reihenfolge)
    // ═══════════════════════════════════════════════════

    public ListStore List { get; }
    public NavigationStore Nav { get; }
    public ChartStore Chart { get; }
    public HistorieStore Historie { get; }
    public StatistikStore Statistik { get; }
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

    // ═══════════════════════════════════════════════════
    // Shared Selectors
    // ═══════════════════════════════════════════════════

    /// <summary>
    /// Das aktuell ausgewählte ImagePair-Record.
    /// Genutzt von: HeaderSection (Zeitstempel), StageSection (Bilder/Debug).
    /// </summary>
    public ImagePairRecord? AktuellerRecord =>
        Nav.AktuelleId is { } id ? List.FindById(id) : null;

    /// <summary>Ob ein Paar ausgewählt ist.</summary>
    public bool HatAuswahl => Nav.AktuelleId != null;

    /// <summary>
    /// Globale Position des ausgewählten Paars (1-basiert).
    /// Genutzt von: HeaderSection (Position-Anzeige).
    /// </summary>
    public int AktuellePosition =>
        (List.Seite - 1) * List.SeitenGroesse + Nav.SelectedIndex + 1;

    /// <summary>
    /// Historie-Einträge für das aktuell ausgewählte Paar.
    /// Genutzt von: HistorieSection.
    /// </summary>
    public IReadOnlyList<HistorieEintrag> AktuelleHistorie =>
        Nav.AktuelleId is { } id ? Historie.GetHistorie(id) : [];

    /// <summary>
    /// Ob die Historie für das aktuelle Paar geladen ist.
    /// Genutzt von: HistorieSection.
    /// </summary>
    public bool AktuelleHistorieGeladen =>
        Nav.AktuelleId is { } id && Historie.IstGeladen(id);
}
