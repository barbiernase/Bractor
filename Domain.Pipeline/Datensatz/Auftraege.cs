using Abstractions;
using Domain.Datensatz;

namespace Domain.Pipeline.Datensatz;

/// <summary>Die Suche, die eine Range beschreibt (1:1 aus den Kriterien), durchpaginiert zu konkreten Paar-Ids auflösen.</summary>
public sealed record SucheRange(RangeKriterien Kriterien) : IAuftrag<IRangeSuche>;

/// <summary>Je Mitglied den Label-Stand und die Bildpfade lesen und den Split deterministisch zuteilen.</summary>
public sealed record FriereMitgliederEin(IReadOnlyList<Guid> Mitglieder, SplitKonfig Split) : IAuftrag<IMitgliederEinfrieren>;
