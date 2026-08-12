using System.Text.Json.Serialization;

namespace GraphExtractor;

// ════════════════════════════════════════════════════════════════════════════
//  Der Wissensgraph als typisiertes Property-Graph-Modell.
//
//  Bewusst NICHT mehr isolierte Listen pro Bausteinart (so war der alte Extractor),
//  sondern EIN gerichteter, kausaler Graph aus `Nodes` + `Edges`, plus abgeleitete
//  `Views` (Fanout, Kausalketten, Diagnosen). Damit ist er traversierbar:
//  „Was passiert, wenn Command X feuert?" = einem Pfad an Kanten folgen.
//
//  Jede Kante trägt `Provenance` — woher die Kausalität stammt. Die Kern-Kanten
//  (command→aggregat, aggregat→event) kommen aus der AUTORITATIVEN Routing-Wahrheit
//  `Infrastructure.Mapping.GeneratedCommandRouting`; die Saga-Kanten aus dem Prozess-DSL.
// ════════════════════════════════════════════════════════════════════════════

public sealed class KnowledgeGraph
{
    public GraphMeta Meta { get; set; } = new();
    public List<Node> Nodes { get; set; } = new();
    public List<Edge> Edges { get; set; } = new();
    public GraphViews Views { get; set; } = new();
}

public sealed class GraphMeta
{
    public string Note { get; set; } =
        "Wissensgraph des CQRS/ES-Frameworks. Kern-Kausalität aus GeneratedCommandRouting (autoritativ), Sagas aus dem Prozess-DSL.";

    /// <summary>„GeneratedCommandRouting" (autoritativ) oder „decider-fallback".</summary>
    public string RoutingSource { get; set; } = "unbekannt";

    public Dictionary<string, int> Counts { get; set; } = new();

    /// <summary>Alle Bounded Contexts (Domänen), sortiert — für Filter/Canvas-Clustering.</summary>
    public List<string> Contexts { get; set; } = new();
}

// ── Knoten ──────────────────────────────────────────────────────────────────

public enum NodeKind { aggregate, command, @event, process, projection, query, pipeline }

public sealed class Node
{
    /// <summary>Stabile Id, präfix-getypt: z.B. <c>cmd:BelasteKonto</c>, <c>evt:KontoBelastet</c>, <c>proc:BestellProzess</c>.</summary>
    public string Id { get; set; } = "";

    [JsonConverter(typeof(JsonStringEnumConverter))]
    public NodeKind Kind { get; set; }

    public string Name { get; set; } = "";
    public string? FullName { get; set; }
    public string? Namespace { get; set; }

    /// <summary>Bounded Context (Domäne) — für Canvas-Clustering + Filter. Bei Commands/Events das behandelnde/emittierende Aggregat.</summary>
    public string? Context { get; set; }

    // Kind-spezifische Nutzlast — nur die passende serialisiert (WhenWritingNull).
    public AggregateInfo? Aggregate { get; set; }
    public CommandInfo? Command { get; set; }
    public EventInfo? Event { get; set; }
    public ProcessInfo? Process { get; set; }
    public ProjectionInfo? Projection { get; set; }
    public QueryInfo? Query { get; set; }
    public PipelineInfo? Pipeline { get; set; }
}

public sealed class AggregateInfo
{
    public List<FieldInfo> State { get; set; } = new();
    public List<string> Handles { get; set; } = new();  // Command-Namen, die dieses Aggregat entscheidet
    public List<string> Emits { get; set; } = new();     // Event-Namen, die es produziert
}

public sealed class CommandInfo
{
    public bool IsCreation { get; set; }
    /// <summary>Aggregat, das den Command behandelt — aus GeneratedCommandRouting (autoritativ). Null ⇒ nicht geroutet.</summary>
    public string? RoutedTo { get; set; }
    /// <summary>Woher der Command kommt: client | process | process-compensation | pipeline | (mehrere).</summary>
    public List<string> Origin { get; set; } = new();
    /// <summary>
    /// Das GEKAPSELTE Ergebnis-Universum dieses Commands aus der Decide-OneOf-Signatur (handler-sensitiv):
    /// genau die Events, die dieser eine Handler alternativ produzieren kann — Erfolg (persistiert) ODER
    /// Ablehnung. Das ist die präzise Relation, nicht die aggregat-grobe „emittiert irgendwann".
    /// </summary>
    public List<CommandOutcome> Produces { get; set; } = new();
    public List<FieldInfo> Fields { get; set; } = new();
}

/// <summary>Ein möglicher Ausgang eines Decide-OneOf: ein Event + ob es persistiert (Erfolg) oder eine Ablehnung ist.</summary>
public sealed class CommandOutcome
{
    public string Event { get; set; } = "";
    public bool Persisted { get; set; }

    /// <summary>
    /// Der Guard-Ausdruck aus der Decider-Syntax, der diesen Zweig wählt (z.B.
    /// <c>State.Verfuegbar &lt; cmd.Betrag</c>) — das „Warum". Null = der ungeschützte
    /// (Sonst-)Zweig. Statisch aus dem Syntaxbaum gehoben (parsen statt nachbauen), damit
    /// der Debugger ihn zur Laufzeit mit echten Werten binden kann.
    /// </summary>
    public string? Guard { get; set; }
}

public sealed class EventInfo
{
    /// <summary>true = persistiert (IEvent), false = Ablehnung (ITransientEvent, nie im Log, kann keine Regel triggern).</summary>
    public bool Persisted { get; set; }
    public string? EmittedBy { get; set; }   // Aggregat-Name
}

public sealed class ProcessInfo
{
    public string Trigger { get; set; } = "";        // Auslöser-Event (simple name)
    /// <summary>In GeneratedProzessRegeln.Alle verdrahtet? Definiert-aber-nicht-registriert ist eine Diagnose.</summary>
    public bool Registered { get; set; }
    public string Pattern { get; set; } = "Linear";  // Diamant | Fan-out | Verkettung | Linear
    public List<SagaRule> Rules { get; set; } = new();
}

/// <summary>Eine Transition (Regel) der Saga — die statisch bekannten Kanten des Petri-Netzes.</summary>
public sealed class SagaRule
{
    public int Index { get; set; }
    public List<string> When { get; set; } = new();  // Bedingungs-Event-Namen (Konjunktion)
    public string Join { get; set; } = "single";     // single | and | count
    public string? Sammel { get; set; }              // Count-Join-Event (bei Join=count)
    public bool FanOut { get; set; }                 // SendeJe → N Commands aus einem Match
    public string Sends { get; set; } = "";          // gefeuerter Command
    public string? Compensates { get; set; }         // Gegenzug-Command (RückgängigDurch)
}

public sealed class ProjectionInfo
{
    public string SubscriberId { get; set; } = "";
    public List<string> Consumes { get; set; } = new(); // Event-Namen
}

public sealed class QueryInfo
{
    public string? ServedByReader { get; set; }
    public string? ReadsProjection { get; set; }
    public List<FieldInfo> Fields { get; set; } = new();
}

public sealed class PipelineInfo
{
    public string PipelineId { get; set; } = "";
    public List<PipelineHandle> Handles { get; set; } = new();
}

public sealed class PipelineHandle
{
    public string Input { get; set; } = "";
    public string InputKind { get; set; } = "event"; // trigger | event
    public List<string> Emits { get; set; } = new();  // Command-Namen
}

public sealed class FieldInfo
{
    public string Name { get; set; } = "";
    public string Type { get; set; } = "";
}

// ── Kanten ──────────────────────────────────────────────────────────────────

public enum EdgeKind
{
    routedTo,       // command  → aggregate   (GeneratedCommandRouting.CommandToAggregate, autoritativ)
    produces,       // command  → event       (Decide-OneOf, handler-sensitiv; persistiert autoritativ aus CommandToEvents, Ablehnung aus Decider)
    triggers,       // event    → process     (Prozess-DSL: Auslöser)
    sends,          // process  → command     (Prozess-DSL: Sende/SendeJe) — Via = ruleIndex
    compensates,    // process  → command     (Prozess-DSL: RückgängigDurch) — Via = ruleIndex
    advances,       // event    → process     (Prozess-DSL: Bedingungs-Event einer Nicht-Auslöser-Regel)
    consumedBy,     // event    → projection  (ISubscriber.Handle)
    readsFrom,      // query    → projection  (IReader<TProjection>)
    pipelineEmits   // pipeline → command
}

public sealed class Edge
{
    public string From { get; set; } = "";
    public string To { get; set; } = "";

    [JsonConverter(typeof(JsonStringEnumConverter))]
    public EdgeKind Kind { get; set; }

    /// <summary>GeneratedCommandRouting | decider | process-dsl | subscriber | reader | pipeline.</summary>
    public string Provenance { get; set; } = "";

    /// <summary>Kontext: bei `emits` der Command; bei `sends`/`compensates`/`advances` der Regel-Index.</summary>
    public string? Via { get; set; }
}

// ── Abgeleitete Sichten ──────────────────────────────────────────────────────

public sealed class GraphViews
{
    public List<EventFanout> EventFanout { get; set; } = new();
    public List<CausalChain> CausalChains { get; set; } = new();
    public List<Finding> Diagnostics { get; set; } = new();
}

/// <summary>Pro Event: alles, was es auslöst — der Blast-Radius eines Events.</summary>
public sealed class EventFanout
{
    public string Event { get; set; } = "";
    public bool Persisted { get; set; }
    public List<string> Projections { get; set; } = new();
    public List<string> TriggersProcesses { get; set; } = new();  // als Auslöser
    public List<string> AdvancesProcesses { get; set; } = new();  // als Bedingungs-Event
    public List<string> Pipelines { get; set; } = new();
}

/// <summary>Eine Kausalkette ab einem Start-Command: command → event → (process) → command → …</summary>
public sealed class CausalChain
{
    public string Start { get; set; } = "";      // Start-Command
    public string Process { get; set; } = "";    // beteiligter Prozess (falls einer)
    public List<string> Steps { get; set; } = new(); // menschenlesbare Pfad-Schritte
}

public sealed class Finding
{
    public string Severity { get; set; } = "info"; // error | warning | info
    public string Code { get; set; } = "";
    public string Message { get; set; } = "";
}
