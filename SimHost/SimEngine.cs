using System.Collections.Concurrent;
using System.Reflection;
using System.Text.Json;
using Abstractions;
using Infrastructure;            // generierte AggregateHandlerFactory (liegt in Domain.dll, Namespace Infrastructure)
using Domain.Prozess;            // generierte GeneratedProzessRegeln

namespace SimHost;

// ── Ausgabe-DTOs (an das Board) ──────────────────────────────────────────────
public record TraceItem(string Kind, string Name);
public record TraceFrame(List<TraceItem> Add, string Note);
public record StateSnapshot(string Aggregate, string Id, Dictionary<string, object?> Fields);
public record StepResult(List<TraceFrame> Frames, List<StateSnapshot> States, string? Error = null);
public record CommandSchema(string Name, string Aggregate, bool Creation, List<FieldSchema> Fields);
public record FieldSchema(string Name, string Type);

/// <summary>
/// Die actor-befreite Runtime: führt die GENERIERTEN Decider/Applier + die echten Saga-Regeln
/// SINGLE-THREADED im Prozess aus — dieselbe Logik wie im Cluster, nur ohne Proto.Actor/Marten/Redis.
/// Ein Command wird mit echten Werten geschickt; der ECHTE OneOf-Zweig fällt aus den Werten + dem
/// aktuellen (in-memory gehaltenen) Aggregat-Zustand. Emittierte Events treiben die Sagas (Regel.Sende),
/// deren Folge-Commands wieder ausgeführt werden — die ganze Kaskade, wertabhängig.
/// </summary>
public sealed class SimEngine
{
    private readonly Assembly _domain;
    private readonly AggregateHandlerFactory _factory = new();

    private readonly Type _iCommand, _iEvent, _iTransient, _iState, _iCreation;
    private readonly Dictionary<Type, Type> _cmdToState = new();      // Command-Typ → State-Typ
    private readonly Dictionary<Type, string> _stateName = new();     // State-Typ → Aggregat-Name
    private readonly Dictionary<string, Type> _cmdByName = new(StringComparer.Ordinal);

    // Sitzungs-Zustand: je Session die Aggregat-States + die Saga-Markierungen.
    private readonly ConcurrentDictionary<string, Session> _sessions = new();

    public SimEngine()
    {
        _iCommand = typeof(ICommand); _iEvent = typeof(IEvent); _iTransient = typeof(ITransientEvent);
        _iState = typeof(IState); _iCreation = typeof(ICreationCommand);
        _domain = typeof(Domain.Konto.Konto).Assembly;

        foreach (var t in _domain.GetTypes())
        {
            if (t.IsClass && !t.IsAbstract && _iCommand.IsAssignableFrom(t))
                _cmdByName[t.Name] = t;
            if (t.IsClass && !t.IsAbstract && _iState.IsAssignableFrom(t))
            {
                _stateName[t] = t.Name;
                var decider = t.GetNestedType("Decider");
                if (decider != null)
                    foreach (var m in decider.GetMethods().Where(m => m.Name == "Decide" && m.GetParameters().Length == 1))
                        _cmdToState[m.GetParameters()[0].ParameterType] = t;
            }
        }
    }

    // ── Schema für die Formulare im Board ────────────────────────────────────
    public List<CommandSchema> Schema() =>
        _cmdByName.Values.Select(c =>
        {
            _cmdToState.TryGetValue(c, out var st);
            var ctor = c.GetConstructors().OrderByDescending(x => x.GetParameters().Length).First();
            return new CommandSchema(c.Name, st != null ? _stateName[st] : "?", _iCreation.IsAssignableFrom(c),
                ctor.GetParameters().Select(p => new FieldSchema(p.Name!, Pretty(p.ParameterType))).ToList());
        }).OrderBy(c => c.Aggregate).ThenBy(c => c.Name).ToList();

    public void Reset(string sessionId) => _sessions.TryRemove(sessionId, out _);

    // ── Ein Command mit Werten schicken → ganze wertabhängige Kaskade ────────
    public StepResult Step(string sessionId, string commandName, JsonElement values)
    {
        if (!_cmdByName.TryGetValue(commandName, out var cmdType))
            return new StepResult(new(), new(), $"Unbekannter Command: {commandName}");

        ICommand cmd;
        try { cmd = (ICommand)JsonSerializer.Deserialize(values.GetRawText(), cmdType, JsonOpts)!; }
        catch (Exception ex) { return new StepResult(new(), new(), $"Werte passen nicht zu {commandName}: {ex.Message}"); }

        var session = _sessions.GetOrAdd(sessionId, _ => new Session());
        var frames = new List<TraceFrame>();
        var rootCorr = ReadAggregateId(cmd);

        frames.Add(new TraceFrame(new() { new("command", commandName) },
            $"Command <b>{commandName}</b> geschickt ({ValuePreview(cmd)})"));

        var queue = new Queue<(ICommand Cmd, Guid Corr)>();
        queue.Enqueue((cmd, rootCorr));
        var guard = 0;

        while (queue.Count > 0 && guard++ < 400)
        {
            var (c, corr) = queue.Dequeue();
            if (!_cmdToState.TryGetValue(c.GetType(), out var stateType))
            {
                frames.Add(new TraceFrame(new(), $"⚠ {c.GetType().Name} wird von keinem Aggregat behandelt."));
                continue;
            }
            var id = ReadAggregateId(c);
            var state = session.GetOrCreate(stateType, id);
            var handler = _factory.CreateHandler(state);

            List<IEvent> produced;
            try { produced = handler.HandleCommand(c).ToList(); }
            catch (Exception ex) { frames.Add(new TraceFrame(new(), $"⚠ Fehler in {c.GetType().Name}: {ex.Message}")); continue; }

            var persisted = new List<IEvent>();
            var rejects = new List<IEvent>();
            foreach (var e in produced)
            {
                if (_iTransient.IsAssignableFrom(e.GetType())) { rejects.Add(e); }
                else { handler.ApplyEvent(e); BumpVersion(state); persisted.Add(e); }
            }

            var items = new List<TraceItem> { new("aggregate", _stateName[stateType]) };
            persisted.ForEach(e => items.Add(new("event", e.GetType().Name)));
            rejects.ForEach(e => items.Add(new("event", e.GetType().Name)));
            var outNote = persisted.Count > 0
                ? $"Aggregat <b>{_stateName[stateType]}</b> → " + string.Join(", ", persisted.Select(e => $"<b>{e.GetType().Name}</b> ({ValuePreview(e)})"))
                : $"Aggregat <b>{_stateName[stateType]}</b> → ⃠ ABGELEHNT: " + string.Join(", ", rejects.Select(e => e.GetType().Name));
            frames.Add(new TraceFrame(items, outNote));

            // Sagas voran treiben (nur mit persistierten Events; Ablehnungen triggern nichts).
            foreach (var e in persisted)
                foreach (var next in AdvanceSagas(e, corr, session, frames))
                    queue.Enqueue((next, corr));
        }

        return new StepResult(frames, SnapshotStates(session));
    }

    // ── Saga-Auswertung (dieselben Regeln wie im Cluster, in-memory gefaltet) ──
    private List<ICommand> AdvanceSagas(IEvent evt, Guid corr, Session session, List<TraceFrame> frames)
    {
        var next = new List<ICommand>();
        foreach (var (procName, rules) in GeneratedProzessRegeln.Alle)
        {
            var isAusloeser = rules.AuslöserTyp == evt.GetType();
            var isTeil = rules.TeilnehmendeEvents.Contains(evt.GetType());
            if (!isAusloeser && !isTeil) continue;

            var key = procName + ":" + corr;
            var neu = false;
            if (isAusloeser && !session.Markings.ContainsKey(key)) neu = true;
            if (!session.Markings.ContainsKey(key) && !isAusloeser) continue;   // Prozess ohne Auslöser nicht starten
            var inst = session.Markings.GetOrAdd(key, _ => new Marking());
            if (neu) frames.Add(new TraceFrame(new() { new("process", procName) }, $"Saga <b>{procName}</b> erwacht (Auslöser {evt.GetType().Name})"));
            inst.Arrived.Add(evt);

            for (var ri = 0; ri < rules.Regeln.Count; ri++)
            {
                if (inst.Fired.Contains(ri)) continue;
                var matched = TryMatch(rules.Regeln[ri], rules, inst.Arrived);
                if (matched == null) continue;
                inst.Fired.Add(ri);
                IReadOnlyList<ICommand> cmds;
                try { cmds = rules.Regeln[ri].Sende(matched); }
                catch (Exception ex) { frames.Add(new TraceFrame(new(), $"⚠ Regel {ri} von {procName}: {ex.Message}")); continue; }
                if (cmds.Count == 0) continue;
                next.AddRange(cmds);
                frames.Add(new TraceFrame(cmds.Select(x => new TraceItem("command", x.GetType().Name)).ToList(),
                    $"Saga <b>{procName}</b> feuert → " + string.Join(", ", cmds.Select(x => $"<b>{x.GetType().Name}</b>"))));
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
            if (e == null) return null;
            matched.Add(e);
        }
        if (r.Sammel != null)
        {
            var aus = arrived.FirstOrDefault(x => x.GetType() == pr.AuslöserTyp);
            if (aus == null) return null;
            var need = r.Sammel.Anzahl(aus);
            var sammels = arrived.Where(x => x.GetType() == r.Sammel.Typ).ToList();
            if (sammels.Count < need) return null;
            matched.AddRange(sammels);
        }
        return matched;
    }

    // ── Helfer ────────────────────────────────────────────────────────────────
    private static readonly JsonSerializerOptions JsonOpts = new() { PropertyNameCaseInsensitive = true };

    private static Guid ReadAggregateId(object msg)
    {
        var p = msg.GetType().GetProperty("AggregateId");
        return p?.GetValue(msg) is Guid g ? g : Guid.Empty;
    }
    private static void BumpVersion(IState state)
    {
        var p = state.GetType().GetProperty("Version");
        if (p != null && p.CanWrite && p.GetValue(state) is { } v)
            p.SetValue(state, Convert.ChangeType((Convert.ToInt64(v) + 1), p.PropertyType));
    }
    private List<StateSnapshot> SnapshotStates(Session s) =>
        s.States.Select(kv => new StateSnapshot(_stateName.GetValueOrDefault(kv.Value.GetType(), kv.Value.GetType().Name),
            ReadAggregateId(kv.Value).ToString()[..8], Fields(kv.Value))).ToList();

    private static Dictionary<string, object?> Fields(object o) =>
        o.GetType().GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Where(p => p.GetIndexParameters().Length == 0 && p.Name != "Id")
            .ToDictionary(p => p.Name, p => Safe(p, o));
    private static object? Safe(PropertyInfo p, object o){ try { var v=p.GetValue(o); return v is System.Collections.ICollection c ? c.Count+" Einträge" : v; } catch { return null; } }

    private static string ValuePreview(object msg) =>
        string.Join(", ", msg.GetType().GetProperties().Where(p => p.Name != "AggregateId" && p.GetIndexParameters().Length == 0)
            .Select(p => { try { return $"{p.Name}={p.GetValue(msg)}"; } catch { return p.Name; } }).Take(4));

    private static string Pretty(Type t) => t.Name switch
    {
        "Guid" => "guid", "Decimal" => "decimal", "Int32" => "int", "Int64" => "long", "Boolean" => "bool", "String" => "string",
        _ => t.IsGenericType ? t.Name.Split('`')[0] + "<…>" : t.Name
    };

    private sealed class Session
    {
        public readonly Dictionary<string, IState> States = new();
        public readonly ConcurrentDictionary<string, Marking> Markings = new();
        public IState GetOrCreate(Type stateType, Guid id)
        {
            var key = stateType.FullName + ":" + id;
            if (!States.TryGetValue(key, out var s)) { s = (IState)Activator.CreateInstance(stateType)!; States[key] = s; }
            return s;
        }
    }
    private sealed class Marking { public readonly List<IEvent> Arrived = new(); public readonly HashSet<int> Fired = new(); }
}
