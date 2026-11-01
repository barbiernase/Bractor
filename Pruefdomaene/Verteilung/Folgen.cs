using Abstractions;

namespace Pruefdomaene.Verteilung;

/// <summary>
/// Eine FOLGE (docs/03 §3.2): die Verteilung ist gestartet — die Abwicklung reserviert die Quelle. Was die Ziele darauf entscheiden, hört sie als
/// Antwort auf ihre Commands (docs/04 §4.4), nicht aus dem Log.
/// </summary>
public record VerteilungAbzuwickeln(Id<Verteilung> VerteilungId, Id<Posten.Posten> QuelleId, IReadOnlyList<Id<Posten.Posten>> Ziele, int MengeJeZiel);
