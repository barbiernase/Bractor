using Abstractions;

namespace Pruefwerk.Katalog.Import;

/// <summary>Annahme und Lauf eines Imports, je ein Kopf.</summary>
public sealed record Importablauf([Kopf] Annahme Annahme, [Kopf] Zeilenlauf Lauf) : IGraph;
