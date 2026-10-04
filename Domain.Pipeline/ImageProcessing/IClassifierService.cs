using Abstractions;
using Domain.ImagePair;

namespace Domain.Pipeline.ImageProcessing;

/// <summary>
/// Domain-Service für KI-Klassifikation — und ein AKTEUR: die KI entscheidet, wie ein Bildpaar klassifiziert wird
/// (docs/konzept-akteure.md). Der Pipeline-Handle, der sie als Parameter nimmt, entscheidet in ihrem Auftrag; der Emit
/// stempelt sie als Urheber. Die konkrete Implementierung (HTTP, gRPC, lokales Modell) ist austauschbar über DI.
/// </summary>
public interface IClassifierService : IAkteur, IDarf<KlassifiziereBildPaarDurchKi>
{
    Task<ClassificationResult> ClassifyPairAsync(Guid pairId);
}

/// <summary>
/// Ergebnis einer KI-Klassifikation.
/// Wird vom Pipeline-Handler ausgewertet und in einen Command umgewandelt.
/// </summary>
public record ClassificationResult(Klassifikation Label);