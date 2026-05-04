using Abstractions;
using Domain.ImagePair;
using Microsoft.Extensions.Logging;
using OpenCvSharp;

namespace Domain.Pipeline.ImageProcessing;

/// <summary>
/// Orchestriert den Lebenszyklus eines ImagePairs:
///   1. DateiErkannt → Parsen → ErstelleImagePair + Preprocessing + MeldeBildVerfuegbar
///   2. ImagePairKomplett (Event) → KI-Klassifikation starten
///
/// WICHTIG: Die gesamte Dateinamen-Interpretation liegt hier — nicht im FileWatcher!
/// </summary>
public partial class ImageProcessingPipeline : IPipelineHandler
{
    private readonly IClassifierService _classifier;
    private readonly IImageResizer _resizer;
    private readonly IHistogramEqualizer _equalizer;
    private readonly string _preprocessedPath;
    private readonly ILogger<ImageProcessingPipeline> _logger;

    private const int PreviewHeight = 512;

    public ImageProcessingPipeline(
        IClassifierService classifier,
        IImageResizer resizer,
        IHistogramEqualizer equalizer,
        PreprocessingConfig config,
        ILogger<ImageProcessingPipeline> logger)
    {
        _classifier = classifier;
        _resizer = resizer;
        _equalizer = equalizer;
        _preprocessedPath = config.OutputPath;
        _logger = logger;

        Directory.CreateDirectory(_preprocessedPath);
    }

    public string PipelineId => "image-processing";

    // ═══════════════════════════════════════════════════
    // TRIGGER-HANDLER
    // ═══════════════════════════════════════════════════

    public async IAsyncEnumerable<ICommand> Handle(
        DateiErkannt trigger, PipelineContext ctx)
    {
        // ── 1. Dateinamen parsen ──

        var resolved = ImagePairFileName.Resolve(trigger.Dateiname);
        if (resolved == null)
        {
            _logger.LogWarning(
                "Pipeline: Dateiname entspricht nicht der Convention, wird ignoriert: {File}",
                trigger.Dateiname);
            yield break;
        }

        _logger.LogInformation(
            "Pipeline: {File} → PairKey={PairKey}, Version={Version}, ProduziertAm={ProduziertAm}",
            trigger.Dateiname, resolved.PairKey, resolved.Version, resolved.ProduziertAm);

        // ── 2. ImagePair erstellen (idempotent) ──

        yield return new ErstelleImagePair(
            resolved.AggregateId,
            resolved.PairKey,
            resolved.ProduziertAm,
            DateTimeOffset.UtcNow,
            trigger.Pfad);

        // ── 3. Preprocessing: TIFF → Resize → HistEq → PNG ──

        _logger.LogInformation(
            "Preprocessing: {File} → Resize({Height}) + HistEq",
            Path.GetFileName(trigger.Pfad), PreviewHeight);

        using var original = await Task.Run(() =>
            Cv2.ImRead(trigger.Pfad, ImreadModes.Color));

        if (original.Empty())
        {
            _logger.LogWarning(
                "Preprocessing: Konnte Datei nicht lesen: {Path}", trigger.Pfad);
            yield break;
        }

        using var resized = _resizer.Resize(original, PreviewHeight);
        using var equalized = _equalizer.Equalize(resized);

        var outputFileName = Path.GetFileNameWithoutExtension(trigger.Dateiname) + "_preview.png";
        var outputPath = Path.Combine(_preprocessedPath, outputFileName);

        await Task.Run(() =>
            Cv2.ImWrite(outputPath, equalized));

        _logger.LogInformation(
            "Preprocessing: ✔ {Input} → {Output} ({W}x{H})",
            Path.GetFileName(trigger.Pfad), outputFileName,
            equalized.Width, equalized.Height);

        // ── 4. Bild als verfügbar melden ──

        var meta = new BildMeta(
            OriginalDateiname: trigger.Dateiname,
            DateigroesseBytes: trigger.DateigroesseBytes,
            BreitePixel: equalized.Width,
            HoehePixel: equalized.Height,
            ErstelltAm: DateTimeOffset.UtcNow);

        yield return new MeldeBildVerfuegbar(
            resolved.AggregateId,
            resolved.Version,
            meta,
            outputPath);
    }

    // ═══════════════════════════════════════════════════
    // EVENT-HANDLER (PubSub)
    // ═══════════════════════════════════════════════════

    public async IAsyncEnumerable<ICommand> Handle(
        ImagePairKomplett evt, PipelineContext ctx)
    {
        var aggregateId = ctx.SourceAggregateId!.Value;
        var result = await _classifier.ClassifyPairAsync(aggregateId);
        yield return new KlassifiziereBildPaarDurchKi(aggregateId, result.Label);
    }

    public Task Handle(PaarNichtKomplett rejection, PipelineContext ctx)
    {
        _logger.LogWarning(
            "Pipeline: Paar {Id} noch nicht komplett, warte auf zweites Bild",
            ctx.SourceAggregateId);
        return Task.CompletedTask;
    }
}

public record PreprocessingConfig(string OutputPath);