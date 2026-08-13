using DomainEditor;

namespace GraphExtractor;

/// <summary>
/// Der ROUND-TRIP: bestehenden Code (als Wissensgraph extrahiert) zurück ins record-zentrische
/// <see cref="EditorModell"/> lesen — Commands/Events als Records (kind-getaggt), Aggregate als
/// Komposition (State + Decider aus den Produces + Applier je persistentem Event). Guards, Feld-
/// Typen und abgeleitete State-Properties reisen mit.
///
/// EHRLICHE GRENZEN: Value Objects und Enums stehen nicht im Graph (keine Marker-Interfaces) und
/// werden nicht zurückgelesen; Feldtypen, die auf sie verweisen, kompilieren nur mit vorhandenem
/// Typ. Die Saga-Sende-Argumente sind Fachlogik und werden zu <c>default</c>-Platzhaltern.
/// </summary>
public static class ModellMapper
{
    public static EditorModell ZuEditorModell(KnowledgeGraph graph)
    {
        var commandNodes = graph.Nodes.Where(n => n.Kind == NodeKind.command && n.Command is not null).ToList();
        var eventNodes = graph.Nodes.Where(n => n.Kind == NodeKind.@event && n.Event is not null).ToList();
        var commandByName = Erste(commandNodes);
        var eventByName = Erste(eventNodes);

        // Records: Commands + Events (persistent/Ablehnung) als kind-getaggte Records.
        var records = new List<Record>();
        foreach (var n in commandNodes.OrderBy(n => n.Name, StringComparer.Ordinal))
            records.Add(new Record
            {
                Name = n.Name, Kind = RecordArt.Command, Namespace = n.Namespace ?? "Domain",
                IstErzeugung = n.Command!.IsCreation,
                Felder = n.Command!.Fields.Select(MappeFeld).ToList(),
            });
        foreach (var n in eventNodes.OrderBy(n => n.Name, StringComparer.Ordinal))
            records.Add(new Record
            {
                Name = n.Name, Kind = n.Event!.Persisted ? RecordArt.Event : RecordArt.Rejection,
                Namespace = n.Namespace ?? "Domain",
                Felder = n.Event!.Fields.Select(MappeFeld).ToList(),
            });

        // Aggregate: Komposition aus State + Decider + Applier.
        var aggregate = graph.Nodes
            .Where(n => n.Kind == NodeKind.aggregate && n.Aggregate is not null && n.FullName is not null)
            .OrderBy(n => n.Name, StringComparer.Ordinal)
            .Select(n => MappeAggregat(n, commandByName))
            .ToList();

        var sagas = graph.Nodes
            .Where(n => n.Kind == NodeKind.process && n.Process is not null)
            .OrderBy(n => n.Name, StringComparer.Ordinal)
            .Select(n => MappeSaga(n, commandByName, eventByName))
            .ToList();

        return new EditorModell { Records = records, Aggregate = aggregate, Sagas = sagas };
    }

    private static Aggregat MappeAggregat(Node node, IReadOnlyDictionary<string, Node> commandByName)
    {
        var info = node.Aggregate!;

        var decider = info.Handles
            .Where(commandByName.ContainsKey)
            .Select(name => commandByName[name])
            .Select(cn => new DecideRegel
            {
                Command = cn.Name,
                Ergibt = cn.Command!.Produces.Select(o => new Ausgang { Event = o.Event, Guard = o.Guard }).ToList(),
            })
            .ToList();

        // Applier: je persistentem Event, das die Commands des Aggregats produzieren (dedupliziert).
        var applier = decider
            .SelectMany(d => commandByName.TryGetValue(d.Command, out var cn) ? cn.Command!.Produces : [])
            .Where(o => o.Persisted)
            .Select(o => o.Event)
            .Distinct(StringComparer.Ordinal)
            .Select(name => new ApplyRegel { Event = name })
            .ToList();

        return new Aggregat
        {
            Name = node.Name,
            Namespace = node.Namespace ?? $"Domain.{node.Name}",
            State = info.State.Select(MappeFeld).ToList(),
            Decider = decider,
            Applier = applier,
        };
    }

    private static Saga MappeSaga(Node node, IReadOnlyDictionary<string, Node> commandByName, IReadOnlyDictionary<string, Node> eventByName)
    {
        var info = node.Process!;
        var ns = node.Namespace ?? $"Domain.{node.Name}";

        var schritte = info.Rules.Select(r => new SagaSchritt
        {
            Wenn = r.When.ToList(),
            Sende = r.Sends,
            Kompensation = r.Compensates,
        }).ToList();

        var referenzen = new List<string?> { NsVon(info.Trigger, eventByName) };
        foreach (var r in info.Rules)
        {
            foreach (var w in r.When) referenzen.Add(NsVon(w, eventByName));
            referenzen.Add(NsVon(r.Sends, commandByName));
            if (r.Compensates is not null) referenzen.Add(NsVon(r.Compensates, commandByName));
        }

        var extraUsings = referenzen
            .Where(x => x is not null && x != ns).Select(x => x!)
            .Distinct(StringComparer.Ordinal).OrderBy(x => x, StringComparer.Ordinal).ToList();

        return new Saga { Name = node.Name, Namespace = ns, TriggerEvent = info.Trigger, Schritte = schritte, ExtraUsings = extraUsings };
    }

    private static Feld MappeFeld(FieldInfo f) => new() { Name = f.Name, Typ = f.Type, Ausdruck = f.Expr };

    private static string? NsVon(string simpleName, IReadOnlyDictionary<string, Node> nach) =>
        nach.TryGetValue(simpleName, out var node) ? node.Namespace : null;

    private static Dictionary<string, Node> Erste(IEnumerable<Node> knoten)
    {
        var d = new Dictionary<string, Node>(StringComparer.Ordinal);
        foreach (var n in knoten) d.TryAdd(n.Name, n);
        return d;
    }
}
