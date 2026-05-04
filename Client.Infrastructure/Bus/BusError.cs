using Client.Infrastructure.Abstractions;

namespace Client.Infrastructure.Bus;

/// <summary>
/// Wird publiziert wenn ein async-Subscriber eine Exception wirft.
/// Stores/ViewModels können darauf reagieren (z.B. Fehlermeldung anzeigen).
/// 
/// Wird immer auf dem UI-Thread publiziert (via PostToSyncContext).
/// </summary>
public record BusError(
    string MessageType,
    string ErrorMessage,
    Exception? Exception = null) : IClientEvent;