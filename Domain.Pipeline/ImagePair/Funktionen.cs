using Abstractions;
using Domain.ImagePair;

namespace Domain.Pipeline.ImagePair;

/// <summary>
/// Ein komplettes Bildpaar mit dem aktiven Modell beurteilen (Strang 1, KI). Eine Funktion draußen: der Klassifikations-Worker trägt sie
/// (er verkörpert den Klassifizierer, der sie rechnen darf); jedes komplette Paar (die Folge des Bildpaars) geht an einen Arbeiter in der Werkstatt, die
/// erste Antwort wirkt — genau einmal, auch wenn zwei Träger antworten.
/// </summary>
public interface IBildpaarBeurteilung : IFunktion
{
    KlassifiziereBildPaarDurchKi Auf(PaarZuBeurteilen f);
}
