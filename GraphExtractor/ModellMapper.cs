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

        // Aggregate: nur State. Decider/Applier sind eigenständig und verweisen aufs Aggregat.
        var aggNodes = graph.Nodes
            .Where(n => n.Kind == NodeKind.aggregate && n.Aggregate is not null && n.FullName is not null)
            .OrderBy(n => n.Name, StringComparer.Ordinal)
            .ToList();

        var aggregate = aggNodes.Select(n => new Aggregat
        {
            Name = n.Name,
            Namespace = n.Namespace ?? $"Domain.{n.Name}",
            State = n.Aggregate!.State.Select(MappeFeld).ToList(),
        }).ToList();

        var decider = new List<DecideRegel>();
        var applier = new List<ApplyRegel>();
        foreach (var n in aggNodes)
        {
            foreach (var cn in n.Aggregate!.Handles.Where(commandByName.ContainsKey).Select(name => commandByName[name]))
                decider.Add(new DecideRegel
                {
                    Aggregat = n.Name,
                    Command = cn.Name,
                    Ergibt = cn.Command!.Produces.Select(o => new Ausgang { Event = o.Event, Guard = o.Guard }).ToList(),
                });

            var persistente = n.Aggregate!.Handles
                .Where(commandByName.ContainsKey).Select(name => commandByName[name])
                .SelectMany(cn => cn.Command!.Produces).Where(o => o.Persisted).Select(o => o.Event)
                .Distinct(StringComparer.Ordinal);
            foreach (var ev in persistente)
                applier.Add(new ApplyRegel { Aggregat = n.Name, Event = ev });
        }

        var sagas = graph.Nodes
            .Where(n => n.Kind == NodeKind.process && n.Process is not null)
            .OrderBy(n => n.Name, StringComparer.Ordinal)
            .Select(n => MappeSaga(n, commandByName, eventByName))
            .ToList();

        return new EditorModell { Records = records, Aggregate = aggregate, Decider = decider, Applier = applier, Sagas = sagas };
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
