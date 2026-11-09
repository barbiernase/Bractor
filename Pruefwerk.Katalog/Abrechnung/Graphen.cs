using Abstractions;

namespace Pruefwerk.Katalog.Abrechnung;

/// <summary>Freigabe und Mahnung, je ein Kopf — domänenneutral; die Übersetzungen der Domäne geben hinein und nehmen heraus.</summary>
public sealed record Abrechnungsablauf([Kopf] Freigabepruefung Freigabe, [Kopf] Mahnlauf Mahnwesen) : IGraph;
