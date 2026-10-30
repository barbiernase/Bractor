using Abstractions;
using Microsoft.CodeAnalysis;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace GraphExtractor;

/// <summary>
/// Der Framework-VERTRAG als typisierte Anker — die EINZIGE Stelle, an der der Extractor weiß, wie das Framework
/// heißt. Alles über <c>typeof</c>/<c>nameof</c> aus der referenzierten <c>Abstractions</c>-Assembly bzw. den
/// Microsoft-APIs: eine Umbenennung im Framework ist ein Compile-Fehler hier, keine stille Drift.
/// Domänen-, Projekt-, Datei- oder Konfig-Namen kommen hier NICHT vor — die werden aus dem Code abgeleitet.
/// </summary>
public static class Vertrag
{
    // ── Marker-Interfaces (metadata names für Compilation.GetTypeByMetadataName) ──
    public static readonly string IState = typeof(IState).FullName!;
    public static readonly string ICommand = typeof(ICommand).FullName!;
    public static readonly string ICreationCommand = typeof(ICreationCommand).FullName!;
    /// <summary>Das typisierte Command-Ziel (<c>ICommand&lt;TAggregat&gt;</c>, docs/10 §10.4): das Aggregat eines Commands steht im Typ.</summary>
    public static readonly string ICommandZiel = typeof(ICommand<>).FullName!;
    public static readonly string IEvent = typeof(IEvent).FullName!;
    public static readonly string ITransientEvent = typeof(ITransientEvent).FullName!;
    public static readonly string IProjektion = typeof(IProjektion).FullName!;
    public static readonly string IReader = typeof(IReader).FullName!;
    public static readonly string IQuery = typeof(IQuery).FullName!;
    public static readonly string IQueryResponse = typeof(IQueryResponse).FullName!;
    public static readonly string IReadModel = typeof(IReadModel).FullName!;
    public static readonly string IGeteiltesReadModel = typeof(IGeteiltesReadModel).FullName!;
    public static readonly string IWriteStore = typeof(IWriteStore).FullName!;
    public static readonly string IReadStore = typeof(IReadStore).FullName!;
    public static readonly string IStore = typeof(IStore).FullName!;
    public static readonly string IAkteur = typeof(IAkteur).FullName!;
    public static readonly string IDarf = typeof(IDarf<>).FullName!;
    public static readonly string IHoert = typeof(IHoert<>).FullName!;
    public static readonly string IClientVertrag = typeof(IClientVertrag).FullName!;
    public static readonly string IVerkoerpert = typeof(IVerkoerpert<>).FullName!;
    public static readonly string IMensch = typeof(IMensch).FullName!;
    public static readonly string IMaschine = typeof(IMaschine).FullName!;
    public static readonly string IKi = typeof(IKi).FullName!;
    public static readonly string IWertobjekt = typeof(IWertobjekt).FullName!;
    public static readonly string IFunktion = typeof(IFunktion).FullName!;
    public static readonly string IQuellNachricht = typeof(IQuellNachricht).FullName!;
    public static readonly string IMessagePayload = typeof(IMessagePayload).FullName!;
    public static readonly string IAusgabe = typeof(IAusgabe).FullName!;
    public static readonly string IDecider = typeof(IDecider<>).FullName!;
    public static readonly string IApplier = typeof(IApplier<>).FullName!;
    public static readonly string AggregatNameAttribute = typeof(AggregatNameAttribute).FullName!;
    public static readonly string RoutingTabelleAttribute = typeof(RoutingTabelleAttribute).FullName!;

    /// <summary>Die Vertrags-Assembly (Abstractions) und ihr Namespace — für die Projekt-Erkennung und usings.</summary>
    public static readonly string VertragsAssembly = typeof(IState).Assembly.GetName().Name!;
    public static readonly string VertragsNamespace = typeof(IState).Namespace!;

    // ── Member-Namen des Vertrags ──
    public static readonly string StateId = nameof(Abstractions.IState.Id);
    public static readonly string StateVersion = nameof(Abstractions.IState.Version);
    /// <summary>Die Frist als Ausgang einer Entscheidung (generischer Vertrags-Typ, Metadatenname mit Stelligkeit).</summary>
    public static readonly string Frist = typeof(Frist<>).FullName!;

    // ── Aggregat-Namensregel des Framework-Generators (EINE Quelle: Abstractions.Aggregatvertrag) ──
    public const string DeciderKlasse = Aggregatvertrag.Decider;
    public const string ApplierKlasse = Aggregatvertrag.Applier;
    public const string DecideMethode = Aggregatvertrag.Decide;
    public const string ApplyMethode = Aggregatvertrag.Apply;

    // ── OneOf (Decide-/Handler-Rückgaben) ──
    public static readonly string OneOf = typeof(OneOf<>).Name.Split('`')[0];

    // ── Microsoft-APIs der Composition Root ──
    public static readonly string DiErweiterungen = typeof(ServiceCollectionServiceExtensions).FullName!;
    public static readonly string DiTryErweiterungen = typeof(Microsoft.Extensions.DependencyInjection.Extensions.ServiceCollectionDescriptorExtensions).FullName!;
    public static readonly string KonfigBinder = typeof(ConfigurationBinder).FullName!;
    public static readonly string KonfigGetValue = nameof(ConfigurationBinder.GetValue);
    public static readonly string KonfigSchnittstelle = typeof(IConfiguration).FullName!;
    public static readonly string Umgebung = typeof(Environment).FullName!;
    public static readonly string UmgebungLesen = nameof(Environment.GetEnvironmentVariable);

    // ── Typ-Vergleich BCL/Vertrag ohne Namens-Strings ──
    /// <summary>Ist <paramref name="s"/> (Original-Definition) der CLR-Typ <paramref name="t"/>? (Namespace + Metadatenname)</summary>
    public static bool Ist(ITypeSymbol? s, Type t) =>
        s is INamedTypeSymbol n && n.OriginalDefinition.MetadataName == t.Name
        && n.OriginalDefinition.ContainingNamespace?.ToDisplayString() == t.Namespace;

    /// <summary>Ist <paramref name="s"/> ein <c>OneOf&lt;…&gt;</c> des Vertrags (beliebige Stelligkeit)?</summary>
    public static bool IstOneOf(ITypeSymbol? s) =>
        s is INamedTypeSymbol n && n.Name == OneOf && n.ContainingNamespace?.ToDisplayString() == typeof(OneOf<>).Namespace;
}
