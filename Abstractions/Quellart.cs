namespace Abstractions;

/// <summary>
/// Marker einer QUELLART (docs/04 §4.7): eine Klasse mit <c>Lausche(CancellationToken)</c>, die meldet, was sie draußen findet — je Element eine
/// <see cref="IMeldung"/>. Ihr Konstruktor nimmt dasselbe wie der einer Funktion: Konfig-Records (die Werte des Eingangs), <c>IZustand&lt;T&gt;</c> (was sie
/// gesehen hat), Technik und <c>ILogger&lt;T&gt;</c>.
/// <code>
/// public sealed class Dateiwaechter(Dateiwaechterkonfig konfig, IZustand&lt;Gesehen&gt; gesehen) : IQuellart
/// {
///     public async IAsyncEnumerable&lt;DateiGefunden&gt; Lausche([EnumeratorCancellation] CancellationToken ct) { … }
/// }
/// </code>
/// Eine Quellart ist nie ein Knoten eines Graphen. Sie VERWIRKLICHT einen Akteur: eine Übersetzung der Akteur-Seite (<c>ITranslate&lt;TMeldung, TZiel&gt;</c>
/// in <c>&lt;Wurzel&gt;.Akteure/Uebersetzungen.cs</c>) macht aus der Meldung eine Nachricht, die der Akteur darf; welche Quellarten ein Akteur haben kann,
/// folgt daraus. Laufen tut sie als EINGANG eines Clients in der Umgebung (Art, Akteur, Rechner, Werte) — der Läufer gibt ihre Nachricht hinein wie
/// eine App, im Namen des Akteurs. Die Quellarten des Frameworks stehen in <c>Core/Katalog</c> (Dateiwächter, Taktgeber, Webhook).
/// </summary>
public interface IQuellart { }
