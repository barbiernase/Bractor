using Abstractions;

namespace Cqrs.Testing;

/// <summary>
/// Einstieg in die Test-/Debug-DSL für die Schreibseite: <c>Gegeben → Wenn → Dann</c>.
///
/// Die DSL treibt die ECHTE generierte Domänenlogik store-frei (über
/// <see cref="IAggregateHandlerFactory"/> → <see cref="IAggregateHandler"/>) — dieselbe
/// Maschine wie SimHost, nur mit Assertions statt HTTP. Jedes Szenario liefert eine
/// inspizierbare <see cref="SzenarioTrace"/>; die Assertions lesen daraus ab, und bei
/// Fehlschlag zeigt die Ausnahme die getroffene Entscheidung (Zustand-vorher → Zweig).
/// </summary>
public static class Szenario
{
    /// <param name="fabrik">Die generierte Fabrik, z.B. <c>new AggregateHandlerFactory()</c> (in Domain.dll).</param>
    /// <param name="aggregatId">Optionale Aggregat-Id; ohne Angabe eine frische Guid.</param>
    public static SzenarioBauer<TState> Für<TState>(IAggregateHandlerFactory fabrik, Guid? aggregatId = null)
        where TState : class, IState, new()
        => new(fabrik, aggregatId ?? Guid.NewGuid());
}

/// <summary>
/// Sammelt Arrange (<see cref="Gegeben"/> / <see cref="Vorab"/>) und führt bei <see cref="Wenn"/>
/// den ganzen Pfad store-frei aus.
/// </summary>
public sealed class SzenarioBauer<TState> where TState : class, IState, new()
{
    private readonly IAggregateHandlerFactory _fabrik;
    private readonly Guid _id;
    private readonly List<IEvent> _gegeben = new();
    private readonly List<ICommand> _vorab = new();

    internal SzenarioBauer(IAggregateHandlerFactory fabrik, Guid id)
    {
        _fabrik = fabrik;
        _id = id;
    }

    public Guid AggregatId => _id;

    /// <summary>
    /// Seedet die Historie als EVENTS (der Log bis hier). Baut den Zustand per <c>Apply</c>,
    /// ohne den Decider laufen zu lassen — die reinste, kanonische Given-Form (Invariante 1:
    /// die Wahrheit ist der Log). Mehrfach/aneinanderreihbar.
    /// </summary>
    public SzenarioBauer<TState> Gegeben(params IEvent[] events)
    {
        _gegeben.AddRange(events);
        return this;
    }

    /// <summary>
    /// Seedet die Historie als COMMANDS (Arrange, das der echte Decider+Applier durchläuft).
    /// Bequem, board-nah — genau, was beim „Board-Session als Test exportieren" herausfällt.
    /// Eine Ablehnung hier ist fast immer ein Setup-Fehler und wird deshalb laut.
    /// </summary>
    public SzenarioBauer<TState> Vorab(params ICommand[] commands)
    {
        _vorab.AddRange(commands);
        return this;
    }

    /// <summary>
    /// Führt Given-Events, dann Vorab-Commands, dann das getestete Command aus und liefert das
    /// inspizierbare Ergebnis.
    /// </summary>
    public Ergebnis<TState> Wenn(ICommand command)
    {
        var state = new TState { Id = _id, Version = 0 };
        var handler = _fabrik.CreateHandler(state);
        var schritte = new List<Schritt>();

        // 1) Given-Events: Zustand falten, ohne Decider.
        foreach (var e in _gegeben)
        {
            handler.ApplyEvent(e);
            state.Version++;
        }

        // 2) Vorab-Commands: echter Decider+Applier, protokolliert. Ablehnung = Setup-Fehler.
        foreach (var c in _vorab)
        {
            var schritt = Ausführen(handler, state, c, SchrittHerkunft.Vorab);
            schritte.Add(schritt);
            var abgelehnt = schritt.Ablehnungen.FirstOrDefault();
            if (abgelehnt is not null)
                throw new SzenarioFehler(
                    $"Vorab-Command {c.GetType().Name} wurde abgelehnt ({abgelehnt.Typ}) — der gegebene " +
                    "Zustand ist damit nicht der gemeinte. Arrange gehört in .Gegeben(...) oder muss " +
                    "sauber durchlaufen.\n\n" + Bau(schritte, state).AlsText());
        }

        // 3) Das eine getestete Command.
        schritte.Add(Ausführen(handler, state, command, SchrittHerkunft.Wenn));

        return new Ergebnis<TState>(state, Bau(schritte, state));
    }

    private SzenarioTrace Bau(List<Schritt> schritte, TState state) => new()
    {
        AggregatId = _id,
        AggregatTyp = typeof(TState).Name,
        Gegeben = _gegeben.ToList(),
        Schritte = schritte.ToList(),
        Endzustand = Zustandsspiegel.Von(state),
    };

    private static Schritt Ausführen(IAggregateHandler handler, TState state, ICommand cmd, SchrittHerkunft herkunft)
    {
        var vorher = Zustandsspiegel.Von(state);
        var ausgang = new List<Ereignis>();
        foreach (var e in handler.HandleCommand(cmd).ToList())
        {
            var persistent = e is not ITransientEvent;
            ausgang.Add(new Ereignis(e, persistent));
            if (persistent)
            {
                handler.ApplyEvent(e);
                state.Version++; // im Betrieb macht das die AggregateActorBase
            }
        }
        return new Schritt
        {
            Command = cmd,
            AggregatId = state.Id,
            AggregatTyp = typeof(TState).Name,
            Herkunft = herkunft,
            Ausgang = ausgang,
            ZustandVorher = vorher,
            ZustandNachher = Zustandsspiegel.Von(state),
        };
    }
}

/// <summary>
/// Das ausgeführte Szenario: die <see cref="Trace"/> plus der typisierte Endzustand. Die
/// <c>Dann…</c>-Assertions lesen vom letzten (Wenn-)Schritt ab; jede Fehlermeldung hängt den
/// vollen Entscheidungs-Bericht an.
/// </summary>
public sealed class Ergebnis<TState> where TState : class, IState, new()
{
    private readonly TState _state;

    internal Ergebnis(TState state, SzenarioTrace trace)
    {
        _state = state;
        Trace = trace;
    }

    /// <summary>Die inspizierbare Reise durch den Entscheidungsraum.</summary>
    public SzenarioTrace Trace { get; }

    /// <summary>Der typisierte Endzustand — für freie Assertions (z.B. FluentAssertions).</summary>
    public TState Zustand => _state;

    private Schritt Wenn => Trace.Letzter;

    /// <summary>Die persistenten Events des Wenn-Schritts stimmen exakt (Reihenfolge + Wert) mit den erwarteten.</summary>
    public Ergebnis<TState> Dann(params IEvent[] erwartet)
    {
        var tatsächlich = Wenn.Persistente.Select(a => a.Event).ToList();
        if (!erwartet.SequenceEqual(tatsächlich))
            throw new SzenarioFehler(
                "Persistente Events weichen ab.\n" +
                $"  erwartet:    {Berichte.Liste(erwartet)}\n" +
                $"  tatsächlich: {Berichte.Liste(tatsächlich)}\n\n" + Trace.AlsText());
        return this;
    }

    /// <summary>Der Wenn-Schritt erzeugte (mindestens) ein persistentes Event dieses Typs.</summary>
    public Ergebnis<TState> Dann<TEvent>() where TEvent : IEvent
    {
        if (!Wenn.Persistente.Any(a => a.Event is TEvent))
            throw new SzenarioFehler(
                $"Erwartet persistentes {typeof(TEvent).Name}, aber der Decider wählte: " +
                $"{Berichte.Liste(Wenn.Ausgang.Select(a => a.Event))}.\n\n" + Trace.AlsText());
        return this;
    }

    /// <summary>Der Wenn-Schritt wurde mit dieser Ablehnung (ITransientEvent) beschieden.</summary>
    public Ergebnis<TState> DannAbgelehnt<TEvent>() where TEvent : ITransientEvent
    {
        if (!Wenn.Ablehnungen.Any(a => a.Event is TEvent))
            throw new SzenarioFehler(
                $"Erwartet Ablehnung {typeof(TEvent).Name}, aber der Decider lieferte: " +
                $"{Berichte.Liste(Wenn.Ausgang.Select(a => a.Event))}.\n\n" + Trace.AlsText());
        return this;
    }

    /// <summary>Der Wenn-Schritt erzeugte keinerlei Ablehnung.</summary>
    public Ergebnis<TState> DannKeineAblehnung()
    {
        if (Wenn.Ablehnungen.Count > 0)
            throw new SzenarioFehler(
                $"Unerwartete Ablehnung: {Berichte.Liste(Wenn.Ablehnungen.Select(a => a.Event))}.\n\n" + Trace.AlsText());
        return this;
    }

    /// <summary>Der Endzustand erfüllt das Prädikat. <paramref name="weil"/> erscheint in der Fehlermeldung.</summary>
    public Ergebnis<TState> UndZustand(Func<TState, bool> prädikat, string? weil = null)
    {
        if (!prädikat(_state))
            throw new SzenarioFehler(
                $"Zustands-Bedingung verletzt{(weil is null ? "" : $": {weil}")}.\n" +
                $"  Endzustand: {Berichte.Felder(Trace.Endzustand)}\n\n" + Trace.AlsText());
        return this;
    }
}

/// <summary>
/// Assertion-Fehler der DSL. Die Meldung enthält immer den vollen Entscheidungs-Bericht,
/// damit man sofort sieht, WARUM der Decider so entschied — nicht nur, DASS es abwich.
/// </summary>
public sealed class SzenarioFehler : Exception
{
    public SzenarioFehler(string message) : base(message) { }
}
