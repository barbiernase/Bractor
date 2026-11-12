using Abstractions;

namespace Infrastructure.Funktionen;

/// <summary>
/// Rechnet ein <c>Auf</c> der Funktion (Nummer <paramref name="auf"/>) mit seinen Ports als Objekten (<c>T?</c> als null, <c>IReadOnlyList&lt;T&gt;</c> als
/// Liste, <c>OneOf</c> als der gekommene Fall): ihre Ausgaben in Reihenfolge (Commands, Hinweise).
/// </summary>
public delegate IAsyncEnumerable<object> FunktionsRechnung(object umsetzung, int auf, object?[] argumente, Herkunft herkunft, IFaehigkeiten? faehigkeiten,
    CancellationToken ct);

/// <summary>
/// Was die Laufzeit von einem Knoten braucht (docs/04 §4.4): der Name seiner Funktion, wie eine Aktivierung entsteht (Bereich, Umsetzung, Zustand),
/// wie sie rechnet und ob ihr <c>Auf</c> Lese-Fähigkeiten nimmt, dazu Instanz, Knoten und seine Kanten im Graphen.
/// </summary>
public sealed record FunktionsTraeger(string Name, Func<IServiceProvider, FunktionsAktivierung>? Aktivierung, FunktionsRechnung? Rechne, bool Liest = false)
{
    private static readonly IReadOnlySet<Type> KeineKanten = new HashSet<Type>();

    /// <summary>Die Instanz, in deren Graph die Funktion ein Knoten ist (leer: ohne Graph, nur Prüfstände).</summary>
    public string Instanz { get; init; } = "";

    /// <summary>Der Name des Knotens im Graphen.</summary>
    public string Knoten { get; init; } = "";

    /// <summary>Die Commands ohne Ziel, die der Knoten an Knoten desselben Graphen gibt (der Graph-Actor bringt sie hin; alles andere verlässt den Graphen).</summary>
    public IReadOnlySet<Type> ImGraphen { get; init; } = KeineKanten;

    /// <summary>
    /// Die Übersetzungen der Domänen nach Typ (docs/04 §4.4): jede Ausgabe, die eine nimmt, rechnet das Kind inline — ein Ausgang wie
    /// ein Zwischenstand; ebenso jedes Event, mit dem ein Aggregat den Command einer Übersetzung beantwortet.
    /// </summary>
    public Uebersetzungen Uebersetzungen { get; init; } = Uebersetzungen.Leer;

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
    /// <summary>Standard für <see cref="Parallel"/>: ein Kind rechnet einen Eingang samt den Übersetzungen seiner Ausgaben — so viele
    /// zugleich, wie früher Partitionen mal Slots Eingänge einzeln liefen, und mehr.</summary>
    public const int StandardParallel = 256, StandardWarteschlange = 10_000, StandardWiederholungen = 3;
}

/// <summary>
/// Die KÖPFE der laufenden Instanzen (docs/04 §4.4, aus den Graphen und den Instanzen der Umgebung, <see cref="GraphInstanzen.Koepfe"/>): Eingangstyp →
/// die Instanzen, deren Graph ihn am Kopf nimmt — ein Command ohne Ziel (genau eine), eine Quell-Nachricht, ein Hinweis. Der Bote schickt direkt; nimmt
/// niemand, geht nichts hinaus.
/// </summary>
public sealed class Verkettung(IReadOnlyDictionary<Type, string[]> hoerer, Uebersetzungen? uebersetzungen = null)
{
    public static readonly Verkettung Leer = new(new Dictionary<Type, string[]>());

    public Uebersetzungen Uebersetzungen { get; } = uebersetzungen ?? Uebersetzungen.Leer;

    public IReadOnlyList<string> Hoerer(Type nachricht) => hoerer.TryGetValue(nachricht, out var f) ? f : Array.Empty<string>();

    /// <summary>
    /// Wohin die Nachricht eines Clients am Kopf geht: an die Instanzen, deren Graph sie am Kopf nimmt — sonst übersetzt (eine Kopf-Übersetzung der
    /// Domäne, docs/04 §4.4) an die Instanzen, deren Graph das Ergebnis nimmt. Je Ziel die Nachricht, die dort ankommt.
    /// </summary>
    public IReadOnlyList<(string Instanz, object Nachricht)> Ziele(object nachricht)
    {
        if (Hoerer(nachricht.GetType()) is { Count: > 0 } direkt) return direkt.Select(i => (i, nachricht)).ToList();
        var ziele = new List<(string, object)>();
        foreach (var u in Uebersetzungen.Fuer(nachricht.GetType()))
        {
            var y = u.Rechne(nachricht);
            ziele.AddRange(Hoerer(y.GetType()).Select(i => (i, y)));
        }
        return ziele;
    }
}
