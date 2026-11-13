namespace Abstractions;

/// <summary>
/// Ein BÜNDEL von Events für eine Projektion (docs/04 §4.3.1, docs/10 §10.3): <c>Auf(Buendel&lt;A, B&gt; b, …)</c> wird gerufen, sobald zu einem
/// Schlüssel von jedem Typ des Bündels ein Event im Log steht — bei jedem weiteren Event eines dieser Typen erneut, je Typ mit dem neuesten.
/// <code>
/// public Task Auf(Buendel&lt;WareReserviert, ZahlungEingegangen&gt; b, Herkunft h, ISchreibeLieferung lieferung) => …;
/// </code>
/// Das Bündel hat keinen eigenen Namen: es heißt nach seinen Events (im Editor „WareReserviert ⋈ ZahlungEingegangen“). Der SCHLÜSSEL folgt aus den
/// Events (Code-Fakt): die eine typisierte Id <c>Id&lt;T&gt;</c>, die alle gemeinsam haben — bei einem Event, das <c>T</c> entschied, sein Stream,
/// sonst sein einziges Feld <c>Id&lt;T&gt;</c>. Haben die Events keine gemeinsam, gilt das Bündel global (je Typ das neueste Event); haben sie
/// mehrere, wählt <see cref="UeberAttribute{T}"/> am Parameter. Ausgewertet wird gegen das Log selbst, ohne Zustand: zum Event an der Sequenz
/// <c>s</c> die neuesten der übrigen Typen vor <c>s</c> — gleich bei jedem Neuaufbau und jeder Partitionierung (Invariante 1). Die
/// <see cref="Herkunft"/> ist die des auslösenden Events.
/// </summary>
public interface IBuendel
{
    /// <summary>Die Events des Bündels in der Reihenfolge seiner Typargumente.</summary>
    IReadOnlyList<IEvent> Events { get; }
}

/// <summary>Die Grenzen eines Bündels — dieselben wie die Typen unten (Buendel&lt;T1, T2&gt; bis Buendel&lt;T1, …, T16&gt;); eine Quelle für Werkzeuge.</summary>
public static class Buendelgrenzen
{
    public const int Mindestens = 2, Hoechstens = 16;
}

/// <summary>
/// Wählt den Schlüssel eines Bündels, wenn seine Events mehrere typisierte Ids gemeinsam haben:
/// <c>Auf([Ueber&lt;Bestellung&gt;] Buendel&lt;WareReserviert, ZahlungEingegangen&gt; b, …)</c>.
/// </summary>
[AttributeUsage(AttributeTargets.Parameter, AllowMultiple = false, Inherited = false)]
public sealed class UeberAttribute<T> : Attribute where T : IState { }

/// <summary>Ein Bündel aus 2 Events (<see cref="IBuendel"/>).</summary>
public sealed record Buendel<T1, T2>(T1 Erstes, T2 Zweites) : IBuendel where T1 : IEvent where T2 : IEvent
{
    public IReadOnlyList<IEvent> Events => [Erstes, Zweites];
}

/// <summary>Ein Bündel aus 3 Events (<see cref="IBuendel"/>).</summary>
public sealed record Buendel<T1, T2, T3>(T1 Erstes, T2 Zweites, T3 Drittes) : IBuendel where T1 : IEvent where T2 : IEvent where T3 : IEvent
{
    public IReadOnlyList<IEvent> Events => [Erstes, Zweites, Drittes];
}

/// <summary>Ein Bündel aus 4 Events (<see cref="IBuendel"/>).</summary>
public sealed record Buendel<T1, T2, T3, T4>(T1 Erstes, T2 Zweites, T3 Drittes, T4 Viertes) : IBuendel where T1 : IEvent where T2 : IEvent where T3 : IEvent where T4 : IEvent
{
    public IReadOnlyList<IEvent> Events => [Erstes, Zweites, Drittes, Viertes];
}

/// <summary>Ein Bündel aus 5 Events (<see cref="IBuendel"/>).</summary>
public sealed record Buendel<T1, T2, T3, T4, T5>(T1 Erstes, T2 Zweites, T3 Drittes, T4 Viertes, T5 Fuenftes) : IBuendel where T1 : IEvent where T2 : IEvent where T3 : IEvent where T4 : IEvent where T5 : IEvent
{
    public IReadOnlyList<IEvent> Events => [Erstes, Zweites, Drittes, Viertes, Fuenftes];
}

/// <summary>Ein Bündel aus 6 Events (<see cref="IBuendel"/>).</summary>
public sealed record Buendel<T1, T2, T3, T4, T5, T6>(T1 Erstes, T2 Zweites, T3 Drittes, T4 Viertes, T5 Fuenftes, T6 Sechstes) : IBuendel where T1 : IEvent where T2 : IEvent where T3 : IEvent where T4 : IEvent where T5 : IEvent where T6 : IEvent
{
    public IReadOnlyList<IEvent> Events => [Erstes, Zweites, Drittes, Viertes, Fuenftes, Sechstes];
}

/// <summary>Ein Bündel aus 7 Events (<see cref="IBuendel"/>).</summary>
public sealed record Buendel<T1, T2, T3, T4, T5, T6, T7>(T1 Erstes, T2 Zweites, T3 Drittes, T4 Viertes, T5 Fuenftes, T6 Sechstes, T7 Siebtes) : IBuendel where T1 : IEvent where T2 : IEvent where T3 : IEvent where T4 : IEvent where T5 : IEvent where T6 : IEvent where T7 : IEvent
{
    public IReadOnlyList<IEvent> Events => [Erstes, Zweites, Drittes, Viertes, Fuenftes, Sechstes, Siebtes];
}

/// <summary>Ein Bündel aus 8 Events (<see cref="IBuendel"/>).</summary>
public sealed record Buendel<T1, T2, T3, T4, T5, T6, T7, T8>(T1 Erstes, T2 Zweites, T3 Drittes, T4 Viertes, T5 Fuenftes, T6 Sechstes, T7 Siebtes, T8 Achtes) : IBuendel where T1 : IEvent where T2 : IEvent where T3 : IEvent where T4 : IEvent where T5 : IEvent where T6 : IEvent where T7 : IEvent where T8 : IEvent
{
    public IReadOnlyList<IEvent> Events => [Erstes, Zweites, Drittes, Viertes, Fuenftes, Sechstes, Siebtes, Achtes];
}

/// <summary>Ein Bündel aus 9 Events (<see cref="IBuendel"/>).</summary>
public sealed record Buendel<T1, T2, T3, T4, T5, T6, T7, T8, T9>(T1 Erstes, T2 Zweites, T3 Drittes, T4 Viertes, T5 Fuenftes, T6 Sechstes, T7 Siebtes, T8 Achtes, T9 Neuntes) : IBuendel where T1 : IEvent where T2 : IEvent where T3 : IEvent where T4 : IEvent where T5 : IEvent where T6 : IEvent where T7 : IEvent where T8 : IEvent where T9 : IEvent
{
    public IReadOnlyList<IEvent> Events => [Erstes, Zweites, Drittes, Viertes, Fuenftes, Sechstes, Siebtes, Achtes, Neuntes];
}

/// <summary>Ein Bündel aus 10 Events (<see cref="IBuendel"/>).</summary>
public sealed record Buendel<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10>(T1 Erstes, T2 Zweites, T3 Drittes, T4 Viertes, T5 Fuenftes, T6 Sechstes, T7 Siebtes, T8 Achtes, T9 Neuntes, T10 Zehntes) : IBuendel where T1 : IEvent where T2 : IEvent where T3 : IEvent where T4 : IEvent where T5 : IEvent where T6 : IEvent where T7 : IEvent where T8 : IEvent where T9 : IEvent where T10 : IEvent
{
    public IReadOnlyList<IEvent> Events => [Erstes, Zweites, Drittes, Viertes, Fuenftes, Sechstes, Siebtes, Achtes, Neuntes, Zehntes];
}

/// <summary>Ein Bündel aus 11 Events (<see cref="IBuendel"/>).</summary>
public sealed record Buendel<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11>(T1 Erstes, T2 Zweites, T3 Drittes, T4 Viertes, T5 Fuenftes, T6 Sechstes, T7 Siebtes, T8 Achtes, T9 Neuntes, T10 Zehntes, T11 Elftes) : IBuendel where T1 : IEvent where T2 : IEvent where T3 : IEvent where T4 : IEvent where T5 : IEvent where T6 : IEvent where T7 : IEvent where T8 : IEvent where T9 : IEvent where T10 : IEvent where T11 : IEvent
{
    public IReadOnlyList<IEvent> Events => [Erstes, Zweites, Drittes, Viertes, Fuenftes, Sechstes, Siebtes, Achtes, Neuntes, Zehntes, Elftes];
}

/// <summary>Ein Bündel aus 12 Events (<see cref="IBuendel"/>).</summary>
public sealed record Buendel<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12>(T1 Erstes, T2 Zweites, T3 Drittes, T4 Viertes, T5 Fuenftes, T6 Sechstes, T7 Siebtes, T8 Achtes, T9 Neuntes, T10 Zehntes, T11 Elftes, T12 Zwoelftes) : IBuendel where T1 : IEvent where T2 : IEvent where T3 : IEvent where T4 : IEvent where T5 : IEvent where T6 : IEvent where T7 : IEvent where T8 : IEvent where T9 : IEvent where T10 : IEvent where T11 : IEvent where T12 : IEvent
{
    public IReadOnlyList<IEvent> Events => [Erstes, Zweites, Drittes, Viertes, Fuenftes, Sechstes, Siebtes, Achtes, Neuntes, Zehntes, Elftes, Zwoelftes];
}

/// <summary>Ein Bündel aus 13 Events (<see cref="IBuendel"/>).</summary>
public sealed record Buendel<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12, T13>(T1 Erstes, T2 Zweites, T3 Drittes, T4 Viertes, T5 Fuenftes, T6 Sechstes, T7 Siebtes, T8 Achtes, T9 Neuntes, T10 Zehntes, T11 Elftes, T12 Zwoelftes, T13 Dreizehntes) : IBuendel where T1 : IEvent where T2 : IEvent where T3 : IEvent where T4 : IEvent where T5 : IEvent where T6 : IEvent where T7 : IEvent where T8 : IEvent where T9 : IEvent where T10 : IEvent where T11 : IEvent where T12 : IEvent where T13 : IEvent
{
    public IReadOnlyList<IEvent> Events => [Erstes, Zweites, Drittes, Viertes, Fuenftes, Sechstes, Siebtes, Achtes, Neuntes, Zehntes, Elftes, Zwoelftes, Dreizehntes];
}

/// <summary>Ein Bündel aus 14 Events (<see cref="IBuendel"/>).</summary>
public sealed record Buendel<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12, T13, T14>(T1 Erstes, T2 Zweites, T3 Drittes, T4 Viertes, T5 Fuenftes, T6 Sechstes, T7 Siebtes, T8 Achtes, T9 Neuntes, T10 Zehntes, T11 Elftes, T12 Zwoelftes, T13 Dreizehntes, T14 Vierzehntes) : IBuendel where T1 : IEvent where T2 : IEvent where T3 : IEvent where T4 : IEvent where T5 : IEvent where T6 : IEvent where T7 : IEvent where T8 : IEvent where T9 : IEvent where T10 : IEvent where T11 : IEvent where T12 : IEvent where T13 : IEvent where T14 : IEvent
{
    public IReadOnlyList<IEvent> Events => [Erstes, Zweites, Drittes, Viertes, Fuenftes, Sechstes, Siebtes, Achtes, Neuntes, Zehntes, Elftes, Zwoelftes, Dreizehntes, Vierzehntes];
}

/// <summary>Ein Bündel aus 15 Events (<see cref="IBuendel"/>).</summary>
public sealed record Buendel<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12, T13, T14, T15>(T1 Erstes, T2 Zweites, T3 Drittes, T4 Viertes, T5 Fuenftes, T6 Sechstes, T7 Siebtes, T8 Achtes, T9 Neuntes, T10 Zehntes, T11 Elftes, T12 Zwoelftes, T13 Dreizehntes, T14 Vierzehntes, T15 Fuenfzehntes) : IBuendel where T1 : IEvent where T2 : IEvent where T3 : IEvent where T4 : IEvent where T5 : IEvent where T6 : IEvent where T7 : IEvent where T8 : IEvent where T9 : IEvent where T10 : IEvent where T11 : IEvent where T12 : IEvent where T13 : IEvent where T14 : IEvent where T15 : IEvent
{
    public IReadOnlyList<IEvent> Events => [Erstes, Zweites, Drittes, Viertes, Fuenftes, Sechstes, Siebtes, Achtes, Neuntes, Zehntes, Elftes, Zwoelftes, Dreizehntes, Vierzehntes, Fuenfzehntes];
}

/// <summary>Ein Bündel aus 16 Events (<see cref="IBuendel"/>).</summary>
public sealed record Buendel<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12, T13, T14, T15, T16>(T1 Erstes, T2 Zweites, T3 Drittes, T4 Viertes, T5 Fuenftes, T6 Sechstes, T7 Siebtes, T8 Achtes, T9 Neuntes, T10 Zehntes, T11 Elftes, T12 Zwoelftes, T13 Dreizehntes, T14 Vierzehntes, T15 Fuenfzehntes, T16 Sechzehntes) : IBuendel where T1 : IEvent where T2 : IEvent where T3 : IEvent where T4 : IEvent where T5 : IEvent where T6 : IEvent where T7 : IEvent where T8 : IEvent where T9 : IEvent where T10 : IEvent where T11 : IEvent where T12 : IEvent where T13 : IEvent where T14 : IEvent where T15 : IEvent where T16 : IEvent
{
    public IReadOnlyList<IEvent> Events => [Erstes, Zweites, Drittes, Viertes, Fuenftes, Sechstes, Siebtes, Achtes, Neuntes, Zehntes, Elftes, Zwoelftes, Dreizehntes, Vierzehntes, Fuenfzehntes, Sechzehntes];
}
