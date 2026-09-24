using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace GraphExtractor;

// Zwischen-Repräsentation (raw), die der GraphBuilder zum Property-Graph und der ModellMapper zum
// Editor-Modell zusammensetzt. Grundsatz: ALLES, was der Scaffolder zurückschreiben kann, wird hier
// verlustfrei gelesen (Felder inkl. Defaults, Handcode-Rümpfe, Parameternamen, Saga-Lambdas) — sonst
// laufen Code und Editor auseinander. Die ParitaetsPruefung (--check) beweist das als Fixpunkt.

public sealed class DomainModel
{
    public List<AggregateRaw> Aggregates { get; } = new();
    public List<ProcessRaw> Processes { get; } = new();
    public List<ProjectionRaw> Projections { get; } = new();
    public List<QueryRaw> Queries { get; } = new();
    public List<ReaderRaw> Readers { get; } = new();
    public List<PipelineRaw> Pipelines { get; } = new();
    public List<StoreRaw> Stores { get; } = new();

    /// <summary>Records ohne Nachrichten-Marker in Domain-Assemblies (Value Objects).</summary>
    public List<RecordRaw> ValueObjects { get; } = new();
    /// <summary><c>IQueryResponse</c>-Records.</summary>
    public List<RecordRaw> Responses { get; } = new();
    /// <summary><c>IReadModel</c>-Typen (Dokumente der Leseseite).</summary>
    public List<RecordRaw> ReadModels { get; } = new();
    /// <summary><c>IPipelineTrigger</c>-Records (Ingress-Nachrichten).</summary>
    public List<RecordRaw> Triggers { get; } = new();
    /// <summary>Enums in Domain-Assemblies.</summary>
    public List<EnumRaw> Enums { get; } = new();
    /// <summary>
    /// Konfigurations-Records: Records ohne Nachrichten-Rolle, die per DI in den KONSTRUKTOR eines Konsumenten
    /// (Pipeline, Projektion/Reaktion, Reader, Store-Impl) injiziert werden — keine Value Objects der Domäne.
    /// </summary>
    public List<RecordRaw> Konfigs { get; } = new();
    /// <summary>Konfig-Record-Name → Namen der Konsumenten, die ihn injizieren.</summary>
    public Dictionary<string, List<string>> KonfigNutzer { get; } = new(StringComparer.Ordinal);

    public HashSet<string> RegisteredProcesses { get; } = new(StringComparer.Ordinal);

    // ── Der Rahmen, wie der Code ihn vorgibt (für den Scaffolder: wohin, wie heißt es) ──
    /// <summary>Namen der inneren Decider-/Applier-Klassen und ihrer Methoden, wie der Code sie benennt (häufigste).</summary>
    public string? DeciderKlasse, ApplierKlasse, DecideMethode, ApplyMethode;
    /// <summary>Namespace → absolutes Verzeichnis, in dem seine Typen liegen (häufigstes).</summary>
    public Dictionary<string, string> NamespaceVerzeichnisse { get; } = new(StringComparer.Ordinal);
    /// <summary>global usings der Domänen-Compilations.</summary>
    public List<string> GlobaleUsings { get; } = new();
    /// <summary>Größte Stelligkeit von OneOf bzw. RegelBauer im Vertrag — aus der Compilation gezählt.</summary>
    public int OneOfMax, UndMax;
    /// <summary>Wurzel-Namespace je Domänen-Projekt → Projektverzeichnis (absolut) — aus der Projektlage.</summary>
    public Dictionary<string, string> ProjektWurzeln { get; } = new(StringComparer.Ordinal);
    /// <summary>Solution-Verzeichnis (absolut) — Bezug für relative Pfade im Editor-Modell.</summary>
    public string Wurzel { get; set; } = "";

    /// <summary>Event-FullName → (Simple, Persisted, Felder, Meta).</summary>
    public Dictionary<string, EventType> Events { get; } = new(StringComparer.Ordinal);
    /// <summary>Command-FullName → (Simple, IsCreation, Felder, Meta).</summary>
    public Dictionary<string, CommandType> Commands { get; } = new(StringComparer.Ordinal);
}

/// <summary>Quelltext-Metadaten eines Typs: Doku (summary), Handcode-Rumpf, Datei-usings, Herkunfts-Assembly.</summary>
/// <param name="IstDomäne">Liegt der Typ in einer Domänen-Assembly (abgeleitet, <see cref="Projektlage"/>) — editierbarer Fachcode, kein Framework?</param>
/// <param name="Typart">Die Deklarationsform ohne <c>public</c>, verbatim: <c>record</c>, <c>sealed record</c>, <c>record struct</c>, <c>class</c> …</param>
/// <param name="Basen">Basistypen AUSSER dem Rollen-Marker (z. B. zusätzliche Interfaces) — verbatim aus der Basisliste.</param>
/// <param name="Attribute">Attribut-Listen des Typs verbatim (null = keine).</param>
/// <param name="OhneParameterliste">Record ohne <c>(…)</c> bzw. jede Klasse/Struct (Felder dann nur als Properties).</param>
public sealed record TypMeta(string Namespace, string Assembly, string? Doku, string? Zusatz, List<string> Usings, bool IstDomäne = false, string? Datei = null,
    string Typart = "public record", List<string>? Basen = null, string? Attribute = null, bool OhneParameterliste = false);

public sealed record EventType(string Simple, bool Persisted, List<FieldInfo> Fields, TypMeta Meta);
public sealed record CommandType(string Simple, bool IsCreation, List<FieldInfo> Fields, TypMeta Meta);

public sealed class RecordRaw
{
    public string Name = "", Full = "";
    public List<FieldInfo> Fields = new();
    public TypMeta Meta = new("", "", null, null, new());
    /// <summary>Nur ReadModel: der Store, dessen Funktionen diesen Typ lesen/schreiben (typisiert gematcht, eindeutig).</summary>
    public string? Store;
    /// <summary>Nur ReadModel: mehrere Stores nennen den Typ — keine Zuordnung geraten, sondern alle Kandidaten.</summary>
    public List<string>? StoreKandidaten;
}

public sealed class EnumRaw
{
    public string Name = "", Full = "";
    public List<string> Werte = new();
    public TypMeta Meta = new("", "", null, null, new());
    /// <summary>Hat ein Member den Wert 0 (implizit oder explizit)? Der Proto-Wire verliert 0 → Diagnose.</summary>
    public bool HatNull;
}

public sealed class AggregateRaw
{
    public string Name = "", Namespace = "", Full = "";
    public List<FieldInfo> State = new();
    public string? Doku, StateZusatz, DeciderZusatz, ApplierZusatz;
    /// <summary>Absolute Quelldateien: State-Deklaration, innerer Decider, innerer Applier; je Command/Event die Methode.</summary>
    public string? Datei, DeciderDatei, ApplierDatei;
    public Dictionary<string, string> DecideDateien = new(), ApplyDateien = new();
    /// <summary>Wie der Code sie benennt (null = nicht genau eine Klasse); Geschachtelt = innere Klasse des States.</summary>
    public string? DeciderKlasse, ApplierKlasse;
    public bool DeciderGeschachtelt, ApplierGeschachtelt;
    /// <summary>Methodenname je Command- bzw. Event-FullName (wie im Code).</summary>
    public Dictionary<string, string> DecideMethoden = new(StringComparer.Ordinal), ApplyMethoden = new(StringComparer.Ordinal);
    public List<string> Usings = new();
    public List<string> HandlesCommandsFull = new();  // FullNames der entschiedenen Commands (Quelltext-Reihenfolge)
    /// <summary>Ablehnungen (ITransientEvent) je Command — der Sad-Path, den GeneratedCommandRouting bewusst auslässt.</summary>
    public List<(string CmdFull, string EvtFull)> Rejects = new();
    /// <summary>Die OneOf-Ausgänge je Command IN SIGNATUR-REIHENFOLGE (Event-FullNames, Erfolg + Ablehnung).</summary>
    public Dictionary<string, List<string>> DecideOutcomes = new();
    /// <summary>Parametername je Command-FullName (der Rumpf referenziert ihn).</summary>
    public Dictionary<string, string> DecideParams = new();

    /// <summary>Guard-Ausdruck je Zweig, Schlüssel <c>cmdFull|EvtSimpleName</c> — das „Warum" aus der Decider-Syntax.</summary>
    public Dictionary<string, string> Guards = new();

    /// <summary>Der ECHTE Decide-Rumpf je Command (FullName → dedenteter Body-Text; "" = leer).</summary>
    public Dictionary<string, string> DecideBodies = new();
    /// <summary>Die ECHTEN Apply-Methoden in Quelltext-Reihenfolge (Event-FullName).</summary>
    public List<string> ApplyEventsFull = new();
    /// <summary>Der ECHTE Apply-Rumpf je Event (SimpleName → dedenteter Body-Text; "" = bewusster No-op).</summary>
    public Dictionary<string, string> ApplyBodies = new();
    /// <summary>Parametername je Event-SimpleName.</summary>
    public Dictionary<string, string> ApplyParams = new();
}

public sealed class ProcessRaw
{
    public string Name = "", Namespace = "";
    public string TriggerFull = "";
    public string? Doku, Datei;
    public List<string> Usings = new();
    public List<RuleRaw> Rules = new();
}

public sealed class RuleRaw
{
    public List<string> WhenFull = new();   // Bedingungs-Events (FullNames)
    public string Join = "single";          // single | and | count
    public string? SammelFull;
    /// <summary>Der Count-Join-Lambda verbatim (<c>t => n</c>).</summary>
    public string? SammelLambda;
    public bool FanOut;
    public string SendsFull = "";
    /// <summary>Der Sende-/SendeJe-Lambda verbatim.</summary>
    public string? SendLambda;
    public string? CompensatesFull;
    public bool CompFanOut;
    public string? CompLambda;
}

public sealed class ProjectionRaw
{
    public string Name = "", Full = "", Namespace = "";
    /// <summary>Der Wert der SubscriberId, wenn er zur Compile-Zeit konstant ist; sonst null (nicht geraten).</summary>
    public string? SubscriberId;
    /// <summary>Geordneter Pull (<c>IPullSubscriber</c>)?</summary>
    public bool Pull;
    /// <summary>Append-artig (<c>IAppendProjektion</c> ⇒ Co-Commit-Pflicht, GA-1)?</summary>
    public bool Append;
    /// <summary>Emittierender Konsument (Reaktion): mindestens ein Handle yieldet Commands.</summary>
    public bool IstReaktion;
    public List<string> ConsumesFull = new();
    /// <summary>Handle-Rumpf je konsumiertem Event (Simple-Name → Body-Text).</summary>
    public Dictionary<string, string> HandleBodies = new();
    /// <summary>Aufgerufene Store-Funktionen je Event (Simple-Name → [(Store, Methode)]) — für die Projektion→Store-Kante.</summary>
    public Dictionary<string, List<(string Store, string Method, bool IsRead)>> HandleStoreCalls = new();
    /// <summary>Ge-yieldete Commands je Event (Simple-Name → Command-Simple-Namen) — Reaktion.</summary>
    public Dictionary<string, List<string>> HandleSends = new();
    /// <summary>Ge-yieldete (reaktiv veröffentlichte) Events je Event (Simple-Name → Event-Simple-Namen).</summary>
    public Dictionary<string, List<string>> HandlePublishes = new();
}

public sealed class QueryRaw
{
    public string Name = "", Full = "";
    public List<FieldInfo> Fields = new();
    public TypMeta Meta = new("", "", null, null, new());
}

public sealed class ReaderRaw
{
    public string Name = "", Full = "", Namespace = "", ProjectionName = "";
    public bool TrackDeps = true;
    public List<string> QueryNames = new();
    /// <summary>Handle-Rumpf je Query (Query-Simple-Name → Body-Text).</summary>
    public Dictionary<string, string> HandleBodies = new();
    /// <summary>Aufgerufene Store-Read-Funktionen je Query (Query-Simple-Name → [(Store, Methode)]).</summary>
    public Dictionary<string, List<(string Store, string Method, bool IsRead)>> HandleStoreCalls = new();
    /// <summary>Die (OneOf-)Responses je Query (Query-Simple-Name → Response-Simple-Namen) aus der Rückgabe-Signatur.</summary>
    public Dictionary<string, List<string>> HandleResponses = new();
}

public sealed class PipelineRaw
{
    public string Name = "", Full = "", Namespace = "";
    /// <summary>Der Wert der PipelineId, wenn er zur Compile-Zeit konstant ist; sonst null (nicht geraten).</summary>
    public string? PipelineId;
    /// <summary>Per Konstruktor injizierte Konfigurations-Records (Simple-Namen).</summary>
    public List<string> Konfigs = new();
    /// <summary>Je Handle: Eingang, Kanal (trigger | event | self) und ge-yieldete Commands.</summary>
    public List<(string InputFull, string InputKind, List<string> EmitsFull)> Handles = new();
    /// <summary>Handle-Rumpf je Eingang (Input-FullName → Body-Text).</summary>
    public Dictionary<string, string> HandleBodies = new();
    /// <summary>Ge-yieldete Trigger-Nachrichten je Handle (Input-FullName → [Trigger-Simple-Name]) — für die Pipeline→Pipeline-Kette.</summary>
    public Dictionary<string, List<string>> HandleEmitsTriggers = new();
    /// <summary><c>ctx.ScheduleSelf(new X(), delay)</c> je Handle (Input-FullName → [(Self-Simple-Name, Delay-Ausdruck)]).</summary>
    public Dictionary<string, List<(string Name, string Delay)>> HandleSchedules = new();
}

/// <summary>Eine Store-Funktion (aus dem Store-Interface + Impl-Rumpf).</summary>
public sealed class StoreFnRaw
{
    public string Name = "";
    public bool IsRead;
    public List<(string Name, string Type)> Params = new();
    public string? Return;
    public string? Body;
    /// <summary>Alle Typen (FullNames), die die Signatur nennt — auch in Typ-Argumenten (für ReadModel → Store).</summary>
    public HashSet<string> TypRefs = new(StringComparer.Ordinal);
}

/// <summary>
/// Ein Store = ein <c>IWriteStore</c>-Interface + die <c>IReadStore&lt;TWrite&gt;</c>-Interfaces, die es als Partner
/// nennen (bzw. ein alleinstehendes <c>IReadStore</c>). Name = das Interface, das ihn verankert (frei benannt).
/// </summary>
public sealed class StoreRaw
{
    public string Name = "", Namespace = "";
    /// <summary>FullNames der Schreib-/Lese-Interfaces dieses Stores.</summary>
    public string? WriteIface;
    public List<string> ReadIfaces = new();
    public List<StoreFnRaw> Fns = new();
    /// <summary>Die konkreten Implementierungen (FullNames) — für die ReadModel-Zuordnung über den Impl-Rumpf.</summary>
    public List<string> ImplsFull = new();
    /// <summary>Mehr als eine konkrete Klasse implementiert dasselbe Interface → DI-Auflösung mehrdeutig.</summary>
    public List<string> MehrdeutigeImpls = new();
}

/// <summary>
/// Extrahiert das gesamte Domänen-Modell semantisch (über Marker-Interfaces, nicht textuell) — inklusive der
/// Sagas aus dem Prozess-DSL und der Handcode-Anteile, die der Scaffolder für den Round-trip braucht.
/// </summary>
public sealed class DomainExtractor
{
    private readonly List<Compilation> _comps;
    private readonly List<INamedTypeSymbol> _types;

    private readonly INamedTypeSymbol? _iState, _iCommand, _iCreation, _iEvent, _iTransient,
        _iSubscriber, _iPull, _iAppend, _iReader, _iQuery, _iQueryResponse, _iReadModel,
        _iPipelineHandler, _iPipelineTrigger, _iSelfMessage, _iProzessDef, _iPayload, _iPipelineOutput;


    /// <summary>Die Domänen-Assemblies (abgeleitet aus der <see cref="Projektlage"/>).</summary>
    private readonly HashSet<string> _domänen;
    /// <summary>Die globalen usings der Domänen-Compilations (ImplicitUsings + global using) + der Vertrags-Namespace — nie „explizit".</summary>
    private readonly HashSet<string> _impliziteUsings;
    private readonly INamedTypeSymbol? _iDecider, _iApplier, _iAggEnvelope, _pipelineContext,
        _iWriteStore, _iReadStore, _iReadStoreT, _iWertobjekt, _prozessTyp;
    /// <summary>State-FullName → die Typen, die <c>IDecider&lt;State&gt;</c> bzw. <c>IApplier&lt;State&gt;</c> implementieren (egal wo deklariert).</summary>
    private readonly Dictionary<string, List<INamedTypeSymbol>> _deciderJeState = new(StringComparer.Ordinal), _applierJeState = new(StringComparer.Ordinal);

    public DomainExtractor(IEnumerable<Compilation> comps, ISet<string> domänenAssemblies)
    {
        _comps = comps.ToList();
        _types = Sym.AllConcreteTypes(_comps);
        _domänen = new HashSet<string>(domänenAssemblies, StringComparer.Ordinal);

        INamedTypeSymbol? Get(string n) => _comps.Select(c => c.GetTypeByMetadataName(n)).FirstOrDefault(x => x != null);
        _iState = Get(Vertrag.IState);
        _iCommand = Get(Vertrag.ICommand);
        _iCreation = Get(Vertrag.ICreationCommand);
        _iEvent = Get(Vertrag.IEvent);
        _iTransient = Get(Vertrag.ITransientEvent);
        _iSubscriber = Get(Vertrag.ISubscriber);
        _iPull = Get(Vertrag.IPullSubscriber);
        _iAppend = Get(Vertrag.IAppendProjektion);
        _iReader = Get(Vertrag.IReader);
        _iQuery = Get(Vertrag.IQuery);
        _iQueryResponse = Get(Vertrag.IQueryResponse);
        _iReadModel = Get(Vertrag.IReadModel);
        _iPipelineHandler = Get(Vertrag.IPipelineHandler);
        _iPipelineTrigger = Get(Vertrag.IPipelineTrigger);
        _iSelfMessage = Get(Vertrag.IPipelineSelfMessage);
        _iProzessDef = Get(Vertrag.IProzessDefinition);
        _iPayload = Get(Vertrag.IMessagePayload);
        _iPipelineOutput = Get(Vertrag.IPipelineOutput);
        _iDecider = Get(Vertrag.IDecider);
        _iApplier = Get(Vertrag.IApplier);
        _iAggEnvelope = Get(Vertrag.IAggregateEnvelope);
        _pipelineContext = Get(Vertrag.PipelineContext);
        _iWriteStore = Get(Vertrag.IWriteStore);
        _iReadStore = Get(Vertrag.IReadStore);
        _iReadStoreT = Get(Vertrag.IReadStoreT);
        _iWertobjekt = Get(Vertrag.IWertobjekt);
        _prozessTyp = Get(Vertrag.ProzessMetadatenName);

        // Aggregat-Komposition über den TYP: wer IDecider<T>/IApplier<T> implementiert, gehört zum State T.
        foreach (var t in _types)
            foreach (var i in t.AllInterfaces)
            {
                var def = i.OriginalDefinition.Fq();
                var ziel = def == _iDecider?.Fq() ? _deciderJeState : def == _iApplier?.Fq() ? _applierJeState : null;
                if (ziel == null || i.TypeArguments.Length != 1) continue;
                var state = i.TypeArguments[0].Fq();
                if (!ziel.TryGetValue(state, out var liste)) ziel[state] = liste = new();
                if (!liste.Any(x => x.Fq() == t.Fq())) liste.Add(t);
            }

        _impliziteUsings = new HashSet<string>(StringComparer.Ordinal) { Vertrag.VertragsNamespace };
        foreach (var c in _comps.Where(c => _domänen.Contains(c.AssemblyName ?? "")))
            foreach (var t in c.SyntaxTrees)
                foreach (var u in t.GetRoot().DescendantNodes(n => n is CompilationUnitSyntax).OfType<UsingDirectiveSyntax>())
                    if (u.GlobalKeyword.IsKind(SyntaxKind.GlobalKeyword) && u.Alias == null && u.Name != null)
                        _impliziteUsings.Add(u.Name.ToString().Replace("global::", ""));
    }

    private bool IstDomänenAssembly(IAssemblySymbol? a) => a != null && _domänen.Contains(a.Name);
    private static bool Implementiert(INamedTypeSymbol t, INamedTypeSymbol? generischesIface) =>
        generischesIface != null && t.AllInterfaces.Any(i => i.OriginalDefinition.ToDisplayString() == generischesIface.ToDisplayString());

    private SemanticModel Model(SyntaxTree tree) =>
        _comps.First(c => c.ContainsSyntaxTree(tree)).GetSemanticModel(tree);

    public DomainModel Extract()
    {
        var m = new DomainModel();

        CatalogEventsAndCommands(m);
        ExtractRegisteredProcesses(m);
        // Stores ZUERST: die Konsumenten-Handler lösen ihre Store-Aufrufe gegen die erkannten Store-Interfaces auf.
        ExtractStores(m);

        foreach (var t in _types)
        {
            if (Sym.Implements(t, _iState) && _deciderJeState.ContainsKey(t.Fq())) m.Aggregates.Add(ReadAggregate(t));
            if (Sym.Implements(t, _iProzessDef)) m.Processes.Add(ReadProcess(t));
            if (Sym.Implements(t, _iSubscriber)) { var p = TryReadProjection(t); if (p != null) m.Projections.Add(p); }
            if (Sym.Implements(t, _iQuery)) m.Queries.Add(ReadQuery(t));
            if (Sym.Implements(t, _iPipelineHandler)) m.Pipelines.Add(ReadPipeline(t));
            var reader = TryReadReader(t);
            if (reader != null) m.Readers.Add(reader);
        }

        CatalogDomainTypes(m);
        SeparateKonfigs(m);
        LinkReadModelStores(m);

        LeseRahmen(m);

        // Deterministische Reihenfolge (die Symbol-Enumeration ist es nicht garantiert).
        m.Aggregates.Sort((a, b) => string.CompareOrdinal(a.Full, b.Full));
        m.Processes.Sort((a, b) => string.CompareOrdinal(a.Namespace + "." + a.Name, b.Namespace + "." + b.Name));
        return m;
    }

    /// <summary>
    /// Der Rahmen aus dem Code: Verzeichnis je Namespace (nur wenn EINDEUTIG — alle Typen des Namespace liegen in
    /// genau einem Verzeichnis; sonst kein Eintrag statt einer Mehrheits-Schätzung) und die globalen usings.
    /// Die Klassen-/Methodennamen der Aggregate sind Vertrag (<see cref="Vertrag.DeciderKlasse"/> …), nicht geschätzt.
    /// </summary>
    private void LeseRahmen(DomainModel m)
    {
        m.DeciderKlasse = Vertrag.DeciderKlasse;
        m.ApplierKlasse = Vertrag.ApplierKlasse;
        m.DecideMethode = Vertrag.DecideMethode;
        m.ApplyMethode = Vertrag.ApplyMethode;

        foreach (var g in DomainQuellTypen()
                     .SelectMany(t => QuellDeklarationen(t).Select(d => (Ns: t.ContainingNamespace.Fq(), Dir: Path.GetDirectoryName(d.SyntaxTree.FilePath) ?? "")))
                     .GroupBy(x => x.Ns))
        {
            var dirs = g.Select(x => x.Dir).Distinct(StringComparer.Ordinal).ToList();
            if (dirs.Count == 1) m.NamespaceVerzeichnisse[g.Key] = dirs[0];
        }

        m.GlobaleUsings.AddRange(_impliziteUsings.Where(u => u != Vertrag.VertragsNamespace).OrderBy(u => u, StringComparer.Ordinal));

        // Stelligkeiten des Vertrags: die Compilation fragen, welche generischen Varianten es gibt.
        int Max(string ns, string name)
        {
            var n = 0;
            for (var k = 1; k <= 32; k++)
                if (_comps.Any(c => c.GetTypeByMetadataName($"{ns}.{name}`{k}") != null)) n = k;
            return n;
        }
        m.OneOfMax = Max(typeof(Abstractions.OneOf<>).Namespace!, Vertrag.OneOf);
        m.UndMax = Max(Vertrag.DslNamespace, typeof(Abstractions.RegelBauer).Name);
    }

    // ── Domänen-Typen ohne Nachrichten-Rolle: Value Objects, Responses, ReadModels, Trigger, Enums ──

    /// <summary>Quelltext-Typen der Domain*-Assemblies (nicht Framework, nicht Metadaten, nicht generiert).</summary>
    private IEnumerable<INamedTypeSymbol> DomainQuellTypen()
    {
        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (var comp in _comps.Where(c => _domänen.Contains(c.AssemblyName ?? "")))
            foreach (var t in AlleTypen(comp.Assembly.GlobalNamespace))
                if (t.DeclaringSyntaxReferences.Any(r => !IstGeneriert(r.SyntaxTree)) && seen.Add(t.Fq()))
                    yield return t;
    }

    private static IEnumerable<INamedTypeSymbol> AlleTypen(INamespaceSymbol ns)
    {
        foreach (var t in ns.GetTypeMembers())
            foreach (var x in MitGeschachtelten(t)) yield return x;
        foreach (var sub in ns.GetNamespaceMembers())
            foreach (var t in AlleTypen(sub)) yield return t;
    }

    private static IEnumerable<INamedTypeSymbol> MitGeschachtelten(INamedTypeSymbol t)
    {
        yield return t;
        foreach (var n in t.GetTypeMembers())
            foreach (var x in MitGeschachtelten(n)) yield return x;
    }

    private void CatalogDomainTypes(DomainModel m)
    {
        foreach (var t in DomainQuellTypen())
        {
            if (t.TypeKind == TypeKind.Enum)
            {
                m.Enums.Add(ReadEnum(t));
                continue;
            }
            // Geschachtelte Typen gehören zum Handcode ihres Containers (Zusatz), keine eigenen Bausteine.
            if (t.ContainingType != null || t.TypeKind is not (TypeKind.Class or TypeKind.Struct) || t.IsStatic || t.IsAbstract) continue;

            var raw = new RecordRaw { Name = t.Name, Full = t.Fq(), Fields = Felder(t), Meta = Meta(t) };
            if (Sym.Implements(t, _iQueryResponse)) m.Responses.Add(raw);
            else if (Sym.Implements(t, _iReadModel)) m.ReadModels.Add(raw);
            else if (Sym.Implements(t, _iPipelineTrigger)) m.Triggers.Add(raw);
            // Value Object = als Wertobjekt markiert ODER ein Record (Datenträger per Sprachkonstrukt) ohne jede Rolle.
            // Klassen ohne Marker sind Dienste/Helfer (z. B. Store-Implementierungen), keine Werte.
            else if ((Sym.Implements(t, _iWertobjekt) || t.IsRecord)
                     && !Sym.Implements(t, _iPayload) && !Sym.Implements(t, _iPipelineOutput) && !Sym.Implements(t, _iQuery)
                     && !Sym.Implements(t, _iSelfMessage) && !Sym.Implements(t, _iState)
                     && !Sym.Implements(t, _iWriteStore) && !Sym.Implements(t, _iReadStore))
                m.ValueObjects.Add(raw);
        }
        m.Enums.Sort((a, b) => string.CompareOrdinal(a.Full, b.Full));
        m.ValueObjects.Sort((a, b) => string.CompareOrdinal(a.Full, b.Full));
        m.Responses.Sort((a, b) => string.CompareOrdinal(a.Full, b.Full));
        m.ReadModels.Sort((a, b) => string.CompareOrdinal(a.Full, b.Full));
        m.Triggers.Sort((a, b) => string.CompareOrdinal(a.Full, b.Full));
    }

    private EnumRaw ReadEnum(INamedTypeSymbol t)
    {
        var e = new EnumRaw { Name = t.Name, Full = t.Fq(), Meta = Meta(t, mitZusatz: false) };
        var decl = t.DeclaringSyntaxReferences.Select(r => r.GetSyntax()).OfType<EnumDeclarationSyntax>().FirstOrDefault();
        foreach (var f in t.GetMembers().OfType<IFieldSymbol>().Where(f => f.HasConstantValue))
        {
            var syn = decl?.Members.FirstOrDefault(x => x.Identifier.Text == f.Name);
            e.Werte.Add(syn?.EqualsValue is { } ev ? $"{f.Name} = {ev.Value}" : f.Name);
            if (Convert.ToInt64(f.ConstantValue) == 0) e.HatNull = true;
        }
        return e;
    }

    /// <summary>
    /// ReadModel → Store: bevorzugt der Store, dessen Fn-Signaturen den Typ nennen; sonst der Store, dessen
    /// IMPLEMENTIERUNG ihn benutzt (<c>LoadAsync&lt;T&gt;</c>, <c>EnqueueStore(new T …)</c>) — typisiert über Symbole.
    /// </summary>
    private void LinkReadModelStores(DomainModel m)
    {
        foreach (var rm in m.ReadModels)
        {
            // Eindeutig oder gar nicht: nennen mehrere Stores den Typ, wird NICHT geraten (→ Diagnose im Graph).
            var perSignatur = m.Stores.Where(s => s.Fns.Any(f => f.TypRefs.Contains(rm.Full))).ToList();
            var kandidaten = perSignatur.Count > 0 ? perSignatur
                : m.Stores.Where(s => s.ImplsFull.Any(impl => ImplReferenziert(impl, rm.Full))).ToList();
            if (kandidaten.Count == 1) rm.Store = kandidaten[0].Name;
            else if (kandidaten.Count > 1) rm.StoreKandidaten = kandidaten.Select(s => s.Name).ToList();
        }
    }

    private bool ImplReferenziert(string implFull, string typFull)
    {
        var t = _types.FirstOrDefault(x => x.Fq() == implFull);
        if (t == null) return false;
        var simple = typFull[(typFull.LastIndexOf('.') + 1)..];
        foreach (var decl in QuellDeklarationen(t))
        {
            var model = Model(decl.SyntaxTree);
            foreach (var id in decl.DescendantNodes().OfType<SimpleNameSyntax>().Where(n => n.Identifier.Text == simple))
                if (model.GetSymbolInfo(id).Symbol is INamedTypeSymbol sym && sym.Fq() == typFull) return true;
        }
        return false;
    }

    /// <summary>
    /// Konfigurations-Records von Value Objects trennen: ein Record, der im Konstruktor eines Konsumenten
    /// (Pipeline, Subscriber, Reader, Store-Impl) steht, ist per DI injizierte Konfiguration — kein Fachwert.
    /// </summary>
    private void SeparateKonfigs(DomainModel m)
    {
        var voByFull = m.ValueObjects.ToDictionary(v => v.Full, StringComparer.Ordinal);
        var storeImpls = m.Stores.SelectMany(s => s.ImplsFull).ToHashSet(StringComparer.Ordinal);
        var injiziert = new HashSet<string>(StringComparer.Ordinal);
        foreach (var t in _types)
        {
            var istKonsument = Sym.Implements(t, _iPipelineHandler) || Sym.Implements(t, _iSubscriber)
                || t.AllInterfaces.Any(i => _iReader != null && i.OriginalDefinition.Fq() == _iReader.Fq())
                || storeImpls.Contains(t.Fq());
            if (!istKonsument) continue;
            foreach (var ctor in t.InstanceConstructors.Where(c => c.DeclaredAccessibility == Accessibility.Public))
                foreach (var p in ctor.Parameters)
                {
                    if (p.Type is not INamedTypeSymbol pt || !voByFull.ContainsKey(pt.Fq())) continue;
                    injiziert.Add(pt.Fq());
                    if (!m.KonfigNutzer.TryGetValue(pt.Name, out var nutzer)) m.KonfigNutzer[pt.Name] = nutzer = new();
                    if (!nutzer.Contains(t.Name)) nutzer.Add(t.Name);
                    var pipe = m.Pipelines.FirstOrDefault(x => x.Full == t.Fq());
                    if (pipe != null && !pipe.Konfigs.Contains(pt.Name)) pipe.Konfigs.Add(pt.Name);
                }
        }
        // Genau die injizierten Typen (per FullName) — nicht alles, was zufällig gleich heißt.
        foreach (var vo in m.ValueObjects.Where(v => injiziert.Contains(v.Full)).ToList())
        {
            m.ValueObjects.Remove(vo);
            m.Konfigs.Add(vo);
        }
        m.Konfigs.Sort((a, b) => string.CompareOrdinal(a.Full, b.Full));
    }

    // ── Stores: über die Vertrags-Marker IWriteStore / IReadStore<TWrite> (Namen frei) ──────────────

    /// <summary>Store-Interface (FullName) → (Store-Name, ist Lese-Seite).</summary>
    private readonly Dictionary<string, (string Store, bool IsRead)> _storeIfaces = new(StringComparer.Ordinal);

    private void ExtractStores(DomainModel m)
    {
        // 1) Die Store-Interfaces der Domäne: direkt markiert (IWriteStore / IReadStore / IReadStore<T>).
        var ifaces = DomainQuellTypen().Where(t => t.TypeKind == TypeKind.Interface
            && (Sym.Implements(t, _iWriteStore) || Sym.Implements(t, _iReadStore))).ToList();
        var stores = new Dictionary<string, StoreRaw>(StringComparer.Ordinal);      // Anker-Interface → Store
        var storeJeIface = new Dictionary<string, StoreRaw>(StringComparer.Ordinal); // jedes Store-Interface → Store
        StoreRaw Store(INamedTypeSymbol anker)
        {
            if (!stores.TryGetValue(anker.Fq(), out var st))
                stores[anker.Fq()] = st = new StoreRaw { Name = anker.Name, Namespace = anker.ContainingNamespace.Fq() };
            return st;
        }
        foreach (var w in ifaces.Where(i => Sym.Implements(i, _iWriteStore)))
        {
            var st = Store(w);
            st.WriteIface = w.Fq();
            storeJeIface[w.Fq()] = st;
            _storeIfaces[w.Fq()] = (st.Name, false);
        }
        foreach (var r in ifaces.Where(i => Sym.Implements(i, _iReadStore)))
        {
            // Partner als TYP: IReadStore<TWrite>; ohne Partner ist die Lese-Sicht ihr eigener Store.
            var partner = r.AllInterfaces.FirstOrDefault(x => x.OriginalDefinition.Fq() == _iReadStoreT?.Fq())?.TypeArguments[0] as INamedTypeSymbol;
            var st = partner != null && stores.ContainsKey(partner.Fq()) ? stores[partner.Fq()] : Store(r);
            st.ReadIfaces.Add(r.Fq());
            storeJeIface[r.Fq()] = st;
            _storeIfaces[r.Fq()] = (st.Name, true);
        }

        // 2) Funktionen je Seite aus den Interface-Membern, Rümpfe aus der Implementierung, die die Laufzeit registriert.
        var implsJeIface = new Dictionary<string, List<string>>(StringComparer.Ordinal);
        foreach (var iface in ifaces)
        {
            var isRead = _storeIfaces[iface.Fq()].IsRead;
            var store = storeJeIface[iface.Fq()];
            var impls = _types.Where(t => t.TypeKind is TypeKind.Class or TypeKind.Struct && !t.IsAbstract
                                          && t.AllInterfaces.Any(x => x.Fq() == iface.Fq())).ToList();
            implsJeIface[iface.Fq()] = impls.Select(x => x.Name).Distinct().OrderBy(x => x, StringComparer.Ordinal).ToList();
            foreach (var impl in impls) if (!store.ImplsFull.Contains(impl.Fq())) store.ImplsFull.Add(impl.Fq());

            foreach (var im in new[] { iface }.Concat(iface.AllInterfaces).Where(x => x.Fq() != _iWriteStore?.Fq() && x.Fq() != _iReadStore?.Fq()
                                                                                      && x.OriginalDefinition.Fq() != _iReadStoreT?.Fq())
                         .SelectMany(x => x.GetMembers().OfType<IMethodSymbol>()))
            {
                var fn = store.Fns.FirstOrDefault(f => f.Name == im.Name && f.IsRead == isRead);
                if (fn == null)
                {
                    fn = new StoreFnRaw
                    {
                        Name = im.Name, IsRead = isRead,
                        Params = im.Parameters.Select(p => (p.Name, ShortType(p.Type))).ToList(),
                        Return = isRead ? UnwrapTask(im.ReturnType) : null,
                    };
                    foreach (var tr in im.Parameters.Select(p => p.Type).Append(im.ReturnType).SelectMany(TypUndArgumente)) fn.TypRefs.Add(tr);
                    store.Fns.Add(fn);
                }
                foreach (var impl in impls)
                    if ((fn.Body == null || RegistrierteImpls().Contains(impl.Fq()))
                        && Sym.Implementierung(impl, im) is IMethodSymbol implM && MethodBody(implM) is { } body)
                        fn.Body = body;
            }
        }
        foreach (var (iface, impls) in implsJeIface)
            if (impls.Count > 1)
                storeJeIface[iface].MehrdeutigeImpls.Add($"{iface[(iface.LastIndexOf('.') + 1)..]}: {string.Join(", ", impls)}");
        m.Stores.AddRange(stores.Values.OrderBy(s => s.Name, StringComparer.Ordinal));
    }

    /// <summary>Ein Typ und alle Typ-Argumente darin, rekursiv (FullNames der Original-Definitionen und Argumente).</summary>
    private static IEnumerable<string> TypUndArgumente(ITypeSymbol t)
    {
        if (t is IArrayTypeSymbol a) { foreach (var x in TypUndArgumente(a.ElementType)) yield return x; yield break; }
        yield return t.OriginalDefinition.Fq();
        if (t is INamedTypeSymbol n)
            foreach (var ta in n.TypeArguments)
                foreach (var x in TypUndArgumente(ta)) yield return x;
    }

    private static string UnwrapTask(ITypeSymbol t) =>
        IstTask(t) ? ShortType(((INamedTypeSymbol)t).TypeArguments[0]) : ShortType(t);

    private static bool IstTask(ITypeSymbol t) => Vertrag.Ist(t, typeof(Task<>)) || Vertrag.Ist(t, typeof(ValueTask<>));

    private HashSet<string>? _registriert;
    /// <summary>
    /// Die konkreten Domänen-Typen, die das GENERAT (DI-Registrierung des Framework-Generators) erzeugt/registriert —
    /// welche Store-Implementierung die Laufzeit wirklich nimmt, statt einer Namens-Vorliebe.
    /// </summary>
    private HashSet<string> RegistrierteImpls()
    {
        if (_registriert != null) return _registriert;
        _registriert = new HashSet<string>(StringComparer.Ordinal);
        foreach (var c in _comps)
            foreach (var tree in c.SyntaxTrees.Where(IstGeneriert))
            {
                var model = c.GetSemanticModel(tree);
                foreach (var oc in tree.GetRoot().DescendantNodes().OfType<ObjectCreationExpressionSyntax>())
                    if (model.GetTypeInfo(oc).Type is INamedTypeSymbol nt && IstDomänenAssembly(nt.ContainingAssembly)) _registriert.Add(nt.Fq());
                foreach (var g in tree.GetRoot().DescendantNodes().OfType<GenericNameSyntax>())
                    foreach (var ta in g.TypeArgumentList.Arguments)
                        if (model.GetTypeInfo(ta).Type is INamedTypeSymbol at && at.TypeKind == TypeKind.Class && IstDomänenAssembly(at.ContainingAssembly)) _registriert.Add(at.Fq());
            }
        return _registriert;
    }


    // ── Kataloge: alle Events + Commands mit Klassifikation ──────────────────

    private void CatalogEventsAndCommands(DomainModel m)
    {
        foreach (var t in _types)
        {
            if (Sym.Implements(t, _iEvent))
            {
                var persisted = !Sym.Implements(t, _iTransient);
                m.Events[t.Fq()] = new EventType(t.Name, persisted, Felder(t), Meta(t));
            }
            if (Sym.Implements(t, _iCommand))
            {
                var isCreation = Sym.Implements(t, _iCreation);
                m.Commands[t.Fq()] = new CommandType(t.Name, isCreation, Felder(t), Meta(t));
            }
        }
    }

    // ── Aggregate ────────────────────────────────────────────────────────────

    private List<INamedTypeSymbol> DeciderVon(INamedTypeSymbol t) => _deciderJeState.GetValueOrDefault(t.Fq()) ?? new();
    private List<INamedTypeSymbol> ApplierVon(INamedTypeSymbol t) => _applierJeState.GetValueOrDefault(t.Fq()) ?? new();

    private AggregateRaw ReadAggregate(INamedTypeSymbol t)
    {
        var agg = new AggregateRaw { Name = t.Name, Namespace = t.ContainingNamespace.Fq(), Full = t.Fq() };
        var decls = QuellDeklarationen(t).OfType<TypeDeclarationSyntax>().ToList();
        // State-Datei = die Deklaration, die mehr trägt als nur geschachtelte Typen (Decider/Applier).
        agg.Datei = decls.FirstOrDefault(d => d.Members.Any(m => m is not BaseTypeDeclarationSyntax))?.SyntaxTree.FilePath ?? decls.FirstOrDefault()?.SyntaxTree.FilePath;
        agg.Doku = decls.Select(Summary).FirstOrDefault(d => d != null);
        agg.Usings = decls.SelectMany(d => DateiUsings(d)).Distinct(StringComparer.Ordinal).OrderBy(x => x, StringComparer.Ordinal).ToList();

        // State: einfache Auto-Properties + abgeleitete (=> expr) werden Felder; alles andere ist Handcode (Zusatz).
        var zusatz = new List<MemberDeclarationSyntax>();
        foreach (var decl in decls)
            foreach (var member in decl.Members)
            {
                if (member is BaseTypeDeclarationSyntax nested && Model(nested.SyntaxTree).GetDeclaredSymbol(nested) is INamedTypeSymbol ns
                    && (Implementiert(ns, _iDecider) || Implementiert(ns, _iApplier))) continue;
                if (member is PropertyDeclarationSyntax prop && TryStateFeld(prop) is { } feld)
                {
                    if (feld.Name == Vertrag.StateId || feld.Name == Vertrag.StateVersion) continue;
                    if (Model(prop.SyntaxTree).GetDeclaredSymbol(prop) is IPropertySymbol ps) feld.ElementTyp = ElementTyp(ps.Type);
                    agg.State.Add(feld);
                    continue;
                }
                zusatz.Add(member);
            }
        agg.StateZusatz = MemberText(zusatz);

        var deciders = DeciderVon(t);
        agg.DeciderKlasse = deciders.Count == 1 ? deciders[0].Name : null;
        agg.DeciderGeschachtelt = deciders.Count == 1 && deciders[0].ContainingType?.Fq() == t.Fq();
        agg.DeciderDatei = deciders.SelectMany(QuellDeklarationen).FirstOrDefault()?.SyntaxTree.FilePath;
        var deciderZusatz = new List<MemberDeclarationSyntax>();
        foreach (var dd in deciders.SelectMany(QuellDeklarationen).OfType<TypeDeclarationSyntax>())
            foreach (var member in dd.Members)
            {
                // Entscheidung = öffentliche Methode mit Command als erstem Parameter (Name egal — der Typ trägt die Rolle).
                if (member is MethodDeclarationSyntax md && md.Modifiers.Any(SyntaxKind.PublicKeyword)
                    && Model(md.SyntaxTree).GetDeclaredSymbol(md) is IMethodSymbol method
                    && method.Parameters.Length >= 1 && method.Parameters[0].Type is INamedTypeSymbol cmd
                    && Sym.Implements(cmd, _iCommand))
                {
                    var cmdFull = cmd.Fq();
                    agg.HandlesCommandsFull.Add(cmdFull);
                    agg.DecideMethoden[cmdFull] = method.Name;
                    agg.DecideDateien[cmdFull] = md.SyntaxTree.FilePath;
                    agg.DecideParams[cmdFull] = method.Parameters[0].Name;
                    var ausgaenge = UniverseEvents(method.ReturnType).ToList();
                    agg.DecideOutcomes[cmdFull] = ausgaenge.Select(e => e.Fq()).ToList();
                    foreach (var reject in ausgaenge.Where(e => Sym.Implements(e, _iTransient)))
                        agg.Rejects.Add((cmdFull, reject.Fq()));
                    foreach (var (evtSimple, guard) in ExtractGuards(method))
                        agg.Guards.TryAdd(cmdFull + "|" + evtSimple, guard);
                    var db = MethodBody(method);
                    if (db != null) agg.DecideBodies[cmdFull] = db;
                    continue;
                }
                deciderZusatz.Add(member);
            }
        agg.DeciderZusatz = MemberText(deciderZusatz);

        // Apply-Methoden: die ECHTEN (nicht aus den Commands abgeleitet) — inkl. bewusst leerer No-ops.
        var appliers = ApplierVon(t);
        agg.ApplierKlasse = appliers.Count == 1 ? appliers[0].Name : null;
        agg.ApplierGeschachtelt = appliers.Count == 1 && appliers[0].ContainingType?.Fq() == t.Fq();
        agg.ApplierDatei = appliers.SelectMany(QuellDeklarationen).FirstOrDefault()?.SyntaxTree.FilePath;
        var applierZusatz = new List<MemberDeclarationSyntax>();
        foreach (var ad in appliers.SelectMany(QuellDeklarationen).OfType<TypeDeclarationSyntax>())
            foreach (var member in ad.Members)
            {
                // Faltung = öffentliche void-Methode mit Event als erstem Parameter.
                if (member is MethodDeclarationSyntax md && md.Modifiers.Any(SyntaxKind.PublicKeyword)
                    && Model(md.SyntaxTree).GetDeclaredSymbol(md) is IMethodSymbol { ReturnsVoid: true } method
                    && method.Parameters.Length >= 1 && method.Parameters[0].Type is INamedTypeSymbol evt
                    && Sym.Implements(evt, _iEvent))
                {
                    agg.ApplyEventsFull.Add(evt.Fq());
                    agg.ApplyMethoden[evt.Fq()] = method.Name;
                    agg.ApplyDateien[evt.Name] = md.SyntaxTree.FilePath;
                    agg.ApplyParams[evt.Name] = method.Parameters[0].Name;
                    var ab = MethodBody(method);
                    if (ab != null) agg.ApplyBodies[evt.Name] = ab;
                    continue;
                }
                applierZusatz.Add(member);
            }
        agg.ApplierZusatz = MemberText(applierZusatz);
        return agg;
    }

    /// <summary>
    /// Eine State-Property als Feld, wenn der Scaffolder sie verlustfrei zurückschreiben kann:
    /// <c>public T X { get; set; } [= init;]</c>, <c>public T X { get; } [= init;]</c> oder <c>public T X => expr;</c>.
    /// Alles andere (Rumpf-Accessoren, <c>private set</c>, <c>init</c>, static, Attribute) bleibt Handcode.
    /// </summary>
    private static FieldInfo? TryStateFeld(PropertyDeclarationSyntax p)
    {
        if (!p.Modifiers.Any(SyntaxKind.PublicKeyword) || p.Modifiers.Any(SyntaxKind.StaticKeyword)
            || p.Modifiers.Count != 1 || p.AttributeLists.Count > 0 || p.ExplicitInterfaceSpecifier != null)
            return null;
        if (p.ExpressionBody is { } eb)
            return new FieldInfo { Name = p.Identifier.Text, Type = p.Type.ToString(), Expr = eb.Expression.ToString() };
        if (p.AccessorList is not { } al || al.Accessors.Any(a => a.Body != null || a.ExpressionBody != null || a.Modifiers.Count > 0))
            return null;
        var kinds = al.Accessors.Select(a => a.Kind()).ToList();
        var nurGet = kinds.SequenceEqual(new[] { SyntaxKind.GetAccessorDeclaration });
        var getSet = kinds.SequenceEqual(new[] { SyntaxKind.GetAccessorDeclaration, SyntaxKind.SetAccessorDeclaration });
        if (!nurGet && !getSet) return null;
        return new FieldInfo
        {
            Name = p.Identifier.Text, Type = p.Type.ToString(),
            Default = p.Initializer?.Value.ToString(), NurGet = nurGet,
        };
    }

    // ── Quelltext-Helfer ─────────────────────────────────────────────────────

    private static bool IstGeneriert(SyntaxTree tree) => Projektlage.IstGeneriert(tree);

    /// <summary>Die handgeschriebenen Deklarationen eines Typs (partial über Dateien), deterministisch nach Dateipfad.</summary>
    private static IEnumerable<SyntaxNode> QuellDeklarationen(INamedTypeSymbol t) =>
        t.DeclaringSyntaxReferences
            .Where(r => !IstGeneriert(r.SyntaxTree))
            .OrderBy(r => r.SyntaxTree.FilePath, StringComparer.Ordinal).ThenBy(r => r.Span.Start)
            .Select(r => r.GetSyntax());

    /// <summary>Doku, Handcode-Rumpf und Datei-usings eines Typs.</summary>
    private TypMeta Meta(INamedTypeSymbol t, bool mitZusatz = true)
    {
        var decl = QuellDeklarationen(t).OfType<BaseTypeDeclarationSyntax>().FirstOrDefault();
        string? zusatz = null;
        // Handcode = alle Member AUSSER den Property-Feldern (die sind Felder, siehe Felder()).
        if (mitZusatz && decl is TypeDeclarationSyntax td && td.OpenBraceToken.IsKind(SyntaxKind.OpenBraceToken))
            zusatz = MemberText(td.Members.Where(mm => !(mm is PropertyDeclarationSyntax pp && EigenschaftsFeld(pp) != null)));

        // Deklarationsform verbatim (Modifizierer + Schlüsselwort), Basistypen außer dem Rollen-Marker, Attribute.
        var typart = decl switch
        {
            RecordDeclarationSyntax r => string.Join(" ", r.Modifiers.Where(x => !x.IsKind(SyntaxKind.PartialKeyword)).Select(x => x.Text)
                .Append(r.Keyword.Text).Concat(r.ClassOrStructKeyword.IsKind(SyntaxKind.None) ? [] : new[] { r.ClassOrStructKeyword.Text })),
            TypeDeclarationSyntax c => string.Join(" ", c.Modifiers.Where(x => !x.IsKind(SyntaxKind.PartialKeyword)).Select(x => x.Text).Append(c.Keyword.Text)),
            _ => "public record",
        };
        List<string>? basen = null;
        if (decl?.BaseList is { } bl)
        {
            var model = Model(decl.SyntaxTree);
            // Die Rollen-Marker (sie bestimmen die Art und werden aus ihr geschrieben) — alle übrigen Basen sind Code-Fakt.
            var rollen = new[] { _iCommand, _iCreation, _iEvent, _iTransient, _iQuery, _iQueryResponse, _iReadModel, _iPipelineTrigger }
                .Where(x => x != null).Select(x => x!.Fq()).ToHashSet(StringComparer.Ordinal);
            basen = bl.Types.Where(b => model.GetTypeInfo(b.Type).Type is not INamedTypeSymbol bt || !rollen.Contains(bt.Fq()))
                .Select(b => b.ToString()).ToList();
            if (basen.Count == 0) basen = null;
        }
        var attribute = decl is { AttributeLists.Count: > 0 } ? string.Join("\n", decl.AttributeLists.Select(a => a.ToString())) : null;

        return new TypMeta(t.ContainingNamespace.Fq(), t.ContainingAssembly?.Name ?? "",
            decl == null ? null : Summary(decl), zusatz, decl == null ? new() : DateiUsings(decl).ToList(),
            IstDomänenAssembly(t.ContainingAssembly), decl?.SyntaxTree.FilePath, typart, basen, attribute,
            decl is TypeDeclarationSyntax { ParameterList: null });
    }

    private IEnumerable<string> DateiUsings(SyntaxNode decl)
    {
        var usings = decl.SyntaxTree.GetRoot().DescendantNodes(n => n is CompilationUnitSyntax or BaseNamespaceDeclarationSyntax)
            .OfType<UsingDirectiveSyntax>()
            .Where(u => u.Alias == null && u.StaticKeyword.IsKind(SyntaxKind.None) && u.GlobalKeyword.IsKind(SyntaxKind.None))
            .Select(u => u.Name?.ToString() ?? "")
            .Where(n => n.Length > 0 && !_impliziteUsings.Contains(n));
        return usings.Distinct(StringComparer.Ordinal);
    }

    /// <summary>Der Inhalt des <c>&lt;summary&gt;</c>-Doku-Kommentars als Text (Zeilen ohne <c>///</c>), sonst null.</summary>
    private static string? Summary(SyntaxNode decl)
    {
        var doc = decl.GetLeadingTrivia().Select(tr => tr.GetStructure()).OfType<DocumentationCommentTriviaSyntax>().LastOrDefault();
        var summary = doc?.Content.OfType<XmlElementSyntax>().FirstOrDefault(x => x.StartTag.Name.ToString() == "summary");
        if (summary == null) return null;
        var zeilen = string.Concat(summary.Content.Select(c => c.ToFullString()))
            .Replace("\r\n", "\n").Split('\n')
            .Select(z => z.TrimStart())
            .Select(z => z.StartsWith("///", StringComparison.Ordinal) ? z[3..] : z)
            .Select(z => z.StartsWith(' ') ? z[1..] : z)
            .Select(z => z.TrimEnd())
            .ToList();
        while (zeilen.Count > 0 && zeilen[0].Length == 0) zeilen.RemoveAt(0);
        while (zeilen.Count > 0 && zeilen[^1].Length == 0) zeilen.RemoveAt(zeilen.Count - 1);
        return zeilen.Count == 0 ? null : string.Join("\n", zeilen);
    }

    /// <summary>Member-Deklarationen als dedenteter Quelltext (inkl. Kommentare) — der Handcode-„Zusatz".</summary>
    private static string? MemberText(IEnumerable<MemberDeclarationSyntax> members)
    {
        var list = members.ToList();
        if (list.Count == 0) return null;
        var text = string.Concat(list.Select(m => m.ToFullString()));
        var d = Dedent(text.Replace("\r\n", "\n")).Trim('\n', '\r', ' ', '\t');
        return d.Length == 0 ? null : d;
    }

    /// <summary>
    /// Den Methoden-Rumpf als dedenteten Text heben: ALLES zwischen den Klammern (inkl. Kommentare wie die
    /// <c>// 🤖 Prompt:</c>-Zeile). <c>""</c> = bewusst leerer Rumpf; null = kein Quelltext.
    /// </summary>
    private static string? MethodBody(IMethodSymbol m)
    {
        if (m.DeclaringSyntaxReferences.FirstOrDefault()?.GetSyntax() is not MethodDeclarationSyntax s) return null;
        if (s.Body is { } b)
        {
            var text = b.SyntaxTree.GetText().ToString(
                Microsoft.CodeAnalysis.Text.TextSpan.FromBounds(b.OpenBraceToken.Span.End, b.CloseBraceToken.SpanStart));
            return Dedent(text.Replace("\r\n", "\n")).Trim('\n', '\r', ' ', '\t');
        }
        if (s.ExpressionBody is { } e) return "return " + e.Expression + ";";
        return null;
    }

    /// <summary>Gemeinsame führende Einrückung entfernen — der Body war je nach Datei tief eingerückt.</summary>
    private static string Dedent(string text)
    {
        var lines = text.Replace("\r\n", "\n").Split('\n');
        var min = int.MaxValue;
        foreach (var l in lines)
        {
            if (l.Trim().Length == 0) continue;
            var lead = l.Length - l.TrimStart().Length;
            if (lead < min) min = lead;
        }
        if (min is int.MaxValue or 0) return string.Join("\n", lines.Select(l => l.TrimEnd()));
        return string.Join("\n", lines.Select(l => (l.Length >= min ? l[min..] : l.TrimStart()).TrimEnd()));
    }

    /// <summary>
    /// Das „Warum" je ge-yieldetem Event: die Bedingungen ALLER umschließenden <c>if</c> bis zum Methodenrumpf, je nach
    /// Zweig — im <c>then</c>-Zweig die Bedingung, im <c>else</c>-Zweig ihre Negation <c>!(…)</c>, äußerste zuerst,
    /// mit <c>&amp;&amp;</c> verbunden. Der Event-Typ kommt aus dem Symbol des ge-yieldeten Ausdrucks (nicht aus dem Text).
    /// Ein Yield ohne umschließendes <c>if</c> hat keinen Guard.
    /// </summary>
    private IEnumerable<(string EvtSimple, string Guard)> ExtractGuards(IMethodSymbol method)
    {
        if (method.DeclaringSyntaxReferences.FirstOrDefault()?.GetSyntax() is not MethodDeclarationSyntax syntax)
            yield break;
        var body = (SyntaxNode?)syntax.Body ?? syntax.ExpressionBody;
        if (body == null) yield break;
        var model = Model(syntax.SyntaxTree);

        foreach (var y in body.DescendantNodes().OfType<YieldStatementSyntax>())
        {
            if (y.Expression == null || model.GetTypeInfo(y.Expression).Type is not INamedTypeSymbol et) continue;
            var bedingungen = new List<string>();
            foreach (var ifs in y.Ancestors().TakeWhile(a => a != body).OfType<IfStatementSyntax>())
            {
                var c = ifs.Condition.ToString().Replace("this.", "");
                if (ifs.Statement.Span.Contains(y.Span)) bedingungen.Add(c);
                else if (ifs.Else != null && ifs.Else.Span.Contains(y.Span)) bedingungen.Add($"!({c})");
            }
            if (bedingungen.Count == 0) continue; // ungeschützter Zweig → kein Guard
            bedingungen.Reverse();
            yield return (et.Name, string.Join(" && ", bedingungen));
        }
    }

    // ── Prozesse / Sagas: der DSL-Walk ───────────────────────────────────────

    private ProcessRaw ReadProcess(INamedTypeSymbol t)
    {
        var proc = new ProcessRaw { Name = t.Name, Namespace = t.ContainingNamespace.Fq() };
        var classDecl = QuellDeklarationen(t).FirstOrDefault();
        if (classDecl != null)
        {
            proc.Datei = classDecl.SyntaxTree.FilePath;
            proc.Doku = Summary(classDecl);
            proc.Usings = DateiUsings(classDecl).ToList();
        }

        // Die Regeln-Property über die Interface-IMPLEMENTIERUNG (auch explizit implementiert) — nicht über ihren Namen am Typ.
        var regelnIface = _iProzessDef?.GetMembers(Vertrag.Regeln).OfType<IPropertySymbol>().FirstOrDefault();
        var regelnImpl = regelnIface == null ? null : Sym.Implementierung(t, regelnIface) as IPropertySymbol;
        var propSyntax = regelnImpl?.DeclaringSyntaxReferences.Select(r => r.GetSyntax()).OfType<PropertyDeclarationSyntax>().FirstOrDefault();
        if (propSyntax == null) return proc;
        var model = Model(propSyntax.SyntaxTree);

        // Der Aufruf Prozess<TAuslöser>.Definiere(…) — per Methoden-Symbol erkannt; der Auslöser ist sein Typ-Argument.
        foreach (var inv in propSyntax.DescendantNodes().OfType<InvocationExpressionSyntax>())
        {
            if (model.GetSymbolInfo(inv).Symbol is not IMethodSymbol ms || ms.Name != Vertrag.Definiere
                || ms.ContainingType?.OriginalDefinition.Fq() != _prozessTyp?.Fq()) continue;
            proc.TriggerFull = ms.ContainingType.TypeArguments[0].Fq();
            if (inv.ArgumentList.Arguments.FirstOrDefault()?.Expression is not LambdaExpressionSyntax lambda) break;

            // Jede Regel = eine Verb-Kette, deren letztes Glied von keinem weiteren Verb fortgesetzt wird.
            var verbAufrufe = lambda.Body.DescendantNodesAndSelf(n => n is not LambdaExpressionSyntax || n == lambda.Body)
                .OfType<InvocationExpressionSyntax>()
                .Where(x => Vertrag.IstRegelVerb(model.GetSymbolInfo(x).Symbol as IMethodSymbol))
                .ToList();
            var fortgesetzt = verbAufrufe.Select(x => Empfänger(x, model)).OfType<InvocationExpressionSyntax>().ToHashSet();
            foreach (var ende in verbAufrufe.Where(x => !fortgesetzt.Contains(x)).OrderBy(x => x.SpanStart))
            {
                var rule = ReadRule(ende, model);
                if (rule != null) proc.Rules.Add(rule);
            }
            break;
        }
        return proc;
    }

    /// <summary>
    /// Das Glied VOR einem Verb-Aufruf: der Empfänger <c>x.Verb&lt;T&gt;()</c>; ist der Empfänger eine lokale Variable,
    /// deren Initialisierer (so bleibt eine über Anweisungen verteilte Kette eine Kette).
    /// </summary>
    private static ExpressionSyntax? Empfänger(InvocationExpressionSyntax inv, SemanticModel model)
    {
        if (inv.Expression is not MemberAccessExpressionSyntax ma) return null;
        var e = ma.Expression;
        if (model.GetSymbolInfo(e).Symbol is ILocalSymbol lokal
            && lokal.DeclaringSyntaxReferences.FirstOrDefault()?.GetSyntax() is VariableDeclaratorSyntax { Initializer.Value: { } init })
            return init;
        return e;
    }

    private RuleRaw? ReadRule(InvocationExpressionSyntax ende, SemanticModel model)
    {
        // Die Kette vom Ende rückwärts einsammeln, dann in Aufruf-Reihenfolge lesen.
        var kette = new List<(IMethodSymbol M, InvocationExpressionSyntax Inv)>();
        for (ExpressionSyntax? x = ende; x is InvocationExpressionSyntax inv; x = Empfänger(inv, model))
            if (model.GetSymbolInfo(inv).Symbol is IMethodSymbol ms && Vertrag.IstRegelVerb(ms)) kette.Add((ms, inv));
            else break;
        kette.Reverse();

        var rule = new RuleRaw();
        foreach (var (ms, inv) in kette)
        {
            if (ms.TypeArguments.FirstOrDefault() is not INamedTypeSymbol argTyp) continue;
            var arg = argTyp.Fq();
            var lambdaArg = inv.ArgumentList.Arguments.FirstOrDefault()?.Expression;
            var lambdaText = lambdaArg == null ? null : Dedent(lambdaArg.ToString().Replace("\r\n", "\n"));
            var verb = ms.Name;
            if (verb == Vertrag.Auf || verb == Vertrag.Und) rule.WhenFull.Add(arg);
            else if (verb == Vertrag.UndAlle) { rule.Join = "count"; rule.SammelFull = arg; rule.SammelLambda = lambdaText; }
            else if (verb == Vertrag.Sende) { rule.SendsFull = arg; rule.SendLambda = lambdaText; }
            else if (verb == Vertrag.SendeJe) { rule.SendsFull = arg; rule.FanOut = true; rule.SendLambda = lambdaText; }
            else if (verb == Vertrag.RückgängigDurch || verb == Vertrag.RückgängigDurchJe)
            {
                rule.CompensatesFull = arg;
                rule.CompFanOut = verb == Vertrag.RückgängigDurchJe;
                rule.CompLambda = lambdaText;
            }
        }
        if (rule.Join != "count")
            rule.Join = rule.WhenFull.Count > 1 ? "and" : "single";
        return string.IsNullOrEmpty(rule.SendsFull) ? null : rule;
    }

    /// <summary>
    /// Registrierte Prozesse STRUKTURELL aus dem Generat: Einträge <c>["Name"] = new P().Regeln</c> mit P als
    /// <c>IProzessDefinition</c> — ohne den Namen der generierten Registry-Klasse zu kennen.
    /// </summary>
    private void ExtractRegisteredProcesses(DomainModel m)
    {
        if (_iProzessDef == null) return;
        foreach (var c in _comps)
            foreach (var tree in c.SyntaxTrees.Where(IstGeneriert))
            {
                SemanticModel? model = null;
                foreach (var asg in tree.GetRoot().DescendantNodes().OfType<AssignmentExpressionSyntax>())
                {
                    if (asg.Left is not ImplicitElementAccessSyntax access ||
                        access.ArgumentList.Arguments.FirstOrDefault()?.Expression is not LiteralExpressionSyntax { Token.Value: string name }) continue;
                    var oc = asg.Right.DescendantNodesAndSelf().OfType<ObjectCreationExpressionSyntax>().FirstOrDefault();
                    if (oc == null) continue;
                    model ??= c.GetSemanticModel(tree);
                    if (model.GetTypeInfo(oc).Type is INamedTypeSymbol pt && Sym.Implements(pt, _iProzessDef))
                        m.RegisteredProcesses.Add(name);
                }
            }
    }

    // ── Projektionen / Reaktionen / Queries / Reader / Pipelines ─────────────

    private ProjectionRaw? TryReadProjection(INamedTypeSymbol t)
    {
        // Handler = öffentliche Methode: Event als erster Parameter + Aggregat-Umschlag (Name egal, die Typen tragen die Rolle).
        var methods = OeffentlicheMethoden(t)
            .Where(mm => mm.Parameters.Length >= 1 && Sym.Implements(mm.Parameters[0].Type, _iEvent)
                         && mm.Parameters.Skip(1).Any(p => p.Type.ToDisplayString() == _iAggEnvelope?.ToDisplayString())).ToList();
        if (methods.Count == 0) return null;
        var handles = methods.Select(mm => mm.Parameters[0].Type.Fq()).Distinct().ToList();

        var subId = KonstanterWert(t, _iSubscriber, Vertrag.SubscriberId);

        var raw = new ProjectionRaw
        {
            Name = t.Name, Full = t.Fq(), Namespace = t.ContainingNamespace.Fq(), SubscriberId = subId, ConsumesFull = handles,
            Pull = Sym.Implements(t, _iPull), Append = _iAppend != null && Sym.Implements(t, _iAppend),
        };
        foreach (var mm in methods)
        {
            var evt = mm.Parameters[0].Type.Name;
            var body = MethodBody(mm);
            if (body != null) raw.HandleBodies[evt] = body;
            var calls = ExtractStoreCalls(mm).Distinct().ToList();
            if (calls.Count > 0) raw.HandleStoreCalls[evt] = calls;

            // Rückgabe IAsyncEnumerable<T | OneOf<…>>: ge-yieldete Commands (→ Reaktion) bzw. Events (reaktiv).
            var ausgaben = YieldTypen(mm.ReturnType).ToList();
            var sends = ausgaben.Where(x => Sym.Implements(x, _iCommand)).Select(x => x.Name).ToList();
            var pubs = ausgaben.Where(x => Sym.Implements(x, _iEvent)).Select(x => x.Name).ToList();
            if (sends.Count > 0) { raw.HandleSends[evt] = sends; raw.IstReaktion = true; }
            if (pubs.Count > 0) raw.HandlePublishes[evt] = pubs;
        }
        return raw;
    }

    /// <summary>Die Element-Typen einer <c>IAsyncEnumerable&lt;T&gt;</c>/<c>IEnumerable&lt;T&gt;</c>-Rückgabe (OneOf aufgefächert).</summary>
    private static IEnumerable<INamedTypeSymbol> YieldTypen(ITypeSymbol rt)
    {
        if (!(Vertrag.Ist(rt, typeof(IAsyncEnumerable<>)) || Vertrag.Ist(rt, typeof(IEnumerable<>)))) yield break;
        if (((INamedTypeSymbol)rt).TypeArguments[0] is not INamedTypeSymbol el) yield break;
        if (Vertrag.IstOneOf(el))
        {
            foreach (var a in el.TypeArguments.OfType<INamedTypeSymbol>()) yield return a;
        }
        else yield return el;
    }

    private QueryRaw ReadQuery(INamedTypeSymbol t) =>
        new() { Name = t.Name, Full = t.Fq(), Fields = Felder(t), Meta = Meta(t) };

    private ReaderRaw? TryReadReader(INamedTypeSymbol t)
    {
        var readerIface = t.AllInterfaces.FirstOrDefault(i => _iReader != null && i.OriginalDefinition.Fq() == _iReader.Fq());
        if (readerIface == null || readerIface.TypeArguments.Length != 1) return null;

        var methods = OeffentlicheMethoden(t)
            .Where(mm => mm.Parameters.Length >= 1 && Sym.Implements(mm.Parameters[0].Type, _iQuery)).ToList();
        var queries = methods.Select(mm => mm.Parameters[0].Type.Name).Distinct().ToList();

        // [ProjectionReader(TrackDeps = false)] — Default true.
        var attr = t.GetAttributes().FirstOrDefault(a => a.AttributeClass?.ToDisplayString() == Vertrag.ProjectionReaderAttribute);
        var trackDeps = attr?.NamedArguments.FirstOrDefault(a => a.Key == Vertrag.TrackDeps).Value.Value as bool? ?? true;

        var raw = new ReaderRaw
        {
            Name = t.Name, Full = t.Fq(), Namespace = t.ContainingNamespace.Fq(),
            ProjectionName = readerIface.TypeArguments[0].Name, QueryNames = queries, TrackDeps = trackDeps,
        };
        foreach (var mm in methods)
        {
            var q = mm.Parameters[0].Type.Name;
            var body = MethodBody(mm);
            if (body != null) raw.HandleBodies[q] = body;
            var calls = ExtractStoreCalls(mm).Distinct().ToList();
            if (calls.Count > 0) raw.HandleStoreCalls[q] = calls;
            // Task<R> / Task<OneOf<R1,R2>> → Responses.
            var inner = IstTask(mm.ReturnType) ? ((INamedTypeSymbol)mm.ReturnType).TypeArguments[0] as INamedTypeSymbol : null;
            if (inner != null)
                raw.HandleResponses[q] = (Vertrag.IstOneOf(inner) ? inner.TypeArguments.OfType<INamedTypeSymbol>() : new[] { inner })
                    .Select(x => x.Name).ToList();
        }
        return raw;
    }

    private PipelineRaw ReadPipeline(INamedTypeSymbol t)
    {
        var pipe = new PipelineRaw { Name = t.Name, Full = t.Fq(), Namespace = t.ContainingNamespace.Fq() };
        pipe.PipelineId = KonstanterWert(t, _iPipelineHandler, Vertrag.PipelineId);

        // Handler = öffentliche Methode mit PipelineContext-Parameter (Eingang = erster Parameter).
        foreach (var method in OeffentlicheMethoden(t).Where(mm => mm.Parameters.Skip(1).Any(p => p.Type.ToDisplayString() == _pipelineContext?.ToDisplayString())))
        {
            if (method.Parameters.Length < 1 || method.Parameters[0].Type is not INamedTypeSymbol input) continue;
            var kind = Sym.Implements(input, _iPipelineTrigger) ? "trigger"
                : _iSelfMessage != null && Sym.Implements(input, _iSelfMessage) ? "self"
                : "event";
            var emits = EmittedCommands(method).ToList();
            pipe.Handles.Add((input.Fq(), kind, emits));
            var body = MethodBody(method);
            if (body != null) pipe.HandleBodies[input.Fq()] = body;
            var trigs = EmittedTriggers(method).ToList();
            if (trigs.Count > 0) pipe.HandleEmitsTriggers[input.Fq()] = trigs;
            var scheds = ScheduleSelfAufrufe(method).ToList();
            if (scheds.Count > 0) pipe.HandleSchedules[input.Fq()] = scheds;
        }
        return pipe;
    }

    /// <summary><c>ctx.ScheduleSelf(new X(…), delay)</c> im Methodenrumpf → (X, delay-Ausdruck).</summary>
    private IEnumerable<(string Name, string Delay)> ScheduleSelfAufrufe(IMethodSymbol method)
    {
        var node = method.DeclaringSyntaxReferences.FirstOrDefault()?.GetSyntax();
        if (node == null) yield break;
        var model = Model(node.SyntaxTree);
        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (var inv in node.DescendantNodes().OfType<InvocationExpressionSyntax>())
        {
            if (inv.Expression is not MemberAccessExpressionSyntax sma || sma.Name.Identifier.Text != Vertrag.ScheduleSelf
                || model.GetSymbolInfo(inv).Symbol?.ContainingType?.ToDisplayString() != _pipelineContext?.ToDisplayString()) continue;
            var args = inv.ArgumentList.Arguments;
            if (args.Count == 0) continue;
            var typ = model.GetTypeInfo(args[0].Expression).Type;
            var name = typ?.Name ?? args[0].Expression.ToString();
            var delay = args.Count > 1 ? args[1].Expression.ToString() : "";
            if (seen.Add(name)) yield return (name, delay);
        }
    }

    /// <summary>
    /// Die Typen der TATSÄCHLICH ausgegebenen Werte eines Handlers: <c>yield return x</c> (bzw. <c>return x</c> in
    /// Task-Handlern) — über den Typ des Ausdrucks, OneOf aufgefächert. Ein nur konstruiertes, nie ausgegebenes Objekt zählt nicht.
    /// </summary>
    private IEnumerable<INamedTypeSymbol> AusgegebeneTypen(IMethodSymbol method)
    {
        var node = method.DeclaringSyntaxReferences.FirstOrDefault()?.GetSyntax();
        if (node == null) yield break;
        var model = Model(node.SyntaxTree);
        foreach (var n in node.DescendantNodes(x => x is not (LambdaExpressionSyntax or LocalFunctionStatementSyntax)))
        {
            var expr = n switch
            {
                YieldStatementSyntax { Expression: { } y } => y,
                ReturnStatementSyntax { Expression: { } r } => r,
                _ => null,
            };
            if (expr == null || model.GetTypeInfo(expr).Type is not INamedTypeSymbol typ) continue;
            if (Vertrag.IstOneOf(typ)) { foreach (var a in typ.TypeArguments.OfType<INamedTypeSymbol>()) yield return a; }
            else yield return typ;
        }
    }

    private IEnumerable<string> EmittedCommands(IMethodSymbol method) =>
        AusgegebeneTypen(method).Where(t => Sym.Implements(t, _iCommand)).Select(t => t.Fq()).Distinct(StringComparer.Ordinal);

    /// <summary>Ausgegebene <c>IPipelineTrigger</c>-Nachrichten (Simple-Namen) — die Pipeline→Pipeline-Kette.</summary>
    private IEnumerable<string> EmittedTriggers(IMethodSymbol method) =>
        AusgegebeneTypen(method).Where(t => Sym.Implements(t, _iPipelineTrigger)).Select(t => t.Name).Distinct(StringComparer.Ordinal);

    /// <summary>
    /// Der Compile-Zeit-Wert eines Vertrags-Members (z. B. <c>SubscriberId</c>) am Typ: über die Interface-Implementierung
    /// (auch explizit), ausgewertet als Konstante (<c>"x"</c>, <c>const</c>, <c>nameof</c>, Verkettung). Nicht konstant ⇒ null.
    /// </summary>
    private string? KonstanterWert(INamedTypeSymbol t, INamedTypeSymbol? iface, string member)
    {
        var vertrag = iface?.GetMembers(member).OfType<IPropertySymbol>().FirstOrDefault();
        if (vertrag == null || Sym.Implementierung(t, vertrag) is not IPropertySymbol impl) return null;
        foreach (var syn in impl.DeclaringSyntaxReferences.Select(r => r.GetSyntax()).OfType<PropertyDeclarationSyntax>())
        {
            var expr = syn.ExpressionBody?.Expression ?? syn.Initializer?.Value
                ?? syn.AccessorList?.Accessors.FirstOrDefault(a => a.IsKind(SyntaxKind.GetAccessorDeclaration)) switch
                {
                    { ExpressionBody: { } eb } => eb.Expression,
                    { Body.Statements: [ReturnStatementSyntax { Expression: { } re }] } => re,
                    _ => null,
                };
            if (expr != null && Model(syn.SyntaxTree).GetConstantValue(expr) is { HasValue: true, Value: string wert }) return wert;
        }
        return null;
    }

    /// <summary>
    /// Aufgerufene Store-Funktionen im Handle-Rumpf → (Store, Methode, Lese-Seite?). Über das AUFGELÖSTE Methoden-Symbol:
    /// entweder eine Methode eines Store-Interfaces selbst, oder (Aufruf über die konkrete Klasse) die Implementierung
    /// eines Store-Interface-Members. Hebt die Topologie-Kante Projektion/Reader → Store.
    /// </summary>
    private IEnumerable<(string Store, string Method, bool IsRead)> ExtractStoreCalls(IMethodSymbol method)
    {
        var syntaxRef = method.DeclaringSyntaxReferences.FirstOrDefault();
        if (syntaxRef == null) yield break;
        var node = syntaxRef.GetSyntax();
        var model = Model(node.SyntaxTree);
        foreach (var inv in node.DescendantNodes().OfType<InvocationExpressionSyntax>())
        {
            if (model.GetSymbolInfo(inv).Symbol is not IMethodSymbol ziel) continue;
            ziel = ziel.ReducedFrom ?? ziel;
            var ct = ziel.ContainingType;
            if (ct == null) continue;
            if (_storeIfaces.TryGetValue(ct.OriginalDefinition.Fq(), out var direkt))
            {
                yield return (direkt.Store, ziel.Name, direkt.IsRead);
                continue;
            }
            foreach (var i in ct.AllInterfaces)
            {
                if (!_storeIfaces.TryGetValue(i.OriginalDefinition.Fq(), out var ueber)) continue;
                if (i.GetMembers().OfType<IMethodSymbol>().Any(im => SymbolEqualityComparer.Default.Equals(ct.FindImplementationForInterfaceMember(im), ziel)))
                    yield return (ueber.Store, ziel.Name, ueber.IsRead);
            }
        }
    }

    // ── Helfer ───────────────────────────────────────────────────────────────

    /// <summary>Die HANDGESCHRIEBENEN öffentlichen Instanz-Methoden (generierte Dispatch-Member sind keine Handler).</summary>
    private static IEnumerable<IMethodSymbol> OeffentlicheMethoden(INamedTypeSymbol t) =>
        t.GetMembers().OfType<IMethodSymbol>().Where(m => m.MethodKind == MethodKind.Ordinary && m.DeclaredAccessibility == Accessibility.Public && !m.IsStatic
                                                          && m.DeclaringSyntaxReferences.Any(r => !IstGeneriert(r.SyntaxTree)));

    /// <summary>
    /// Die Felder eines Typs, wie er sie DEKLARIERT: Positions-Parameter eines Records (Typ + Default verbatim) und
    /// Auto-Properties der Form <c>public [required] T X { get; [set|init;] } [= v;]</c> (Accessor-Satz festgehalten).
    /// Parameter eines KLASSEN-Konstruktors sind keine Felder (nur Eingaben). Ohne Quelltext (Metadaten): die
    /// öffentlichen Instanz-Properties des Symbols.
    /// </summary>
    private List<FieldInfo> Felder(INamedTypeSymbol t)
    {
        var decls = QuellDeklarationen(t).OfType<TypeDeclarationSyntax>().ToList();
        if (decls.Count == 0)
            return t.GetMembers().OfType<IPropertySymbol>()
                .Where(p => !p.IsStatic && !p.IsIndexer && p.DeclaredAccessibility == Accessibility.Public && !p.IsImplicitlyDeclared)
                .Select(p => new FieldInfo { Name = p.Name, Type = ShortType(p.Type), ElementTyp = ElementTyp(p.Type) }).ToList();
        var felder = new List<FieldInfo>();
        foreach (var decl in decls)
        {
            var model = Model(decl.SyntaxTree);
            if (t.IsRecord && decl.ParameterList is { } pl)
                foreach (var p in pl.Parameters)
                    felder.Add(new FieldInfo
                    {
                        Name = p.Identifier.Text, Type = p.Type?.ToString() ?? "", Default = p.Default?.Value.ToString(),
                        ElementTyp = model.GetDeclaredSymbol(p) is IParameterSymbol ps ? ElementTyp(ps.Type) : null,
                    });
            foreach (var prop in decl.Members.OfType<PropertyDeclarationSyntax>())
                if (EigenschaftsFeld(prop) is { } f)
                {
                    if (model.GetDeclaredSymbol(prop) is IPropertySymbol psym) f.ElementTyp = ElementTyp(psym.Type);
                    felder.Add(f);
                }
        }
        return felder;
    }

    /// <summary>Der Element-Typ einer Sammlung: Array-Element bzw. das T von <c>IEnumerable&lt;T&gt;</c> (auch der Typ selbst). <c>string</c> ist keine Sammlung.</summary>
    private static string? ElementTyp(ITypeSymbol t)
    {
        if (t.SpecialType == SpecialType.System_String) return null;
        if (t is IArrayTypeSymbol a) return ShortType(a.ElementType);
        if (t is not INamedTypeSymbol n) return null;
        var ie = new[] { n }.Concat(n.AllInterfaces)
            .FirstOrDefault(i => i.OriginalDefinition.SpecialType == SpecialType.System_Collections_Generic_IEnumerable_T);
        return ie == null ? null : ShortType(ie.TypeArguments[0]);
    }

    /// <summary>
    /// Eine Property ist ein FELD, wenn sie verlustfrei als solches zurückschreibbar ist: <c>public</c> (optional
    /// <c>required</c>), ohne Attribute, Auto-Accessoren <c>get;</c> / <c>get; set;</c> / <c>get; init;</c>, optional mit
    /// Initialisierer. Alles andere (berechnet, Rümpfe, Attribute) bleibt Handcode.
    /// </summary>
    private static FieldInfo? EigenschaftsFeld(PropertyDeclarationSyntax p)
    {
        var mods = p.Modifiers.Select(x => x.Kind()).ToList();
        if (!mods.Contains(SyntaxKind.PublicKeyword) || mods.Any(k => k is not (SyntaxKind.PublicKeyword or SyntaxKind.RequiredKeyword))) return null;
        if (p.AttributeLists.Count > 0 || p.ExplicitInterfaceSpecifier != null || p.ExpressionBody != null) return null;
        if (p.AccessorList is not { } al || al.Accessors.Any(a => a.Body != null || a.ExpressionBody != null || a.Modifiers.Count > 0 || a.AttributeLists.Count > 0))
            return null;
        var kinds = al.Accessors.Select(a => a.Kind()).ToList();
        string? zugriff = kinds switch
        {
            [SyntaxKind.GetAccessorDeclaration] => "{ get; }",
            [SyntaxKind.GetAccessorDeclaration, SyntaxKind.SetAccessorDeclaration] => "{ get; set; }",
            [SyntaxKind.GetAccessorDeclaration, SyntaxKind.InitAccessorDeclaration] => "{ get; init; }",
            _ => null,
        };
        if (zugriff == null) return null;
        return new FieldInfo
        {
            Name = p.Identifier.Text, Type = p.Type.ToString(), Default = p.Initializer?.Value.ToString(),
            Zugriff = zugriff, Pflicht = mods.Contains(SyntaxKind.RequiredKeyword),
        };
    }

    private static string ShortType(ITypeSymbol t) =>
        t.ToDisplayString(SymbolDisplayFormat.MinimallyQualifiedFormat
            .AddMiscellaneousOptions(SymbolDisplayMiscellaneousOptions.IncludeNullableReferenceTypeModifier));

    /// <summary>Alle Event-Typen aus einem Decide-Rückgabetyp <c>IEnumerable&lt;OneOf&lt;…&gt;&gt;</c> / <c>IEnumerable&lt;E&gt;</c> in Signatur-Reihenfolge.</summary>
    private IEnumerable<INamedTypeSymbol> UniverseEvents(ITypeSymbol returnType)
    {
        if (returnType is not INamedTypeSymbol enumerable || enumerable.TypeArguments.Length != 1) yield break;
        if (enumerable.TypeArguments[0] is not INamedTypeSymbol element) yield break;

        var candidates = Vertrag.IstOneOf(element) ? element.TypeArguments.ToList() : new List<ITypeSymbol> { element };
        foreach (var ta in candidates)
            if (ta is INamedTypeSymbol et && Sym.Implements(et, _iEvent))
                yield return et;
    }
}
