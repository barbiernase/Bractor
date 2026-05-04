namespace Client.Infrastructure.Connection;

/// <summary>
/// Löst einen Server-Dateipfad in eine für den Client
/// konsumierbare Form auf.
///
/// Implementierung ist Host-spezifisch:
///   Blazor Server → relative URL zum eigenen File-Endpunkt
///   Desktop WPF   → lokaler Dateipfad oder Data-URI
///   MAUI          → Platform-spezifischer Dateizugriff
///
/// Das Framework kennt keine Implementierung.
/// Der Host registriert seine Variante im DI.
/// </summary>
public interface IFilePathResolver
{
    /// <summary>
    /// Löst einen Server-Dateipfad in eine konsumierbare Referenz auf.
    ///
    /// Bewusst async — ein S3-Resolver müsste Presigned URLs holen,
    /// ein CDN-Resolver könnte Health-Checks machen.
    /// Lokale Resolver geben Task.FromResult zurück.
    /// </summary>
    Task<ResolvedFile> ResolveAsync(
        string path, CancellationToken ct = default);
}

/// <summary>
/// Ergebnis einer Pfadauflösung.
///
/// AccessUrl:   Die konsumierbare Referenz (URL, Pfad, Data-URI).
/// ContentType: MIME-Typ der Datei.
/// SizeBytes:   Dateigröße in Bytes (null wenn unbekannt).
/// </summary>
public record ResolvedFile(
    string AccessUrl,
    string ContentType,
    long? SizeBytes);