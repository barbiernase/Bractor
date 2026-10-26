using Microsoft.CodeAnalysis;

namespace GraphExtractor;

/// <summary>
/// Die Lage der Projekte — aus der Solution ABGELEITET, ohne einen getippten Projekt-, Pfad- oder Präfix-Namen. Die einzige Namensregel ist die
/// der Projektstruktur (docs/07, <c>Abstractions/Projektstruktur.cs</c>): die Wurzel der Anwendung kommt aus dem Namen der Komposition
/// (<c>&lt;Wurzel&gt;.Laufzeit</c>), ihre Rollen-Projekte sind Wurzel + Suffix.
/// <list type="bullet">
/// <item><b>Vertrag</b>: das Projekt, das die Marker-Interfaces baut (Assembly von <see cref="Vertrag.IState"/>).</item>
/// <item><b>Laufzeit</b>: das Projekt, in dem der Framework-Generator die Command→Aggregat-Routing-Tabelle erzeugt hat
///   (strukturell erkannt, <see cref="RoutingTruth"/>) — dort werden die Domänen zusammengeführt.</item>
/// <item><b>Analyse</b>: die Laufzeit + ihre transitive Referenz-Hülle (alles, was das Produkt fährt).</item>
/// <item><b>Domäne</b>: Analyse-Projekte (außer Vertrag und Laufzeit), die zur Wurzel gehören (Rollen-Projekt oder darauf aufbauend) und im
///   HANDGESCHRIEBENEN Quelltext mindestens einen Typ mit einer Domänen-Rolle des Vertrags deklarieren (Command, Event, State, Decider, Query,
///   ReadModel, Store, Projektion, Funktion, Wertobjekt …). Das Framework baut auf kein Rollen-Projekt auf.</item>
/// <item><b>Hosts</b>: Programme mit HANDGESCHRIEBENEM Einstiegspunkt, die die Laufzeit einbinden — dort lebt die
///   Composition Root. Testprojekte haben einen generierten Einstiegspunkt und fallen so heraus.</item>
/// </list>
/// </summary>
public sealed class Projektlage
{
    public required Project Vertragsprojekt { get; init; }
    public required Project Laufzeit { get; init; }
    /// <summary>
    /// Die Compilation der Laufzeit — nur beim kalten Lesen. Das warme Lesen der Werkbank (<see cref="NurDomänenAsync"/>, E7) übersetzt
    /// die Laufzeit (v1-Generatoren, schwer) nicht; dort gilt die Routing-Ableitung aus den Decide-Signaturen.
    /// </summary>
    public Compilation? LaufzeitCompilation { get; init; }
    /// <summary>Wurde die Laufzeit über ihre generierte Routing-Tabelle erkannt (sonst: abgeleitet als Zusammenführung der Aggregat-Projekte)?</summary>
    public bool LaufzeitAusRouting { get; init; }
    public required List<(Project Projekt, Compilation Compilation)> Analyse { get; init; }
    public required HashSet<string> DomänenAssemblies { get; init; }
    public required List<(Project Projekt, Compilation Compilation)> Hosts { get; init; }
    public required HashSet<string> AggregatAssemblies { get; init; }
    /// <summary>Welche Syntaxbäume dieses Solution-Stands handgeschrieben sind.</summary>
    public required Quelltexte Quelltexte { get; init; }

    public List<Compilation> Compilations => Analyse.Select(a => a.Compilation).ToList();

    /// <summary>Wurzel-Namespace (DefaultNamespace, sonst Assembly-Name) je Domänen-Projekt → Projektverzeichnis.</summary>
    public Dictionary<string, string> ProjektWurzeln() => Analyse
        .Where(a => DomänenAssemblies.Contains(a.Compilation.AssemblyName ?? "") && a.Projekt.FilePath != null)
        .GroupBy(a => a.Projekt.DefaultNamespace ?? a.Projekt.AssemblyName)
        .ToDictionary(g => g.Key, g => Path.GetDirectoryName(g.First().Projekt.FilePath)!, StringComparer.Ordinal);
    public IEnumerable<Compilation> DomänenCompilations => Analyse.Where(a => DomänenAssemblies.Contains(a.Compilation.AssemblyName ?? "")).Select(a => a.Compilation);
    /// <summary>Die Domänen-Projekte (Editor-Projekte): ihre Dateien beobachtet die Werkbank.</summary>
    public IEnumerable<Project> DomänenProjekte => Analyse.Where(a => DomänenAssemblies.Contains(a.Compilation.AssemblyName ?? "")).Select(a => a.Projekt);

    public static async Task<Projektlage> ErmittleAsync(Solution solution, Action<string> log)
    {
        var quelltexte = new Quelltexte(solution);
        var graph = solution.GetProjectDependencyGraph();
        var vertrag = solution.Projects.FirstOrDefault(p => p.AssemblyName == Vertrag.VertragsAssembly)
            ?? throw new InvalidOperationException($"Kein Projekt baut die Vertrags-Assembly '{Vertrag.VertragsAssembly}'.");

        // Alle Nutzer des Vertrags, von „unten" nach „oben" (wenige → viele Abhängigkeiten).
        var nutzer = graph.GetProjectsThatTransitivelyDependOnThisProject(vertrag.Id)
            .Select(id => solution.GetProject(id)!)
            .OrderBy(p => graph.GetProjectsThatThisProjectTransitivelyDependsOn(p.Id).Count)
            .ThenBy(p => p.Name, StringComparer.Ordinal)
            .ToList();

        // Laufzeit = die Komposition der Anwendung, die die Werkzeuge bearbeiten (Projektstruktur.StandardWurzel, docs/07) — eine zweite
        //   Anwendung derselben Solution (Test-Anwendung) bleibt außen vor; ohne sie (Sonde, fremde Solution) das niedrigste Projekt mit
        //   generierter Routing-Tabelle.
        var komposition = Abstractions.Projektstruktur.Projekt(Abstractions.Projektstruktur.StandardWurzel, Abstractions.Projektstruktur.Rolle.Komposition);
        if (nutzer.Any(p => p.AssemblyName == komposition))
            nutzer = nutzer.Where(p => p.AssemblyName == komposition).Concat(nutzer.Where(p => p.AssemblyName != komposition)).ToList();
        Project? laufzeit = null; Compilation? laufzeitComp = null;
        foreach (var p in nutzer)
        {
            var c = await p.GetCompilationAsync();
            if (c != null && RoutingTruth.HatRouting(c, quelltexte)) { laufzeit = p; laufzeitComp = c; break; }
        }
        var ausRouting = laufzeit != null;
        if (laufzeit == null)
        {
            // Ohne Routing-Generat (Code mit Fehlern, Generator nicht gelaufen) wirft die Lage nicht: Laufzeit ist dann das niedrigste
            //   Projekt, das alle Aggregat-Projekte zusammenführt (Projekt-Referenzen = Code-Fakt); ohne Aggregate der Vertrag selbst.
            var aggregatProjekte = new List<ProjectId>();
            foreach (var p in nutzer)
                if (await p.GetCompilationAsync() is { } c && Deklariert(c, new[] { Vertrag.IDecider }, quelltexte)) aggregatProjekte.Add(p.Id);
            laufzeit = nutzer.FirstOrDefault(p => p.AssemblyName == komposition)
                ?? (aggregatProjekte.Count == 0 ? vertrag : nutzer.FirstOrDefault(p =>
                    aggregatProjekte.All(a => a == p.Id || graph.GetProjectsThatThisProjectTransitivelyDependsOn(p.Id).Contains(a))) ?? vertrag);
            laufzeitComp = await laufzeit.GetCompilationAsync();
            log($"   ⚠ Keine generierte Routing-Tabelle — Laufzeit abgeleitet ({laufzeit.Name}), Routing aus den Decide-Signaturen.");
        }

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
        var aggregatProjekte2 = analyse.Where(a => Deklariert(a.Item2, new[] { Vertrag.IDecider }, quelltexte)).Select(a => a.Item1).ToList();
        // Die Wurzel der Anwendung aus dem Namen der Komposition (docs/07, Rolle Komposition). Domäne sind ihre Rollen-Projekte und, was auf
        //   eines aufbaut (ein fremdes Projekt mit Domänen-Typen meldet dann die Lage-Prüfung); das Framework (Infrastructure, Core …) deklariert
        //   zwar Nachrichten mit Rollen-Markern, baut aber auf kein Rollen-Projekt auf.
        var (wurzel, rolle) = Abstractions.Projektstruktur.Zerlege(laufzeit.AssemblyName);
        bool Anwendung(Project p) => rolle != Abstractions.Projektstruktur.Rolle.Komposition
            || Abstractions.Projektstruktur.IstProjekt(wurzel, p.AssemblyName)
            || graph.GetProjectsThatThisProjectTransitivelyDependsOn(p.Id).Any(id => Abstractions.Projektstruktur.IstProjekt(wurzel, solution.GetProject(id)!.AssemblyName));
        var domäne = analyse.Where(a => a.Item1.Id != laufzeit.Id && a.Item1.Id != vertrag.Id && Anwendung(a.Item1) && Deklariert(a.Item2, DomänenRollen, quelltexte))
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
            if (c == null || ort == null || quelltexte.IstGeneriert(ort.SourceTree!)) continue;
            hosts.Add((p, c));
        }

        log($"   Vertrag: {vertrag.Name} · Laufzeit: {laufzeit.Name} · Domäne: {string.Join(", ", domäne.OrderBy(x => x))} · Hosts: {string.Join(", ", hosts.Select(h => h.Item1.Name))}");
        return new Projektlage
        {
            Vertragsprojekt = vertrag, Laufzeit = laufzeit, LaufzeitCompilation = laufzeitComp, LaufzeitAusRouting = ausRouting, Analyse = analyse,
            DomänenAssemblies = domäne, Hosts = hosts, Quelltexte = quelltexte,
            AggregatAssemblies = aggregatProjekte2.Select(p => p.AssemblyName).ToHashSet(StringComparer.Ordinal),
        };
    }

    /// <summary>
    /// Dieselbe Lage auf einem neuen Solution-Stand — gelesen nur aus den Compilations UNTERHALB der Laufzeit (Vertrag, Kern, Domänen;
    /// Werkbank E7): die Laufzeit und die Hosts werden nicht übersetzt (v1-Generatoren, schwer). Projekt-Rollen bleiben, wie die kalte Lage
    /// sie erkannt hat (neue Projekte brauchen ein neues kaltes Laden). Roslyn übersetzt dabei nur, was sich seit dem letzten Stand geändert hat.
    /// </summary>
    public async Task<Projektlage> NurDomänenAsync(Solution neu, CancellationToken ct = default)
    {
        var analyse = new List<(Project, Compilation)>();
        foreach (var (p, _) in Analyse.Where(a => a.Projekt.Id != Laufzeit.Id || a.Projekt.Id == Vertragsprojekt.Id))
            if (neu.GetProject(p.Id) is { } np && await np.GetCompilationAsync(ct) is { } c) analyse.Add((np, c));
        return new Projektlage
        {
            Vertragsprojekt = neu.GetProject(Vertragsprojekt.Id) ?? Vertragsprojekt, Laufzeit = neu.GetProject(Laufzeit.Id) ?? Laufzeit,
            LaufzeitCompilation = null, LaufzeitAusRouting = false, Analyse = analyse, DomänenAssemblies = DomänenAssemblies, Hosts = [],
            AggregatAssemblies = AggregatAssemblies, Quelltexte = new Quelltexte(neu),
        };
    }

    /// <summary>Die Rollen-Marker des Vertrags, deren handgeschriebene Implementierung ein Projekt zur Domäne macht.</summary>
    private static readonly string[] DomänenRollen =
    {
        Vertrag.IState, Vertrag.IDecider, Vertrag.IApplier, Vertrag.ICommand, Vertrag.IEvent, Vertrag.IQuery, Vertrag.IQueryResponse,
        Vertrag.IReadModel, Vertrag.IWriteStore, Vertrag.IReadStore, Vertrag.ISubscriber, Vertrag.IReader, Vertrag.IFunktion,
        Vertrag.IWertobjekt, Vertrag.IAkteur, Vertrag.IClientVertrag,
    };

    /// <summary>Deklariert die Compilation im handgeschriebenen Quelltext einen Typ, der einen der Marker implementiert (per Symbol)?</summary>
    private static bool Deklariert(Compilation c, IEnumerable<string> marker, Quelltexte quelltexte)
    {
        var ziele = marker.Select(c.GetTypeByMetadataName).Where(x => x != null).Select(x => x!.OriginalDefinition.Fq()).ToHashSet(StringComparer.Ordinal);
        if (ziele.Count == 0) return false;
        return AlleTypen(c.Assembly.GlobalNamespace).Any(t =>
            t.AllInterfaces.Any(i => ziele.Contains(i.OriginalDefinition.Fq()))
            && t.DeclaringSyntaxReferences.Any(r => quelltexte.IstHand(r.SyntaxTree)));
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

}

/// <summary>
/// Welche Syntaxbäume handgeschrieben sind — je Solution-Stand ein eigenes Objekt (kein statischer Zustand: die Werkbank hält einen
/// warmen Workspace, Sonde und Parität rechnen auf Forks). Handgeschrieben = ein Projekt-DOKUMENT der Solution; Source-Generator-Ausgaben
/// sind nie Dokumente.
/// </summary>
public sealed class Quelltexte
{
    private readonly HashSet<string> _dokumente = new(StringComparer.Ordinal);

    public Quelltexte(Solution solution)
    {
        foreach (var d in solution.Projects.SelectMany(p => p.Documents))
            if (d.FilePath != null) _dokumente.Add(d.FilePath);
    }

    /// <summary>
    /// Generierter Quelltext — DIE eine Regel für alle Stellen des Extractors: kein Projekt-Dokument (also von einem
    /// Source-Generator beigesteuert) oder mit dem Generator-Kopf <c>&lt;auto-generated</c> (so markiert der
    /// Codegen-Prepass seine eingecheckte Ausgabe).
    /// </summary>
    public bool IstGeneriert(SyntaxTree tree) =>
        (_dokumente.Count > 0 && !_dokumente.Contains(tree.FilePath))
        || tree.GetRoot().GetLeadingTrivia().ToString().Contains("<auto-generated", StringComparison.Ordinal);

    /// <summary>Handgeschrieben (Gegenteil von <see cref="IstGeneriert"/>).</summary>
    public bool IstHand(SyntaxTree tree) => !IstGeneriert(tree);
}
