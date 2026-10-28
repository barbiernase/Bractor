using Abstractions;

namespace Pruefdomaene.Kontingent;

/// <summary>Die freien Plätze des Kontingents — der Stand, auf dem eine Belegung entschieden würde.</summary>
public record KontingentStand(Id<Kontingent> Kontingent, int Frei) : IQueryResponse;

/// <summary>Das Kontingent ist (noch) nicht eingerichtet.</summary>
public record KontingentUnbekannt(Id<Kontingent> Kontingent) : IQueryResponse;
