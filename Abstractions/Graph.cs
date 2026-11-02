namespace Abstractions;

/// <summary>
/// Marker eines GRAPHEN (docs/04 §4.4): <c>record X(Knoten…) : IGraph</c> — jeder Parameter ist ein Knoten (Name = Konfig-Schlüssel, Typ = eine
/// Funktion oder ein anderer Graph), die Reihenfolge bedeutet nichts. Kanten gibt es nicht zum Schreiben: gibt ein Knoten einen Command aus, bekommt
/// ihn jeder Knoten des Graphen, der ihn nimmt; was kein Knoten nimmt, verlässt den Graphen (an ein Aggregat, an den Kopf eines anderen Graphen, ein
/// Hinweis). Die Eingänge, die kein Knoten erzeugt, sind seine Köpfe — je Eingangstyp genau einer. Ein Graph ist eine Blaupause: er läuft als
/// Instanz mit eigener Konfig (<c>umgebungen/&lt;name&gt;/graphen/&lt;instanz&gt;.json</c>), beliebig oft.
/// </summary>
public interface IGraph { }

/// <summary>
/// Ein Lauf eines Graphen ist gescheitert (docs/04 §4.4, Entscheidung 11): ein Knoten warf auch nach seinen Wiederholungen. Der Kopf ist geparkt, die
/// offenen Zweige sind abgebrochen; der Hinweis geht an die Akteure, die ihn hören.
/// </summary>
/// <param name="Instanz">Die Instanz des Graphen.</param>
/// <param name="Lauf">Die Kennung des Kopfs (des Eingangs, der den Lauf startete).</param>
/// <param name="Knoten">Der Knoten, der scheiterte (Pfad im Graphen).</param>
/// <param name="Grund">Die Meldung des letzten Fehlers.</param>
public sealed record LaufGescheitert(string Instanz, Guid Lauf, string Knoten, string Grund) : ITransientEvent;
