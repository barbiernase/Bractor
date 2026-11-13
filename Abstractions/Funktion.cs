namespace Abstractions;

/// <summary>
/// Marker einer FUNKTION (docs/04 §4.4, docs/10 §10.3; Code-Fakt über <see cref="Funktionsvertrag"/>): eine Klasse, je Operation eine Methode
/// <c>Auf(Nachricht[, Herkunft h][, Lese-Fähigkeiten…][, CancellationToken])</c>. Ein <c>Auf</c> nimmt genau eine Nachricht und rechnet je Ankunft:
/// einen Command ohne Ziel oder ein Event als Antwort auf einen eigenen Command an ein Aggregat — kein <c>T?</c>, <c>OneOf</c> oder
/// <c>IReadOnlyList</c> als Eingang (GR-EINGANG, CQRS100); zusammengeführt wird im Logik-Code. Die Fälle der Rückgabe sind die Ausgänge — ein
/// konkreter Typ, <c>OneOf&lt;…&gt;</c>, ein Strom (<c>IEnumerable</c>/<c>IAsyncEnumerable</c>), <c>T</c> und <c>OneOf</c> auch als <c>Task&lt;…&gt;</c> —,
/// mit Commands und Hinweisen. Eine Funktion ist ein Knoten eines Graphen (<see cref="IGraph"/>), steht vor dem Aggregat, liest nie das Log und hört keine
/// Hinweise; Gleichzeitigkeit und Wiederholung tragen Parent und Kinder. Was draußen geschieht, meldet keine Funktion, sondern eine Quellart (<c>IQuellart</c>).
/// Was draußen gerechnet wird, ist keine Funktion, sondern Arbeit eines Akteurs (<c>IHoert</c> eines Commands ohne Ziel).
/// <para>Ihr Konstruktor nimmt Konfig-Records (die Werte am Knoten der Instanz), <c>ILogger&lt;T&gt;</c>, ihren Zustand (<c>IZustand&lt;T&gt;</c>) und
/// <b>Technik</b>: jede andere Schnittstelle oder Klasse der Anwendung, ohne Marker — Bild skalieren, ein Modell laden, einen Brief versenden. Technik
/// läuft im Prozess ihres Halters, je Aktivierung neu gebaut und mit ihr freigegeben (<c>IAsyncDisposable</c>/<c>IDisposable</c>); was sie laden muss,
/// lädt sie beim ersten Aufruf. Geschachtelt nimmt eine Technik-Umsetzung selbst Technik. Gebunden wird generiert, ohne Reflection (CQRS090).</para>
/// </summary>
public interface IFunktion { }
