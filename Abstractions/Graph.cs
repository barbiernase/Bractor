namespace Abstractions;

/// <summary>
/// Marker eines GRAPHEN (docs/04 §4.4): <c>record X(Knoten…) : IGraph</c> — jeder Parameter ist ein Knoten (Name = Konfig-Schlüssel, Typ = eine
/// Funktion oder ein anderer Graph), die Reihenfolge bedeutet nichts. Kanten gibt es nicht zum Schreiben: gibt ein Knoten einen Command aus, bekommt
/// ihn jeder Knoten des Graphen, der ihn nimmt; was kein Knoten nimmt, verlässt den Graphen (an ein Aggregat, an den Kopf eines anderen Graphen, ein
/// Hinweis). Die Eingänge, die kein Knoten erzeugt, sind seine Köpfe — je Eingangstyp genau einer. Ein Graph ist eine Blaupause: er läuft als
/// Instanz mit eigener Konfig (<c>umgebungen/&lt;name&gt;/graphen/&lt;instanz&gt;.json</c>), beliebig oft.
/// </summary>
public interface IGraph { }
