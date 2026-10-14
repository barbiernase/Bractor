using Abstractions;

namespace Pruefdomaene.Posten;

public record EroeffnePosten(Guid AggregateId, int Bestand) : ICreationCommand;

/// <summary>Reserviert eine Menge — scheitert, wenn nicht genug verfügbar ist (Ablehnung → Kompensation im Prozess).</summary>
public record ReservierePosten(Guid AggregateId, int Menge) : ICommand;

/// <summary>Gibt eine Reservierung wieder frei (Gegenzug der Reservierung).</summary>
public record GibPostenFrei(Guid AggregateId, int Menge) : ICommand;

/// <summary>Bucht eine Menge auf den Bestand (Fan-out-Ziel).</summary>
public record BuchePostenZu(Guid AggregateId, int Menge) : ICommand;
