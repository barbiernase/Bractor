namespace Abstractions;

/// <summary>
/// Marker eines AUFRUFBAREN OBJEKTS — einer Funktion (docs/10 §10.1, docs/10 §10.3; Code-Fakt über <see cref="Funktionsvertrag"/>). Ob das
/// Framework sie ruft oder ihr Halter, sagen allein ihre Methoden — nie beides an einem Objekt:
/// <list type="bullet">
/// <item><b>Mit Eingängen</b>: je Eingangstyp eine Methode <c>Auf(TEingang e[, Herkunft h][, Lese-Fähigkeiten…][, CancellationToken])</c> mit
///   freier Ausgabe in der Signatur — ein konkreter Typ, <c>OneOf&lt;…&gt;</c>, <c>T?</c> (nichts), ein Strom (<c>IEnumerable</c>/<c>IAsyncEnumerable</c>),
///   je auch als <c>Task&lt;…&gt;</c>. Eingang ist ein Event (auch eine Quell-Nachricht) oder ein Hinweis; ausgegeben werden Commands, Events und
///   Hinweise. Das Framework ruft sie: Position, Fortschritt, Gleichzeitigkeit, Wiederholung und das Festhalten der Ausgabe (R12) trägt der
///   Funktions-Actor. Nur sie hat einen Laufort (Host, Extern, Hand) und kann einem Akteur zum Rechnen gegeben werden (<c>IDarf&lt;F&gt;</c>).</item>
/// <item><b>Gerufen</b> (kein <c>Auf</c>, frei benannte öffentliche Methoden): die Technik hinter einer Funktion — Bild skalieren, ein Modell laden,
///   einen Brief versenden. Sie steht im Konstruktor ihres Halters (einer Funktion oder eines anderen gerufenen Objekts, CQRS089) und läuft in
///   dessen Prozess, je Aktivierung des Halters neu gebaut und mit ihr freigegeben (<c>IAsyncDisposable</c>/<c>IDisposable</c>).</item>
/// </list>
/// Als <b>Klasse</b> ist sie ihre eigene Umsetzung; als <b>Schnittstelle</b> hat sie Umsetzungen (öffentliche Klassen, die sie implementieren) —
/// welche läuft, wählt die Umgebung (<c>Funktionen:&lt;Name&gt;:Implementierung</c>, Default: die einzige); eine Schnittstelle mit Eingängen kann
/// auch draußen laufen (Worker, Client, Hand). Ein Konstruktor nimmt gerufene Funktionen, Konfig-Records (<c>Konfig:&lt;Record&gt;</c>),
/// <c>ILogger&lt;T&gt;</c> und — nur eine Funktion mit Eingängen — ihren Zustand (<c>IZustand&lt;T&gt;</c>), CQRS090. Gebunden wird generiert, ohne
/// Reflection.
/// </summary>
public interface IFunktion { }
