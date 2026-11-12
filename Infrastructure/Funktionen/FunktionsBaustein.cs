using System.Runtime.CompilerServices;
using Abstractions;

namespace Infrastructure.Funktionen;

// ═══════════════════════════════════════════════════════════════════════════════════════════════════════════════════════
// FUNKTION ALS BAUSTEIN (docs/04 §4.4, docs/10 §10.3): je Funktion (Klasse = im Host, Schnittstelle = draußen oder mit Umsetzung im Host) ein
// generierter Eintrag — Eingänge, Ausgaben, Lese-Fähigkeiten, wie sie gebaut wird und ihre reflexionsfreie Rechnung. Die Kette tragen Wurzel, Parent
// und Kinder (FunktionsLaufzeitWiring), nicht die Funktion.
// ═══════════════════════════════════════════════════════════════════════════════════════════════════════════════════════

/// <summary>
/// Ein <c>Auf</c> einer Funktion (Index in <see cref="FunktionsBaustein.Eingaenge"/> = seine Nummer): sein (erster) Eingangstyp, ob die Ausgabe ein
/// Strom ist, was herauskommen kann, seine Lese-Fähigkeiten und alle Typen seiner Ports.
/// </summary>
/// <param name="Strom">Die Ausgabe ist ein Strom (viele Ausgaben je Eingang, jede gesendet, bevor die nächste kommt).</param>
/// <param name="Ausgaben">Die konkreten Ausgabe-Typen (die Fälle der Signatur).</param>
/// <param name="Faehigkeiten">Die Lese-Fähigkeiten des <c>Auf</c> (eine solche Funktion läuft nur im Host).</param>
/// <param name="Typen">Alle Typen, die das <c>Auf</c> an seinen Ports nimmt (ein Join hat mehrere; leer = nur <paramref name="Typ"/>).</param>
public sealed record FunktionsEingang(Type Typ, bool Strom, IReadOnlyList<Type> Ausgaben, IReadOnlyList<Type> Faehigkeiten,
    IReadOnlyList<Type>? Typen = null)
{
    /// <summary>Alle Typen der Ports.</summary>
    public IReadOnlyList<Type> Ports => Typen is { Count: > 0 } t ? t : [Typ];
}

/// <summary>
/// Eine Funktion in Baustein-Form: generiert (<c>GeneratedFunktionsBausteine</c>) für jede Funktions-Klasse — oder zur Laufzeit für die ARBEIT eines
/// Akteurs (<see cref="Arbeit"/>): die Commands, die er hört, rechnet draußen eine Sitzung, die ihn verkörpert.
/// </summary>
/// <param name="Baue">Die generierte Fabrik der Klasse — im Bereich der Aktivierung, mit ihrem Zustand (<see cref="Zustand{T}"/>, null ohne
/// <c>IZustand&lt;T&gt;</c>); null = draußen (die Arbeit eines Akteurs).</param>
/// <param name="Dienste">Was der Konstruktor der Klasse braucht (Dienste, Konfig-Records) — für die Boot-Prüfung der Bindung.</param>
/// <param name="Rechne">Die Rechnung: Eingang → Strom der Ausgaben (Commands, Hinweise), wie die Signatur sie zulässt (null = draußen).</param>
/// <param name="Zustand">Der Zustand (<c>IZustand&lt;T&gt;</c> im Konstruktor, docs/04 §4.4), gesichert je Instanz und Knoten — null = ohne Zustand.</param>
/// <param name="Lausche">Eine Quellfunktion: die Nummer, unter der die Rechnung ihr <c>Lausche</c> ruft (null = keine Quelle).</param>
public sealed record FunktionsBaustein(
    Type Typ,
    string Name,
    IReadOnlyList<FunktionsEingang> Eingaenge,
    Func<IServiceProvider, object?, object>? Baue,
    IReadOnlyList<Type> Dienste,
    FunktionsRechnung? Rechne,
    FunktionsZustandsform? Zustand = null,
    int? Lausche = null)
{
    /// <summary>Der Wirker im Log (Spur): <c>funktion:&lt;Name&gt;</c>.</summary>
    public string Wirker => WirkerId.FuerFunktion(Name);

    /// <summary>Der Eingang für einen Typ, oder null.</summary>
    public FunktionsEingang? Eingang(Type typ) => Eingaenge.FirstOrDefault(e => e.Ports.Contains(typ));

    /// <summary>Hat irgendein Eingang Lese-Fähigkeiten? Dann läuft die Funktion nur im Host.</summary>
    public bool Liest => Eingaenge.Any(e => e.Faehigkeiten.Count > 0);
}

/// <summary>
/// Bringt die beiden Rückgaben eines <c>Auf</c> (<c>Task&lt;T&gt;</c> bzw. <c>Task&lt;OneOf&lt;…&gt;&gt;</c>; ein <c>IAsyncEnumerable&lt;…&gt;</c>) auf einen
/// Strom von Ausgaben — der Generator ruft die passende Form, mit einer Abbildung je Element (<c>x =&gt; x.Value</c> für OneOf). Ein leerer Strom heißt „nichts“.
/// </summary>
public static class Ausgaben
{
    public static async IAsyncEnumerable<TA> Einzeln<T, TA>(Task<T> wert, Func<T, TA?> abbildung) where TA : class
    {
        if (abbildung(await wert.ConfigureAwait(false)) is { } a) yield return a;
    }

    public static async IAsyncEnumerable<TA> Strom<T, TA>(IAsyncEnumerable<T>? strom, Func<T, TA?> abbildung,
        [EnumeratorCancellation] CancellationToken ct = default) where TA : class
    {
        if (strom is null) yield break;
        await foreach (var x in strom.WithCancellation(ct).ConfigureAwait(false))
            if (abbildung(x) is { } a) yield return a;
    }
}
