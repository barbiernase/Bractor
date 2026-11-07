using Abstractions;

namespace Pruefdomaene.Pipeline.Import;

/// <summary>Ein Import: angefordert (Quell-Nachricht) wird er angelegt; zu laufen (ein Client reagiert auf den Start) liest er seine Zeilen.</summary>
public sealed record Importablauf([Kopf] Importannahme Annahme, [Kopf] Importlauf Lauf) : IGraph;
