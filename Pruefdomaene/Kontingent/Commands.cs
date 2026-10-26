using Abstractions;

namespace Pruefdomaene.Kontingent;

public record RichteKontingentEin(Id<Kontingent> AggregateId, int Plaetze) : ICommand<Kontingent>, ICreationCommand;

/// <summary>Belegt Plätze — scheitert, wenn nicht genug frei sind (Ablehnung → Kompensation im Prozess).</summary>
public record BelegeKontingent(Id<Kontingent> AggregateId, int Plaetze) : ICommand<Kontingent>;

/// <summary>Gibt belegte Plätze wieder frei (Gegenzug der Belegung).</summary>
public record EntlasteKontingent(Id<Kontingent> AggregateId, int Plaetze) : ICommand<Kontingent>;
