using Domain.ImagePair;

namespace Domain.Pipeline.ImageProcessing;

/// <summary>
/// Domain-Service für KI-Klassifikation.
/// Wird vom Pipeline-Handler aufgerufen, die konkrete Implementierung
/// (HTTP, gRPC, lokales Modell) ist austauschbar über DI.
/// </summary>
public interface IClassifierService
{
    Task<ClassificationResult> ClassifyPairAsync(Guid pairId);
}

/// <summary>
/// Ergebnis einer KI-Klassifikation.
/// Wird vom Pipeline-Handler ausgewertet und in einen Command umgewandelt.
/// </summary>
public record ClassificationResult(Klassifikation Label);