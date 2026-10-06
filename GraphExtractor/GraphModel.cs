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

public enum NodeKind { aggregate, command, @event, process, projection, query, pipeline, akteur, vertrag, client }

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
    public AkteurInfo? Akteur { get; set; }
    public VertragInfo? Vertrag { get; set; }
    public ClientInfo? Client { get; set; }
}

/// <summary>
/// Ein Client (<c>interface IX : IClientVertrag</c>, docs/konzept-akteure.md §4) — die Software an der Leitung: getragene
/// Akteur-Vertrags-Teile, was er sendet/fragt, was er nur zur Kenntnis hört; verkörpert ist abgeleitet.
/// </summary>
public sealed class ClientInfo
{
    /// <summary>Handshake-Name (Interface ohne führendes I).</summary>
    public string Handshake { get; set; } = "";
    public List<string> Traegt { get; set; } = new();
    public List<string> Sendet { get; set; } = new();
    public List<string> Fragt { get; set; } = new();
    public List<string> Kenntnis { get; set; } = new();
    public List<string> Verkoerpert { get; set; } = new();
}

/// <summary>Ein Akteur (<c>IAkteur</c>): Art und was er spontan hineingibt (<c>IDarf&lt;T&gt;</c>, einfache Namen).</summary>
public sealed class AkteurInfo
{
    public string? Art { get; set; }
    public List<string> Darf { get; set; } = new();
    /// <summary>Id des Vertrags-Knotens (<c>vertrag:IX</c>), wenn der Akteur draußen auf Events reagiert.</summary>
    public string? Vertrag { get; set; }
}

/// <summary>
/// Ein Akteur-Vertrag (<c>interface IX : IAkteurVertrag&lt;X&gt;</c>, docs/konzept-akteure.md §3) — die Schnittstelle, gegen die der Client
/// programmiert: je <c>Auf(Event)</c> eine Zusage. Hash = derselbe, den Server-Tabelle und Python-Generat tragen.
/// </summary>
public sealed class VertragInfo
{
    public string Akteur { get; set; } = "";
    public string Hash { get; set; } = "";
    public List<VertragsZusage> Zusagen { get; set; } = new();
}

public sealed class VertragsZusage
{
    public string Eingang { get; set; } = "";
    public List<string> Ausgaenge { get; set; } = new();
    public bool Strom { get; set; }
}

public sealed class AggregateInfo
{
    public List<FieldInfo> State { get; set; } = new();
    public List<string> Handles { get; set; } = new();  // Command-Namen, die dieses Aggregat entscheidet
    public List<string> Emits { get; set; } = new();     // Event-Namen, die es produziert
    /// <summary>Echter Decide-Rumpf je Command-Simple-Name — für den Code-Round-trip (Code-Knoten im Editor).</summary>
    public Dictionary<string, string> DecideBodies { get; set; } = new();
    /// <summary>Echter Apply-Rumpf je Event-Simple-Name.</summary>
    public Dictionary<string, string> ApplyBodies { get; set; } = new();
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
}

public sealed class EventInfo
{
    /// <summary>true = persistiert (IEvent), false = Ablehnung (ITransientEvent, nie im Log, kann keine Regel triggern).</summary>
    public bool Persisted { get; set; }
    public string? EmittedBy { get; set; }   // Aggregat-Name
    /// <summary>Die Record-Felder des Events (für den Round-trip Code → Editor-Modell → Code).</summary>
    public List<FieldInfo> Fields { get; set; } = new();
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
    public string Sends { get; set; } = "";          // gefeuerter Command (leer, wenn die Regel eine Funktion ruft)
    public string? Ruft { get; set; }                // gerufene Katalog-Funktion (Rufe<F>)
    public string? Zeitlimit { get; set; }           // Zeitlimit-Ausdruck verbatim
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
    /// <summary>
    /// Nur State-Felder: gesetzt ⇒ ABGELEITETE Read-only-Property mit diesem Ausdruck
    /// (z. B. <c>Saldo - Reserviert</c>) statt eines gespeicherten <c>{ get; set; }</c>-Felds.
    /// Null ⇒ gespeichertes Feld. Für den Round-trip Code → Editor-Modell → Code.
    /// </summary>
    public string? Expr { get; set; }
    /// <summary>Default-/Initialwert verbatim (Record-Parameter <c>= null</c>, State-Initializer <c>= new()</c>).</summary>
    public string? Default { get; set; }
    /// <summary>Nur State-Felder: <c>{ get; }</c> statt <c>{ get; set; }</c>.</summary>
    public bool NurGet { get; set; }
    /// <summary>
    /// Nur Record-/Klassen-Felder: als Property deklariert (nicht als Positions-Parameter) — der Accessor-Satz
    /// verbatim normalisiert, z. B. <c>{ get; init; }</c>. Null ⇒ Positions-Parameter des Records.
    /// </summary>
    public string? Zugriff { get; set; }
    /// <summary>Property mit <c>required</c>.</summary>
    public bool Pflicht { get; set; }
    /// <summary>Sammlung: der Element-Typ, wenn der Feldtyp <c>IEnumerable&lt;T&gt;</c> ist (per Symbol, nicht per Name; <c>string</c> nicht).</summary>
    public string? ElementTyp { get; set; }
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
    pipelineEmits,  // pipeline → command
    darf,           // akteur   → command/query (IDarf<T>: spontan)
    hatVertrag,     // akteur   → vertrag     (IAkteurVertrag<A>)
    zusageAuf,    // event    → vertrag     (Auf(Event)) — Via = Event
    zusageGibt,   // vertrag  → command     (Rückgabe von Auf(Event)) — Via = Event
    traegt,         // client   → vertrag     (IClientVertrag erbt den Akteur-Vertrags-Teil)
    sendet,         // client   → command     (ISendet<T>)
    fragt,          // client   → query       (IFragt<T>)
    hoert,          // event    → client      (void Auf(E) im Client-Vertrag: Kenntnis)
    verkoerpert     // client   → akteur      (abgeleitet: Teil-Akteure + IDarf-Halter von Sendet/Fragt)
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
