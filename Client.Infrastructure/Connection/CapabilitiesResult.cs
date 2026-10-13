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
    /// <summary>Der Vertrag, den der Server angenommen hat, und SEIN Hash (weicht er ab, ist der Client gegen einen anderen Stand gebaut).</summary>
    public string Vertrag { get; init; } = "";
    public string VertragHash { get; init; } = "";
    
    public static ClientCapabilities FromResponse(ProtoRepo.CapabilitiesResponse response)
    {
        return new ClientCapabilities
        {
            SessionId = response.SessionId,
            AllowedCommands = response.AllowedCommands.ToList(),
            SubscribedEvents = response.SubscribedEvents.ToList(),
            SupportedQueries = response.SupportedQueries.ToList(),
            Vertrag = response.Vertrag,
            VertragHash = response.VertragHash,
        };
    }
}