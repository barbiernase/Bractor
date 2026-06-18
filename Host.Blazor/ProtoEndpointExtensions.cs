// REPO-PFAD: Host.Blazor/ProtoEndpointExtensions.cs  (NEU)
// Registrierung in Program.cs: app.MapProtoEndpoint();
using System.Security.Cryptography;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace Host.Blazor;

/// <summary>
/// HTTP-Endpunkt für die Proto-Distribution (Laufzeit-Sicherheitsnetz).
///
/// Liefert das domain.proto und einen Versions-Hash an Python-Clients.
/// Der Python-Client prüft den Hash beim Start und regeneriert seine
/// Typen nur wenn sich das Proto geändert hat.
///
/// Endpunkte:
///   GET /api/proto/domain.proto  → die Proto-Datei
///   GET /api/proto/version       → SHA256-Hash
///   GET /api/proto/info          → Metadaten (Diagnose)
///
/// Die Proto-Datei wird von deploy.sh nach /opt/cqrs/proto/ kopiert.
/// </summary>
public static class ProtoEndpointExtensions
{
    private static string? _cachedHash;

    public static IEndpointRouteBuilder MapProtoEndpoint(
        this IEndpointRouteBuilder endpoints,
        string protoDir = "/opt/cqrs/proto")
    {
        var protoPath = Path.Combine(protoDir, "domain.proto");

        endpoints.MapGet("/api/proto/domain.proto", () =>
        {
            if (!File.Exists(protoPath))
                return Results.NotFound("domain.proto not found. Run deploy.sh first.");

            return Results.File(protoPath, "text/plain",
                fileDownloadName: "domain.proto");
        });

        endpoints.MapGet("/api/proto/version", () =>
        {
            if (!File.Exists(protoPath))
                return Results.NotFound("domain.proto not found");

            _cachedHash ??= ComputeHash(protoPath);
            return Results.Text(_cachedHash);
        });

        endpoints.MapGet("/api/proto/info", () =>
        {
            if (!File.Exists(protoPath))
                return Results.NotFound("domain.proto not found");

            _cachedHash ??= ComputeHash(protoPath);
            var info = new FileInfo(protoPath);

            return Results.Json(new
            {
                hash = _cachedHash,
                size_bytes = info.Length,
                modified_utc = info.LastWriteTimeUtc,
            });
        });

        Console.WriteLine($"[Setup] Proto-Endpoint: /api/proto/ → {protoPath}");
        return endpoints;
    }

    private static string ComputeHash(string path)
    {
        using var stream = File.OpenRead(path);
        var hash = SHA256.HashData(stream);
        return Convert.ToHexString(hash).ToLowerInvariant();
    }
}
