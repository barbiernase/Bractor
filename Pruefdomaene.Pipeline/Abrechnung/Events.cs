using Abstractions;
using Pruefdomaene.Abrechnung;

namespace Pruefdomaene.Pipeline.Abrechnung;

/// <summary>Hinweis des Mahnwesens: für diese Rechnung ist keine Mahnung nötig (verlierbar, höchstens einmal).</summary>
public record MahnungUnnoetig(Id<Rechnung> Rechnung) : ITransientEvent;
