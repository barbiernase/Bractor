namespace Abstractions;

/// <summary>
/// Marker einer FUNKTION (docs/10 §10.3; Code-Fakt über <see cref="Funktionsvertrag"/>): je Eingangstyp eine Methode
/// <c>Auf(TEingang e[, Herkunft h][, Lese-Fähigkeiten…][, CancellationToken])</c> mit freier Ausgabe in der Signatur — ein konkreter Typ,
/// <c>OneOf&lt;…&gt;</c>, <c>T?</c> (nichts), ein Strom (<c>IEnumerable</c>/<c>IAsyncEnumerable</c>), je auch als <c>Task&lt;…&gt;</c>. Eingang ist eine
/// Folge oder ein Zwischenergebnis (schlichte Records), eine Quell-Nachricht, ein Hinweis oder — als Antwort auf einen eigenen Command — ein Event;
/// ausgegeben werden Commands, Zwischenergebnisse und Hinweise. Sie steht vor dem Aggregat und liest nie das Log (docs/04 §4.4); Gleichzeitigkeit und
/// Wiederholung tragen Parent und Kinder. Sie hat einen Laufort (Host, Extern, Hand) und kann einem Akteur zum Rechnen gegeben werden
/// (<c>IDarf&lt;F&gt;</c>).
/// <para>Als <b>Klasse</b> ist sie ihre eigene Umsetzung; als <b>Schnittstelle</b> hat sie Umsetzungen (öffentliche Klassen, die sie implementieren) —
/// welche läuft, wählt die Umgebung (<c>Funktionen:&lt;Name&gt;:Implementierung</c>, Default: die einzige); eine Schnittstelle kann auch draußen laufen
/// (Worker, Client, Hand).</para>
/// <para>Ihr Konstruktor nimmt Konfig-Records (<c>Konfig:&lt;Record&gt;</c>), <c>ILogger&lt;T&gt;</c>, ihren Zustand (<c>IZustand&lt;T&gt;</c>) und
/// <b>Technik</b>: jede andere Schnittstelle oder Klasse der Anwendung, ohne Marker — Bild skalieren, ein Modell laden, einen Brief versenden. Technik
/// läuft im Prozess ihres Halters, je Aktivierung neu gebaut und mit ihr freigegeben (<c>IAsyncDisposable</c>/<c>IDisposable</c>); was sie laden muss,
/// lädt sie beim ersten Aufruf. Geschachtelt nimmt eine Technik-Umsetzung selbst Technik. Gebunden wird generiert, ohne Reflection (CQRS090).</para>
/// </summary>
public interface IFunktion { }
