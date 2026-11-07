using Abstractions;

namespace Pruefdomaene.Messung;

/// <summary>Ein Messwert einer Messstelle (der erste legt sie an).</summary>
public record ErfasseMesswert(Id<Messstelle> AggregateId, double Wert) : ICommand<Messstelle>;

/// <summary>Die Schätzung, die der Schätzer zu einem Messwert abgibt (ein Knoten des Graphen <c>Merkmalbildung</c>, docs/04 §4.4).</summary>
public record VermerkeSchaetzung(Id<Messstelle> AggregateId, double Erwartet, int Anzahl) : ICommand<Messstelle>;

/// <summary>Ein Messwert über der Schwelle (die Prüfung rechnet im Host oder draußen).</summary>
public record MeldeAusreisser(Id<Messstelle> AggregateId, double Wert) : ICommand<Messstelle>;

