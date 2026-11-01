using Abstractions;

namespace Domain.Datensatz;

// FOLGEN des Datensatzes (docs/03 §3.2): was eine Entscheidung den Funktionen sagt, die sie hören — schlichte Records, nicht im Log.

/// <summary>Eine Range ist angefordert: die Kriterien sind gegen die Bildpaare aufzulösen.</summary>
public record RangeAufzuloesen(Id<Datensatz> Datensatz, RangeKriterien Kriterien);

/// <summary>Das Einfrieren ist angefordert: je (autoritativem) Mitglied der Label-Stand, dazu der Split.</summary>
public record EinfrierenAbzuschliessen(Id<Datensatz> Datensatz, IReadOnlyList<Id<ImagePair.ImagePair>> Mitglieder, SplitKonfig Split);
