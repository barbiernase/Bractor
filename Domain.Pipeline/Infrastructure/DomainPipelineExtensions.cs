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
    /// <summary>
    /// Aus der Umgebung: der Konfig-Record <see cref="PreprocessingConfig"/> steht im Abschnitt <c>Konfig:PreprocessingConfig</c>
    /// (Struktur ist Code — der Record; der Wert ist Konfig der Umgebung).
    /// </summary>
    public static IServiceCollection AddDomainPipelineServices(this IServiceCollection services, Microsoft.Extensions.Configuration.IConfiguration konfig)
        => services.AddDomainPipelineServices(konfig[$"Konfig:{nameof(PreprocessingConfig)}:{nameof(PreprocessingConfig.OutputPath)}"]
            ?? throw new InvalidOperationException($"Konfig:{nameof(PreprocessingConfig)}:{nameof(PreprocessingConfig.OutputPath)} fehlt (Ausgabe der Bild-Funktionen)."));

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
