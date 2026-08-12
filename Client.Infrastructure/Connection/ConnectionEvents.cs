using Client.Infrastructure.Abstractions;

namespace Client.Infrastructure.Connection;

/// <summary>
/// Verbindung zum Server erfolgreich hergestellt.
/// Wird nach dem Capabilities-Handshake publiziert.
/// </summary>
public record ConnectionEstablished(string SessionId) : IClientEvent;

/// <summary>
/// Verbindung zum Server verloren.
/// Wird bei gRPC-Fehlern oder explizitem Disconnect publiziert.
/// </summary>
public record ConnectionLost(string? Reason) : IClientEvent;

/// <summary>
/// Reconnect-Versuch wird unternommen.
/// </summary>
public record ReconnectAttempt(int Attempt) : IClientEvent;

/// <summary>
/// Ein Command konnte nicht an den Server gesendet werden.
/// Z.B. bei Verbindungsabbruch während des Sends.
/// </summary>
public record CommandSendFailed(
    string CommandType,
    Guid AggregateId,
    string ErrorMessage) : IClientEvent;

/// <summary>
/// ★ T2b: Ein Command wurde gesendet, aber nach mehreren Versuchen kam KEINE korrelierte Antwort
/// (weder Event noch CommandFailed). Der Ausgang ist UNBEKANNT — der Command könnte angewandt und nur
/// das Event verloren sein. Bewusst KEIN „Fehlgeschlagen": ein erneutes Senden ist dank deterministischer
/// CommandId server-seitig idempotent (kein Doppel-Effekt). Die UI kann den Nutzer um Rückversicherung
/// bitten oder still den Read-Model-Stand abwarten.
/// </summary>
public record CommandUnbestaetigt(
    string CommandType,
    Guid AggregateId,
    int Versuche) : IClientEvent;