using Abstractions;

namespace Pruefdomaene.Posten;

public record EroeffnePosten(Id<Posten> AggregateId, int Bestand, int Mindestbestand = 0) : ICommand<Posten>, ICreationCommand;

/// <summary>Reserviert eine Menge — scheitert, wenn nicht genug verfügbar ist (Ablehnung → Kompensation im Prozess).</summary>
public record ReservierePosten(Id<Posten> AggregateId, int Menge) : ICommand<Posten>;

/// <summary>Gibt eine Reservierung wieder frei (Gegenzug der Reservierung).</summary>
public record GibPostenFrei(Id<Posten> AggregateId, int Menge) : ICommand<Posten>;

/// <summary>Bucht eine Menge auf den Bestand (Fan-out-Ziel, Nachschub).</summary>
public record BuchePostenZu(Id<Posten> AggregateId, int Menge) : ICommand<Posten>;
