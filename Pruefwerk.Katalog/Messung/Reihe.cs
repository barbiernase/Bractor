namespace Pruefwerk.Katalog.Messung;

/// <summary>Die bisherigen Werte einer Stelle (leer, solange keine bekannt sind) — was die Fähigkeit <see cref="IHoleReihe"/> liefert.</summary>
public record Reihe(IReadOnlyList<double> Werte);
