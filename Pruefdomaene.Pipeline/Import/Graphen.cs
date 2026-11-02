using Abstractions;

namespace Pruefdomaene.Pipeline.Import;

/// <summary>Ein Import: angefordert (Quell-Nachricht) wird er angelegt; zu laufen (Command aus dem Decide) liest er seine Zeilen.</summary>
public sealed record Importablauf(Importannahme Annahme, Importlauf Lauf) : IGraph;
