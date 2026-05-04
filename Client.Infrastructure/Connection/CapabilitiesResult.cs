namespace Client.Infrastructure.Connection;

/// <summary>
/// Ergebnis des Capabilities-Handshakes — Client-seitige Sicht.
///
/// Umbenannt von CapabilitiesResult zu ClientCapabilities,
/// da der Server bereits einen CapabilitiesResult im globalen Namespace hat.
/// </summary>
public record ClientCapabilities
{
    public required string SessionId { get; init; }
    public required IReadOnlyList<string> AllowedCommands { get; init; }
    public required IReadOnlyList<string> SubscribedEvents { get; init; }
    public required IReadOnlyList<string> SupportedQueries { get; init; }
    
    public static ClientCapabilities FromResponse(ProtoRepo.CapabilitiesResponse response)
    {
        return new ClientCapabilities
        {
            SessionId = response.SessionId,
            AllowedCommands = response.AllowedCommands.ToList(),
            SubscribedEvents = response.SubscribedEvents.ToList(),
            SupportedQueries = response.SupportedQueries.ToList()
        };
    }
}