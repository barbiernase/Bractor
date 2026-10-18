using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Abstractions;

public interface IState
{
    int Version { get; set; }
    Guid Id { get; set; }

}

/// <summary>
/// Super-Marker für alle Typen die als Pipeline-Output yieldbar sind.
/// Lockert das OneOf-Constraint: OneOf&lt;T1, T2&gt; where Ti : IPipelineOutput
/// erlaubt sowohl IMessagePayload-Subtypen (Commands, Events) als auch IPipelineTrigger.
///
/// Konsumenten die nur Transport-Typen erwarten (Envelopes, BrokerSubscription,
/// BrokerIdentity) constrainen weiterhin auf IMessagePayload — nicht betroffen.
/// </summary>
public interface IPipelineOutput { }

public interface IMessagePayload : IPipelineOutput { }
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
        /// <param name="correlationId">Optional: Korrelations-Id, wird ins Log gestempelt (Phase 1).</param>
        /// <param name="causationId">Optional: Verursacher-Id (auslösender Command), wird ins Log gestempelt.</param>
        /// <param name="aggregateType">Optional: Aggregat-Typname, wird als Log-Header gestempelt.</param>
        /// <param name="akteur">Optional: der Akteur des auslösenden Commands (Header <c>akteur</c>) — reist so durch die Kette
        /// zu Pipelines/Prozessen/Fristen, die daraus Commands erzeugen (docs/konzept-akteure.md §5.1).</param>
        /// <param name="beilagen">Optional: Framework-Buchhaltung in derselben Transaktion (<see cref="Beilage"/>) — die Quittung des Commands
        /// für die Inbox, Eintrag und Ende eines Vorgangs. Kennt die Inbox die CommandId einer Quittung schon, ist nichts geschrieben und es fliegt
        /// <see cref="KommandoSchonEntschiedenException"/>.</param>
        Task AppendEventsAsync(
            Guid aggregateId,
            int expectedVersion,
            IReadOnlyList<IEvent> events,
            string? correlationId = null,
            string? causationId = null,
            string? aggregateType = null,
            string? akteur = null,
            IReadOnlyList<Beilage>? beilagen = null);

        /// <summary>
        /// Hängt die <paramref name="teile"/> mehrerer Commands an EINEN Stream an — atomar in einer Transaktion, mit Versionsprüfung gegen
        /// <paramref name="expectedVersion"/>, Metadaten und Beilagen je Teil (das Bündel eines Aggregat-Actors, docs/03 §3.1). Kennt die Inbox
        /// die CommandId einer der Quittungen schon, ist nichts geschrieben (<see cref="KommandoSchonEntschiedenException"/>). Ohne eigene Ablage
        /// (Prüfstand-Seams): nicht unterstützt — ein Dekorator muss ihn ausdrücklich weiterreichen.
        /// </summary>
        Task AppendTeileAsync(Guid aggregateId, int expectedVersion, IReadOnlyList<AppendTeil> teile, string? aggregateType)
            => throw new NotSupportedException($"{GetType().Name} hängt keine Bündel an");

        /// <summary>
        /// Der Ausgang eines Commands laut Inbox, oder null, wenn sie ihn nicht kennt (nie entschieden oder älter als der Horizont). Der
        /// Aggregat-Actor fragt nur, wenn sein Cache nichts weiß und der Weg ohne die Antwort falsch würde: nach einem gescheiterten Append und
        /// vor einem Versionskonflikt. Ohne Ablage (Prüfstand): unbekannt.
        /// </summary>
        Task<KommandoAusgang?> AusgangAsync(Guid commandId, CancellationToken ct)
            => Task.FromResult<KommandoAusgang?>(null);

        /// <summary>
        /// Globales Leseprimitiv für den Poll der Emittenten (docs/04 §4.1): eine Seite der Streams, die jenseits von
        /// <paramref name="afterGlobalSequence"/> ein Event eines der <paramref name="typen"/> bekommen haben (Speicher-Diskriminatoren samt
        /// Upcasting-Vorgängern wie bei <see cref="ReadPartitionAsync"/>; null = jeder Typ), höchstens <paramref name="limit"/> solche Events
        /// weit, und die Marke, bis zu der die Seite reicht — nie über die gesetzte Sequenz (siehe <see cref="ReadPartitionAsync"/>). Liest nur Sequenz und Stream, keine
        /// Daten; Tombstones zählen nicht. Der Poller weckt die Adapter der Streams — so heilt er das letzte verlorene Signal vor Stille.
        /// </summary>
        Task<StreamChanges> ReadChangedStreamsAsync(
            long afterGlobalSequence, IReadOnlyCollection<string>? typen, int limit, CancellationToken ct);

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

        /// <summary>
        /// Liest die Events eines Streams ab <paramref name="fromVersion"/>, geordnet,
        /// jeweils mit ihrer Version im EventEnvelope.
        ///
        /// Das eine tragende neue Leseprimitiv des Pull-Ansatzes (docs/04 §4.1):
        /// <paramref name="fromVersion"/> = angewandte Marke + 1. Genutzt von
        /// Adaptern, (späteren) Treibern und dem Rebuilder.
        /// </summary>
        Task<IReadOnlyList<EventEnvelope>> ReadStreamAsync(
            Guid streamId, int fromVersion, CancellationToken ct);

        /// <summary>
        /// Genau das Event an <paramref name="version"/> eines Streams (bei einem Split-Upcast das erste), oder null. Für Leser, die ein
        /// bekanntes Event brauchen (Auslöser eines Vorgangs, das Event zu einem Signal) — statt den Rest des Streams ab dieser Version zu
        /// lesen. Ohne eigene Ablage (Prüfstand) über <see cref="ReadStreamAsync"/>.
        /// </summary>
        async Task<EventEnvelope?> ReadEventAsync(Guid streamId, int version, CancellationToken ct)
            => (await ReadStreamAsync(streamId, version, ct)).FirstOrDefault(e => e.AggregateVersion == version);

        /// <summary>
        /// Die Events eines Streams ab <paramref name="fromVersion"/>, aber nur die mit der Korrelation <paramref name="korrelation"/> — für den
        /// Prozess-Manager, der in einem viel beschriebenen Ziel-Stream nur die Wirkungen seiner eigenen Befehle braucht. Die Datenbank liest den
        /// Rest weiter über den Stream-Index, liefert aber nur die eigenen Zeilen (keine fremden Daten über die Leitung, nichts auszupacken).
        ///
        /// Dazu der Kopf des Streams (seine höchste Version, gelesen vor den Events): bis dorthin hat der Leser alles Eigene gesehen — jedes
        /// Anhängen an einen Stream wartet auf das vorige (Versionsprüfung an seinem Kopf), ein sichtbares Event hat also keine unsichtbaren davor.
        /// Ohne Cursor (<paramref name="fromVersion"/> = 0) liest der Store erst ab dem ersten Event, das nach dem ersten Event von
        /// <paramref name="beginn"/> geschrieben wurde (dem Start des Vorgangs in seinem eigenen Log): davor kann es nichts Eigenes geben, und die
        /// Geschichte des Streams von vor dem Vorgang kostet nichts. <see cref="KorrelierteEvents.Ab"/> nennt die Version, ab der er las.
        /// Ohne eigene Ablage (Prüfstand) über <see cref="ReadStreamAsync"/>, ab <paramref name="fromVersion"/>.
        /// </summary>
        async Task<KorrelierteEvents> ReadKorreliertAsync(Guid streamId, int fromVersion, string korrelation, Guid beginn, CancellationToken ct)
        {
            var alle = await ReadStreamAsync(streamId, fromVersion, ct);
            var kopf = alle.Count == 0 ? Math.Max(fromVersion - 1, 0) : alle.Max(e => e.AggregateVersion);
            return new KorrelierteEvents(alle.Where(e => e.CorrelationId == korrelation).ToList(), kopf, fromVersion);
        }

        /// <summary>
        /// Leseprimitiv des Batch-Konsums (docs/04 §4.1): eine Seite der Partition <paramref name="partition"/> von
        /// <paramref name="partitionen"/> jenseits der globalen Sequenz <paramref name="nach"/>, geordnet nach Sequenz, höchstens
        /// <paramref name="limit"/> Events. <paramref name="typen"/> = die Speicher-Diskriminatoren der abonnierten Typen samt ihrer
        /// Upcasting-Vorgänger (null = ohne Typfilter); <paramref name="ausser"/> = Sequenzen, die der Leser schon erledigt hat.
        /// Tombstones liest es nicht; materialisiert wird wie in <see cref="ReadStreamAsync"/>.
        /// Vor der Seite bestimmt es die <b>gesetzte</b> Sequenz: bis dorthin ist jede Sequenz endgültig — committet und sichtbar oder nie.
        /// Wie der Store das weiß, ist seine Sache (Postgres: aus den Transaktionsnummern, ohne Uhr); eine Transaktion, die noch läuft, wird
        /// nie überholt, auch nicht, wenn sie lange offen ist (Nachzügler-Regel wie beim Poll).
        /// </summary>
        Task<PartitionsSeite> ReadPartitionAsync(
            long nach, int partition, int partitionen, IReadOnlyCollection<string>? typen, IReadOnlyCollection<long> ausser, int limit,
            CancellationToken ct);

        /// <summary>
        /// Welche der <paramref name="streams"/> es schon gibt (eine Abfrage). Der Prozess-Poll fragt so je Seite, welche Vorgänge schon
        /// gestartet sind, statt jeden zu wecken. Ohne Ablage (Prüfstand): keiner — der Aufrufer weckt dann jeden, wie ohne die Frage.
        /// </summary>
        Task<IReadOnlyCollection<Guid>> StreamsVorhandenAsync(IReadOnlyCollection<Guid> streams, CancellationToken ct)
            => Task.FromResult<IReadOnlyCollection<Guid>>(Array.Empty<Guid>());
    }

    /// <summary>
    /// Ergebnis von <see cref="IEventStoreRepository.ReadKorreliertAsync"/>: die eigenen Events, der Kopf des Streams beim Lesen (bis dorthin ist
    /// alles Eigene dabei) und die Version, ab der der Store tatsächlich las (≥ der verlangten).
    /// </summary>
    public sealed record KorrelierteEvents(IReadOnlyList<EventEnvelope> Events, int Kopf, int Ab);

    /// <summary>Ein Event einer Partitions-Seite mit seiner globalen Sequenz.</summary>
    public sealed record PartitionsEvent(long Sequenz, EventEnvelope Event);

    /// <summary>
    /// Eine Seite des Batch-Konsums: die Events (nach Sequenz geordnet; ein Split-Upcast liefert mehrere mit derselben Sequenz) und die
    /// gesetzte Sequenz — bis dorthin darf die Position vorrücken. <paramref name="Voll"/>: das Limit war erreicht, es kann mehr geben.
    /// </summary>
    public sealed record PartitionsSeite(IReadOnlyList<PartitionsEvent> Events, long Gesetzt, bool Voll);


    /// <summary>
    /// Eine Seite eines Poll-Scans: die seit der letzten High-Water-Mark geänderten Streams und die neue High-Water-Mark (globale
    /// Event-Sequenz). <paramref name="Voll"/>: das Limit war erreicht, jenseits der Marke kann es mehr geben.
    /// </summary>
    public record StreamChanges(IReadOnlyList<Guid> StreamIds, long HighWaterMark, bool Voll = false);

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

    /// <summary>
    /// Marker: dieses ReadModel ist ein GETEILTES Dokument — es wird aus MEHREREN Streams beschrieben (z. B. ein
    /// Singleton-Zeiger). Die Konsum-Maschine garantiert einen Schreiber nur je (Projektion, Stream); für ein
    /// geteiltes Dokument gilt das nicht. Deshalb registriert der Generator es mit Optimistic Concurrency, und ein
    /// Store darf es nur über den konfliktgeprüften Weg ändern (Lesen → Ändern → Schreiben mit Versionsprüfung;
    /// ein Konflikt verwirft den ganzen Stapel samt Marke, der Adapter wiederholt ihn).
    ///
    /// Wo es geht, ist ein geteiltes Dokument zu vermeiden: Zeilen je (Stream, Schlüssel) mit zusammengesetzter Id
    /// berühren nie dasselbe Dokument und brauchen keine Versionsprüfung.
    /// </summary>
    public interface IGeteiltesReadModel : IReadModel { }

    /// <summary>
    /// Ein Store meldet beim Commit eines Stapels, dass ein geteiltes Dokument zwischenzeitlich von einem anderen
    /// Stream geändert wurde. Nichts aus dem Stapel ist durabel (Effekte und Marke fielen gemeinsam zurück) — der
    /// Stapel kann gefahrlos wiederholt werden. Store-agnostisch: der Adapter kennt nur diese Ausnahme.
    /// </summary>
    public sealed class SchreibKonfliktException : Exception
    {
        public SchreibKonfliktException(string message, Exception inner) : base(message, inner) { }
    }

    /// <summary>
    /// Marker: dieses Interface ist eine SCHREIB-FÄHIGKEIT eines Projektions-Stores — genau EINE Funktion
    /// (CQRS051). Ein Handle bekommt die Fähigkeiten, die er benutzen darf, als Parameter; der generierte
    /// Dispatch löst sie aus dem DI-Bereich auf (eine Store-Instanz je Bereich → Co-Commit bleibt). So steht die
    /// Obergrenze „welche Store-Funktionen darf dieser Handle rufen" in der Signatur, vom Compiler erzwungen.
    /// Der Name des Interfaces ist frei.
    /// </summary>
    public interface IWriteStore { }

    /// <summary>
    /// Marker: dieses Interface ist eine LESE-FÄHIGKEIT eines Projektions-Stores — genau EINE Funktion
    /// (CQRS051). Von Reader- oder Pipeline-Handles als Parameter angefordert.
    /// </summary>
    public interface IReadStore { }

    /// <summary>
    /// Marker: dieses Interface ist ein STORE — das Bündel seiner Fähigkeiten (erbt die <see cref="IWriteStore"/>-
    /// und <see cref="IReadStore"/>-Interfaces) und damit die Transaktionsgrenze. Genau eine Klasse implementiert
    /// es; die DI registriert diese eine Instanz je Bereich unter jeder Fähigkeit (generiert).
    /// </summary>
    public interface IStore { }

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
        /// Wird beim Start aufgerufen (nach Subscriptions). Planen geht hier NICHT — die erste Selbst-Nachricht
        /// plant ein <c>Handle(PipelineGestartet, ctx)</c> mit <c>Selbst&lt;T&gt;</c> im OneOf.
        /// </summary>
        Task OnInitializeAsync(PipelineContext ctx) => Task.CompletedTask;
 
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
    /// Trigger werden zwischen Pipelines gesendet oder von nativen Actors
    /// (FileWatcher, Timer, Webhook) direkt an Pipeline-Actors geschickt.
    ///
    /// Erbt von IPipelineOutput, damit Pipelines Trigger als Output yielden können
    /// (OneOf&lt;SomeCommand, SomeTrigger&gt;). Der Generator routet Trigger-Outputs
    /// über TriggerToPipelineId an die Ziel-Pipeline.
    ///
    /// Der PipelineDispatchGenerator unterscheidet Handle-Methoden nach
    /// Parameter[0]-Typ: IPipelineTrigger → Trigger-Kanal, IEvent → PubSub-Kanal.
    /// </summary>
    // : IWireMessage — ein konkreter Trigger wird direkt als Top-Level-Cluster-Nachricht gesendet
    //   (PipelineTriggerSender: RequestAsync&lt;PipelineAck&gt;(identity, trigger)), reist also cross-node
    //   und muss vom Wire-Serializer abgedeckt sein (Multi-Node Iter.2).
    public interface IPipelineTrigger : IPipelineOutput, IWireMessage { }

    /// <summary>
    /// Marker-Interface für Pipeline-interne Self-Messages.
    ///
    /// Self-Messages sind Implementierungsdetails einzelner Pipelines (z.B. PollTick,
    /// TimeoutCheck). Sie werden NUR über einen <c>Selbst&lt;T&gt;</c>-Ausgang geplant und
    /// durchlaufen NICHT das Framework:
    ///   - Kein Proto-Mapping (DtoMapperGenerator ignoriert sie)
    ///   - Keine TypeRegistry-Einträge (TypeRegistryGenerator ignoriert sie)
    ///   - Kein TriggerToPipelineId-Lookup (PipelineActorGenerator ignoriert sie)
    ///   - Nicht direkt im OneOf, sondern verpackt: <c>OneOf&lt;…, Selbst&lt;T&gt;&gt;</c>
    ///
    /// Parallel zur bestehenden Hierarchie — NICHT unter IPipelineOutput.
    /// </summary>
    public interface IPipelineSelfMessage { }