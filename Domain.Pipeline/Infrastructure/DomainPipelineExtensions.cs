using Domain.Pipeline.ImageProcessing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Domain.Pipeline.Infrastructure;

/// <summary>
/// DI-Registrierung der Dienste hinter den Bild-Funktionen (OpenCV-Implementierungen). Die Datei-Quelle ist kein Domänen-Dienst mehr:
/// sie ist der fest codierte Datei-Treiber, konfiguriert unter <c>Quellen:DateiErkannt</c> (docs/konzept-werkbank.md §3).
/// </summary>
public static class DomainPipelineExtensions
{
    /// <param name="preprocessedPath">Wohin die Bild-Funktionen ihre Ergebnisse schreiben (Konfig der Funktions-Implementierung).</param>
    public static IServiceCollection AddDomainPipelineServices(this IServiceCollection services, string preprocessedPath)
    {
        services.AddSingleton<IImageResizer, ImageResizer>();
        services.AddSingleton<IHistogramEqualizer, HistogramEqualizer>();
        services.AddSingleton(new PreprocessingConfig(preprocessedPath));
        Console.WriteLine($"[Domain] Bild-Funktionen: IImageResizer, IHistogramEqualizer, PreprocessingConfig → {preprocessedPath}");
        return services;
    }
}
