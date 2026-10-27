namespace Abstractions;

/// <summary>
/// Gekapselter, durabler ZUSTAND einer Funktion (Plan Pub/Sub-Neustrukturierung §2.9, E6, R19) — kein Domänen-Zustand, sondern was die Technik
/// hinter einer Funktion über ihre Aufrufe hinweg braucht: ein Modell, das weiterlernt, gelernte Gewichte, ein Zähler, ein Cache, der nach einem
/// Umzug nicht kalt sein soll. Code-Fakt: steht <c>IZustand&lt;T&gt;</c> im Konstruktor einer Funktions-Klasse, hat sie Zustand; der Generator sieht
/// es über Roslyn und verdrahtet ihn ohne Reflection.
/// <code>
/// public sealed class Schaetzer(IZustand&lt;Schaetzmodell&gt; modell) : IFunktion
/// {
///     public void Auf(MesswertErfasst e, Herkunft h) => modell.Wert.Lerne(h.Stream, e.Wert);            // lernt aus dem Log
///     public VermerkeSchaetzung? Auf(Merkmal m) => modell.Wert.Schaetze(m);                              // Hinweis rein, Command raus
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

/// <summary>
/// Die Funktion hat EINE Adresse statt einer je Partition (Plan §2.9, A6): ein einziger Funktions-Actor liest alle Partitionen des Logs und hört
/// alle Hinweise — für einen Zustand, der für alles gilt (ein Netz, ein Modell über alle Streams). Ohne das Attribut hat eine Funktion eine Adresse
/// je Partition (<c>funktion-&lt;F&gt;/&lt;Partition&gt;</c>), ein Hinweis geht an die Partition seines Herkunfts-Streams. Gilt für C# und Python gleich
/// (an der Klasse bzw. an der Schnittstelle einer Funktion draußen).
/// </summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Interface, AllowMultiple = false, Inherited = false)]
public sealed class EineAdresseAttribute : Attribute { }
