using Microsoft.CodeAnalysis;

namespace GraphExtractor;

/// <summary>
/// Die Lage der Projekte — vollständig aus der Solution ABGELEITET, ohne einen Projekt-, Pfad- oder Präfix-Namen:
/// <list type="bullet">
/// <item><b>Vertrag</b>: das Projekt, das die Marker-Interfaces baut (Assembly von <see cref="Vertrag.IState"/>).</item>
/// <item><b>Laufzeit</b>: das Projekt, in dem der Framework-Generator die Command→Aggregat-Routing-Tabelle erzeugt hat
///   (strukturell erkannt, <see cref="RoutingTruth"/>) — dort werden die Domänen zusammengeführt.</item>
/// <item><b>Analyse</b>: die Laufzeit + ihre transitive Referenz-Hülle (alles, was das Produkt fährt).</item>
/// <item><b>Domäne</b>: Analyse-Projekte (außer Vertrag und Laufzeit), die im HANDGESCHRIEBENEN Quelltext mindestens einen
///   Typ mit einer Domänen-Rolle des Vertrags deklarieren (Command, Event, State, Decider, Query, ReadModel, Store,
///   Konsument, Pipeline, Prozess, Wertobjekt …). Framework-Projekte (Kern) deklarieren keine solche Rolle.</item>
/// <item><b>Hosts</b>: Programme mit HANDGESCHRIEBENEM Einstiegspunkt, die die Laufzeit einbinden — dort lebt die
///   Composition Root. Testprojekte haben einen generierten Einstiegspunkt und fallen so heraus.</item>
/// </list>
/// </summary>
public sealed class Projektlage
{
    public required Project Vertragsprojekt { get; init; }
    public required Project Laufzeit { get; init; }
    public required Compilation LaufzeitCompilation { get; init; }
    public required List<(Project Projekt, Compilation Compilation)> Analyse { get; init; }
    public required HashSet<string> DomänenAssemblies { get; init; }
    public required List<(Project Projekt, Compilation Compilation)> Hosts { get; init; }
    public required HashSet<string> AggregatAssemblies { get; init; }

    public List<Compilation> Compilations => Analyse.Select(a => a.Compilation).ToList();

    /// <summary>Wurzel-Namespace (DefaultNamespace, sonst Assembly-Name) je Domänen-Projekt → Projektverzeichnis.</summary>
    public Dictionary<string, string> ProjektWurzeln() => Analyse
        .Where(a => DomänenAssemblies.Contains(a.Compilation.AssemblyName ?? "") && a.Projekt.FilePath != null)
        .GroupBy(a => a.Projekt.DefaultNamespace ?? a.Projekt.AssemblyName)
        .ToDictionary(g => g.Key, g => Path.GetDirectoryName(g.First().Projekt.FilePath)!, StringComparer.Ordinal);
    public IEnumerable<Compilation> DomänenCompilations => Analyse.Where(a => DomänenAssemblies.Contains(a.Compilation.AssemblyName ?? "")).Select(a => a.Compilation);

    public static async Task<Projektlage> ErmittleAsync(Solution solution, Action<string> log)
    {
        RegistriereDokumente(solution);
        var graph = solution.GetProjectDependencyGraph();
        var vertrag = solution.Projects.FirstOrDefault(p => p.AssemblyName == Vertrag.VertragsAssembly)
            ?? throw new InvalidOperationException($"Kein Projekt baut die Vertrags-Assembly '{Vertrag.VertragsAssembly}'.");

        // Alle Nutzer des Vertrags, von „unten" nach „oben" (wenige → viele Abhängigkeiten).
        var nutzer = graph.GetProjectsThatTransitivelyDependOnThisProject(vertrag.Id)
            .Select(id => solution.GetProject(id)!)
            .OrderBy(p => graph.GetProjectsThatThisProjectTransitivelyDependsOn(p.Id).Count)
            .ThenBy(p => p.Name, StringComparer.Ordinal)
            .ToList();

        // Laufzeit = das niedrigste Projekt mit generierter Routing-Tabelle.
        Project? laufzeit = null; Compilation? laufzeitComp = null;
        foreach (var p in nutzer)
        {
            var c = await p.GetCompilationAsync();
            if (c != null && RoutingTruth.HatRouting(c)) { laufzeit = p; laufzeitComp = c; break; }
        }
        if (laufzeit == null || laufzeitComp == null)
            throw new InvalidOperationException("Keine Laufzeit gefunden (kein Projekt mit generierter Command→Aggregat-Routing-Tabelle).");

        var hülle = graph.GetProjectsThatThisProjectTransitivelyDependsOn(laufzeit.Id).Append(laufzeit.Id)
            .Select(id => solution.GetProject(id)!)
            .Where(p => p.Id == vertrag.Id || graph.GetProjectsThatThisProjectTransitivelyDependsOn(p.Id).Contains(vertrag.Id))
            .ToList();
        var analyse = new List<(Project, Compilation)>();
        foreach (var p in hülle)
        {
            var c = p.Id == laufzeit.Id ? laufzeitComp : await p.GetCompilationAsync();
            if (c != null) analyse.Add((p, c));
        }

        // Aggregat-Projekte: deklarieren im Quelltext einen Typ, der IDecider<T> implementiert.
        var aggregatProjekte = analyse.Where(a => Deklariert(a.Item2, new[] { Vertrag.IDecider })).Select(a => a.Item1).ToList();
        var domäne = analyse.Where(a => a.Item1.Id != laufzeit.Id && a.Item1.Id != vertrag.Id && Deklariert(a.Item2, DomänenRollen))
            .Select(a => a.Item2.AssemblyName ?? "").ToHashSet(StringComparer.Ordinal);

        // Hosts: Einstiegspunkt im handgeschriebenen Quelltext + Laufzeit in der Hülle.
        var hosts = new List<(Project, Compilation)>();
        foreach (var id in graph.GetProjectsThatTransitivelyDependOnThisProject(laufzeit.Id))
        {
            var p = solution.GetProject(id)!;
            if (p.CompilationOptions?.OutputKind is not (OutputKind.ConsoleApplication or OutputKind.WindowsApplication)) continue;
            var c = await p.GetCompilationAsync();
            var ep = c?.GetEntryPoint(default);
            var ort = ep?.Locations.FirstOrDefault(l => l.IsInSource);
            if (c == null || ort == null || IstGeneriert(ort.SourceTree!)) continue;
            hosts.Add((p, c));
        }

        log($"   Vertrag: {vertrag.Name} · Laufzeit: {laufzeit.Name} · Domäne: {string.Join(", ", domäne.OrderBy(x => x))} · Hosts: {string.Join(", ", hosts.Select(h => h.Item1.Name))}");
        return new Projektlage
        {
            Vertragsprojekt = vertrag, Laufzeit = laufzeit, LaufzeitCompilation = laufzeitComp, Analyse = analyse,
            DomänenAssemblies = domäne, Hosts = hosts,
            AggregatAssemblies = aggregatProjekte.Select(p => p.AssemblyName).ToHashSet(StringComparer.Ordinal),
        };
    }

    /// <summary>Die Rollen-Marker des Vertrags, deren handgeschriebene Implementierung ein Projekt zur Domäne macht.</summary>
    private static readonly string[] DomänenRollen =
    {
        Vertrag.IState, Vertrag.IDecider, Vertrag.IApplier, Vertrag.ICommand, Vertrag.IEvent, Vertrag.IQuery, Vertrag.IQueryResponse,
        Vertrag.IReadModel, Vertrag.IWriteStore, Vertrag.IReadStore, Vertrag.ISubscriber, Vertrag.IReader, Vertrag.IPipelineHandler,
        Vertrag.IPipelineTrigger, Vertrag.IProzessDefinition, Vertrag.IWertobjekt,
    };

    /// <summary>Deklariert die Compilation im handgeschriebenen Quelltext einen Typ, der einen der Marker implementiert (per Symbol)?</summary>
    private static bool Deklariert(Compilation c, IEnumerable<string> marker)
    {
        var ziele = marker.Select(c.GetTypeByMetadataName).Where(x => x != null).Select(x => x!.OriginalDefinition.Fq()).ToHashSet(StringComparer.Ordinal);
        if (ziele.Count == 0) return false;
        return AlleTypen(c.Assembly.GlobalNamespace).Any(t =>
            t.AllInterfaces.Any(i => ziele.Contains(i.OriginalDefinition.Fq()))
            && t.DeclaringSyntaxReferences.Any(r => !IstGeneriert(r.SyntaxTree)));
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

    /// <summary>Die Pfade aller Projekt-DOKUMENTE (handgeschrieben) — Source-Generator-Ausgaben sind nie Dokumente.</summary>
    private static readonly HashSet<string> Dokumente = new(StringComparer.Ordinal);

    /// <summary>Die Dokumente einer (auch geforkten) Solution als handgeschrieben registrieren.</summary>
    public static void RegistriereDokumente(Solution solution)
    {
        foreach (var d in solution.Projects.SelectMany(p => p.Documents))
            if (d.FilePath != null) Dokumente.Add(d.FilePath);
    }

    /// <summary>
    /// Generierter Quelltext — DIE eine Regel für alle Stellen des Extractors: kein Projekt-Dokument (also von einem
    /// Source-Generator beigesteuert) oder mit dem Generator-Kopf <c>&lt;auto-generated</c> (so markiert der
    /// Codegen-Prepass seine eingecheckte Ausgabe).
    /// </summary>
    public static bool IstGeneriert(SyntaxTree tree) =>
        (Dokumente.Count > 0 && !Dokumente.Contains(tree.FilePath))
        || tree.GetRoot().GetLeadingTrivia().ToString().Contains("<auto-generated", StringComparison.Ordinal);
}
