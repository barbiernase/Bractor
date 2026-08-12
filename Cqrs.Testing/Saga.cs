using System.Reflection;
using Abstractions;

namespace Cqrs.Testing;

/// <summary>Ob ein Command-Schritt von außen (Wurzel) oder von einer Saga gefeuert wurde.</summary>
public enum Ursprung
{
    Wurzel,
    Saga
}

/// <summary>Ein Command-Schritt der Kaskade: wer feuerte, an welches Aggregat, mit welchem Ausgang.</summary>
public sealed record SagaSchritt
{
    public required ICommand Command { get; init; }
    public required Ursprung Ursprung { get; init; }
    public string? SagaName { get; init; }
    public required Guid AggregatId { get; init; }
    public required string AggregatTyp { get; init; }
    public required IReadOnlyList<Ereignis> Ausgang { get; init; }
    public required IReadOnlyDictionary<string, object?> ZustandNachher { get; init; }

    /// <summary>Kein Aggregat behandelt dieses Command — im Cluster ein Runtime-Hang (Graph-Diagnose UNROUTED-SAGA-CMD).</summary>
    public bool Unrouted { get; init; }
}

/// <summary>Eine noch nicht gefeuerte Transition und die Events, auf die sie (für diese Korrelation) noch wartet.</summary>
public sealed record WartendeRegel(IReadOnlyList<string> Bedingung, IReadOnlyList<string> Fehlt);

/// <summary>Der finale Zustand einer Saga-Instanz: was ankam und welche Transitionen offen blieben (halbgefülltes Join).</summary>
public sealed record SagaMarkierung
{
    public required string Prozess { get; init; }
    public required Guid Korrelation { get; init; }
    public required IReadOnlyList<string> Angekommen { get; init; }
    public required IReadOnlyList<WartendeRegel> Wartend { get; init; }
}

/// <summary>Die inspizierbare Reise einer Saga-Kaskade: die Command-Schritte plus die Marking-Endstände.</summary>
public sealed record SagaTrace
{
    public required ICommand Wurzel { get; init; }
    public required IReadOnlyList<SagaSchritt> Schritte { get; init; }
    public required IReadOnlyList<SagaMarkierung> Markierungen { get; init; }

    /// <summary>Die von Sagas gefeuerten Commands, in Reihenfolge.</summary>
    public IReadOnlyList<ICommand> Feuerungen => Schritte.Where(s => s.Ursprung == Ursprung.Saga).Select(s => s.Command).ToList();

    public string AlsText() => Berichte.Schreibe(this);
}

/// <summary>
/// Einstieg in die Saga-/Prozess-DSL: <c>Gegeben (Aggregat-Historie) → Wenn (Wurzel-Command) →
/// Erwarte (Feuerungen/Endzustände/offene Joins)</c>.
///
/// Treibt die ECHTEN Prozess-Regeln (<see cref="ProzessRegeln"/>) über denselben store-freien Kern
/// wie die Aggregat-DSL — eine headless Variante der SimEngine-Kaskade, nur mit Assertions statt
/// HTTP-Frames. Command→Aggregat-Routing wird aus den Decider-Signaturen der Fabrik-Assembly
/// gelesen (Tooling-Reflection, wie SimEngine).
/// </summary>
public static class SagaSzenario
{
    public static SagaBauer Mit(IAggregateHandlerFactory fabrik, params IProzessDefinition[] prozesse)
        => new(fabrik, prozesse);
}

/// <summary>Sammelt geseedete Aggregat-Historien und startet bei <see cref="Wenn"/> die Kaskade.</summary>
public sealed class SagaBauer
{
    private readonly SagaLaufwerk _lauf;

    internal SagaBauer(IAggregateHandlerFactory fabrik, IProzessDefinition[] prozesse)
        => _lauf = new SagaLaufwerk(fabrik, prozesse.Select(p => (p.GetType().Name, p.Regeln)).ToList());

    /// <summary>Seedet ein beteiligtes Aggregat mit seiner Event-Historie (z.B. die Konten mit Saldo).</summary>
    public SagaBauer Gegeben<TState>(Guid id, params IEvent[] events) where TState : class, IState, new()
    {
        _lauf.Seed(typeof(TState), id, events);
        return this;
    }

    /// <summary>Startet die Kaskade mit einem Wurzel-Command (das den Auslöser-Event erzeugt).</summary>
    public SagaErgebnis Wenn(ICommand wurzel)
        => new(_lauf, _lauf.Fahre(wurzel));

    /// <summary>Startet direkt mit einem Auslöser-Event + Korrelation (ohne das erzeugende Aggregat).</summary>
    public SagaErgebnis WennAusgelöst(IEvent auslöser, Guid korrelation)
        => new(_lauf, _lauf.FahreAbEvent(auslöser, korrelation));
}

/// <summary>Das gefahrene Saga-Szenario: die <see cref="Trace"/> plus die <c>Erwarte…</c>-Assertions.</summary>
public sealed class SagaErgebnis
{
    private readonly SagaLaufwerk _lauf;

    internal SagaErgebnis(SagaLaufwerk lauf, SagaTrace trace)
    {
        _lauf = lauf;
        Trace = trace;
    }

    public SagaTrace Trace { get; }

    /// <summary>Irgendeine Saga hat ein Command dieses Typs gefeuert.</summary>
    public SagaErgebnis Feuert<TCommand>() where TCommand : ICommand
    {
        if (!Trace.Schritte.Any(s => s.Ursprung == Ursprung.Saga && s.Command is TCommand))
            throw new SzenarioFehler($"Erwartet, dass eine Saga {typeof(TCommand).Name} feuert — tat sie nicht.\n\n" + Trace.AlsText());
        return this;
    }

    /// <summary>Keine Saga hat ein Command dieses Typs gefeuert.</summary>
    public SagaErgebnis FeuertNicht<TCommand>() where TCommand : ICommand
    {
        if (Trace.Schritte.Any(s => s.Ursprung == Ursprung.Saga && s.Command is TCommand))
            throw new SzenarioFehler($"Erwartet, dass {typeof(TCommand).Name} NICHT gefeuert wird — wurde es aber.\n\n" + Trace.AlsText());
        return this;
    }

    /// <summary>Die genannten Command-Typen erscheinen (als Teilfolge) in dieser Reihenfolge unter den Saga-Feuerungen.</summary>
    public SagaErgebnis FeuertInReihenfolge(params Type[] commandTypen)
    {
        var gefeuert = Trace.Feuerungen.Select(c => c.GetType()).ToList();
        var i = 0;
        foreach (var t in gefeuert)
            if (i < commandTypen.Length && t == commandTypen[i]) i++;
        if (i < commandTypen.Length)
            throw new SzenarioFehler(
                "Erwartete Feuer-Reihenfolge nicht gefunden.\n" +
                $"  erwartet (Teilfolge): [{string.Join(", ", commandTypen.Select(t => t.Name))}]\n" +
                $"  tatsächlich:          [{string.Join(", ", gefeuert.Select(t => t.Name))}]\n\n" + Trace.AlsText());
        return this;
    }

    public SagaErgebnis FeuertInReihenfolge<TA, TB>() => FeuertInReihenfolge(typeof(TA), typeof(TB));
    public SagaErgebnis FeuertInReihenfolge<TA, TB, TC>() => FeuertInReihenfolge(typeof(TA), typeof(TB), typeof(TC));

    /// <summary>Keine Saga feuerte ein Command, das kein Aggregat behandelt (UNROUTED-SAGA-CMD = Cluster-Hang).</summary>
    public SagaErgebnis KeinUnroutedCommand()
    {
        var tot = Trace.Schritte.FirstOrDefault(s => s.Unrouted);
        if (tot is not null)
            throw new SzenarioFehler($"Saga feuerte {tot.Command.GetType().Name}, das kein Aggregat behandelt — im Cluster ein Hang.\n\n" + Trace.AlsText());
        return this;
    }

    /// <summary>Eine Saga blieb an einem Join hängen und wartet noch auf ein Event dieses Typs.</summary>
    public SagaErgebnis SagaWartetAuf<TEvent>(string? prozess = null) where TEvent : IEvent
    {
        var fehlt = typeof(TEvent).Name;
        var treffer = Trace.Markierungen
            .Where(m => prozess is null || m.Prozess == prozess)
            .Any(m => m.Wartend.Any(w => w.Fehlt.Contains(fehlt)));
        if (!treffer)
            throw new SzenarioFehler($"Erwartet, dass eine Saga auf {fehlt} wartet (halbgefülltes Join) — tut sie nicht.\n\n" + Trace.AlsText());
        return this;
    }

    /// <summary>Keine Saga blieb offen — alle Transitionen sind gefeuert.</summary>
    public SagaErgebnis KeineOffeneSaga()
    {
        if (Trace.Markierungen.Any(m => m.Wartend.Count > 0))
            throw new SzenarioFehler("Es blieb (mindestens) eine Saga offen (halbgefülltes Join).\n\n" + Trace.AlsText());
        return this;
    }

    /// <summary>Der Endzustand des genannten Aggregats erfüllt das Prädikat.</summary>
    public SagaErgebnis EndzustandVon<TState>(Guid id, Func<TState, bool> prädikat, string? weil = null)
        where TState : class, IState, new()
    {
        var state = _lauf.Zustand<TState>(id);
        if (state is null)
            throw new SzenarioFehler($"Kein Zustand für {typeof(TState).Name} ({id:N})[..8] — nie berührt.\n\n" + Trace.AlsText());
        if (!prädikat(state))
            throw new SzenarioFehler(
                $"Endzustand von {typeof(TState).Name} verletzt die Bedingung{(weil is null ? "" : $": {weil}")}.\n" +
                $"  {Berichte.Felder(Reflektion.Felder(state))}\n\n" + Trace.AlsText());
        return this;
    }
}

/// <summary>
/// Der store-freie Saga-Motor — Command→Aggregat-Routing, gehaltene Aggregat-Zustände und die
/// Marking-Faltung. Spiegelt <c>SimEngine.AdvanceSagas</c>/<c>TryMatch</c> (dieselbe Kaskade,
/// nur ohne HTTP), damit Test und Board dasselbe Verhalten sehen.
/// </summary>
internal sealed class SagaLaufwerk
{
    private readonly IAggregateHandlerFactory _fabrik;
    private readonly IReadOnlyList<(string Name, ProzessRegeln Regeln)> _prozesse;
    private readonly Dictionary<Type, Type> _cmdToState = new();
    private readonly Dictionary<(Type, Guid), IState> _states = new();
    private readonly Dictionary<string, Instanz> _mark = new();

    public SagaLaufwerk(IAggregateHandlerFactory fabrik, IReadOnlyList<(string, ProzessRegeln)> prozesse)
    {
        _fabrik = fabrik;
        _prozesse = prozesse;
        // Command→State aus den Decider-Signaturen der Fabrik-Assembly (Domain.dll), wie SimEngine.
        foreach (var t in fabrik.GetType().Assembly.GetTypes())
        {
            if (!t.IsClass || t.IsAbstract || !typeof(IState).IsAssignableFrom(t)) continue;
            var decider = t.GetNestedType("Decider");
            if (decider is null) continue;
            foreach (var m in decider.GetMethods().Where(m => m.Name == "Decide" && m.GetParameters().Length == 1))
                _cmdToState[m.GetParameters()[0].ParameterType] = t;
        }
    }

    public void Seed(Type stateType, Guid id, IEnumerable<IEvent> events)
    {
        var state = Hole(stateType, id);
        var handler = _fabrik.CreateHandler(state);
        foreach (var e in events)
        {
            handler.ApplyEvent(e);
            state.Version++;
        }
    }

    public TState? Zustand<TState>(Guid id) where TState : class, IState
        => _states.TryGetValue((typeof(TState), id), out var s) ? (TState)s : null;

    public SagaTrace Fahre(ICommand wurzel)
    {
        var schritte = new List<SagaSchritt>();
        var queue = new Queue<(ICommand Cmd, Guid Corr, string? Saga)>();
        queue.Enqueue((wurzel, wurzel.AggregateId, null));
        Kaskade(queue, schritte);
        return Bau(wurzel, schritte);
    }

    public SagaTrace FahreAbEvent(IEvent auslöser, Guid korrelation)
    {
        var schritte = new List<SagaSchritt>();
        var queue = new Queue<(ICommand, Guid, string?)>();
        foreach (var (cmd, proc) in Advance(auslöser, korrelation))
            queue.Enqueue((cmd, korrelation, proc));
        Kaskade(queue, schritte);
        // Als Wurzel dient hier synthetisch der Auslöser (nur für den Bericht).
        return Bau(new AuslöserWurzel(auslöser), schritte);
    }

    private void Kaskade(Queue<(ICommand Cmd, Guid Corr, string? Saga)> queue, List<SagaSchritt> schritte)
    {
        var guard = 0;
        while (queue.Count > 0 && guard++ < 400)
        {
            var (c, corr, saga) = queue.Dequeue();
            var schritt = Ausführe(c, saga);
            schritte.Add(schritt);
            if (schritt.Unrouted) continue;
            foreach (var e in schritt.Ausgang.Where(a => a.Persistent).Select(a => a.Event))
                foreach (var (next, proc) in Advance(e, corr))
                    queue.Enqueue((next, corr, proc));
        }
    }

    private SagaSchritt Ausführe(ICommand c, string? saga)
    {
        var ursprung = saga is null ? Ursprung.Wurzel : Ursprung.Saga;
        if (!_cmdToState.TryGetValue(c.GetType(), out var stateType))
            return new SagaSchritt
            {
                Command = c, Ursprung = ursprung, SagaName = saga, Unrouted = true,
                AggregatId = c.AggregateId, AggregatTyp = "?",
                Ausgang = Array.Empty<Ereignis>(), ZustandNachher = new Dictionary<string, object?>(),
            };

        var state = Hole(stateType, c.AggregateId);
        var handler = _fabrik.CreateHandler(state);
        var ausgang = new List<Ereignis>();
        foreach (var e in handler.HandleCommand(c).ToList())
        {
            var persistent = e is not ITransientEvent;
            ausgang.Add(new Ereignis(e, persistent));
            if (persistent)
            {
                handler.ApplyEvent(e);
                state.Version++;
            }
        }
        return new SagaSchritt
        {
            Command = c, Ursprung = ursprung, SagaName = saga, Unrouted = false,
            AggregatId = c.AggregateId, AggregatTyp = stateType.Name,
            Ausgang = ausgang, ZustandNachher = Zustandsspiegel.Von(state),
        };
    }

    // Mirror von SimEngine.AdvanceSagas: Marking falten, aktivierte Transitionen feuern.
    private List<(ICommand Cmd, string Saga)> Advance(IEvent evt, Guid corr)
    {
        var next = new List<(ICommand, string)>();
        foreach (var (name, regeln) in _prozesse)
        {
            var isAuslöser = regeln.AuslöserTyp == evt.GetType();
            var isTeil = regeln.TeilnehmendeEvents.Contains(evt.GetType());
            if (!isAuslöser && !isTeil) continue;

            var key = name + ":" + corr;
            if (!_mark.ContainsKey(key))
            {
                if (!isAuslöser) continue; // Prozess ohne Auslöser nicht starten
                _mark[key] = new Instanz(name, corr, regeln);
            }
            var inst = _mark[key];
            inst.Arrived.Add(evt);

            for (var ri = 0; ri < regeln.Regeln.Count; ri++)
            {
                if (inst.Fired.Contains(ri)) continue;
                var matched = TryMatch(regeln.Regeln[ri], regeln, inst.Arrived);
                if (matched is null) continue;
                inst.Fired.Add(ri);
                foreach (var cmd in regeln.Regeln[ri].Sende(matched))
                    next.Add((cmd, name));
            }
        }
        return next;
    }

    private static IReadOnlyList<IEvent>? TryMatch(Regel r, ProzessRegeln pr, List<IEvent> arrived)
    {
        var matched = new List<IEvent>();
        foreach (var t in r.Bedingung)
        {
            var e = arrived.FirstOrDefault(x => x.GetType() == t);
            if (e is null) return null;
            matched.Add(e);
        }
        if (r.Sammel is not null)
        {
            var aus = arrived.FirstOrDefault(x => x.GetType() == pr.AuslöserTyp);
            if (aus is null) return null;
            var need = r.Sammel.Anzahl(aus);
            var sammels = arrived.Where(x => x.GetType() == r.Sammel.Typ).ToList();
            if (sammels.Count < need) return null;
            matched.AddRange(sammels);
        }
        return matched;
    }

    private IState Hole(Type stateType, Guid id)
    {
        if (!_states.TryGetValue((stateType, id), out var s))
        {
            s = (IState)Activator.CreateInstance(stateType)!;
            s.Id = id;
            _states[(stateType, id)] = s;
        }
        return s;
    }

    private SagaTrace Bau(ICommand wurzel, List<SagaSchritt> schritte) => new()
    {
        Wurzel = wurzel,
        Schritte = schritte,
        Markierungen = _mark.Values.Select(BauMarkierung).ToList(),
    };

    private static SagaMarkierung BauMarkierung(Instanz inst)
    {
        var wartend = new List<WartendeRegel>();
        for (var ri = 0; ri < inst.Regeln.Regeln.Count; ri++)
        {
            if (inst.Fired.Contains(ri)) continue;
            var r = inst.Regeln.Regeln[ri];
            var fehlt = r.Bedingung
                .Where(t => inst.Arrived.All(e => e.GetType() != t))
                .Select(t => t.Name).ToList();
            if (r.Sammel is not null)
            {
                var aus = inst.Arrived.FirstOrDefault(x => x.GetType() == inst.Regeln.AuslöserTyp);
                var need = aus is null ? -1 : r.Sammel.Anzahl(aus);
                var have = inst.Arrived.Count(x => x.GetType() == r.Sammel.Typ);
                if (need < 0 || have < need) fehlt.Add($"{r.Sammel.Typ.Name}×{(need < 0 ? "?" : need)} (habe {have})");
            }
            wartend.Add(new WartendeRegel(r.Bedingung.Select(t => t.Name).ToList(), fehlt));
        }
        return new SagaMarkierung
        {
            Prozess = inst.Prozess,
            Korrelation = inst.Korrelation,
            Angekommen = inst.Arrived.Select(e => e.GetType().Name).ToList(),
            Wartend = wartend,
        };
    }

    private sealed class Instanz
    {
        public Instanz(string prozess, Guid korrelation, ProzessRegeln regeln)
        {
            Prozess = prozess;
            Korrelation = korrelation;
            Regeln = regeln;
        }

        public string Prozess { get; }
        public Guid Korrelation { get; }
        public ProzessRegeln Regeln { get; }
        public List<IEvent> Arrived { get; } = new();
        public HashSet<int> Fired { get; } = new();
    }
}

/// <summary>Synthetischer Wurzel-„Command" für <see cref="SagaBauer.WennAusgelöst"/> — trägt nur den Auslöser für den Bericht.</summary>
internal sealed record AuslöserWurzel(IEvent Auslöser) : ICommand
{
    public Guid AggregateId => Guid.Empty;
    public override string ToString() => $"(ausgelöst durch {Auslöser})";
}
