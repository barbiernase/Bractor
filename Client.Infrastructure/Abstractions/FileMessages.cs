namespace Client.Infrastructure.Abstractions;

/// <summary>
/// Generischer File-Request. Der User publiziert diesen Typ auf dem Bus.
/// Die FileBridge fängt ihn ab und löst den Pfad auf.
/// Der Path kommt aus Domain-Daten (z.B. BildVerfuegbar.Pfad).
///
/// Kein Marker-Interface nötig — die FileBridge subscribt direkt
/// auf diesen konkreten Typ.
/// </summary>
public record LoadFile(string Path);

/// <summary>
/// Antwort der FileBridge nach erfolgreicher Pfadauflösung.
///
/// SourcePath = der ursprüngliche Path aus LoadFile.
/// AccessUrl  = die konsumierbare Referenz (URL, Pfad, Data-URI).
///
/// Der User-Code nutzt immer AccessUrl. Was sich dahinter verbirgt —
/// relative Route, CDN-URL, lokaler Pfad — ist Resolver-Sache.
/// </summary>
public record FileLoaded(
    string SourcePath,
    string AccessUrl,
    string ContentType,
    long? SizeBytes
) : IClientEvent;

/// <summary>
/// Fehler-Event der FileBridge wenn die Pfadauflösung fehlschlägt.
/// Analog zu CommandSendFailed und QueryFailed.
/// </summary>
public record FileLoadFailed(
    string SourcePath,
    string ErrorMessage
) : IClientEvent;