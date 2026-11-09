using Abstractions;

namespace Pruefwerk.Katalog.Messung;

/// <summary>Ein Wert einer Stelle ist gemessen.</summary>
public record Messwert(Guid Stelle, double Wert) : ICommand;

/// <summary>Das Merkmal eines Werts (Gabel: geprüft und geschätzt).</summary>
public record Merkmal(Guid Stelle, double Wert) : ICommand;

/// <summary>Ein Wert über der Schwelle.</summary>
public record Ausreisser(Guid Stelle, double Wert) : ICommand;

/// <summary>Die Schätzung zu einem Wert: der erwartete Wert und aus wie vielen bisherigen.</summary>
public record Geschaetzt(Guid Stelle, double Erwartet, int Anzahl) : ICommand;
