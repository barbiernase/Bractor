namespace Abstractions;

/// <summary>
/// Gekapselter, durabler ZUSTAND einer Funktion (docs/04 §4.4, docs/10 E6, R19) — kein Domänen-Zustand, sondern was die Technik
/// hinter einer Funktion über ihre Aufrufe hinweg braucht: geladene Gewichte, ein Zähler, ein Cache, der nach einem Umzug nicht kalt sein soll.
/// Was aus Fakten gelernt wird, gehört in eine Projektion; die Funktion liest es per Lese-Fähigkeit. Code-Fakt: steht <c>IZustand&lt;T&gt;</c> im Konstruktor einer Funktions-Klasse, hat sie Zustand; der Generator sieht
/// es über Roslyn und verdrahtet ihn ohne Reflection. Ebenso eine Technik (Klasse bzw. Umsetzung einer Technik-Schnittstelle im Konstruktor einer Funktion): ihr
/// Zustand gehört der Aktivierung ihres Halters — gesichert wird er im selben Dokument (je Funktion ihr Eintrag) mit dessen Position, und der Halter
/// ist damit wie mit eigenem Zustand: ein Eingang zugleich, nie gleichbleibend.
/// <code>
/// public sealed class Zaehlwerk(IZustand&lt;Zaehlstand&gt; stand) : IZaehlwerk
/// {
///     public void Zaehle(Guid stelle) => stand.Wert.Je[stelle] = stand.Wert.Je.GetValueOrDefault(stelle) + 1;
/// }
/// </code>
/// Was das Framework daraus macht (Vorbild Aggregat-Actor: ein Schreiber, Aktivierung, Passivierung, Snapshot, Rehydration — ohne Zustands-Events):
/// <list type="bullet">
/// <item>Die Funktion ruft <c>Auf</c> immer nur einmal zugleich (ein Schreiber je Adresse).</item>
/// <item>Der Zustand wird beim Aktivieren geladen und <b>zusammen mit der Position</b> gesichert (eine Transaktion; ein großer Zustand liegt als
///   Datei in einer Ablage der Umgebung, die Position verweist darauf), höchstens so oft wie die Umgebung sagt und beim Stoppen, Passivieren und
///   Umziehen. Nach einem Absturz rechnet die nächste Aktivierung die Log-Eingänge ab der gesicherten Position erneut — jeder Log-Eingang wirkt
///   genau einmal auf den gesicherten Zustand; ein Hinweis höchstens einmal.</item>
/// <item>Eine Funktion mit Zustand ist nie gleichbleibend: sie hält ihre Ausgabe fest (R12).</item>
/// </list>
/// <typeparamref name="T"/> ist eine eigene, nicht generische Klasse der Anwendung mit öffentlichem Konstruktor ohne Parameter (der Anfangszustand)
/// und öffentlichen Eigenschaften — gesichert über einen generierten Serializer-Kontext. Zustand in Feldern oder Eigenschaften der Funktion selbst
/// ist verboten (CQRS105): er ginge beim Umzug still verloren.
/// </summary>
public interface IZustand<T> where T : class, new()
{
    /// <summary>Der Zustand dieser Aktivierung — lesen, ändern oder ersetzen; gesichert wird, was hier steht.</summary>
    T Wert { get; set; }
}
