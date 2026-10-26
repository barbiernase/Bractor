using Abstractions;
using Domain.ImagePair;

namespace Domain.Pipeline.ImagePair;

/// <summary>
/// Ein komplettes Bildpaar mit dem aktiven Modell beurteilen (Strang 1, KI). Eine Funktion draußen: der Klassifikations-Worker trägt sie
/// (<c>ITraegt&lt;IBildpaarBeurteilung&gt;</c>), der Funktions-Actor bietet ihm jedes komplette Paar an, hält seine Antwort fest und sendet sie —
/// genau einmal, auch wenn zwei Träger antworten.
/// </summary>
public interface IBildpaarBeurteilung : IFunktion
{
    KlassifiziereBildPaarDurchKi Auf(ImagePairKomplett e);
}
