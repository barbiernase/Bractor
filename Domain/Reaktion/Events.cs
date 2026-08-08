using Abstractions;

namespace Domain.Reaktion;

/// <summary>
/// Die Reaktion ist wirksam geworden. Trägt die deterministische Reaktions-Id, damit
/// der Empfänger beim Rehydrieren weiß, welche Reaktionen er bereits verarbeitet hat.
/// </summary>
public record ReaktionGewirkt(
    Guid ReaktionId,
    string Notiz,
    DateTimeOffset Zeitpunkt
) : IEvent;
