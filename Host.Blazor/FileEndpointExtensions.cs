using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.AspNetCore.StaticFiles;

namespace Host.Blazor;

/// <summary>
/// Generischer File-Endpunkt.
///
/// Der Browser schickt den absoluten Dateipfad als URL-Segment.
/// Der Endpunkt rekonstruiert den Pfad, prüft ob er in einem der
/// erlaubten Verzeichnisse liegt, und liefert die Datei per HTTP aus.
///
/// Unterstützt mehrere Basis-Pfade, z.B.:
///   - /mnt/gewebebilder/...    (Originale vom Netzwerk-Mount)
///   - /home/wirksam/cqrs-data/preprocessed/...  (lokal preprocessed)
/// </summary>
public static class FileEndpointExtensions
{
    public static IEndpointRouteBuilder MapCqrsFileEndpoint(
        this IEndpointRouteBuilder endpoints,
        params string[] allowedPaths)
    {
        var provider = new FileExtensionContentTypeProvider();
        var normalizedBases = allowedPaths
            .Where(p => !string.IsNullOrWhiteSpace(p))
            .Select(Path.GetFullPath)
            .ToArray();

        endpoints.MapGet("/api/files/{**path}",
            (string path) =>
            {
                // Catch-all schneidet führendes / ab → wiederherstellen
                var fullPath = "/" + path;

                // Path Traversal Schutz — Datei muss in einem der
                // erlaubten Verzeichnisse liegen
                var normalized = Path.GetFullPath(fullPath);
                var isAllowed = normalizedBases.Any(
                    basePath => normalized.StartsWith(basePath));

                if (!isAllowed)
                    return Results.Forbid();

                if (!File.Exists(fullPath))
                    return Results.NotFound();

                if (!provider.TryGetContentType(
                        fullPath, out var contentType))
                    contentType = "application/octet-stream";

                return Results.File(fullPath, contentType,
                    enableRangeProcessing: true);
            });

        return endpoints;
    }
}