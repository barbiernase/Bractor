using System.Runtime.CompilerServices;
using Abstractions;

namespace Infrastructure.Funktionen;

// ═══════════════════════════════════════════════════════════════════════════════════════════════════════════════════════
// FUNKTION ALS BAUSTEIN (docs/04 §4.4, docs/10 §10.3): je Funktion (Klasse = im Host, Schnittstelle = draußen oder mit Umsetzung im Host) ein
// generierter Eintrag — Eingänge, Ausgaben, Lese-Fähigkeiten, wie sie gebaut wird und ihre reflexionsfreie Rechnung. Den Lauf tragen Wurzel, Parent
// und Kinder (FunktionsLaufzeitWiring), nicht die Funktion.
// ═══════════════════════════════════════════════════════════════════════════════════════════════════════════════════════

/// <summary>Ein Eingang einer Funktion (ein <c>Auf</c>): sein Typ, ob er ein Hinweis ist, ob die Ausgabe ein Strom ist, was herauskommen kann.</summary>
/// <param name="Hinweis">Der Eingang ist ein Hinweis (verlierbar, nicht im Log): höchstens einmal.</param>
/// <param name="Strom">Die Ausgabe ist ein Strom (viele Ausgaben je Eingang, jede gesendet, bevor die nächste kommt).</param>
/// <param name="Ausgaben">Die konkreten Ausgabe-Typen (die Fälle der Signatur).</param>
/// <param name="Faehigkeiten">Die Lese-Fähigkeiten des <c>Auf</c> (eine solche Funktion läuft nur im Host).</param>
public sealed record FunktionsEingang(Type Typ, bool Hinweis, bool Strom, IReadOnlyList<Type> Ausgaben, IReadOnlyList<Type> Faehigkeiten);

/// <summary>
/// Eine Umsetzung, die im Host rechnen kann: die Klasse selbst bzw. eine Implementierung der Schnittstelle, gebaut per generierter Fabrik — im
/// Bereich der Aktivierung, mit ihrem Zustand (<see cref="Zustand{T}"/>, null ohne <c>IZustand&lt;T&gt;</c>).
/// </summary>
public sealed record FunktionsUmsetzung(string Name, Type Typ, Func<IServiceProvider, object?, object> Baue);

/// <summary>
/// Eine Funktion in Baustein-Form (generiert, <c>GeneratedFunktionsBausteine</c>).
/// </summary>
/// <param name="Draussen">Als Schnittstelle deklariert: sie rechnet draußen (Worker, Client, Hand) oder mit einer Implementierung im Host.</param>
/// <param name="Dienste">Was der Konstruktor der Klasse braucht (Dienste, Konfig-Records) — für die Boot-Prüfung der Bindung.</param>
/// <param name="Rechne">Die Rechnung: Eingang → Strom der Ausgaben (Commands, Hinweise), wie die Signatur sie zulässt.</param>
/// <param name="Zustand">Der gekapselte Zustand (<c>IZustand&lt;T&gt;</c> im Konstruktor, docs/04 §4.4, ein Cache) — null = ohne Zustand.</param>
public sealed record FunktionsBaustein(
    Type Typ,
    string Name,
    bool Draussen,
    IReadOnlyList<FunktionsEingang> Eingaenge,
    IReadOnlyList<FunktionsUmsetzung> Umsetzungen,
    IReadOnlyList<Type> Dienste,
    FunktionsRechnung Rechne,
    FunktionsZustandsform? Zustand = null)
{
    /// <summary>Der Wirker im Log (Spur): <c>funktion:&lt;Name&gt;</c>.</summary>
    public string Wirker => WirkerId.FuerFunktion(Name);

    /// <summary>Der Eingang für einen Typ, oder null.</summary>
    public FunktionsEingang? Eingang(Type typ) => Eingaenge.FirstOrDefault(e => e.Typ == typ);

    /// <summary>Hat irgendein Eingang Lese-Fähigkeiten? Dann läuft die Funktion nur im Host.</summary>
    public bool Liest => Eingaenge.Any(e => e.Faehigkeiten.Count > 0);
}

/// <summary>
/// Bringt jede erlaubte Rückgabe eines <c>Auf</c> (konkreter Typ, <c>OneOf</c>, je auch als Task; ein Strom) auf einen Strom von Ausgaben — der
/// Generator ruft die passende Form, mit einer Abbildung je Element (<c>x =&gt; x.Value</c> für OneOf). Ein leerer Strom heißt „nichts“.
/// </summary>
public static class Ausgaben
{
    public static async IAsyncEnumerable<TA> Einzeln<T, TA>(T wert, Func<T, TA?> abbildung) where TA : class
    {
        await Task.CompletedTask;
        if (abbildung(wert) is { } a) yield return a;
    }

    public static async IAsyncEnumerable<TA> EinzelnAsync<T, TA>(Task<T> wert, Func<T, TA?> abbildung) where TA : class
    {
        if (abbildung(await wert.ConfigureAwait(false)) is { } a) yield return a;
    }

    public static async IAsyncEnumerable<TA> Strom<T, TA>(IEnumerable<T>? strom, Func<T, TA?> abbildung,
        [EnumeratorCancellation] CancellationToken ct = default) where TA : class
    {
        await Task.CompletedTask;
        if (strom is null) yield break;
        foreach (var x in strom)
        {
            ct.ThrowIfCancellationRequested();
            if (abbildung(x) is { } a) yield return a;
        }
    }

    public static async IAsyncEnumerable<TA> StromAsync<T, TA>(IAsyncEnumerable<T>? strom, Func<T, TA?> abbildung,
        [EnumeratorCancellation] CancellationToken ct = default) where TA : class
    {
        if (strom is null) yield break;
        await foreach (var x in strom.WithCancellation(ct).ConfigureAwait(false))
            if (abbildung(x) is { } a) yield return a;
    }
}
