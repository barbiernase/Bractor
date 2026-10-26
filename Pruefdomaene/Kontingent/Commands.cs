using Abstractions;
using Pruefdomaene.Posten;

namespace Pruefdomaene.Kontingent;

public record RichteKontingentEin(Id<Kontingent> AggregateId, int Plaetze) : ICommand<Kontingent>, ICreationCommand;

/// <summary>Belegt Plätze für einen Beleg — reichen sie nicht, ist die Absage ein Fakt (<see cref="BelegungAbgelehnt"/>).</summary>
public record BelegeKontingent(Id<Kontingent> AggregateId, int Plaetze, Beleg Beleg) : ICommand<Kontingent>;

/// <summary>Gibt belegte Plätze wieder frei (Gegenzug der Belegung).</summary>
public record EntlasteKontingent(Id<Kontingent> AggregateId, int Plaetze, Beleg Beleg) : ICommand<Kontingent>;
