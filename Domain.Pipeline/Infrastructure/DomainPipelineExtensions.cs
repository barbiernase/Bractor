using Domain.Pipeline.ImageProcessing;
using Infrastructure.Pipeline;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Proto;

namespace Domain.Pipeline.Infrastructure;

/// <summary>
/// DI-Registrierung für Pipeline-Komponenten.
///
/// Registriert:
///   1. Handler-Dependencies (Services die Pipeline-Handler brauchen)
///   2. Trigger-Actor-Registrierungen (welcher Actor, welcher Pfad, welches Ziel)
///
/// NICHT registriert (kommt von anderswo):
///   - Pipeline-Handler selbst → GeneratedPipelines (generiert)
///   - PipelineActorBase → Infrastructure
///   - PipelineStartupService → Infrastructure
///
/// Der Watch-Pfad kann per Parameter überschrieben werden. So bleibt die
/// Konfiguration im Program.cs (Blazor-Host / Scenario-Host / Tests) steuerbar,
/// ohne dass hier etwas geändert werden muss.
/// </summary>
public static class DomainPipelineExtensions
{
    /// <summary>
    /// Default-Pfad wurde bewusst entfernt — der Caller (Program.cs)
    /// muss den Pfad explizit aus der Konfiguration übergeben.
    /// Nur für Tests existiert ein Fallback.
    /// </summary>
    public const string DefaultWatchPath = "/data/input";

    public static IServiceCollection AddDomainPipelineServices(
        this IServiceCollection services,
        string? watchPath = null,
        string? preprocessedPath = null)
    {
        var effectivePath = watchPath ?? DefaultWatchPath;
        var effectivePreprocessed = preprocessedPath
            ?? Path.Combine(effectivePath, ".preprocessed");

        Console.WriteLine("[Domain] Registriere Pipeline-Services...");

        // ═══════════════════════════════════════════════════════
        // Handler-Dependencies
        // ═══════════════════════════════════════════════════════

        services.AddSingleton<IClassifierService, ClassifierService>();
        Console.WriteLine("  + IClassifierService");

        // Bildvorverarbeitung
        services.AddSingleton<IImageResizer, ImageResizer>();
        services.AddSingleton<IHistogramEqualizer, HistogramEqualizer>();
        services.AddSingleton(new PreprocessingConfig(effectivePreprocessed));
        Console.WriteLine($"  + IImageResizer, IHistogramEqualizer");
        Console.WriteLine($"  + PreprocessingConfig → {effectivePreprocessed}");

        // ═══════════════════════════════════════════════════════
        // Trigger-Actor-Registrierungen
        // ═══════════════════════════════════════════════════════

        services.AddSingleton<ITriggerRegistration>(new TriggerRegistration(
            $"FileWatcher-ImageProcessing ({effectivePath})",
            (provider, cluster) => Props.FromProducer(() =>
                new FileWatcherActor(
                    watchPath: effectivePath,
                    targetPipelineId: "image-processing",
                    cluster: cluster,
                    logger: provider.GetRequiredService<ILogger<FileWatcherActor>>()))
        ));
        Console.WriteLine($"  + FileWatcher → image-processing ({effectivePath})");

        Console.WriteLine();
        return services;
    }
}

/// <summary>
/// Konkrete Classifier-Implementierung.
/// In Produktion: HTTP-Client zu einem ML-Service.
/// Hier: Placeholder.
/// </summary>
internal class ClassifierService : IClassifierService
{
    public Task<ClassificationResult> ClassifyPairAsync(Guid pairId)
    {
        // TODO: Echte KI-Klassifikation
        return Task.FromResult(new ClassificationResult(
            Domain.ImagePair.Klassifikation.KeineAnomalie));
    }
}