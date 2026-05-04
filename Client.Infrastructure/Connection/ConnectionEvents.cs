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