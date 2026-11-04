using Abstractions;
using Pruefdomaene.Abrechnung;

namespace Pruefdomaene.Projections.Abrechnung;

/// <summary>Was die Bündel über eine Rechnung wissen: freigegeben (mit Betrag), gestellt und gutgeschrieben — null, solange ein Bündel unvollständig ist.</summary>
public record Rechnungsbild(Id<Rechnung> Rechnung, decimal? Freigegeben, decimal? Gestellt, decimal? Gutgeschrieben) : IQueryResponse;

/// <summary>Die Lage: der Grund der neuesten Sperre und der neueste Ausreißer — null, solange nicht beide da waren.</summary>
public record Lagebild(string? Sperrgrund, double? Ausreisser) : IQueryResponse;
