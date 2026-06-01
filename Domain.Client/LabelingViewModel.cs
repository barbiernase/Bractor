// ── Zielverzeichnis: Domain.Client/ImagePair/ViewModels/LabelingViewModel.cs ──

using Client.Infrastructure.Abstractions;
using Domain.ImagePair;

namespace Domain.Client.ImagePair;

/// <summary>
/// User-Intents für Labeling: Produkt und Kamera.
///
/// Beide Methoden geben nullable ICommand zurück —
/// null wenn kein Paar ausgewählt ist (Generator erzeugt null-Check).
///
/// Liest: ImagePairWorkspace (NavigationStore für AktuelleId)
/// </summary>
public partial class LabelingViewModel : IViewModel
{
    private readonly ImagePairWorkspace _workspace;

    public LabelingViewModel(ImagePairWorkspace workspace)
    {
        _workspace = workspace;
    }

    /// <summary>
    /// Physisches Produkt labeln (OK / Fraglich / Anomalie).
    /// Generator erzeugt: public void LabelProdukt(Klassifikation label)
    /// </summary>
    private LabelPhysischesProdukt? _labelProdukt(Klassifikation label)
        => _workspace.Nav.AktuelleId is { } id
            ? new LabelPhysischesProdukt(id, label)
            : null;

    /// <summary>
    /// Kamera-Bildpaar labeln (Erkennbar / Nicht erkennbar).
    /// Generator erzeugt: public void LabelKamera(Klassifikation label)
    /// </summary>
    private LabelBildPaar? _labelKamera(Klassifikation label)
        => _workspace.Nav.AktuelleId is { } id
            ? new LabelBildPaar(id, label)
            : null;
}
