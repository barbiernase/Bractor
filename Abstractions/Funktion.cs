namespace Abstractions;

/// <summary>
/// Marker einer FUNKTION (Plan Pub/Sub-Neustrukturierung §2.2, §3.3; Code-Fakt über <see cref="Funktionsvertrag"/>):
/// je Eingangstyp eine Methode <c>Auf(TEingang e[, Herkunft h][, Lese-Fähigkeiten…][,
///   CancellationToken])</c> mit freier Ausgabe in der Signatur — ein konkreter Typ, <c>OneOf&lt;…&gt;</c>, <c>T?</c> (nichts), ein Strom
///   (<c>IEnumerable</c>/<c>IAsyncEnumerable</c>), je auch als <c>Task&lt;…&gt;</c>. Als <b>Klasse</b> läuft sie im Host (Dienste im Konstruktor), als
///   <b>Schnittstelle</b> draußen (Worker, Client, Hand) oder mit einer Implementierung im Host — der Laufort steht in der Umgebung. Eingang ist ein
///   Event (auch eine Quell-Nachricht) oder ein Hinweis; ausgegeben werden Commands, Events und Hinweise. Position, Fortschritt, Gleichzeitigkeit,
///   Wiederholung und das Festhalten der Ausgabe (R12) trägt der Funktions-Actor des Frameworks.
/// </summary>
public interface IFunktion { }
