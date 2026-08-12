using Abstractions;

namespace Cqrs.Testing;

/// <summary>
/// Woher ein Command-Schritt kam: aus dem Arrange (<see cref="Vorab"/>) oder das eine
/// getestete Command (<see cref="Wenn"/>). Given-Events sind KEIN Command-Schritt — sie
/// seeden nur den Zustand und stehen separat in <see cref="SzenarioTrace.Gegeben"/>.
/// </summary>
public enum SchrittHerkunft
{
    Vorab,
    Wenn
}

/// <summary>
/// Ein einzelnes vom Decider erzeugtes Ereignis plus die eine Unterscheidung, die zählt:
/// persistent (kommt in den Log) oder Ablehnung (<see cref="ITransientEvent"/>, wirkungslos).
/// </summary>
public sealed record Ereignis(IEvent Event, bool Persistent)
{
    public bool Ablehnung => !Persistent;
    public string Typ => Event.GetType().Name;
}

/// <summary>
/// EIN Entscheidungs-Schritt: ein Command traf auf einen Zustand, der Decider wählte einen
/// Zweig (<see cref="Ausgang"/>), der Applier entwickelte den Zustand weiter. Genau die drei
/// Dinge, die der Fachautor schreibt — nichts von der Technik dazwischen. Das ist der
/// Rohstoff, den später die GUI rendert.
/// </summary>
public sealed record Schritt
{
    public required ICommand Command { get; init; }
    public required Guid AggregatId { get; init; }
    public required string AggregatTyp { get; init; }
    public required SchrittHerkunft Herkunft { get; init; }

    /// <summary>Die tatsächlich erzeugten Ereignisse dieses Schritts, in Reihenfolge.</summary>
    public required IReadOnlyList<Ereignis> Ausgang { get; init; }

    /// <summary>Zustands-Momentaufnahme VOR dem Command (flache Feld→Wert-Map, inkl. abgeleiteter Felder).</summary>
    public required IReadOnlyDictionary<string, object?> ZustandVorher { get; init; }

    /// <summary>Zustands-Momentaufnahme NACH dem Command.</summary>
    public required IReadOnlyDictionary<string, object?> ZustandNachher { get; init; }

    public IReadOnlyList<Ereignis> Persistente => Ausgang.Where(a => a.Persistent).ToList();
    public IReadOnlyList<Ereignis> Ablehnungen => Ausgang.Where(a => a.Ablehnung).ToList();
}

/// <summary>
/// Die vollständige, inspizierbare Reise eines Szenarios durch den Entscheidungsraum:
/// die geseedete Historie (<see cref="Gegeben"/>), die Command-Schritte (Vorab + Wenn) und
/// der Endzustand. Eine Trace ist ein wertgebundener Teilpfad durch die Domäne — dasselbe
/// Objekt, das ein Test asserted, eine GUI erzeugt und ein Debugger abspielt.
/// </summary>
public sealed record SzenarioTrace
{
    public required Guid AggregatId { get; init; }
    public required string AggregatTyp { get; init; }
    public required IReadOnlyList<IEvent> Gegeben { get; init; }
    public required IReadOnlyList<Schritt> Schritte { get; init; }
    public required IReadOnlyDictionary<string, object?> Endzustand { get; init; }

    /// <summary>Der letzte Schritt — üblicherweise das getestete <c>Wenn</c>.</summary>
    public Schritt Letzter => Schritte[^1];

    /// <summary>Menschenlesbarer Entscheidungs-Bericht (Zustand-vorher → gewählter Zweig → nachher).</summary>
    public string AlsText() => Berichte.Schreibe(this);
}
