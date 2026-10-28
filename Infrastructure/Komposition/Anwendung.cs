using System.Text.Json;
using System.Text.Json.Serialization;
using Abstractions;
using Cqrs.Leitung;
using Infrastructure.Akteure;
using Infrastructure.Dienste;
using Infrastructure.Funktionen;
using Infrastructure.Quellen;
using Marten;
using Microsoft.Extensions.DependencyInjection;
using Proto;
using Proto.Cluster;

namespace Infrastructure.Komposition;

// ═══════════════════════════════════════════════════════════════════════════════════════════════════════════════════════
// DIE ANWENDUNG aus Sicht des Frameworks (docs/07, Rolle Komposition): alles, was die Laufzeit über die Domäne wissen muss, als
// generierte Tabellen. Das Framework (Infrastructure) referenziert kein Domänen-Projekt; die Komposition der Anwendung
// (<Wurzel>.Laufzeit) erzeugt GeneratedAnwendung : IAnwendung und meldet sie an (AddCqrs bzw. Anwendung.Setze in Tests).
// Je Teil-Schnittstelle eine generierte Tabelle — die Namen der Mitglieder sind die der Generate.
// ═══════════════════════════════════════════════════════════════════════════════════════════════════════════════════════

/// <summary>Die generierten Tabellen einer Anwendung (Komposition, <c>GeneratedAnwendung</c>).</summary>
public interface IAnwendung
{
    ITypTabelle Typen { get; }
    IRoutingTabelle Routing { get; }
    IWireTabelle Wire { get; }
    IWirePolyTabelle WirePoly { get; }
    IUpcastingTabelle Upcasting { get; }
    ISignalTabelle Signale { get; }
    IAkteurTabelle Akteure { get; }
    IFunktionsTabelle Funktionen { get; }
    IDienstTabelle Dienste { get; }
    IAggregatTabelle Aggregate { get; }
    /// <summary>Der Weckplan (GeneratedWeckplan): Event-Typ → wen ein Commit weckt (Plan Pub/Sub-Neustrukturierung §2.3).</summary>
    IReadOnlyDictionary<Type, Infrastructure.Wecken.Weckziel[]> Weckplan { get; }

    /// <summary>Die Quellen-Typen und ihre Treiber (GeneratedQuellen).</summary>
    IReadOnlyList<QuellTyp> Quellen { get; }
    /// <summary>Die Query-Typen, die die Leseseite bedient (leer ohne Leseseite).</summary>
    IReadOnlyList<Type> QueryTypen { get; }
    /// <summary>Domänen-Nachricht ↔ Nutzlast der Leitung (aus dem Leitungs-Projekt der Anwendung); <c>null</c> = die Anwendung hat keine
    /// Leitung und damit keinen gRPC-Dienst — hinein geht es dann nur über JSON-Eingang und Quellen (docs/07, Rolle Leitung).</summary>
    INutzlastTabelle? Nutzlast { get; }
    /// <summary>Der STJ-Kontext des Actor-Planes (Wire) und der Event-Speicherung (Marten).</summary>
    JsonSerializerContext WireJson { get; }
    JsonSerializerContext EventJson { get; }

    /// <summary>Stores, Reader und Query-Dienst der Leseseite (generiert im Speicher-Projekt).</summary>
    void RegistriereLeseseite(IServiceCollection services);
    /// <summary>Der Konsum der Projektionen (Batch-Leser je Partition).</summary>
    void RegistrierePullPfade(IServiceCollection services);
    /// <summary>Die Snapshot-Dokumente der Aggregate im Marten-Schema.</summary>
    void RegistriereSnapshots(StoreOptions options);
}

public interface ITypTabelle
{
    IReadOnlyDictionary<string, Type> Events { get; }
    IReadOnlyDictionary<string, Type> Commands { get; }
    IReadOnlyDictionary<string, Type> Queries { get; }
    IReadOnlyDictionary<string, Type> QueryResponses { get; }
    IReadOnlyDictionary<string, Type> Signals { get; }
    IReadOnlyList<(Type Type, string SnakeCaseName)> PersistableEvents { get; }
    IReadOnlyList<Type> PersistableEventTypes { get; }
}

public interface IRoutingTabelle
{
    IReadOnlyDictionary<Type, string> CommandToAggregate { get; }
    IReadOnlyDictionary<Type, IReadOnlyList<Type>> CommandToEvents { get; }
    IEnumerable<Type> Produziert(Type command);
}

public interface IWireTabelle
{
    byte[] Serialize(object message);
    object Deserialize(byte[] bytes, string typeName);
    string TypeName(object message);
    bool CanSerialize(Type type);
    IReadOnlyList<Type> WireMessageTypes { get; }
}

public interface IWirePolyTabelle
{
    string EventDiskriminator(IEvent value);
    void WriteEvent(Utf8JsonWriter writer, IEvent value);
    IEvent ReadEvent(string diskriminator, ref Utf8JsonReader reader);
    string CommandDiskriminator(ICommand value);
    void WriteCommand(Utf8JsonWriter writer, ICommand value);
    ICommand ReadCommand(string diskriminator, ref Utf8JsonReader reader);
    string SignalDiskriminator(IStateChangeSignal value);
    void WriteSignal(Utf8JsonWriter writer, IStateChangeSignal value);
    IStateChangeSignal ReadSignal(string diskriminator, ref Utf8JsonReader reader);
    IReadOnlyList<Type> SignalTypes { get; }
}

public interface IUpcastingTabelle
{
    IReadOnlyList<IEvent> Materialisiere(string diskriminator, string json);
    IReadOnlyList<IEvent> Aufwerten(object data);
    string Diskriminator(IEvent evt);
    IReadOnlyList<(Type Typ, string Diskriminator)> Registrierungen { get; }
    /// <summary>Je aktuellem Event die Speicher-Diskriminatoren, die beim Lesen zu ihm werden (es selbst und seine früheren Versionen).</summary>
    IReadOnlyDictionary<Type, string[]> Herkunft { get; }
}

public interface ISignalTabelle
{
    IStateChangeSignal? Create(IEvent evt, Guid streamId, int version);
    IReadOnlyDictionary<Type, Type> EventToSignal { get; }
}

public interface IAkteurTabelle
{
    IReadOnlyDictionary<string, AkteurRechte> Rechte { get; }
    IReadOnlyDictionary<string, ClientVertrag> ClientVertraege { get; }
}

public interface IFunktionsTabelle
{
    /// <summary>Die Funktionen (<c>Auf</c>, Plan Pub/Sub-Neustrukturierung §3.3, GeneratedFunktionsBausteine).</summary>
    IReadOnlyList<FunktionsBaustein> Bausteine { get; }
    /// <summary>Hinweis-Typ → Funktions-Actors, die ihn hören.</summary>
    IReadOnlyDictionary<Type, string[]> Hinweisplan { get; }
}

public interface IDienstTabelle
{
    IReadOnlyDictionary<string, Type> Namen { get; }
    IReadOnlyList<DienstImplementierung> Implementierungen { get; }
    IReadOnlyDictionary<Type, Type[]> Braucht { get; }
    IReadOnlyList<KonfigRecord> Konfigs { get; }
}

public interface IAggregatTabelle
{
    IServiceCollection RegisterAllAggregateComponents(IServiceCollection services);
    ClusterKind[] GetAllKinds(ActorSystem actorSystem);
}

/// <summary>
/// Die angemeldete Anwendung — EIN Prozess fährt eine Anwendung. Die Komposition meldet sie an (generiertes <c>AddCqrs</c>; Tests über
/// <see cref="Setze"/>); das Framework liest ihre Tabellen über die statischen Abkürzungen.
/// </summary>
public static class Anwendung
{
    private static IAnwendung? _aktuell;

    public static IAnwendung Aktuell => _aktuell ?? throw new InvalidOperationException(
        "Keine Anwendung angemeldet — die Komposition (<Wurzel>.Laufzeit) meldet sie mit AddCqrs bzw. Anwendung.Setze(GeneratedAnwendung.Instanz) an (docs/07).");

    public static bool Angemeldet => _aktuell != null;

    /// <summary>Meldet die Anwendung an. Dieselbe noch einmal ist harmlos; eine andere im selben Prozess ist ein Fehler.</summary>
    public static void Setze(IAnwendung anwendung)
    {
        ArgumentNullException.ThrowIfNull(anwendung);
        var vorher = Interlocked.CompareExchange(ref _aktuell, anwendung, null);
        if (vorher != null && !ReferenceEquals(vorher, anwendung))
            throw new InvalidOperationException($"Im Prozess ist schon die Anwendung {vorher.GetType().FullName} angemeldet — eine je Prozess.");
    }

    public static ITypTabelle Typen => Aktuell.Typen;
    public static IRoutingTabelle Routing => Aktuell.Routing;
    public static IWireTabelle Wire => Aktuell.Wire;
    public static IWirePolyTabelle WirePoly => Aktuell.WirePoly;
    public static IUpcastingTabelle Upcasting => Aktuell.Upcasting;
    public static ISignalTabelle Signale => Aktuell.Signale;
    public static IAkteurTabelle Akteure => Aktuell.Akteure;
    public static IFunktionsTabelle Funktionen => Aktuell.Funktionen;
    public static IDienstTabelle Dienste => Aktuell.Dienste;
    public static IAggregatTabelle Aggregate => Aktuell.Aggregate;
    public static IReadOnlyDictionary<Type, Infrastructure.Wecken.Weckziel[]> Weckplan => Aktuell.Weckplan;
    public static IReadOnlyList<QuellTyp> Quellen => Aktuell.Quellen;
    public static IReadOnlyList<Type> QueryTypen => Aktuell.QueryTypen;
    public static JsonSerializerContext WireJson => Aktuell.WireJson;
    public static JsonSerializerContext EventJson => Aktuell.EventJson;
}
