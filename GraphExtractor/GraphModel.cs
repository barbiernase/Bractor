using System.Text.Json.Serialization;

namespace GraphExtractor;

// ═══════════════════════════════════════════════════════
// ROOT
// ═══════════════════════════════════════════════════════

public class KnowledgeGraph
{
    public List<AggregateNode> Aggregates { get; set; } = new();
    public List<PipelineNode> Pipelines { get; set; } = new();
    public List<ProjectionNode> Projections { get; set; } = new();
    public List<ReaderNode> Readers { get; set; } = new();
    public List<QueryNode> Queries { get; set; } = new();
    public List<ClientStoreNode> ClientStores { get; set; } = new();
    public List<ClientHandlerNode> ClientHandlers { get; set; } = new();
    public List<EventFanout> EventFanouts { get; set; } = new();
}

// ═══════════════════════════════════════════════════════
// AGGREGATE
// ═══════════════════════════════════════════════════════

public class AggregateNode
{
    public string Name { get; set; } = "";
    public string Namespace { get; set; } = "";
    public string FullName { get; set; } = "";
    public StateNode State { get; set; } = new();
    public List<DecideNode> Decides { get; set; } = new();
    public List<ApplyNode> Applies { get; set; } = new();
}

public class StateNode
{
    public List<FieldInfo> Fields { get; set; } = new();
    public List<DerivedProperty> DerivedProperties { get; set; } = new();
    public string? CodePayload { get; set; }
}

public class FieldInfo
{
    public string Name { get; set; } = "";
    public string Type { get; set; } = "";
    public List<FieldInfo> Children { get; set; } = new();
}

public class DerivedProperty
{
    public string Name { get; set; } = "";
    public string CodePayload { get; set; } = "";
}

public class DecideNode
{
    /// <summary>Der Command-Typ den diese Decide-Methode verarbeitet.</summary>
    public string CommandType { get; set; } = "";
    
    /// <summary>true wenn der Command ICreationCommand implementiert.</summary>
    public bool IsCreation { get; set; }
    
    /// <summary>
    /// Das geschlossene Universum der möglichen Ausgänge aus dem OneOf-Rückgabetyp.
    /// Jeder Eintrag ist ein Event-Typ mit seiner Klassifikation (persist/reject).
    /// </summary>
    public List<DecideOutput> Universe { get; set; } = new();
    
    /// <summary>Der vollständige Methodenbody — für Bedingungen, Kombinatorik, Datenherkunft.</summary>
    public string CodePayload { get; set; } = "";
}

public class DecideOutput
{
    public string EventType { get; set; } = "";
    
    /// <summary>"persist" (IEvent) oder "reject" (ITransientEvent)</summary>
    public string Kind { get; set; } = "persist";
}

public class ApplyNode
{
    /// <summary>Der Event-Typ den diese Apply-Methode verarbeitet.</summary>
    public string EventType { get; set; } = "";
    
    /// <summary>Der Methodenbody — welche State-Felder mutiert werden.</summary>
    public string CodePayload { get; set; } = "";
}

// ═══════════════════════════════════════════════════════
// PIPELINE
// ═══════════════════════════════════════════════════════

public class PipelineNode
{
    public string Name { get; set; } = "";
    public string FullName { get; set; } = "";
    public string PipelineId { get; set; } = "";
    public List<PipelineHandleNode> Handles { get; set; } = new();
}

public class PipelineHandleNode
{
    /// <summary>Der Input-Typ (Trigger oder Event).</summary>
    public string InputType { get; set; } = "";
    
    /// <summary>"trigger" oder "event"</summary>
    public string InputKind { get; set; } = "";
    
    /// <summary>Command-Typen die diese Handle-Methode erzeugen kann.</summary>
    public List<string> ProducedCommands { get; set; } = new();
    
    /// <summary>Der Methodenbody — Orchestrierungslogik.</summary>
    public string CodePayload { get; set; } = "";
}

// ═══════════════════════════════════════════════════════
// PROJECTION
// ═══════════════════════════════════════════════════════

public class ProjectionNode
{
    public string Name { get; set; } = "";
    public string FullName { get; set; } = "";
    public string SubscriberId { get; set; } = "";
    public List<ProjectionHandleNode> Handles { get; set; } = new();
}

public class ProjectionHandleNode
{
    /// <summary>Der Event-Typ den dieser Handle verarbeitet.</summary>
    public string EventType { get; set; } = "";
    
    /// <summary>Der Methodenbody — welche ReadModel-Operationen.</summary>
    public string CodePayload { get; set; } = "";
}

// ═══════════════════════════════════════════════════════
// READER + QUERY
// ═══════════════════════════════════════════════════════

public class ReaderNode
{
    public string Name { get; set; } = "";
    public string FullName { get; set; } = "";
    
    /// <summary>Name der Projektion zu der dieser Reader gehört.</summary>
    public string ProjectionName { get; set; } = "";
    
    public bool TrackDeps { get; set; }
    public List<ReaderHandleNode> Handles { get; set; } = new();
}

public class ReaderHandleNode
{
    public string QueryType { get; set; } = "";
    
    /// <summary>
    /// Mögliche Response-Typen. Bei OneOf mehrere, sonst einer.
    /// </summary>
    public List<string> ResponseTypes { get; set; } = new();
    
    public string CodePayload { get; set; } = "";
}

public class QueryNode
{
    public string Name { get; set; } = "";
    public string FullName { get; set; } = "";
    public List<FieldInfo> Fields { get; set; } = new();
}

// ═══════════════════════════════════════════════════════
// CLIENT STORE
// ═══════════════════════════════════════════════════════

public class ClientStoreNode
{
    public string Name { get; set; } = "";
    public string FullName { get; set; } = "";
    
    public List<StoreHandleNode> EventHandles { get; set; } = new();
    public List<StoreHandleNode> ResponseHandles { get; set; } = new();
    public List<StoreHandleNode> RejectionHandles { get; set; } = new();
    public List<StoreHandleNode> InfraHandles { get; set; } = new();
    public List<UserIntentNode> UserIntents { get; set; } = new();
    
    public List<FieldInfo> ObservableState { get; set; } = new();
    public List<DerivedProperty> DerivedProperties { get; set; } = new();
}

public class StoreHandleNode
{
    public string InputType { get; set; } = "";
    
    /// <summary>"server-event", "query-response", "rejection", "client-event", "infrastructure"</summary>
    public string InputKind { get; set; } = "";
    
    public string CodePayload { get; set; } = "";
}

public class UserIntentNode
{
    /// <summary>Der Methodenname (z.B. "WechsleZu", "LabelProdukt").</summary>
    public string MethodName { get; set; } = "";
    
    /// <summary>Die Parameter-Signatur.</summary>
    public string Signature { get; set; } = "";
    
    /// <summary>Command- und Query-Typen die diese Methode auf den Bus publiziert.</summary>
    public List<string> Publishes { get; set; } = new();
    
    public string CodePayload { get; set; } = "";
}

/// <summary>
/// Eigenständiger Client-Handler (wie ImagePairStatistikHandler).
/// Konsumiert Events, emittiert ClientEvents.
/// </summary>
public class ClientHandlerNode
{
    public string Name { get; set; } = "";
    public string FullName { get; set; } = "";
    public List<string> HandledEvents { get; set; } = new();
    public List<string> EmittedClientEvents { get; set; } = new();
    public string CodePayload { get; set; } = "";
}

// ═══════════════════════════════════════════════════════
// EVENT FANOUT (berechnet, nicht direkt extrahiert)
// ═══════════════════════════════════════════════════════

public class EventFanout
{
    public string EventType { get; set; } = "";
    
    /// <summary>"persist" oder "reject"</summary>
    public string Kind { get; set; } = "persist";
    
    /// <summary>Projektionen die dieses Event konsumieren.</summary>
    public List<string> Projections { get; set; } = new();
    
    /// <summary>Pipelines die auf dieses Event reagieren.</summary>
    public List<string> Pipelines { get; set; } = new();
    
    /// <summary>Client-Stores die dieses Event handlen.</summary>
    public List<string> ClientStores { get; set; } = new();
    
    /// <summary>Client-Handler die dieses Event handlen.</summary>
    public List<string> ClientHandlers { get; set; } = new();
}