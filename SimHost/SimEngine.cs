using System.Collections.Concurrent;
using System.Reflection;
using System.Text.Json;
using Abstractions;
using Cqrs.Testing;              // DER GETEILTE KERN: SagaLaufwerk + GuardBinder + DslSchreiber
using Infrastructure;            // generierte AggregateHandlerFactory (liegt in Domain.dll, Namespace Infrastructure)
using Domain.Prozess;            // generierte GeneratedProzessRegeln

namespace SimHost;

// ── Ausgabe-DTOs (an das Board) ──────────────────────────────────────────────
public record TraceItem(string Kind, string Name);
public record TraceFrame(List<TraceItem> Add, string Note);
public record StateSnapshot(string Aggregate, string Id, string Label, Dictionary<string, object?> Fields);
public record FeldAenderung(string Feld, object? Vorher, object? Nachher);
public record ZustandsAenderung(string Aggregate, string Id, string Label, string Command, List<FeldAenderung> Aenderungen);
public record StepResult(List<TraceFrame> Frames, List<StateSnapshot> States, List<ZustandsAenderung> Changes, string? Error = null);
public record CommandSchema(string Name, string Aggregate, bool Creation, List<FieldSchema> Fields);
public record FieldSchema(string Name, string Type);

/// <summary>
/// Die actor-befreite Runtime als DÜNNE Fassade auf den geteilten <see cref="SagaLaufwerk"/>
/// (aus Cqrs.Testing) — dieselbe store-freie Kaskade, die auch die Test-DSL treibt. Der SimHost
/// steuert nur noch das HTTP-Drumherum bei: Command-Deserialisierung, Frame-/State-Mapping fürs
/// Board, die Guard-Bindung mit echten Werten, die Abdeckung und den DSL-Export. Die Ausführungs-
/// logik (Routing, Decider, Marking-Fold) lebt an EINER Stelle im Kern.
/// </summary>
public sealed class SimEngine
{
    private readonly AggregateHandlerFactory _factory = new();
    private readonly IReadOnlyList<(string Name, ProzessRegeln Regeln)> _prozesse;

    private readonly Type _iCommand, _iState, _iCreation;
    private readonly Dictionary<Type, Type> _cmdToState = new();      // Command-Typ → State-Typ (nur fürs Schema)
    private readonly Dictionary<Type, string> _stateName = new();     // State-Typ → Aggregat-Name
    private readonly Dictionary<string, Type> _cmdByName = new(StringComparer.Ordinal);

    private readonly ConcurrentDictionary<string, Session> _sessions = new();

    // Guard-Ausdrücke je "CmdName|EvtName" (aus knowledge-graph.json, offline gehoben) — das „Warum".
    private readonly Dictionary<string, string> _guards = new(StringComparer.Ordinal);
    // Abdeckung: berührte Graph-Ids über alle Sessions (welche Zweige je gefeuert wurden).
    private readonly HashSet<string> _coverage = new();

    public SimEngine()
    {
        _iCommand = typeof(ICommand); _iState = typeof(IState); _iCreation = typeof(ICreationCommand);
        var domain = typeof(Domain.Konto.Konto).Assembly;

        // Nur Anzeige-Metadaten fürs Board-Formular — die Ausführung routet der Kern selbst.
        foreach (var t in domain.GetTypes())
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

        _prozesse = GeneratedProzessRegeln.Alle.Select(kv => (kv.Key, kv.Value)).ToList();
        LadeGuards();
    }

    // Guards aus knowledge-graph.json (neben der .sln) laden — offline gehoben, hier nur gelesen.
    private void LadeGuards()
    {
        var dir = Directory.GetCurrentDirectory();
        for (var i = 0; i < 10 && dir != null; i++)
        {
            var p = Path.Combine(dir, "knowledge-graph.json");
            if (File.Exists(p))
            {
                try
                {
                    using var doc = JsonDocument.Parse(File.ReadAllText(p));
                    foreach (var node in doc.RootElement.GetProperty("nodes").EnumerateArray())
                    {
                        if (!node.TryGetProperty("command", out var cmd) || !node.TryGetProperty("name", out var nm)) continue;
                        if (!cmd.TryGetProperty("produces", out var prod)) continue;
                        foreach (var o in prod.EnumerateArray())
                            if (o.TryGetProperty("guard", out var g) && g.ValueKind == JsonValueKind.String)
                                _guards[nm.GetString() + "|" + o.GetProperty("event").GetString()] = g.GetString()!;
                    }
                }
                catch { /* Guards sind optional — ohne sie zeigt das Board eben kein „weil …". */ }
                return;
            }
            dir = Path.GetDirectoryName(dir);
        }
    }

    public string CoverageJson()
    {
        lock (_coverage)
            return "[" + string.Join(",", _coverage.OrderBy(x => x, StringComparer.Ordinal).Select(x => "\"" + x + "\"")) + "]";
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

    // ── Board-Session → DSL-Quelltext (Regressionstest exportieren) ──────────
    public string Dsl(string sessionId)
    {
        if (!_sessions.TryGetValue(sessionId, out var s) || s.LastRoot is null)
            return "// Noch kein Command geschickt — erst ein Command absenden.";
        if (!_cmdToState.TryGetValue(s.LastRoot.GetType(), out var stateType))
            return "// Das letzte Command wird von keinem Aggregat behandelt.";

        var zielId = s.LastRoot.AggregateId;
        // Vorab = frühere Wurzel-Commands auf dasselbe Aggregat (dieselbe Id), außer dem letzten.
        var vorab = s.Root.Take(s.Root.Count - 1)
            .Where(c => _cmdToState.TryGetValue(c.GetType(), out var st) && st == stateType && c.AggregateId == zielId)
            .ToList();

        return DslSchreiber.Aus(_stateName[stateType], Array.Empty<IEvent>(), vorab, s.LastRoot, s.LastAusgang);
    }

    // ── Ein Command mit Werten schicken → ganze wertabhängige Kaskade (über den Kern) ──
    public StepResult Step(string sessionId, string commandName, JsonElement values)
    {
        if (!_cmdByName.TryGetValue(commandName, out var cmdType))
            return new StepResult(new(), new(), new(), $"Unbekannter Command: {commandName}");

        ICommand cmd;
        try { cmd = (ICommand)JsonSerializer.Deserialize(values.GetRawText(), cmdType, JsonOpts)!; }
        catch (Exception ex) { return new StepResult(new(), new(), new(), $"Werte passen nicht zu {commandName}: {ex.Message}"); }

        var session = _sessions.GetOrAdd(sessionId, _ => new Session { Lauf = new SagaLaufwerk(_factory, _prozesse) });
        session.Root.Add(cmd);
        session.LastRoot = cmd;

        var frames = new List<TraceFrame>
        {
            new(new() { new("command", commandName) }, $"Command <b>{commandName}</b> geschickt ({ValuePreview(cmd)})")
        };

        // Der geteilte Kern fährt die ganze wertabhängige Kaskade.
        var trace = session.Lauf.Fahre(cmd);

        foreach (var s in trace.Schritte)
            frames.Add(FrameFür(s));

        // Ausgang des Wurzel-Commands → speist die DSL-Zusicherung (Dann/DannAbgelehnt).
        session.LastAusgang = trace.Schritte.FirstOrDefault(x => ReferenceEquals(x.Command, cmd))?.Ausgang.ToList() ?? new();

        // Änderungen je berührtem Aggregat (aus Zustand-vorher/nachher) → Hervorhebung + Historie im Board.
        var changes = new List<ZustandsAenderung>();
        foreach (var s in trace.Schritte)
        {
            if (s.Unrouted) continue;
            var diffs = new List<FeldAenderung>();
            foreach (var kv in s.ZustandNachher)
                if (!Equals(s.ZustandVorher.GetValueOrDefault(kv.Key), kv.Value))
                    diffs.Add(new FeldAenderung(kv.Key, s.ZustandVorher.GetValueOrDefault(kv.Key), kv.Value));
            changes.Add(new ZustandsAenderung(s.AggregatTyp, s.AggregatId.ToString(),
                LabelFür(session, s.AggregatTyp, s.AggregatId), s.Command.GetType().Name, diffs));
        }

        return new StepResult(frames, Zustaende(session), changes);
    }

    /// <summary>Alle Aggregat-Instanzen der Session mit ihren aktuellen Werten (für die Instanzen-Liste).</summary>
    public List<StateSnapshot> ZustandListe(string sessionId)
        => _sessions.TryGetValue(sessionId, out var s) ? Zustaende(s) : new();

    private List<StateSnapshot> Zustaende(Session s)
    {
        foreach (var z in s.Lauf.AlleZustände()) LabelFür(s, z.Typ, z.Id); // erst alle beschriften (stabile Nummern)
        return s.Lauf.AlleZustände()
            .Select(z => new StateSnapshot(z.Typ, z.Id.ToString(), LabelFür(s, z.Typ, z.Id), new Dictionary<string, object?>(z.Felder)))
            .OrderBy(x => x.Label, StringComparer.Ordinal)
            .ToList();
    }

    // Stabiles, lesbares Label je (Typ, Id) in Anlege-Reihenfolge: „Konto #1", „Konto #2", …
    private static string LabelFür(Session s, string typ, Guid id)
    {
        if (s.Labels.TryGetValue((typ, id), out var l)) return l;
        s.Counter.TryGetValue(typ, out var n);
        l = $"{typ} #{n + 1}";
        s.Counter[typ] = n + 1;
        s.Labels[(typ, id)] = l;
        return l;
    }

    private TraceFrame FrameFür(SagaSchritt s)
    {
        var cmdName = s.Command.GetType().Name;
        if (s.Unrouted)
            return new TraceFrame(new(), $"⚠ <b>{cmdName}</b> wird von keinem Aggregat behandelt (im Cluster ein Hang).");

        // Abdeckung: welche Zweige dieses Command je gefeuert hat.
        lock (_coverage)
        {
            _coverage.Add("cmd:" + cmdName);
            foreach (var e in s.Ausgang)
            {
                _coverage.Add("evt:" + e.Typ);
                _coverage.Add($"produces:{cmdName}->{e.Typ}");
            }
        }

        // Das „Warum": den Guard-Ausdruck des gefeuerten Zweigs mit echten Werten binden.
        string Warum(Ereignis e) =>
            _guards.TryGetValue(cmdName + "|" + e.Typ, out var g)
                ? $" <span style=\"opacity:.65\">[weil {System.Net.WebUtility.HtmlEncode(GuardBinder.Binde(g, s.ZustandVorher, s.Command))}]</span>"
                : "";

        var items = new List<TraceItem>();
        if (s.Ursprung == Ursprung.Saga && s.SagaName is not null) items.Add(new("process", s.SagaName));
        items.Add(new("command", cmdName));
        items.Add(new("aggregate", s.AggregatTyp));
        foreach (var e in s.Ausgang) items.Add(new("event", e.Typ));

        var praefix = s.Ursprung == Ursprung.Saga ? $"Saga <b>{s.SagaName}</b> feuert <b>{cmdName}</b> → " : "";
        var persist = s.Ausgang.Where(a => a.Persistent).ToList();
        var note = persist.Count > 0
            ? praefix + $"Aggregat <b>{s.AggregatTyp}</b> → " + string.Join(", ", persist.Select(e => $"<b>{e.Typ}</b> ({ValuePreview(e.Event)}){Warum(e)}"))
            : praefix + $"Aggregat <b>{s.AggregatTyp}</b> → ⃠ ABGELEHNT: " + string.Join(", ", s.Ausgang.Where(a => a.Ablehnung).Select(e => $"{e.Typ}{Warum(e)}"));
        return new TraceFrame(items, note);
    }

    // ── Helfer ────────────────────────────────────────────────────────────────
    private static readonly JsonSerializerOptions JsonOpts = new() { PropertyNameCaseInsensitive = true };

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
        public required SagaLaufwerk Lauf { get; init; }
        public readonly List<ICommand> Root = new();        // Wurzel-Commands (die der Nutzer schickte)
        public ICommand? LastRoot;                          // das zuletzt geschickte
        public List<Ereignis> LastAusgang = new();          // dessen Ausgang (für die DSL-Zusicherung)
        public readonly Dictionary<(string, Guid), string> Labels = new(); // stabile Instanz-Labels
        public readonly Dictionary<string, int> Counter = new();           // laufende Nummer je Typ
    }
}
