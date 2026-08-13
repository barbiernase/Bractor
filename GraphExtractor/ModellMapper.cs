using DomainEditor;

namespace GraphExtractor;

/// <summary>
/// Der ROUND-TRIP: bestehenden Code (als Wissensgraph bereits extrahiert) zurück ins editierbare
/// <see cref="EditorModell"/> lesen — damit man Vorhandenes im Editor weiterbaut statt bei null zu
/// beginnen. Reine Umschichtung des schon Extrahierten (inkl. Guards, Feld-Typen, abgeleiteten
/// Properties): eine Wahrheit, keine zweite Interpretation.
///
/// EHRLICHE GRENZE: die Argument-Ausdrücke der Saga-Sende-Lambdas (<c>t.Quelle</c> …) sind Fachlogik
/// und stehen nicht im Graph — der Scaffolder erzeugt dafür kompilierbare <c>default</c>-Platzhalter,
/// die im Editor/Stufe 3 gefüllt werden.
/// </summary>
public static class ModellMapper
{
    public static EditorModell ZuEditorModell(KnowledgeGraph graph)
    {
        var eventNodes = graph.Nodes.Where(n => n.Kind == NodeKind.@event).ToList();
        var commandNodes = graph.Nodes.Where(n => n.Kind == NodeKind.command).ToList();

        // Nachschlage-Tabellen nach einfachem Namen (kanonisch eindeutig; bei Kollision gewinnt der erste).
        var eventByName = Erste(eventNodes);
        var commandByName = Erste(commandNodes);

        var aggregate = graph.Nodes
            .Where(n => n.Kind == NodeKind.aggregate && n.Aggregate is not null && n.FullName is not null)
            .OrderBy(n => n.Name, StringComparer.Ordinal)
            .Select(n => MappeAggregat(n, commandByName, eventByName))
            .ToList();

        var sagas = graph.Nodes
            .Where(n => n.Kind == NodeKind.process && n.Process is not null)
            .OrderBy(n => n.Name, StringComparer.Ordinal)
            .Select(n => MappeSaga(n, commandByName, eventByName))
            .ToList();

        return new EditorModell { Aggregate = aggregate, Sagas = sagas };
    }

    private static Aggregat MappeAggregat(Node node, IReadOnlyDictionary<string, Node> commandByName, IReadOnlyDictionary<string, Node> eventByName)
    {
        var info = node.Aggregate!;

        var commands = info.Handles
            .Where(commandByName.ContainsKey)
            .Select(name => MappeBefehl(commandByName[name]))
            .ToList();

        // Die Events des Aggregats = alle in seinen Command-Ausgängen referenzierten Events.
        var eventNamen = commands
            .SelectMany(c => c.Ergibt.Select(a => a.Event))
            .Distinct(StringComparer.Ordinal);

        var events = eventNamen
            .Where(eventByName.ContainsKey)
            .Select(name => MappeEreignis(eventByName[name]))
            .ToList();

        return new Aggregat
        {
            Name = node.Name,
            Namespace = node.Namespace ?? $"Domain.{node.Name}",
            Felder = info.State.Select(MappeFeld).ToList(),
            Commands = commands,
            Events = events,
        };
    }

    private static Befehl MappeBefehl(Node node)
    {
        var info = node.Command!;
        return new Befehl
        {
            Name = node.Name,
            Felder = info.Fields.Select(MappeFeld).ToList(),
            Ergibt = info.Produces.Select(o => new Ausgang { Event = o.Event, Guard = o.Guard }).ToList(),
        };
    }

    private static Ereignis MappeEreignis(Node node)
    {
        var info = node.Event!;
        return new Ereignis
        {
            Name = node.Name,
            Felder = info.Fields.Select(MappeFeld).ToList(),
            Transient = !info.Persisted,
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
            // SendeArgumente bewusst null → default-Platzhalter (Argument-Ausdrücke sind Fachlogik).
        }).ToList();

        // ExtraUsings: die Namespaces der referenzierten (aggregat-fremden) Commands/Events.
        var referenzen = new List<string?> { NsVon(info.Trigger, eventByName) };
        foreach (var r in info.Rules)
        {
            foreach (var w in r.When) referenzen.Add(NsVon(w, eventByName));
            referenzen.Add(NsVon(r.Sends, commandByName));
            if (r.Compensates is not null) referenzen.Add(NsVon(r.Compensates, commandByName));
        }

        var extraUsings = referenzen
            .Where(x => x is not null && x != ns)
            .Select(x => x!)
            .Distinct(StringComparer.Ordinal)
            .OrderBy(x => x, StringComparer.Ordinal)
            .ToList();

        return new Saga
        {
            Name = node.Name,
            Namespace = ns,
            TriggerEvent = info.Trigger,
            Schritte = schritte,
            ExtraUsings = extraUsings,
        };
    }

    private static Feld MappeFeld(FieldInfo f) => new()
    {
        Name = f.Name,
        Typ = f.Type,
        Ausdruck = f.Expr,
    };

    private static string? NsVon(string simpleName, IReadOnlyDictionary<string, Node> nach) =>
        nach.TryGetValue(simpleName, out var node) ? node.Namespace : null;

    private static Dictionary<string, Node> Erste(IEnumerable<Node> knoten)
    {
        var d = new Dictionary<string, Node>(StringComparer.Ordinal);
        foreach (var n in knoten) d.TryAdd(n.Name, n);
        return d;
    }
}
