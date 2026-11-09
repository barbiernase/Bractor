using Abstractions;

namespace Pruefdomaene.Messung;

/// <summary>Ein Messwert einer Messstelle (der erste legt sie an).</summary>
public record ErfasseMesswert(Id<Messstelle> AggregateId, double Wert) : ICommand<Messstelle>;

/// <summary>Die Schätzung, die der Schätzer zu einem Messwert abgibt (ein Knoten des Graphen <c>Merkmalbildung</c>, docs/04 §4.4).</summary>
public record VermerkeSchaetzung(Id<Messstelle> AggregateId, double Erwartet, int Anzahl) : ICommand<Messstelle>;

/// <summary>Ein Messwert über der Schwelle (die Prüfung rechnet im Host oder draußen).</summary>
public record MeldeAusreisser(Id<Messstelle> AggregateId, double Wert) : ICommand<Messstelle>;


/// <summary>
/// Ein Client fordert die Hochrechnung eines Werts für eine Messstelle an — eine Nachricht der Domäne, die eine Kopf-Übersetzung in den Eingang des
/// Graphen <c>Hochrechnung</c> aus dem Katalog Rechenwerk übersetzt; das Ergebnis kommt als Ausreißer an die Messstelle (docs/plan-ohne-pipeline.md §3).
/// </summary>
public sealed record HochrechnungAngefordert(Id<Messstelle> Stelle, double Wert) : ICommand;

/// <summary>Ein neuer Wert einer Messstelle — die Nachricht der Sachbearbeitung, übersetzt in den Eingang der Merkmalbildung (Katalog Pruefwerk).</summary>
public record NeuerWert(Id<Messstelle> Stelle, double Wert) : ICommand;
