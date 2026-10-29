using System.Runtime.CompilerServices;
using Abstractions;

namespace Infrastructure.Funktionen;

// ═══════════════════════════════════════════════════════════════════════════════════════════════════════════════════════
// FUNKTION ALS BAUSTEIN (docs/10 §10.1, docs/10 §10.3): je Funktion (Klasse = im Host, Schnittstelle = draußen) ein
// generierter Eintrag — Eingänge, Ausgaben, Lese-Fähigkeiten, ob sie nachweislich gleichbleibend ist (R12), wie sie gebaut wird und der
// reflexionsfreie Dispatch (Einzel- und Seiten-Form). Den Lauf trägt der Funktions-Actor (Position, Slots, Festhalten), nicht die Funktion.
// ═══════════════════════════════════════════════════════════════════════════════════════════════════════════════════════

/// <summary>Ein Aufruf der Einzel-Form: Eingang → Strom der Ausgaben (Commands, Events, Hinweise), wie die Signatur sie zulässt.</summary>
/// <param name="funktion">Die gebaute Funktion (Klasse bzw. Implementierung der Schnittstelle).</param>
/// <param name="faehigkeiten">Der Bereich für die Lese-Fähigkeiten dieses Aufrufs; null, wenn das <c>Auf</c> keine nimmt.</param>
public delegate IAsyncEnumerable<IAusgabe> FunktionsAufruf(object funktion, IEvent eingang, Herkunft herkunft, IFaehigkeiten? faehigkeiten,
    CancellationToken ct);

/// <summary>
/// Die Seiten-Form (Batch, vom Generator aus der Einzel-Form gebaut): eine Seite von Eingängen in einem Aufruf, die Ausgaben mit dem Index ihres
/// Eingangs — der Actor nimmt sie für kurze, gleichbleibende Funktionen (ein Bereich, eine Aufzählung je Seite).
/// </summary>
public delegate IAsyncEnumerable<(int Index, IAusgabe Ausgabe)> FunktionsSeite(object funktion, IReadOnlyList<(IEvent Eingang, Herkunft Herkunft)> seite,
    IFaehigkeiten? faehigkeiten, CancellationToken ct);

/// <summary>Ein Eingang einer Funktion (ein <c>Auf</c>): sein Typ, ob er ein Hinweis ist, ob die Ausgabe ein Strom ist, was herauskommen kann.</summary>
/// <param name="Hinweis">Der Eingang ist ein Hinweis (verlierbar, nicht im Log): höchstens einmal, keine Position.</param>
/// <param name="Strom">Die Ausgabe ist ein Strom (viele Ausgaben je Eingang, Fortschritt beim Framework).</param>
/// <param name="MitEvents">Unter den Ausgaben sind Events für das Log (keine Hinweise) — sie stehen dann immer im festgehaltenen Eintrag.</param>
/// <param name="Ausgaben">Die konkreten Ausgabe-Typen (die Fälle der Signatur).</param>
/// <param name="Faehigkeiten">Die Lese-Fähigkeiten des <c>Auf</c> (eine solche Funktion läuft nur im Host).</param>
public sealed record FunktionsEingang(Type Typ, bool Hinweis, bool Strom, bool MitEvents, IReadOnlyList<Type> Ausgaben, IReadOnlyList<Type> Faehigkeiten);

/// <summary>
/// Eine Umsetzung, die im Host rechnen kann: die Klasse selbst bzw. eine Implementierung der Schnittstelle, gebaut per generierter Fabrik — im
/// Bereich der Aktivierung, mit ihrem Zustand (<see cref="Zustand{T}"/>, null ohne <c>IZustand&lt;T&gt;</c>).
/// </summary>
public sealed record FunktionsUmsetzung(string Name, Type Typ, Func<IServiceProvider, object?, object> Baue);

/// <summary>
/// Eine Funktion in Baustein-Form (generiert, <c>GeneratedFunktionsBausteine</c>).
/// </summary>
/// <param name="Draussen">Als Schnittstelle deklariert: sie rechnet draußen (Worker, Client, Hand) oder mit einer Implementierung im Host.</param>
/// <param name="Gleichbleibend">Nachweislich gleichbleibend (R12): eine Klasse ohne Konstruktor-Parameter und ohne Lese-Fähigkeit — derselbe
/// Eingang ergibt dieselbe Ausgabe (Uhr und Zufall verbietet CQRS103). Nur sie darf ihre Ausgabe ohne Festhalten senden.</param>
/// <param name="Dienste">Was der Konstruktor der Klasse braucht (Dienste, Konfig-Records) — für die Boot-Prüfung der Bindung.</param>
/// <param name="Zustand">Der gekapselte Zustand (<c>IZustand&lt;T&gt;</c> im Konstruktor, docs/04 §4.4) — null = ohne Zustand.</param>
/// <param name="EineAdresse">Eine Adresse statt einer je Partition (<c>[EineAdresse]</c>, A6): ein Actor liest alle Partitionen.</param>
public sealed record FunktionsBaustein(
    Type Typ,
    string Name,
    bool Draussen,
    bool Gleichbleibend,
    IReadOnlyList<FunktionsEingang> Eingaenge,
    IReadOnlyList<FunktionsUmsetzung> Umsetzungen,
    IReadOnlyList<Type> Dienste,
    FunktionsAufruf Rufe,
    FunktionsSeite RufeSeite,
    FunktionsZustandsform? Zustand = null,
    bool EineAdresse = false)
{
    /// <summary>Das Cluster-Kind des Funktions-Actors (Identität = Partition bzw. die eine Adresse).</summary>
    public string KindName => Funktionsvertrag.Kind(Name);

    /// <summary>In wie viele Partitionen die Funktion das Log liest: eine bei <see cref="EineAdresse"/>, sonst die des Konsums.</summary>
    public int Partitionen(int konsum) => EineAdresse ? 1 : konsum;

    /// <summary>Die Adresse (Identität des Funktions-Actors), die einen Eingang des Streams <paramref name="stream"/> rechnet (A6).</summary>
    public string Adresse(Guid stream, int konsumPartitionen) =>
        EineAdresse ? Funktionsvertrag.EinzigeIdentitaet : Partitionierung.Von(stream, konsumPartitionen).ToString();

    /// <summary>Hört die Funktion etwas aus dem Log? Sonst hat sie keinen Leser, keine Position, keinen Takt (nur Hinweise).</summary>
    public bool HoertLog => Eingaenge.Any(e => !e.Hinweis);

    /// <summary>Die Id des Stands (Position je Partition).</summary>
    public string StandId => Funktionsvertrag.StandId(Name);

    /// <summary>Der Wirker im Log (Spur): <c>funktion:&lt;Name&gt;</c>.</summary>
    public string Wirker => WirkerId.FuerFunktion(Name);

    /// <summary>Die Eingänge aus dem Log (Events, Quell-Nachrichten) — sie haben eine Position; Hinweise nicht.</summary>
    public IEnumerable<FunktionsEingang> LogEingaenge => Eingaenge.Where(e => !e.Hinweis);

    /// <summary>Der Eingang für einen Event-Typ, oder null.</summary>
    public FunktionsEingang? Eingang(Type typ) => Eingaenge.FirstOrDefault(e => e.Typ == typ);

    /// <summary>Hat irgendein Eingang Lese-Fähigkeiten? Dann läuft die Funktion nur im Host.</summary>
    public bool Liest => Eingaenge.Any(e => e.Faehigkeiten.Count > 0);
}

/// <summary>
/// Bringt jede erlaubte Rückgabe eines <c>Auf</c> (konkreter Typ, <c>OneOf</c>, <c>T?</c>, Strom, je auch als Task) auf einen Strom von Ausgaben —
/// der Generator ruft die passende Form, mit einer Abbildung je Element (<c>x =&gt; x.Value</c> für OneOf). <c>null</c> heißt „nichts“.
/// </summary>
public static class Ausgaben
{
    /// <summary>Ein <c>Auf</c> ohne Ausgabe (nur mit Zustand: der Eingang wirkt allein auf ihn).</summary>
    public static IAsyncEnumerable<IAusgabe> Leer => KeineAsync(Task.CompletedTask);

    public static async IAsyncEnumerable<IAusgabe> KeineAsync(Task lauf)
    {
        await lauf.ConfigureAwait(false);
        yield break;
    }

    public static async IAsyncEnumerable<IAusgabe> KeineAsync(ValueTask lauf)
    {
        await lauf.ConfigureAwait(false);
        yield break;
    }

    public static async IAsyncEnumerable<IAusgabe> Einzeln<T>(T wert, Func<T, IAusgabe?> abbildung)
    {
        await Task.CompletedTask;
        if (abbildung(wert) is { } a) yield return a;
    }

    public static async IAsyncEnumerable<IAusgabe> EinzelnAsync<T>(Task<T> wert, Func<T, IAusgabe?> abbildung)
    {
        if (abbildung(await wert.ConfigureAwait(false)) is { } a) yield return a;
    }

    public static async IAsyncEnumerable<IAusgabe> EinzelnAsync<T>(ValueTask<T> wert, Func<T, IAusgabe?> abbildung)
    {
        if (abbildung(await wert.ConfigureAwait(false)) is { } a) yield return a;
    }

    public static async IAsyncEnumerable<IAusgabe> Strom<T>(IEnumerable<T>? strom, Func<T, IAusgabe?> abbildung,
        [EnumeratorCancellation] CancellationToken ct = default)
    {
        await Task.CompletedTask;
        if (strom is null) yield break;
        foreach (var x in strom)
        {
            ct.ThrowIfCancellationRequested();
            if (abbildung(x) is { } a) yield return a;
        }
    }

    public static async IAsyncEnumerable<IAusgabe> StromAsync<T>(Task<IEnumerable<T>> strom, Func<T, IAusgabe?> abbildung,
        [EnumeratorCancellation] CancellationToken ct = default)
    {
        await foreach (var a in Strom(await strom.ConfigureAwait(false), abbildung, ct).WithCancellation(ct)) yield return a;
    }

    public static async IAsyncEnumerable<IAusgabe> StromAsync<T>(IAsyncEnumerable<T>? strom, Func<T, IAusgabe?> abbildung,
        [EnumeratorCancellation] CancellationToken ct = default)
    {
        if (strom is null) yield break;
        await foreach (var x in strom.WithCancellation(ct).ConfigureAwait(false))
            if (abbildung(x) is { } a) yield return a;
    }
}
