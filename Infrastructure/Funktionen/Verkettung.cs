using Abstractions;

namespace Infrastructure.Funktionen;

/// <summary>Rechnet die Funktion zu einem Eingang: ihre Ausgaben in Reihenfolge (Commands, Zwischenergebnisse, Hinweise).</summary>
public delegate IAsyncEnumerable<object> FunktionsRechnung(object umsetzung, object eingang, Herkunft herkunft, IFaehigkeiten? faehigkeiten, CancellationToken ct);

/// <summary>
/// Was die Laufzeit von einer Funktion braucht (docs/04 §4.4): ihr Name, wie eine Aktivierung entsteht (Bereich, Umsetzung, Zustand, Lebenszyklus),
/// wie sie rechnet und ob ihr <c>Auf</c> Lese-Fähigkeiten nimmt.
/// </summary>
/// <param name="Antworten">Die Event-Typen, die die Funktion als Antwort auf ihre eigenen Commands hört (das Ziel entschied sie, docs/04 §4.4).</param>
public sealed record FunktionsTraeger(string Name, Func<IServiceProvider, FunktionsAktivierung> Aktivierung, FunktionsRechnung Rechne, bool Liest = false,
    IReadOnlySet<Type>? Antworten = null);

/// <summary>Wie eine Funktion an einem Knoten läuft (Konfig <c>Funktionen:&lt;Name&gt;</c>): Parallel, Warteschlange, Vorwärmen, Wiederholungen.</summary>
public sealed record FunktionsOrtKonfig(int Parallel = FunktionsOrtKonfig.StandardParallel, int Warteschlange = FunktionsOrtKonfig.StandardWarteschlange,
    bool Vorwaermen = false, int Wiederholungen = FunktionsOrtKonfig.StandardWiederholungen)
{
    public const int StandardParallel = 4, StandardWarteschlange = 1_000, StandardWiederholungen = 3;
}

/// <summary>
/// Die VERKETTUNG (docs/04 §4.4, zur Build-Zeit generiert): Nachrichten-Typ → die Funktionen, die ihn hören — eine Folge, ein Zwischenergebnis, eine
/// Quell- oder Client-Nachricht, ein Hinweis. Der Bote schickt direkt; hört niemand, geht nichts hinaus.
/// </summary>
public sealed class Verkettung(IReadOnlyDictionary<Type, string[]> hoerer)
{
    public static readonly Verkettung Leer = new(new Dictionary<Type, string[]>());

    public IReadOnlyList<string> Hoerer(Type nachricht) => hoerer.TryGetValue(nachricht, out var f) ? f : Array.Empty<string>();
}
