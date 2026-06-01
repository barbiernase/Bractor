using Infrastructure.Mapping;

public class CapabilitiesHandler
{
    /// <summary>
    /// Verarbeitet einen CapabilitiesRequest.
    /// 
    /// Universell: Akzeptiert alle Typ-Namen und kategorisiert automatisch.
    /// - Events → Teilnehmer will empfangen (PubSub-Subscription)
    /// - Commands → Teilnehmer will senden (Routing an Aggregate)
    /// - Triggers → Teilnehmer will senden (Routing an Pipeline)
    /// - Queries → Teilnehmer will senden (Routing an QueryService)
    /// 
    /// Abwärtskompatibel: Wenn message_types leer, fällt auf event_types zurück
    /// und leitet Commands daraus ab (altes Verhalten für Blazor-Client).
    /// </summary>
    public CapabilitiesResult Handle(ProtoRepo.CapabilitiesRequest request, string sessionId)
    {
        var result = new CapabilitiesResult { SessionId = sessionId };

        // Abwärtskompatibilität: message_types hat Vorrang
        if (request.MessageTypes.Any())
        {
            // Neuer Pfad: universelle Registrierung
            foreach (var typeName in request.MessageTypes)
            {
                var (type, category) = MessageTypeMapping.Resolve(typeName);
                switch (category)
                {
                    case MessageCategory.Event:
                        result.SubscribedEvents.Add(typeName);
                        break;
                    case MessageCategory.Command:
                        result.AllowedCommands.Add(typeName);
                        break;
                    case MessageCategory.Trigger:
                        result.AllowedTriggers.Add(typeName);
                        break;
                    case MessageCategory.Query:
                        result.AllowedQueries.Add(typeName);
                        break;
                    default:
                        result.UnknownTypes.Add(typeName);
                        break;
                }
            }
        }
        else
        {
            // Alter Pfad: event_types → abgeleitete Commands (Blazor-Client)
            foreach (var eventTypeName in request.EventTypes)
            {
                if (MessageTypeMapping.IsKnownEventType(eventTypeName))
                    result.SubscribedEvents.Add(eventTypeName);
                else
                    result.UnknownTypes.Add(eventTypeName);
            }
            
            var allowedCommands = MessageTypeMapping.GetAllowedCommandNames(result.SubscribedEvents);
            result.AllowedCommands.AddRange(allowedCommands);
            result.AllowedQueries.AddRange(MessageTypeMapping.GetAllQueryTypeNames());
        }

        return result;
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
        response.SupportedQueries.AddRange(result.AllowedQueries);
        response.AllowedTriggers.AddRange(result.AllowedTriggers);
        response.UnknownTypes.AddRange(result.UnknownTypes);
        
        return response;
    }
}

/// <summary>
/// Ergebnis der Capabilities-Verarbeitung.
/// Universell: kann alle Nachrichtenkategorien enthalten.
/// </summary>
public class CapabilitiesResult
{
    public List<string> AllowedCommands { get; init; } = new();
    public List<string> SubscribedEvents { get; init; } = new();
    public List<string> AllowedTriggers { get; init; } = new();
    public List<string> AllowedQueries { get; init; } = new();
    public List<string> UnknownTypes { get; init; } = new();
    public string SessionId { get; init; } = string.Empty;
}