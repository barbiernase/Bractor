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
/// Was ein Decide oder eine Funktion ausgeben kann — das Constraint von <see cref="OneOf{T1}"/>: Commands und Events (<see cref="IMessagePayload"/>)
/// sowie eine Frist (<see cref="IFrist"/>, nur aus einem Decide). Konsumenten, die nur Transport-Typen erwarten (Envelopes), constrainen weiter
/// auf <see cref="IMessagePayload"/>.
/// </summary>
public interface IAusgabe { }

public interface IMessagePayload : IAusgabe { }
public interface IEvent : IMessagePayload { }

/// <summary>
/// Die Framework-Sicht auf einen Command: sein Ziel als Stream-Id. Der Fachcode implementiert nie dieses Interface allein, sondern
/// <see cref="ICommand{TAggregat}"/> — dort steht das Ziel typisiert (R7, Plan §3.6), und <see cref="Ziel"/> folgt daraus. Ein Command ohne
/// typisiertes Ziel baut nicht (der Compiler verlangt <see cref="Ziel"/>, CQRS013 verbietet den Umweg).
/// </summary>
public interface ICommand : IMessagePayload
{
    /// <summary>Das Ziel-Aggregat als Stream-Id (= Cluster-Identität des Aggregat-Actors).</summary>
    Guid Ziel { get; }
}

/// <summary>
/// Ein Command an genau ein Aggregat <typeparamref name="TAggregat"/> (Plan §3.1, R3): das Ziel steht im Typ. Routing, Aggregat-Actor, Leitung,
/// Python und Client lesen es von hier — die Decide-Signatur muss im selben Aggregat stehen (CQRS014).
/// </summary>
public interface ICommand<TAggregat> : ICommand where TAggregat : IState
{
    /// <summary>Die Id des Ziel-Aggregats.</summary>
    Id<TAggregat> AggregateId { get; }

    Guid ICommand.Ziel => AggregateId.Wert;
}

/// <summary>
/// Marker-Interface für Queries an Projektionen.
/// Queries sind synchrone Request/Response (keine Zustellung über den Verteiler).
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
    
/// <summary>Marker: ein erzeugender Command (der C#-Client sendet ihn mit erwarteter Version 0). Neben <see cref="ICommand{TAggregat}"/>.</summary>
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

        /// <summary>
        /// Die höchste Version eines Streams (0 = es gibt ihn nicht) — für Schreiber, die an einen fremden Stream anhängen, ohne seinen Inhalt zu
        /// brauchen (eine Quelle mit einem Stream je Schlüssel). Ohne eigene Ablage (Prüfstand) über <see cref="ReadStreamAsync"/>.
        /// </summary>
        async Task<int> KopfAsync(Guid streamId, CancellationToken ct)
            => (await ReadStreamAsync(streamId, 0, ct)).Select(e => e.AggregateVersion).DefaultIfEmpty(0).Max();
    }

    /// <summary>Ein Event einer Partitions-Seite mit seiner globalen Sequenz.</summary>
    public sealed record PartitionsEvent(long Sequenz, EventEnvelope Event);

    /// <summary>
    /// Eine Seite des Batch-Konsums: die Events (nach Sequenz geordnet; ein Split-Upcast liefert mehrere mit derselben Sequenz) und die
    /// gesetzte Sequenz — bis dorthin darf die Position vorrücken. <paramref name="Voll"/>: das Limit war erreicht, es kann mehr geben.
    /// </summary>
    public sealed record PartitionsSeite(IReadOnlyList<PartitionsEvent> Events, long Gesetzt, bool Voll);


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
    /// Marker für Events, die nur verlierbar zugestellt (Hinweis-Netz, Verteiler an Clients),
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
        /// Der Stand je gezeigter Id: bis zu welcher Version die Projektion den Stream verarbeitet hat — ihr Inhalt für den Stream gilt bis
        /// dorthin (bis zum Kopf, wenn danach nur Events kamen, die sie nicht hört). Null, wenn nichts getrackt.
        /// </summary>
        public IReadOnlyList<AggregateMeta>? Deps { get; init; }

        /// <summary>„Noch nicht so weit“: die Projektion hat in der Frist nicht jeden Stream aus <c>mindestens</c> verarbeitet — die Daten sind
        /// der Stand davor.</summary>
        public bool NochNicht { get; init; }

        /// <summary>Die Projektion, die geantwortet hat (ihre SubscriberId) — die Sitzung beobachtet sie, um den Client neu fragen zu lassen
        /// (Plan Pub/Sub-Neustrukturierung §2.7). Nicht auf der Leitung.</summary>
        public string? Projektion { get; init; }
    }



    /// <summary>
    /// Steuert ob für diesen Reader die Stände je Id gelesen werden.
    /// 
    /// Wird auf die Reader-Klasse innerhalb einer Projektion gesetzt.
    /// Der ProjectionQueryService evaluiert dieses Attribut nach dem Handler-Aufruf.
    /// 
    /// Entscheidungsmatrix:
    ///   TrackDeps=true  + ctx.Track() aufgerufen  → Stände je Id lesen
    ///   TrackDeps=true  + kein ctx.Track()         → kein Lesen, Deps=null
    ///   TrackDeps=false + ctx.Track() aufgerufen   → kein Lesen, Deps=null (Attribut überschreibt)
    ///   TrackDeps=false + kein ctx.Track()         → kein Lesen, Deps=null
    /// 
    /// Default: TrackDeps = true (opt-out statt opt-in).
    /// Projektionen die keine Deps brauchen (z.B. AuditLog) setzen TrackDeps = false.
    /// </summary>
    [AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = false)]
    public class ProjectionReaderAttribute : Attribute
    {
        /// <summary>
        /// Wenn true: die Stände je Id werden gelesen (sofern ctx.Track() aufgerufen wurde).
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
        /// Wenn false: kein Lesen, Deps bleiben null.
        /// </summary>
        public bool HasTrackedIds => _trackedIds.Count > 0;

        /// <summary>
        /// Gesammelte ReadModel-IDs (Framework-intern).
        /// </summary>
        public IReadOnlyList<string> TrackedIds => _trackedIds;
    }

    /// <summary>
    /// Der Stand einer Projektion je Stream (Infrastructure: <c>ProjektionsStaende</c>, aus dem Guard je (Projektion, Stream) und dem Log): wie
    /// weit sie ihn verarbeitet hat — gehörte Events bis zum Guard, danach so weit, wie nur Events folgen, die sie nicht hört. Der
    /// ProjectionQueryService wartet vor dem Reader darauf (Read-your-writes) und liest danach den Stand je gezeigter Id.
    /// </summary>
    public interface IReadModelDepsReader
    {
        /// <summary>Der Stand je Id (Deps der Antwort); null bei Fehler oder ohne gültige Id.</summary>
        /// <param name="subscriberId">Die Projektion (ihre SubscriberId)</param>
        /// <param name="readModelIds">Die getrackten Ids aus dem ReadContext</param>
        Task<IReadOnlyList<AggregateMeta>?> ReadAsync(
            string subscriberId,
            IReadOnlyList<string> readModelIds);

        /// <summary>Wartet höchstens <paramref name="frist"/>, bis die Projektion jeden Stream mindestens bis zu seiner Version verarbeitet hat.
        /// false = noch nicht so weit.</summary>
        Task<bool> WarteAsync(string subscriberId, IReadOnlyList<StreamStand> mindestens, TimeSpan frist, CancellationToken ct = default);
    }
