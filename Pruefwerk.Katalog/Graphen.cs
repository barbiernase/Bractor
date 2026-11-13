using Abstractions;

namespace Pruefwerk.Katalog;

// ── Abrechnung ──

/// <summary>Freigabe und Mahnung, je ein Kopf — domänenneutral; die Übersetzungen der Domäne geben hinein und nehmen heraus.</summary>
public sealed record Abrechnungsablauf([Kopf] Freigabepruefung Freigabe, [Kopf] Mahnlauf Mahnwesen) : IGraph;

// ── Import ──

/// <summary>Annahme und Lauf eines Imports, je ein Kopf.</summary>
public sealed record Importablauf([Kopf] Annahme Annahme, [Kopf] Zeilenlauf Lauf) : IGraph;

// ── Messung ──

/// <summary>Die Merkmalbildung: das Merkmal am Kopf, dahinter eine Gabel — geprüft und geschätzt (der Schätzer mit Zustand im Zählwerk).</summary>
public sealed record Merkmalbildung([Kopf] Merkmalsbildung Bildung, [Von(nameof(Bildung))] Ausreisserpruefung Pruefung,
    [Von(nameof(Bildung))] Schaetzer Schaetzer) : IGraph;
