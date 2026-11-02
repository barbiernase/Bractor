using Abstractions;

namespace Infrastructure.Funktionen;

/// <summary>
/// Rechnet ein <c>Auf</c> der Funktion (Nummer <paramref name="auf"/>) mit seinen Ports als Objekten (<c>T?</c> als null, <c>IReadOnlyList&lt;T&gt;</c> als
/// Liste, <c>OneOf</c> als der gekommene Fall): ihre Ausgaben in Reihenfolge (Commands, Hinweise).
/// </summary>
public delegate IAsyncEnumerable<object> FunktionsRechnung(object umsetzung, int auf, object?[] argumente, Herkunft herkunft, IFaehigkeiten? faehigkeiten,
    CancellationToken ct);

/// <summary>Rechnet die Arbeit eines Akteurs draußen (<see cref="Werkstatt"/>): Kennung des Auftrags, Command, Herkunft, Korrelation.</summary>
public delegate IAsyncEnumerable<object> FunktionsRechnungDraussen(Guid kennung, object eingang, Herkunft herkunft, string? korrelation, CancellationToken ct);

/// <summary>
/// Was die Laufzeit von einem Knoten braucht (docs/04 §4.4): der Name seiner Funktion, wie eine Aktivierung entsteht (Bereich, Umsetzung, Zustand),
/// wie sie rechnet und ob ihr <c>Auf</c> Lese-Fähigkeiten nimmt — oder, wenn sie draußen rechnet, nur der Weg dorthin (<see cref="Draussen"/>) —, dazu
/// Instanz, Knoten und seine Kanten im Graphen.
/// </summary>
/// <param name="Antworten">Die Event-Typen, die die Funktion als Antwort auf ihre eigenen Commands hört (das Ziel entschied sie, docs/04 §4.4), je mit der
/// Nummer ihres <c>Auf</c>.</param>
/// <param name="Draussen">Draußen (die Arbeit eines Akteurs): ein Arbeiter rechnet, das Kind hat keine Aktivierung.</param>
public sealed record FunktionsTraeger(string Name, Func<IServiceProvider, FunktionsAktivierung>? Aktivierung, FunktionsRechnung? Rechne, bool Liest = false,
    IReadOnlyDictionary<Type, int>? Antworten = null, FunktionsRechnungDraussen? Draussen = null)
{
    private static readonly IReadOnlySet<Type> KeineKanten = new HashSet<Type>();

    /// <summary>Die Instanz, in deren Graph die Funktion ein Knoten ist (leer: ohne Graph, nur Prüfstände).</summary>
    public string Instanz { get; init; } = "";

    /// <summary>Der Name des Knotens im Graphen.</summary>
    public string Knoten { get; init; } = "";

    /// <summary>Die Commands ohne Ziel, die der Knoten an Knoten desselben Graphen gibt (der Graph-Actor bringt sie hin; alles andere verlässt den Graphen).</summary>
    public IReadOnlySet<Type> ImGraphen { get; init; } = KeineKanten;

    /// <summary>So lange darf eine Rechnung dauern (<see cref="TimeSpan.Zero"/> = ohne Grenze).</summary>
    public TimeSpan Zeitlimit { get; init; }

    /// <summary>Eine Quellfunktion: die Nummer, unter der die Rechnung ihr <c>Lausche</c> ruft (null = keine Quelle).</summary>
    public int? Lausche { get; init; }

    /// <summary>Der Ort des Knotens (<c>instanz/knoten</c>) — Schlüssel der Command-Ids; ohne Instanz der Name der Funktion.</summary>
    public string Ort => Instanz.Length == 0 ? Name : Instanz + "/" + Knoten;
}

/// <summary>Wie ein Knoten einer Instanz auf einem Cluster-Knoten läuft (aus seiner Konfig): Parallel, Warteschlange, Vorwärmen, Wiederholungen.</summary>
public sealed record FunktionsOrtKonfig(int Parallel = FunktionsOrtKonfig.StandardParallel, int Warteschlange = FunktionsOrtKonfig.StandardWarteschlange,
    bool Vorwaermen = false, int Wiederholungen = FunktionsOrtKonfig.StandardWiederholungen)
{
    /// <summary>Standard für <see cref="Parallel"/>: ein Kind rechnet einen Eingang samt den Antworten auf seine Commands (eine ganze Kette) — so viele
    /// zugleich, wie früher Partitionen mal Slots Eingänge einzeln liefen, und mehr.</summary>
    public const int StandardParallel = 256, StandardWarteschlange = 10_000, StandardWiederholungen = 3;
}

/// <summary>
/// Die KÖPFE der laufenden Instanzen (docs/04 §4.4, aus den Graphen und den Instanzen der Umgebung, <see cref="GraphInstanzen.Koepfe"/>): Eingangstyp →
/// die Instanzen, deren Graph ihn am Kopf nimmt — ein Command ohne Ziel (genau eine), eine Quell-Nachricht, ein Hinweis. Der Bote schickt direkt; nimmt
/// niemand, geht nichts hinaus.
/// </summary>
public sealed class Verkettung(IReadOnlyDictionary<Type, string[]> hoerer)
{
    public static readonly Verkettung Leer = new(new Dictionary<Type, string[]>());

    public IReadOnlyList<string> Hoerer(Type nachricht) => hoerer.TryGetValue(nachricht, out var f) ? f : Array.Empty<string>();
}
