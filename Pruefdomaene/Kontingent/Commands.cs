using Abstractions;

namespace Pruefdomaene.Kontingent;

public record RichteKontingentEin(Guid AggregateId, int Plaetze) : ICreationCommand;

/// <summary>Belegt Plätze — scheitert, wenn nicht genug frei sind (Ablehnung → Kompensation im Prozess).</summary>
public record BelegeKontingent(Guid AggregateId, int Plaetze) : ICommand;

/// <summary>Gibt belegte Plätze wieder frei (Gegenzug der Belegung).</summary>
public record EntlasteKontingent(Guid AggregateId, int Plaetze) : ICommand;
