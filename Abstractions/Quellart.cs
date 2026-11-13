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
/// Eine Quellart lauscht (<c>Lausche</c>) — oder sie NIMMT AN (<see cref="IAnnahme{TMeldung}"/>): dann wartet nicht sie draußen, sondern der Host nimmt
/// ihre Meldungen entgegen.
/// </summary>
public interface IQuellart { }

/// <summary>
/// Marker einer ANNEHMENDEN Quellart (docs/04 §4.7): sie lauscht nicht — ihre Meldungen nimmt der HOST an, auf jedem Knoten, und gibt sie hinein wie
/// ein Läufer (dieselbe Übersetzung der Akteur-Seite, dieselben Ids aus der Kennung der Meldung). Was sie meldet, steht im Typargument (Code-Fakt);
/// ein <c>Lausche</c> hat sie nicht. Heute nimmt der Host über HTTP an: <c>POST /eingang/webhook/&lt;Pfad&gt;</c> auf dem Einblick-Port, als
/// <see cref="Aufruf"/> (Kennung aus dem Kopf <c>kennung</c>, Inhalt = der Körper) — darum meldet eine Annahme <see cref="Aufruf"/>, und ihre Konfig
/// nennt die Werte <see cref="Annahmevertrag.Pfad"/> und wahlweise <see cref="Annahmevertrag.Schluessel"/>.
/// <code>
/// public sealed class Webhook(Webhookkonfig konfig) : IAnnahme&lt;Aufruf&gt;;
/// </code>
/// </summary>
/// <typeparam name="TMeldung">Was die Annahme meldet.</typeparam>
public interface IAnnahme<TMeldung> : IQuellart where TMeldung : IMeldung { }

/// <summary>Die Werte einer Annahme, die der Host selbst liest (docs/04 §4.7): eine Quelle für Laufzeit, Werkbank und Prüfung.</summary>
public static class Annahmevertrag
{
    /// <summary>Der Pfad, unter dem der Host annimmt (<c>POST /eingang/webhook/&lt;Pfad&gt;</c>) — Pflicht, je Umgebung eindeutig.</summary>
    public const string Pfad = "Pfad";

    /// <summary>Das Geheimnis, das ein Absender im Kopf <see cref="SchluesselKopf"/> mitgibt — wahlweise; in der Umgebung als <c>${env:NAME}</c>.</summary>
    public const string Schluessel = "Schluessel";

    /// <summary>Der Kopf mit dem Schlüssel des Absenders.</summary>
    public const string SchluesselKopf = "schluessel";

    /// <summary>Der Kopf mit der Kennung des Aufrufs — Pflicht: nur so wirkt eine Wiederholung einmal.</summary>
    public const string KennungKopf = "kennung";

    /// <summary>Der Weg, unter dem der Host annimmt (ohne Pfad).</summary>
    public const string Route = "/eingang/webhook";

    /// <summary>Ein Pfad in seiner Form zum Vergleich (ohne führende und folgende Schrägstriche).</summary>
    public static string Normal(string? pfad) => (pfad ?? "").Trim().Trim('/');
}
