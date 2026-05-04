using Infrastructure.Mapping;

public class CapabilitiesHandler
{
    /// <summary>
    /// Verarbeitet einen CapabilitiesRequest.
    /// </summary>
    public CapabilitiesResult Handle(ProtoRepo.CapabilitiesRequest request, string sessionId)
    {
        var requestedEventTypes = request.EventTypes.ToList();
        var validEventTypes = new List<string>();
        var unknownEventTypes = new List<string>();

        // 1. Event-Namen validieren
        foreach (var eventTypeName in requestedEventTypes)
        {
            if (MessageTypeMapping.IsKnownEventType(eventTypeName))
            {
                validEventTypes.Add(eventTypeName);
            }
            else
            {
                unknownEventTypes.Add(eventTypeName);
            }
        }

        // 2. Erlaubte Commands ermitteln
        var allowedCommands = MessageTypeMapping.GetAllowedCommandNames(validEventTypes);

        // 3. NEU: Unterstützte Queries ermitteln
        var supportedQueries = MessageTypeMapping.GetAllQueryTypeNames();

        // 4. Result bauen
        return new CapabilitiesResult
        {
            AllowedCommands = allowedCommands.ToList(),
            SubscribedEvents = validEventTypes,
            UnknownEventTypes = unknownEventTypes,
            SupportedQueries = supportedQueries.ToList(),  // NEU
            SessionId = sessionId
        };
    }

    /// <summary>
    /// Baut die Proto-Response aus dem Result.
    /// </summary>
    public ProtoRepo.CapabilitiesResponse BuildResponse(CapabilitiesResult result)
    {
        var response = new ProtoRepo.CapabilitiesResponse
        {
            SessionId = result.SessionId
        };
        
        response.AllowedCommands.AddRange(result.AllowedCommands);
        response.SubscribedEvents.AddRange(result.SubscribedEvents);
        response.SupportedQueries.AddRange(result.SupportedQueries);  // NEU
        
        return response;
    }
}

/// <summary>
/// Ergebnis der Capabilities-Verarbeitung.
/// </summary>
public class CapabilitiesResult
{
    public List<string> AllowedCommands { get; init; } = new();
    public List<string> SubscribedEvents { get; init; } = new();
    public List<string> UnknownEventTypes { get; init; } = new();
    public List<string> SupportedQueries { get; init; } = new();  // NEU
    public string SessionId { get; init; } = string.Empty;
}