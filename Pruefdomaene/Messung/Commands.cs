using Abstractions;

namespace Pruefdomaene.Messung;

/// <summary>Ein Messwert einer Messstelle (der erste legt sie an).</summary>
public record ErfasseMesswert(Id<Messstelle> AggregateId, double Wert) : ICommand<Messstelle>;

/// <summary>Die Schätzung, die das gelernte Modell zu einem Messwert abgibt (aus dem Hinweis-Netz, Plan §2.9).</summary>
public record VermerkeSchaetzung(Id<Messstelle> AggregateId, double Erwartet, int Anzahl) : ICommand<Messstelle>;

/// <summary>Ein Messwert über der Schwelle (die Prüfung rechnet im Host oder draußen).</summary>
public record MeldeAusreisser(Id<Messstelle> AggregateId, double Wert) : ICommand<Messstelle>;
