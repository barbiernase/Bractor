using System.Text.Json.Serialization;

namespace GraphExtractor;

// ════════════════════════════════════════════════════════════════════════════
//  Der Wissensgraph als typisiertes Property-Graph-Modell.
//
//  Bewusst NICHT mehr isolierte Listen pro Bausteinart (so war der alte Extractor),
//  sondern EIN gerichteter, kausaler Graph aus `Nodes` + `Edges`, plus abgeleitete
//  `Views` (Fanout, Diagnosen). Damit ist er traversierbar:
//  „Was passiert, wenn Command X feuert?" = einem Pfad an Kanten folgen.
//
//  Jede Kante trägt `Provenance` — woher die Kausalität stammt. Die Kern-Kanten
//  (command→aggregat, aggregat→event) kommen aus der AUTORITATIVEN Routing-Wahrheit
//  `Infrastructure.Mapping.GeneratedCommandRouting`.
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
        "Wissensgraph des CQRS/ES-Frameworks. Kern-Kausalität aus GeneratedCommandRouting (autoritativ).";

    /// <summary>„GeneratedCommandRouting" (autoritativ) oder „decider-fallback".</summary>
    public string RoutingSource { get; set; } = "unbekannt";

    public Dictionary<string, int> Counts { get; set; } = new();

    /// <summary>Alle Bounded Contexts (Domänen), sortiert — für Filter/Canvas-Clustering.</summary>
    public List<string> Contexts { get; set; } = new();
}

// ── Knoten ──────────────────────────────────────────────────────────────────

public enum NodeKind { aggregate, command, @event, projection, query, akteur, client }

public sealed class Node
{
    /// <summary>Stabile Id, präfix-getypt: z.B. <c>cmd:BelasteKonto</c>, <c>evt:KontoBelastet</c>, <c>proj:Kontoauszug</c>.</summary>
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
    public ProjectionInfo? Projection { get; set; }
    public QueryInfo? Query { get; set; }
    public AkteurInfo? Akteur { get; set; }
    public ClientInfo? Client { get; set; }
}

/// <summary>
/// Ein Client (<c>interface IX : IClientVertrag</c>, docs/konzept-akteure.md §4) — die Software an der Leitung: die Akteure, die er
/// verkörpert, und was er nur zur Kenntnis hört; abgeleitet daraus, was er trägt, sendet und fragt (die <c>IDarf</c> seiner Akteure).
/// </summary>
public sealed class ClientInfo
{
    /// <summary>Handshake-Name (Interface ohne führendes I).</summary>
    public string Handshake { get; set; } = "";
    /// <summary><c>IVerkoerpert&lt;A&gt;</c>: die Akteure, für die er spricht.</summary>
    public List<string> Verkoerpert { get; set; } = new();
    public List<string> Kenntnis { get; set; } = new();
    /// <summary>Abgeleitet: die Funktionen, Commands/Quell-Nachrichten/Hinweise und Queries, die seine Akteure dürfen.</summary>
    public List<string> Traegt { get; set; } = new();
    public List<string> Sendet { get; set; } = new();
    public List<string> Fragt { get; set; } = new();
}

/// <summary>Ein Akteur (<c>IAkteur</c>): Art und was er hineingeben darf (<c>IDarf&lt;T&gt;</c>, einfache Namen).</summary>
public sealed class AkteurInfo
{
    public string? Art { get; set; }
    public List<string> Darf { get; set; } = new();
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

public sealed class ProjectionInfo
{
    /// <summary><c>[MindestensEinmal]</c>: darf in einen Store der Klasse 3 schreiben (R14). Die Kennung ist der Typname.</summary>
    public bool MindestensEinmal { get; set; }
    public List<string> Consumes { get; set; } = new(); // Event-Namen
}

public sealed class QueryInfo
{
    public string? ServedByReader { get; set; }
    /// <summary>Das Aggregat, das die Query beantwortet (Frage im Decider, docs/03 §3.2) — eine Query hat genau ein Ziel (R4).</summary>
    public string? AnsweredByAggregate { get; set; }
    /// <summary>Die Projektionen hinter dem Reader (aus seinen Lese-Fähigkeiten: wer in denselben Store schreibt).</summary>
    public List<string> ReadsProjections { get; set; } = new();
    public List<FieldInfo> Fields { get; set; } = new();
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
    /// <summary>Von einer Treiber-Basis geerbt (z. B. <c>DateiGemeldet.Name</c>): sichtbar, aber nie zurückgeschrieben.</summary>
    public bool Geerbt { get; set; }
}

// ── Kanten ──────────────────────────────────────────────────────────────────

public enum EdgeKind
{
    routedTo,       // command  → aggregate   (GeneratedCommandRouting.CommandToAggregate, autoritativ)
    produces,       // command  → event       (Decide-OneOf, handler-sensitiv; persistiert autoritativ aus CommandToEvents, Ablehnung aus Decider)
    consumedBy,     // event    → projection  (IProjektion.Auf)
    readsFrom,      // query    → projection  (Lese-Fähigkeiten des Readers ↔ Schreib-Fähigkeiten der Projektion, derselbe Store)
    answeredBy,     // query    → aggregate   (Frage im Decider, docs/03 §3.2: exakter Stand)
    darf,           // akteur   → command/query (IDarf<T>)
    hoert,          // event    → client      (IHoert<E>: Kenntnis)
    verkoerpert     // client   → akteur      (IVerkoerpert<A>; was über die Leitung geht, sind die darf-Kanten des Akteurs)
}

public sealed class Edge
{
    public string From { get; set; } = "";
    public string To { get; set; } = "";

    [JsonConverter(typeof(JsonStringEnumConverter))]
    public EdgeKind Kind { get; set; }

    /// <summary>GeneratedCommandRouting | decider | subscriber | reader | akteur.</summary>
    public string Provenance { get; set; } = "";

    /// <summary>Kontext: bei `traegt` die getragene Funktion.</summary>
    public string? Via { get; set; }
}

// ── Abgeleitete Sichten ──────────────────────────────────────────────────────

public sealed class GraphViews
{
    public List<EventFanout> EventFanout { get; set; } = new();
    public List<Finding> Diagnostics { get; set; } = new();
}

/// <summary>Pro Event: alles, was es auslöst — der Blast-Radius eines Events.</summary>
public sealed class EventFanout
{
    public string Event { get; set; } = "";
    public bool Persisted { get; set; }
    public List<string> Projections { get; set; } = new();
    /// <summary>Funktionen, die das Event hören (je <c>Auf</c> ein Eingang).</summary>
    public List<string> Funktionen { get; set; } = new();
}

public sealed class Finding
{
    public string Severity { get; set; } = "info"; // error | warning | info
    public string Code { get; set; } = "";
    public string Message { get; set; } = "";
}
