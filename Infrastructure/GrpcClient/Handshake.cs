using Infrastructure.Mapping;

/// <summary>
/// Die Ermittlung am Handshake: was der Client nennt (<c>message_types</c>: Events, die er hören, Commands und Queries, die er senden will),
/// nach Kategorie sortiert. Wirksam wird daraus erst Vertrag ∩ Token
/// (<see cref="Infrastructure.Akteure.ClientVertragsPruefung.Wende"/>, <see cref="Infrastructure.Akteure.AkteurTor.Wende"/>) — mit Vertrag zählt
/// die Tabelle des Servers, nicht die Selbstauskunft.
/// </summary>
public static class Handshake
{
    public static CapabilitiesResult Ermittle(Cqrs.Leitung.CapabilitiesRequest request, string sessionId)
    {
        var result = new CapabilitiesResult { SessionId = sessionId };
        foreach (var typeName in request.MessageTypes)
        {
            switch (MessageTypeMapping.Resolve(typeName).Category)
            {
                case MessageCategory.Event: result.SubscribedEvents.Add(typeName); break;
                case MessageCategory.Command: result.AllowedCommands.Add(typeName); break;
                case MessageCategory.Query: result.AllowedQueries.Add(typeName); break;
                default: result.UnknownTypes.Add(typeName); break;
            }
        }
        return result;
    }

    public static Cqrs.Leitung.CapabilitiesResponse Antwort(CapabilitiesResult result)
    {
        var response = new Cqrs.Leitung.CapabilitiesResponse { SessionId = result.SessionId };
        response.AllowedCommands.AddRange(result.AllowedCommands);
        response.SubscribedEvents.AddRange(result.SubscribedEvents);
        response.SupportedQueries.AddRange(result.AllowedQueries);
        response.UnknownTypes.AddRange(result.UnknownTypes);
        return response;
    }
}

/// <summary>Das Ergebnis des Handshakes einer Sitzung (nach Vertrag ∩ Token: was sie senden, fragen und hören darf).</summary>
public class CapabilitiesResult
{
    public List<string> AllowedCommands { get; init; } = new();
    public List<string> SubscribedEvents { get; init; } = new();
    public List<string> AllowedQueries { get; init; } = new();
    public List<string> UnknownTypes { get; init; } = new();

    public string SessionId { get; init; } = string.Empty;
}
