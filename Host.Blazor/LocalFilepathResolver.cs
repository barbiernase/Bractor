using Client.Infrastructure.Connection;
using Microsoft.AspNetCore.StaticFiles;

namespace Host.Blazor;

/// <summary>
/// IFilePathResolver für Blazor Server.
///
/// Nimmt den absoluten Pfad aus dem Domain-Modell und baut
/// eine URL für den File-Endpunkt. Der Browser holt die Datei
/// per HTTP, der Endpunkt liest sie von Platte.
/// </summary>
public class LocalFilePathResolver : IFilePathResolver
{
    private readonly FileExtensionContentTypeProvider _contentTypes = new();

    public Task<ResolvedFile> ResolveAsync(
        string path, CancellationToken ct = default)
    {
        if (!File.Exists(path))
            throw new FileNotFoundException(
                $"File not found: {path}");

        if (!_contentTypes.TryGetContentType(
                path, out var contentType))
            contentType = "application/octet-stream";

        var size = new FileInfo(path).Length;

        // Absoluten Pfad als URL-Segment: führendes / abschneiden
        var urlPath = path.TrimStart('/');
        var escaped = string.Join("/",
            urlPath.Split('/').Select(Uri.EscapeDataString));

        return Task.FromResult(new ResolvedFile(
            $"/api/files/{escaped}",
            contentType,
            size));
    }
}