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
    public static readonly string IEvent = typeof(IEvent).FullName!;
    public static readonly string ITransientEvent = typeof(ITransientEvent).FullName!;
    public static readonly string ISubscriber = typeof(ISubscriber).FullName!;
    public static readonly string IPullSubscriber = typeof(IPullSubscriber).FullName!;
    public static readonly string IAppendProjektion = typeof(IAppendProjektion).FullName!;
    public static readonly string IReader = typeof(IReader<>).FullName!;
    public static readonly string IQuery = typeof(IQuery).FullName!;
    public static readonly string IQueryResponse = typeof(IQueryResponse).FullName!;
    public static readonly string IReadModel = typeof(IReadModel).FullName!;
    public static readonly string IWriteStore = typeof(IWriteStore).FullName!;
    public static readonly string IReadStore = typeof(IReadStore).FullName!;
    public static readonly string IReadStoreT = typeof(IReadStore<>).FullName!;
    public static readonly string IWertobjekt = typeof(IWertobjekt).FullName!;
    public static readonly string IPipelineHandler = typeof(IPipelineHandler).FullName!;
    public static readonly string IPipelineTrigger = typeof(IPipelineTrigger).FullName!;
    public static readonly string IPipelineSelfMessage = typeof(IPipelineSelfMessage).FullName!;
    public static readonly string IProzessDefinition = typeof(IProzessDefinition).FullName!;
    public static readonly string IMessagePayload = typeof(IMessagePayload).FullName!;
    public static readonly string IPipelineOutput = typeof(IPipelineOutput).FullName!;
    public static readonly string IDecider = typeof(IDecider<>).FullName!;
    public static readonly string IApplier = typeof(IApplier<>).FullName!;
    public static readonly string IAggregateEnvelope = typeof(IAggregateEnvelope).FullName!;
    public static readonly string PipelineContext = typeof(PipelineContext).FullName!;
    public static readonly string IFristplan = typeof(IFristplan).FullName!;
    public static readonly string Frist = typeof(Frist).FullName!;
    public static readonly string ProjectionReaderAttribute = typeof(ProjectionReaderAttribute).FullName!;
    public static readonly string AggregatNameAttribute = typeof(AggregatNameAttribute).FullName!;
    public static readonly string IngressAttribute = typeof(IngressAttribute).FullName!;
    public static readonly string RoutingTabelleAttribute = typeof(RoutingTabelleAttribute).FullName!;
    public static readonly string IngressOrt = nameof(Abstractions.IngressAttribute.Ort);
    /// <summary>Ingress-Art (Enum-Wert) → Modus-Kennung des Editors.</summary>
    public static readonly Dictionary<int, string> IngressModus = new()
    {
        [(int)IngressArt.Webhook] = "webhook", [(int)IngressArt.Timer] = "timer", [(int)IngressArt.Datei] = "filewatch",
    };

    /// <summary>Die Vertrags-Assembly (Abstractions) und ihr Namespace — für die Projekt-Erkennung und usings.</summary>
    public static readonly string VertragsAssembly = typeof(IState).Assembly.GetName().Name!;
    public static readonly string VertragsNamespace = typeof(IState).Namespace!;

    // ── Member-Namen des Vertrags ──
    public static readonly string StateId = nameof(Abstractions.IState.Id);
    public static readonly string StateVersion = nameof(Abstractions.IState.Version);
    public static readonly string SubscriberId = nameof(Abstractions.ISubscriber.SubscriberId);
    public static readonly string PipelineId = nameof(Abstractions.IPipelineHandler.PipelineId);
    public static readonly string Regeln = nameof(Abstractions.IProzessDefinition.Regeln);
    public static readonly string TrackDeps = nameof(Abstractions.ProjectionReaderAttribute.TrackDeps);
    public static readonly string FristPlane = nameof(Abstractions.IFristplan.PlaneAsync);
    public static readonly string FristEntferne = nameof(Abstractions.IFristplan.EntferneAsync);
    public static readonly string FristKontext = nameof(Abstractions.Frist.Kontext);
    public static readonly string FristFällig = nameof(Abstractions.Frist.Fällig);
    public static readonly string ScheduleSelf = nameof(Abstractions.PipelineContext.ScheduleSelf);

    // ── Aggregat-Namensregel des Framework-Generators (EINE Quelle: Abstractions.Aggregatvertrag) ──
    public const string DeciderKlasse = Aggregatvertrag.Decider;
    public const string ApplierKlasse = Aggregatvertrag.Applier;
    public const string DecideMethode = Aggregatvertrag.Decide;
    public const string ApplyMethode = Aggregatvertrag.Apply;

    // ── Prozess-DSL (Fluent-Verben) ──
    public static readonly string ProzessTyp = typeof(Prozess<>).Name.Split('`')[0];
    public static readonly string ProzessMetadatenName = typeof(Prozess<>).FullName!;
    /// <summary>Namespace der DSL-Typen (Prozess, RegelBauer…, RegelAbschluss…) — Verben werden über ihr Symbol erkannt.</summary>
    public static readonly string DslNamespace = typeof(Prozess<>).Namespace!;
    public static readonly string Definiere = nameof(Prozess<IEvent>.Definiere);
    public static readonly string Auf = nameof(RegelBauer.Auf);
    public static readonly string Und = nameof(RegelBauer<IEvent>.Und);
    public static readonly string UndAlle = nameof(RegelBauer<IEvent>.UndAlle);
    public static readonly string Sende = nameof(RegelBauer<IEvent>.Sende);
    public static readonly string SendeJe = nameof(RegelBauer<IEvent>.SendeJe);
    public static readonly string RückgängigDurch = nameof(RegelAbschluss<IEvent>.RückgängigDurch);
    public static readonly string RückgängigDurchJe = nameof(RegelAbschluss<IEvent>.RückgängigDurchJe);
    public static readonly HashSet<string> RegelVerben = new(StringComparer.Ordinal)
        { Auf, Und, UndAlle, Sende, SendeJe, RückgängigDurch, RückgängigDurchJe };

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

    /// <summary>Ist <paramref name="m"/> ein Verb der Prozess-DSL (per Symbol: Vertrags-Assembly + DSL-Namespace + Verb-Name)?</summary>
    public static bool IstRegelVerb(IMethodSymbol? m) =>
        m?.ContainingType is { } ct && ct.ContainingAssembly?.Name == VertragsAssembly
        && ct.ContainingNamespace?.ToDisplayString() == DslNamespace && RegelVerben.Contains(m.Name);

    /// <summary>Ist <paramref name="s"/> ein <c>OneOf&lt;…&gt;</c> des Vertrags (beliebige Stelligkeit)?</summary>
    public static bool IstOneOf(ITypeSymbol? s) =>
        s is INamedTypeSymbol n && n.Name == OneOf && n.ContainingNamespace?.ToDisplayString() == typeof(OneOf<>).Namespace;
}
