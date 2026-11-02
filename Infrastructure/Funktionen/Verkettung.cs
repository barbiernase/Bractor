using Abstractions;

namespace Infrastructure.Funktionen;

/// <summary>Rechnet die Funktion zu einem Eingang: ihre Ausgaben in Reihenfolge (Commands, Zwischenergebnisse, Hinweise).</summary>
public delegate IAsyncEnumerable<object> FunktionsRechnung(object umsetzung, object eingang, Herkunft herkunft, IFaehigkeiten? faehigkeiten, CancellationToken ct);

/// <summary>Rechnet die Funktion draußen (<see cref="Werkstatt"/>): Kennung des Eingangs, Nachricht, Herkunft, Korrelation, ob er ein Hinweis ist.</summary>
public delegate IAsyncEnumerable<object> FunktionsRechnungDraussen(Guid kennung, object eingang, Herkunft herkunft, string? korrelation, bool hinweis,
    CancellationToken ct);

/// <summary>
/// Was die Laufzeit von einem Knoten braucht (docs/04 §4.4): der Name seiner Funktion, wie eine Aktivierung entsteht (Bereich, Umsetzung, Zustand),
/// wie sie rechnet und ob ihr <c>Auf</c> Lese-Fähigkeiten nimmt — oder, wenn sie draußen rechnet, nur der Weg dorthin (<see cref="Draussen"/>) —, dazu
/// Instanz, Knoten und seine Kanten im Graphen.
/// </summary>
/// <param name="Antworten">Die Event-Typen, die die Funktion als Antwort auf ihre eigenen Commands hört (das Ziel entschied sie, docs/04 §4.4).</param>
/// <param name="Draussen">Draußen (Laufort Extern oder Hand): ein Arbeiter rechnet, das Kind hat keine Aktivierung.</param>
public sealed record FunktionsTraeger(string Name, Func<IServiceProvider, FunktionsAktivierung>? Aktivierung, FunktionsRechnung? Rechne, bool Liest = false,
    IReadOnlySet<Type>? Antworten = null, FunktionsRechnungDraussen? Draussen = null)
{
    private static readonly IReadOnlyDictionary<Type, string[]> KeineKanten = new Dictionary<Type, string[]>();

    /// <summary>Die Instanz, in deren Graph die Funktion ein Knoten ist (leer: ohne Graph, nur Prüfstände).</summary>
    public string Instanz { get; init; } = "";

    /// <summary>Der Name des Knotens im Graphen.</summary>
    public string Knoten { get; init; } = "";

    /// <summary>Die Kanten des Knotens: Command ohne Ziel → die Knoten desselben Graphen, die ihn nehmen.</summary>
    public IReadOnlyDictionary<Type, string[]> Kanten { get; init; } = KeineKanten;

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
