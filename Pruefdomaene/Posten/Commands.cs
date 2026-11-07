using Abstractions;

namespace Pruefdomaene.Posten;

public record EroeffnePosten(Id<Posten> AggregateId, int Bestand, int Mindestbestand = 0) : ICommand<Posten>, ICreationCommand;

/// <summary>Reserviert eine Menge für einen Beleg — reicht der Bestand nicht, ist die Absage ein Fakt (<see cref="ReservierungAbgelehnt"/>).</summary>
public record ReservierePosten(Id<Posten> AggregateId, int Menge, Beleg Beleg) : ICommand<Posten>;

/// <summary>Gibt eine Reservierung wieder frei (Gegenzug der Reservierung).</summary>
public record GibPostenFrei(Id<Posten> AggregateId, int Menge, Beleg Beleg) : ICommand<Posten>;

/// <summary>Bucht eine Menge auf den Bestand (Ziel einer Verteilung, Nachschub).</summary>
public record BuchePostenZu(Id<Posten> AggregateId, int Menge, Beleg Beleg) : ICommand<Posten>;
