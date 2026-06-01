// ── Zielverzeichnis: Domain.Client/ImagePair/ViewModels/FilterViewModel.cs ──

using Client.Infrastructure.Abstractions;

namespace Domain.Client.ImagePair;

/// <summary>
/// User-Intents für Filter-Änderungen.
///
/// Emittiert einfache Wert-Events — der ListRefreshHandler baut daraus die Query.
/// Kein Store-Zugriff nötig.
///
/// Generator erzeugt:
///   public void SetFilter(string wert)
///   public IRelayCommand&lt;string&gt; SetFilterCommand
///   public void ToggleInspektionsFilter()
///   public IRelayCommand ToggleInspektionsFilterCommand
/// </summary>
public partial class FilterViewModel : IViewModel
{
    /// <summary>
    /// Filter wechseln: "alle", "offen", "anomalie".
    /// </summary>
    private FilterWertGeaendert _setFilter(string wert)
        => new(wert);

    /// <summary>
    /// Inspektionsfilter toggeln (nur nicht-inspizierte zeigen).
    /// </summary>
    private InspektionsfilterGetoggelt _toggleInspektionsFilter()
        => new();
}
