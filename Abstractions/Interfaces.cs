using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Abstractions;

public interface IState
{
    int Version { get; set; }
    Guid Id { get; set; }

}

public interface IMessagePayload { }
public interface IEvent : IMessagePayload { }

public interface ICommand : IMessagePayload
{
    Guid AggregateId { get; }
}


// Nach den bestehenden Interfaces:
// public interface ICommand : IMessagePayload { }
// public interface ICreationCommand : ICommand { }

/// <summary>
/// Marker-Interface für Queries an Projektionen.
/// Queries gehen NICHT durch PubSub – sie sind synchrone Request/Response.
/// Erbt bewusst NICHT von IMessagePayload.
/// </summary>
public interface IQuery { }

/// <summary>
/// Marker-Interface für Query-Antworten.
/// Teil der IMessagePayload-Hierarchie, da Responses über gRPC transportiert werden.
/// Ermöglicht Nutzung von OneOf&lt;TResponse1, TResponse2&gt; für typsichere Reader-Signaturen.
/// </summary>
public interface IQueryResponse : IMessagePayload { }

/// <summary>
/// Marker-Interface für Reader-Klassen innerhalb von Projektionen.
/// Analog zu IDecider/IApplier bei Aggregaten.
/// Der Generic-Parameter verknüpft den Reader mit seiner Projektion.
/// </summary>
public interface IReader<TProjection> where TProjection : ISubscriber { }
    
public interface ICreationCommand : ICommand{ }
    
public interface IDecider<TState> where TState : IState { }
    
public interface IApplier<TState> where TState : IState { }
    
public interface IAggregateHandler
{
    IEnumerable<IEvent> HandleCommand(ICommand command);
    void ApplyEvent(IEvent @event);
}
    
public interface IAggregateHandlerFactory
{
    IAggregateHandler CreateHandler(IState state);
}

/// <summary>
/// Universelle Transport-Metadaten für jede Nachricht.
/// Enthält nur Felder die JEDE Nachricht hat — auch Queries.
/// </summary>
public interface IMessageEnvelope
{
    Guid MessageId { get; }
    DateTimeOffset CreatedAtUtc { get; }
    string CorrelationId { get; }
    string UserId { get; }
    IMessagePayload Payload { get; }
}

/// <summary>
/// Erweiterung für Nachrichten mit Aggregate-Kontext (Events, Commands).
/// Queries haben keinen Aggregate-Kontext und nutzen nur IMessageEnvelope.
/// </summary>
public interface IAggregateEnvelope : IMessageEnvelope
{
    Guid AggregateId { get; }
    string AggregateType { get; }
}

public interface IAggregateRepository
{
    Task<TState?> Load<TState>(Guid aggregateId) where TState : IState, new();
}
    

    public interface IAggregateMessenger
    {
        Task<CommandResult> CommitAsync<TAggregate>(
            Guid aggregateId, 
            int expectedVersion, 
            IReadOnlyList<IEvent> events) 
            where TAggregate : IState, new();
    }



    /// <summary>
    /// Definiert den Vertrag für einen Event Store, der für das Speichern
    /// und Abrufen von Event-Streams für Aggregate zuständig ist.
    /// </summary>
    public interface IEventStoreRepository
    {
        /// <summary>
        /// Hängt eine Liste von Events atomar an den Stream eines bestimmten Aggregats an.
        /// Führt einen Optimistic Concurrency Check durch.
        /// </summary>
        /// <param name="aggregateId">Die ID des Aggregats.</param>
        /// <param name="expectedVersion">Die Version, die der Stream haben muss, damit die Events angehängt werden können.</param>
        /// <param name="events">Die Liste der neuen Events, die gespeichert werden sollen.</param>
        /// <exception cref="ConcurrencyException">Wird ausgelöst, wenn die expectedVersion nicht mit der aktuellen Version des Streams übereinstimmt.</exception>
        /// <returns>Ein Task, der die asynchrone Schreiboperation repräsentiert.</returns>
        Task AppendEventsAsync(
            Guid aggregateId,
            int expectedVersion,
            IReadOnlyList<IEvent> events);

        /// <summary>
        /// Rekonstruiert den aktuellen Zustand eines Aggregats, indem alle zugehörigen
        /// Events aus dem Store geladen und auf ein neues State-Objekt angewendet werden.
        /// </summary>
        /// <typeparam name="TState">Der Typ des Aggregat-Zustands, der wiederhergestellt werden soll.</typeparam>
        /// <param name="aggregateId">Die ID des Aggregats.</param>
        /// <returns>
        /// Ein Task, der den wiederhergestellten Aggregat-Zustand enthält,
        /// oder null, wenn für diese ID keine Events gefunden wurden.
        /// </returns>
        Task<TState?> LoadStateAsync<TState>(Guid aggregateId) where TState : class, IState, new();
    }


    public interface IAggregateDispatcher
    {
        void Dispatch(CommandEnvelope envelope);
    }
    
    /// <summary>
    /// Marker-Interface für Subscriber-Logik-Klassen.
    /// Analog zu IState für Aggregates.
    /// </summary>
    public interface ISubscriber
    {
        /// <summary>
        /// Eindeutige ID für diesen Subscriber.
        /// Bestimmt das Shard-Routing.
        /// </summary>
        string SubscriberId { get; }
    
        /// <summary>
        /// Wird beim Start aufgerufen (nach Subscriptions).
        /// </summary>
        Task OnInitializeAsync() => Task.CompletedTask;
    
        /// <summary>
        /// Wird beim Stoppen aufgerufen (vor Unsubscribe).
        /// </summary>
        Task OnShutdownAsync() => Task.CompletedTask;
    }
    

    /// <summary>
    /// Marker für Events, die nur über PubSub verteilt,
    /// aber nicht im EventStore persistiert werden.
    /// 
    /// Beispiel: CommandFailed wird an den auslösenden Client gesendet,
    /// aber nie in einen Event-Stream geschrieben.
    /// 
    /// Der Source Generator schließt ITransientEvent-Typen
    /// vom Marten Event-Typ-Mapping aus.
    /// </summary>
    public interface ITransientEvent : IEvent { }

    /// <summary>
    /// Schneller, verteilter Index für Aggregate-Versionen.
    /// Abgeleitet, nicht autoritativ – die Wahrheit liegt im EventStore.
    ///
    /// Wird nach jedem erfolgreichen Append aktualisiert.
    /// Wird von Clients und Projektionen für Stale Detection gelesen.
    /// 
    /// Eigene Datei, nicht in Interfaces.cs – bildet mit AggregateVersionInfo
    /// eine abgeschlossene Einheit.
    /// </summary>
    public interface IVersionTracker
    {
        /// <summary>
        /// Aktualisiert die Version eines Aggregats.
        /// Wird vom Actor nach erfolgreichem EventStore-Append aufgerufen.
        /// </summary>
        Task TrackAsync(Guid aggregateId, string aggregateType, int newVersion);

        /// <summary>
        /// Liest die aktuelle Version eines Aggregats.
        /// Gibt null zurück, wenn das Aggregat nicht im Tracker existiert
        /// (z.B. nach Redis-Neustart oder wenn das Aggregat noch nie getrackt wurde).
        /// </summary>
        Task<AggregateVersionInfo?> GetAsync(Guid aggregateId);

        /// <summary>
        /// Liest die Versionen mehrerer Aggregate in einem Roundtrip.
        /// Für Batch-Stale-Detection in ReadModels.
        /// Aggregate die nicht im Tracker existieren fehlen im Dictionary.
        /// </summary>
        Task<IReadOnlyDictionary<Guid, AggregateVersionInfo>> GetManyAsync(
            IReadOnlyList<Guid> aggregateIds);
    }

    /// <summary>
    /// Versionsinformation eines Aggregats im Tracker.
    /// </summary>
    public record AggregateVersionInfo(
        string AggregateType,
        int Version,
        DateTimeOffset UpdatedAt);
        

    /// <summary>
    /// Marker-Interface für ReadModels.
    /// Gibt dem Typsystem und dem Source Generator semantische Information:
    /// "Dieser Record ist ein ReadModel einer Projektion."
    ///
    /// Keine Felder, keine Pflicht-Properties. ReadModels bleiben reine Domain-Records.
    /// </summary>
    public interface IReadModel { }
    
    public record AggregateMeta(Guid Id, string AggregateType, int Version);


    /// <summary>
    /// Framework-Wrapper um den Reader-Return-Wert.
    /// 
    /// Liefert dem Client zwei Dinge:
    /// - Data: Die fachlichen Daten (unverändert, beliebige Struktur)
    /// - Deps: Aggregate-Abhängigkeiten für den Versioning-Kreis (null wenn nicht getrackt)
    /// </summary>
    public class QueryResponse<T>
    {
        /// <summary>
        /// Return-Wert des Readers — unverändert, beliebige Struktur.
        /// </summary>
        public required T Data { get; init; }

        /// <summary>
        /// Aggregate-Abhängigkeiten. Null wenn nichts getrackt.
        /// Flache Liste aller Aggregate von denen das ReadModel abhängt.
        /// </summary>
        public IReadOnlyList<AggregateMeta>? Deps { get; init; }
    }



    /// <summary>
    /// Steuert ob für diesen Reader Deps aus Redis geladen werden.
    /// 
    /// Wird auf die Reader-Klasse innerhalb einer Projektion gesetzt.
    /// Der ProjectionQueryService evaluiert dieses Attribut nach dem Handler-Aufruf.
    /// 
    /// Entscheidungsmatrix:
    ///   TrackDeps=true  + ctx.Track() aufgerufen  → Redis Deps laden
    ///   TrackDeps=true  + kein ctx.Track()         → kein Redis, Deps=null
    ///   TrackDeps=false + ctx.Track() aufgerufen   → kein Redis, Deps=null (Attribut überschreibt)
    ///   TrackDeps=false + kein ctx.Track()         → kein Redis, Deps=null
    /// 
    /// Default: TrackDeps = true (opt-out statt opt-in).
    /// Projektionen die keine Deps brauchen (z.B. AuditLog) setzen TrackDeps = false.
    /// </summary>
    [AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = false)]
    public class ProjectionReaderAttribute : Attribute
    {
        /// <summary>
        /// Wenn true: Deps werden aus Redis geladen (sofern ctx.Track() aufgerufen wurde).
        /// Wenn false: Deps werden nie geladen, unabhängig von ctx.Track().
        /// Default: true.
        /// </summary>
        public bool TrackDeps { get; set; } = true;
    }

    /// <summary>
    /// Kontext für einen Query-Aufruf.
    /// 
    /// Wird vom ProjectionQueryService erstellt und an den Reader-Handler übergeben.
    /// Der Entwickler registriert ReadModel-IDs via Track() — 
    /// das Framework liest die gesammelten IDs nach dem Handler und holt Deps.
    /// 
    /// Gegenstück zu WriteContext (Write-Seite).
    /// </summary>
    public class ReadContext
    {
        private readonly List<string> _trackedIds = new();

        /// <summary>
        /// Markiert eine ReadModel-ID.
        /// "Für dieses ReadModel sollen Deps geladen werden."
        /// 
        /// Typisch: ctx.Track(query.ArtikelId.ToString())
        /// </summary>
        public void Track(string readModelId)
        {
            ArgumentNullException.ThrowIfNull(readModelId);
            _trackedIds.Add(readModelId);
        }

        /// <summary>
        /// Wurden Track-Aufrufe gemacht?
        /// Wenn false: Kein Redis-Read nötig, Deps bleiben null.
        /// </summary>
        public bool HasTrackedIds => _trackedIds.Count > 0;

        /// <summary>
        /// Gesammelte ReadModel-IDs (Framework-intern).
        /// </summary>
        public IReadOnlyList<string> TrackedIds => _trackedIds;
    }

    /// <summary>
    /// Liest ReadModel-Abhängigkeiten (Deps).
    /// 
    /// Implementierung typischerweise via Redis (ReadModelDepsReader).
    /// Wird vom ProjectionQueryService nach dem Reader-Handler aufgerufen.
    /// </summary>
    public interface IReadModelDepsReader
    {
        /// <summary>
        /// Liest Deps für eine oder mehrere ReadModel-IDs.
        /// </summary>
        /// <param name="subscriberId">Der Subscriber/Projektion-Name</param>
        /// <param name="readModelIds">Die getrackten ReadModel-IDs aus dem ReadContext</param>
        /// <returns>Deduplizierte Deps oder null bei Fehler/leerem Ergebnis</returns>
        Task<IReadOnlyList<AggregateMeta>?> ReadAsync(
            string subscriberId,
            IReadOnlyList<string> readModelIds);
    }
    
    /// <summary>
    /// Marker-Interface für Pipeline-Logik-Klassen.
    /// Analog zu ISubscriber (Projektionen) und IState (Aggregate).
    ///
    /// Pipeline-Handler empfangen Trigger (extern) oder Events (PubSub)
    /// und erzeugen Commands an Aggregate. Sie sind das serverseitige
    /// Gegenstück zum gRPC-Client.
    ///
    /// Der Entwickler schreibt partial classes mit Handle()-Methoden.
    /// Der PipelineDispatchGenerator erzeugt DispatchTriggerAsync/DispatchEventAsync.
    /// Der PipelineActorGenerator erzeugt den Actor + DI-Wiring.
    /// </summary>
    public interface IPipelineHandler
    {
        /// <summary>
        /// Eindeutige ID für diese Pipeline.
        /// Bestimmt ClusterIdentity UND PubSub-SubscriberId.
        /// </summary>
        string PipelineId { get; }
 
        /// <summary>
        /// Wird beim Start aufgerufen (nach Subscriptions).
        /// </summary>
        Task OnInitializeAsync() => Task.CompletedTask;
 
        /// <summary>
        /// Wird beim Stoppen aufgerufen (vor Unsubscribe).
        /// </summary>
        Task OnShutdownAsync() => Task.CompletedTask;
    }
    
    
    
    /// <summary>
    /// Marker-Interface für externe Trigger-Nachrichten.
    ///
    /// Trigger sind KEINE Events (nicht persistiert, kein EventStore).
    /// Trigger sind KEINE Commands (nicht an Aggregate gerichtet).
    /// Trigger kommen von nativen Proto.Actors (FileWatcher, Timer, Webhook)
    /// und werden direkt an Pipeline-Actors gesendet.
    ///
    /// Der PipelineDispatchGenerator unterscheidet Handle-Methoden nach
    /// Parameter[0]-Typ: IPipelineTrigger → Trigger-Kanal, IEvent → PubSub-Kanal.
    /// </summary>
    public interface IPipelineTrigger { }