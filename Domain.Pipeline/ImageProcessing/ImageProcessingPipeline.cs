using Abstractions;
using Domain.ImagePair;
using Microsoft.Extensions.Logging;

namespace Domain.Pipeline.ImageProcessing;

/// <summary>
/// Bild-Eingang des KameraSystems — nur noch der Übersetzer am Rand: eine erkannte Datei (Trigger, nicht im Log) wird
/// gedeutet und als Commands ins Log gebracht. Die Aufbereitung selbst (Verkleinern, Histogramm-Ausgleich) ist KEIN Rumpf
/// mehr, sondern der <see cref="BildaufbereitungProzess"/> aus Katalog-Funktionen, der auf <c>RohbildEingegangen</c> läuft.
/// Die Commands tragen den Akteur des Triggers (KameraSystem, der einzige mit IDarf&lt;DateiErkannt&gt;).
/// Klassifiziert wird nicht hier: das tut der Akteur Klassifizierer draußen (hört ImagePairKomplett).
///
/// Die Dateinamen-Interpretation liegt hier — nicht im FileWatcher.
/// </summary>
public partial class ImageProcessingPipeline : IPipelineHandler
{
    private readonly ILogger<ImageProcessingPipeline> _logger;

    public ImageProcessingPipeline(ILogger<ImageProcessingPipeline> logger)
    {
        _logger = logger;
    }

    public string PipelineId => "image-processing";

    public IEnumerable<OneOf<ErstelleImagePair, NimmRohbildAuf>> Handle(DateiErkannt trigger, PipelineContext ctx)
    {
        var resolved = ImagePairFileName.Resolve(trigger.Dateiname);
        if (resolved == null)
        {
            _logger.LogWarning("Pipeline: Dateiname entspricht nicht der Convention, wird ignoriert: {File}", trigger.Dateiname);
            yield break;
        }

        // Das Paar anlegen (idempotent: die zweite Datei eines Paars wird am Aggregat abgelehnt) …
        yield return new ErstelleImagePair(
            resolved.AggregateId, resolved.PairKey, resolved.ProduziertAm, DateTimeOffset.UtcNow, trigger.Pfad);

        // … und das Rohbild dieser Version ins Log — ab hier übernimmt der Prozess.
        yield return new NimmRohbildAuf(
            resolved.AggregateId, resolved.Version, trigger.Pfad, trigger.Dateiname, trigger.DateigroesseBytes);
    }
}

public record PreprocessingConfig(string OutputPath);
