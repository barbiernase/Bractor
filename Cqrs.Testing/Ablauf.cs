using Abstractions;

namespace Cqrs.Testing;

/// <summary>Ein Command-Schritt eines Ablaufs: wer ihn gab (null = von außen, sonst die Funktion bzw. <c>uhr</c>), an welches Aggregat, mit welchem Ausgang.</summary>
public sealed record AblaufSchritt
{
    public required ICommand Command { get; init; }
    public string? Von { get; init; }
    public required Guid AggregatId { get; init; }
    public required string AggregatTyp { get; init; }
    public required IReadOnlyList<Ereignis> Ausgang { get; init; }
    public required IReadOnlyDictionary<string, object?> ZustandVorher { get; init; }
    public required IReadOnlyDictionary<string, object?> ZustandNachher { get; init; }

    /// <summary>Kein angemeldetes Aggregat nimmt den Command — im Betrieb ginge er an ein Aggregat, das der Test nicht kennt.</summary>
    public bool Unrouted { get; init; }

    public override string ToString() => $"{(Von is null ? "→" : Von + " →")} {AggregatTyp}/{AggregatId.ToString()[..8]}: {Command.GetType().Name} ⇒ "
        + (Unrouted ? "UNROUTED" : string.Join(", ", Ausgang.Select(e => e.Typ + (e.Persistent ? "" : "!"))));
}

/// <summary>
/// Einstieg in die Ablauf-DSL (Abläufe sind Aggregate + Funktionen + Fristen): <c>Gegeben (Historien) →
/// Wenn (Command von außen) → Dann (Commands der Funktionen, Endzustände, Fristen)</c>. Treibt die ECHTEN Decider/Applier über die generierte Fabrik
/// und die ECHTEN Funktionen über ihre <c>Auf</c>-Methoden — store-frei, ohne Reflection: Aggregate und Kanten meldet der Test typisiert an.
/// Was der Funktions-Actor garantiert (Festhalten, Inbox, Position), prüft dieses Werkzeug nicht — nur, was die Bausteine fachlich entscheiden.
/// </summary>
public static class Ablauf
{
    public static AblaufBauer Mit(IAggregateHandlerFactory fabrik) => new(fabrik);
}

/// <summary>Die Welt eines Ablaufs: angemeldete Aggregate, Funktionen (je Eingangstyp ein Auf), Historien; <see cref="Wenn"/> treibt die Kaskade.</summary>
public sealed class AblaufBauer
{
    /// <summary>Ein Ablauf, der mehr Schritte braucht, kreist (R6) — der Test bricht ab, statt zu hängen.</summary>
    public const int HoechstensSchritte = 10_000;

    private sealed record AggregatArt(string Typ, Func<ICommand, bool> Nimmt, Func<Guid, IState> Neu);
    private sealed record Kante(string Funktion, Type Eingang, Func<IEvent, Herkunft, IEnumerable<IAusgabe>> Auf);
    private sealed class Instanz(IState state, IAggregateHandler handler, string typ)
    {
        public IState State { get; } = state;
        public IAggregateHandler Handler { get; } = handler;
        public string Typ { get; } = typ;
        public List<IEvent> Log { get; } = new();
    }

    private readonly IAggregateHandlerFactory _fabrik;
    private readonly List<AggregatArt> _arten = new();
    private readonly List<Kante> _kanten = new();
    private readonly Dictionary<(string, Guid), Instanz> _instanzen = new();
    private readonly List<(Guid Stream, int Version, FristGesetzt Frist)> _fristen = new();
    private readonly List<AblaufSchritt> _alle = new();

    /// <summary>Die Log-Zeit, die jede Herkunft trägt (fest, damit ein Ablauf beim Wiederholen gleich rechnet).</summary>
    public DateTimeOffset Zeit { get; set; } = new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);

    internal AblaufBauer(IAggregateHandlerFactory fabrik) => _fabrik = fabrik;

    /// <summary>Meldet ein Aggregat an: es nimmt jeden <c>ICommand&lt;TState&gt;</c>.</summary>
    public AblaufBauer Aggregat<TState>() where TState : class, IState, new()
    {
        _arten.Add(new AggregatArt(typeof(TState).Name, c => c is ICommand<TState>, id => new TState { Id = id, Version = 0 }));
        return this;
    }

    /// <summary>Eine Kante einer Funktion: ihr <c>Auf</c> für <typeparamref name="TEingang"/>, das nichts oder eine Ausgabe liefert.</summary>
    public AblaufBauer Funktion<TEingang>(string funktion, Func<TEingang, Herkunft, IAusgabe?> auf) where TEingang : IEvent
    {
        _kanten.Add(new Kante(funktion, typeof(TEingang), (e, h) => auf((TEingang)e, h) is { } a ? new[] { a } : Array.Empty<IAusgabe>()));
        return this;
    }

    /// <summary>Eine Kante einer Funktion, die einen Strom ausgibt (Fan-out).</summary>
    public AblaufBauer Funktion<TEingang>(string funktion, Func<TEingang, Herkunft, IEnumerable<IAusgabe>> auf) where TEingang : IEvent
    {
        _kanten.Add(new Kante(funktion, typeof(TEingang), (e, h) => auf((TEingang)e, h)));
        return this;
    }

    /// <summary>Seedet ein angemeldetes Aggregat mit seiner Historie (Apply ohne Decider).</summary>
    public AblaufBauer Gegeben<TState>(Guid id, params IEvent[] events) where TState : class, IState, new()
    {
        var i = Hole(typeof(TState).Name, id, () => new TState { Id = id, Version = 0 });
        foreach (var e in events)
        {
            i.Handler.ApplyEvent(e);
            i.State.Version++;
            i.Log.Add(e);
        }
        return this;
    }

    /// <summary>Ein Command von außen — und alles, was Funktionen daraus machen, bis der Ablauf ruht. Fristen bleiben stehen (<see cref="Fristen"/>).</summary>
    public AblaufErgebnis Wenn(ICommand command) => Laufe(new[] { ((ICommand)command, (string?)null) });

    /// <summary>Die Uhr: jede gesetzte, noch nicht gefeuerte Frist ist fällig — ihr Command geht an das Aggregat, das sie setzte.</summary>
    public AblaufErgebnis Fristen()
    {
        var faellig = _fristen.Select(f => (f.Frist.Befehl, (string?)"uhr")).ToList();
        _fristen.Clear();
        return Laufe(faellig);
    }

    /// <summary>Die gesetzten, noch nicht gefeuerten Fristen.</summary>
    public IReadOnlyList<FristGesetzt> OffeneFristen => _fristen.Select(f => f.Frist).ToList();

    private AblaufErgebnis Laufe(IEnumerable<(ICommand Command, string? Von)> start)
    {
        var schlange = new Queue<(ICommand, string?)>(start);
        var schritte = new List<AblaufSchritt>();
        while (schlange.Count > 0)
        {
            if (_alle.Count + schritte.Count >= HoechstensSchritte)
                throw new SzenarioFehler($"Der Ablauf ruht nicht nach {HoechstensSchritte} Schritten — ein Kreis ohne Zustandsschritt?\n"
                    + string.Join("\n", schritte.TakeLast(20)));
            var (cmd, von) = schlange.Dequeue();
            var art = _arten.FirstOrDefault(a => a.Nimmt(cmd));
            if (art is null)
            {
                schritte.Add(new AblaufSchritt
                {
                    Command = cmd, Von = von, AggregatId = cmd.Ziel, AggregatTyp = "?", Ausgang = Array.Empty<Ereignis>(), Unrouted = true,
                    ZustandVorher = new Dictionary<string, object?>(), ZustandNachher = new Dictionary<string, object?>(),
                });
                continue;
            }
            var i = Hole(art.Typ, cmd.Ziel, () => art.Neu(cmd.Ziel));
            var vorher = Zustandsspiegel.Von(i.State);
            var ausgang = new List<Ereignis>();
            var neu = new List<(IEvent Event, int Version)>();
            foreach (var e in i.Handler.HandleCommand(cmd).ToList())
            {
                var persistent = e is not ITransientEvent;
                ausgang.Add(new Ereignis(e, persistent));
                if (!persistent) continue;
                i.Handler.ApplyEvent(e);
                i.State.Version++;
                i.Log.Add(e);
                neu.Add((e, i.State.Version));
            }
            schritte.Add(new AblaufSchritt
            {
                Command = cmd, Von = von, AggregatId = cmd.Ziel, AggregatTyp = art.Typ, Ausgang = ausgang,
                ZustandVorher = vorher, ZustandNachher = Zustandsspiegel.Von(i.State),
            });
            foreach (var (e, version) in neu)
            {
                if (e is FristGesetzt f) { _fristen.Add((cmd.Ziel, version, f)); continue; }
                foreach (var k in _kanten.Where(k => k.Eingang == e.GetType()))
                    foreach (var a in k.Auf(e, new Herkunft(cmd.Ziel, version, Zeit, null, WirkerId.FuerFunktion(k.Funktion))))
                        if (a is ICommand c) schlange.Enqueue((c, k.Funktion));
            }
        }
        _alle.AddRange(schritte);
        return new AblaufErgebnis(this, schritte);
    }

    private Instanz Hole(string typ, Guid id, Func<IState> neu)
    {
        if (_instanzen.TryGetValue((typ, id), out var i)) return i;
        var state = neu();
        return _instanzen[(typ, id)] = new Instanz(state, _fabrik.CreateHandler(state), typ);
    }

    internal IState? Zustand(string typ, Guid id) => _instanzen.TryGetValue((typ, id), out var i) ? i.State : null;

    internal IReadOnlyList<IEvent> Log(string typ, Guid id) => _instanzen.TryGetValue((typ, id), out var i) ? i.Log : Array.Empty<IEvent>();

    /// <summary>Alle Schritte seit dem ersten <see cref="Wenn"/>.</summary>
    public IReadOnlyList<AblaufSchritt> Schritte => _alle;
}

/// <summary>Was ein <see cref="AblaufBauer.Wenn"/> bewirkte — die Schritte dieser Kaskade, dazu Zustand und Log jedes Aggregats der Welt.</summary>
public sealed class AblaufErgebnis
{
    private readonly AblaufBauer _welt;

    internal AblaufErgebnis(AblaufBauer welt, IReadOnlyList<AblaufSchritt> schritte)
    {
        _welt = welt;
        Schritte = schritte;
    }

    public IReadOnlyList<AblaufSchritt> Schritte { get; }

    /// <summary>Die Commands, die Funktionen (oder die Uhr) in dieser Kaskade gaben.</summary>
    public IReadOnlyList<ICommand> Gefeuert => Schritte.Where(s => s.Von is not null).Select(s => s.Command).ToList();

    public string AlsText() => string.Join("\n", Schritte);

    /// <summary>Eine Funktion gab in dieser Kaskade (mindestens) einen <typeparamref name="TCommand"/>.</summary>
    public AblaufErgebnis Feuert<TCommand>(int anzahl = -1) where TCommand : ICommand
    {
        var n = Gefeuert.Count(c => c is TCommand);
        if (anzahl < 0 ? n == 0 : n != anzahl)
            throw new SzenarioFehler($"Erwartet {(anzahl < 0 ? "mindestens einen" : anzahl.ToString())} {typeof(TCommand).Name} aus einer Funktion, "
                + $"gefeuert: {n}.\n\n{AlsText()}");
        return this;
    }

    /// <summary>Kein Command dieser Kaskade blieb ohne Aggregat.</summary>
    public AblaufErgebnis AllesGeroutet()
    {
        if (Schritte.FirstOrDefault(s => s.Unrouted) is { } s)
            throw new SzenarioFehler($"{s.Command.GetType().Name} nimmt kein angemeldetes Aggregat.\n\n{AlsText()}");
        return this;
    }

    /// <summary>Der Zustand des Aggregats <paramref name="id"/> erfüllt <paramref name="praedikat"/>.</summary>
    public AblaufErgebnis EndzustandVon<TState>(Guid id, Func<TState, bool> praedikat, string? weil = null) where TState : class, IState, new()
    {
        if (_welt.Zustand(typeof(TState).Name, id) is not TState s)
            throw new SzenarioFehler($"{typeof(TState).Name}/{id} kommt im Ablauf nicht vor.\n\n{AlsText()}");
        if (!praedikat(s))
            throw new SzenarioFehler($"Zustand von {typeof(TState).Name}/{id} verletzt{(weil is null ? "" : $": {weil}")}.\n"
                + $"  Endzustand: {Berichte.Felder(Zustandsspiegel.Von(s))}\n\n{AlsText()}");
        return this;
    }

    /// <summary>Die persistenten Events im Log des Aggregats <paramref name="id"/>, die <typeparamref name="TEvent"/> sind.</summary>
    public IReadOnlyList<TEvent> Events<TState, TEvent>(Guid id) where TState : class, IState, new() where TEvent : IEvent =>
        _welt.Log(typeof(TState).Name, id).OfType<TEvent>().ToList();
}
