using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Infrastructure.SourceGeneration;

/// <summary>
/// Regeln, die das GESAMTBILD aller Domänen-Projekte brauchen — Erzeuger und Konsumenten liegen in verschiedenen Projekten (ein Event in
/// <c>Domain</c>, die Funktion, die es erzeugt, in <c>Domain.Pipeline</c>; eine Query in <c>Domain</c>, ihr Reader in <c>Domain.Projections</c>).
/// Darum laufen sie in der Laufzeit-Compilation, die alle Domänen-Projekte referenziert, und lesen nur Signaturen aus den Metadaten
/// (Decide, Handles, Funktionen, Akteure, Verträge, Clients) — Prozess- und Fluss-Rümpfe sind dort nicht sichtbar und werden nicht gebraucht:
/// beide falten ihren Vorgang im Log (Zustandsschritt), und was sie erzeugen, sind Commands/Aufträge, keine Events (Werkbank P7.4).
/// <list type="bullet">
/// <item>CQRS084 (GR-QUERY) — eine Query beantwortet genau ein Reader.</item>
/// <item>CQRS085 (GR-MODUL-AUSGANG-OFFEN, UNUSED-EVENT; Warnung) — ein Event, eine Ablehnung oder eine Response, die niemand erzeugt.</item>
/// <item>CQRS086 (GR-MODUL-EINGANG-OFFEN; Warnung) — ein Command ohne Decider, eine Query ohne Reader.</item>
/// <item>CQRS087 (GR-GETRAGEN; Warnung) — sobald es Clients gibt: eine Zusage mit Ausgabe, deren Vertrags-Teil kein Client trägt.</item>
/// <item>CQRS088 (GR-ZYKLUS; Warnung) — ein Kreis ohne Zustandsschritt: Projektionen/Reaktionen, die sich über transiente Events gegenseitig
///   wecken (alle übrigen Wege laufen durch ein Aggregat, einen Prozess oder einen Fluss).</item>
/// </list>
/// Die Befunde haben keinen Quell-Ort in dieser Compilation (die Typen liegen in den Domänen-Projekten); jede Meldung beginnt deshalb mit dem
/// einfachen Namen des Typs in Anführungszeichen — daran hängt die Werkbank sie an die Karte.
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class GesamtbildAnalyzer : DiagnosticAnalyzer
{
    public const string QueryId = "CQRS084", AusgangOffenId = "CQRS085", EingangOffenId = "CQRS086", GetragenId = "CQRS087", ZyklusId = "CQRS088";

    private static DiagnosticDescriptor D(string id, string titel, string text, DiagnosticSeverity s) =>
        new(id, titel, text, "CQRS.Gesamtbild", s, isEnabledByDefault: true);

    private static readonly DiagnosticDescriptor Query = D(QueryId, "Query mit mehreren Readern",
        "'{0}': die Query beantworten {1} Reader ({2}) — genau einer (GR-QUERY)", DiagnosticSeverity.Error);
    private static readonly DiagnosticDescriptor AusgangOffen = D(AusgangOffenId, "Ausgang ohne Erzeuger",
        "'{0}' ({1}) erzeugt niemand — {2} (GR-MODUL-AUSGANG-OFFEN)", DiagnosticSeverity.Warning);
    private static readonly DiagnosticDescriptor EingangOffen = D(EingangOffenId, "Eingang ohne Konsument",
        "'{0}' ({1}) nimmt niemand an — {2} (GR-MODUL-EINGANG-OFFEN)", DiagnosticSeverity.Warning);
    private static readonly DiagnosticDescriptor Getragen = D(GetragenId, "Zusage ohne Client",
        "'{0}': die Zusage Auf({1}) → {2} trägt kein Client — die Kette bricht hier ab (GR-GETRAGEN)", DiagnosticSeverity.Warning);
    private static readonly DiagnosticDescriptor Zyklus = D(ZyklusId, "Kreis ohne Zustandsschritt",
        "'{0}': Kreis ohne Aggregat, Prozess oder Fluss über transiente Events: {1} (GR-ZYKLUS)", DiagnosticSeverity.Warning);

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArray.Create(Query, AusgangOffen, EingangOffen, Getragen, Zyklus);

    /// <summary>Die Rollen-Marker, deren Implementierung eine Assembly zur Domäne macht (wie die Projektlage des Extractors).</summary>
    private static readonly string[] DomaenenRollen =
    {
        "IState", "IDecider`1", "IApplier`1", "ICommand", "IEvent", "IQuery", "IQueryResponse", "IReadModel", "IWriteStore", "IReadStore",
        "ISubscriber", "IReader`1", "IPipelineHandler", "IPipelineTrigger", "IProzessDefinition", "IWertobjekt",
    };

    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        context.RegisterCompilationAction(Pruefe);
    }

    private static void Pruefe(CompilationAnalysisContext ctx)
    {
        var c = ctx.Compilation;
        INamedTypeSymbol? G(string n) => c.GetTypeByMetadataName("Abstractions." + n);
        var (iEvent, iTransient, iCommand, iQuery, iResponse) = (G("IEvent"), G("ITransientEvent"), G("ICommand"), G("IQuery"), G("IQueryResponse"));
        var (iDecider, iReader, iSubscriber, iFunktion, iAkteur, iDarf) = (G("IDecider`1"), G("IReader`1"), G("ISubscriber"), G("IFunktion"), G("IAkteur"), G("IDarf`1"));
        var (iVertrag, iClient, iSendet, iQuelle) = (G("IAkteurVertrag`1"), G("IClientVertrag"), G("ISendet`1"), G("IQuellNachricht"));
        if (iEvent == null || iCommand == null || iDecider == null) return;
        var rollen = DomaenenRollen.Select(G).Where(x => x != null).Select(x => x!).ToList();

        // Domänen-Assemblies = referenzierte Assemblies (nicht der Vertrag), die einen Rollen-Marker implementieren.
        var typen = new List<INamedTypeSymbol>();
        foreach (var a in c.SourceModule.ReferencedAssemblySymbols.Where(a => a.Name != "Abstractions"))
        {
            var alle = Alle(a.GlobalNamespace).ToList();
            if (alle.Any(t => rollen.Any(r => Ist(t, r)))) typen.AddRange(alle);
        }
        if (typen.Count == 0) return;

        bool IstT(ITypeSymbol t, INamedTypeSymbol? i) => i != null && Ist(t, i);
        bool Generisch(INamedTypeSymbol t, INamedTypeSymbol? g) => g != null && t.AllInterfaces.Any(i => SymbolEqualityComparer.Default.Equals(i.OriginalDefinition, g));
        IEnumerable<IMethodSymbol> Methoden(INamedTypeSymbol t, string? name = null) => t.GetMembers().OfType<IMethodSymbol>()
            .Where(m => m.MethodKind == MethodKind.Ordinary && m.DeclaredAccessibility == Accessibility.Public && m.Parameters.Length > 0 && (name == null || m.Name == name));
        var erzeugt = new HashSet<ITypeSymbol>(SymbolEqualityComparer.Default);
        var entschieden = new HashSet<ITypeSymbol>(SymbolEqualityComparer.Default);
        var readerJeQuery = new Dictionary<ITypeSymbol, List<string>>(SymbolEqualityComparer.Default);
        var konsumenten = new List<(INamedTypeSymbol T, List<ITypeSymbol> Ein, List<ITypeSymbol> Aus)>();

        foreach (var t in typen.Where(t => !t.IsAbstract || t.TypeKind == TypeKind.Interface))
        {
            if (t.TypeKind == TypeKind.Class && Generisch(t, iDecider))
                foreach (var m in Methoden(t, Abstractions.Aggregatvertrag.Decide))
                {
                    entschieden.Add(m.Parameters[0].Type);
                    erzeugt.UnionWith(Faelle(m.ReturnType));
                }
            if (t.TypeKind == TypeKind.Class && Generisch(t, iReader))
                foreach (var m in Methoden(t).Where(m => IstT(m.Parameters[0].Type, iQuery)))
                {
                    if (!readerJeQuery.TryGetValue(m.Parameters[0].Type, out var l)) readerJeQuery[m.Parameters[0].Type] = l = new List<string>();
                    l.Add(t.Name);
                    erzeugt.UnionWith(Faelle(m.ReturnType));
                }
            if (t.TypeKind == TypeKind.Class && IstT(t, iSubscriber))
            {
                var hs = Methoden(t).Where(m => IstT(m.Parameters[0].Type, iEvent)).ToList();
                var aus = hs.SelectMany(m => Faelle(m.ReturnType)).ToList();
                erzeugt.UnionWith(aus);
                konsumenten.Add((t, hs.Select(m => m.Parameters[0].Type).ToList(), aus));
            }
            if (t.TypeKind == TypeKind.Interface && iFunktion != null && IstT(t, iFunktion) && !SymbolEqualityComparer.Default.Equals(t, iFunktion))
                foreach (var m in t.GetMembers().OfType<IMethodSymbol>().Where(m => m.IsAbstract))
                    erzeugt.UnionWith(Faelle(m.ReturnType));
            if (IstT(t, iAkteur))
                erzeugt.UnionWith(t.AllInterfaces.Where(i => iDarf != null && SymbolEqualityComparer.Default.Equals(i.OriginalDefinition, iDarf)).Select(i => i.TypeArguments[0]));
            if (t.TypeKind == TypeKind.Interface && IstT(t, iClient))
                erzeugt.UnionWith(t.AllInterfaces.Where(i => iSendet != null && SymbolEqualityComparer.Default.Equals(i.OriginalDefinition, iSendet)).Select(i => i.TypeArguments[0]));
        }

        // GR-QUERY und offene Eingänge.
        foreach (var t in typen.Where(t => !t.IsAbstract && t.TypeKind != TypeKind.Interface))
        {
            if (IstT(t, iQuery))
            {
                var r = readerJeQuery.TryGetValue(t, out var l) ? l : new List<string>();
                if (r.Count > 1) ctx.ReportDiagnostic(Diagnostic.Create(Query, Location.None, t.Name, r.Count, string.Join(", ", r)));
                if (r.Count == 0) ctx.ReportDiagnostic(Diagnostic.Create(EingangOffen, Location.None, t.Name, "Query", "kein Reader beantwortet sie"));
            }
            else if (IstT(t, iCommand) && !entschieden.Contains(t))
                ctx.ReportDiagnostic(Diagnostic.Create(EingangOffen, Location.None, t.Name, "Command", "kein Aggregat entscheidet ihn (Decide)"));
        }

        // Offene Ausgänge: Events/Ablehnungen ohne Erzeuger (Quell-Nachrichten erzeugt der Treiber bzw. die Außenwelt), Responses ohne Reader-Handle.
        var alsFeld = new HashSet<ITypeSymbol>(SymbolEqualityComparer.Default);
        foreach (var r in typen.Where(t => IstT(t, iResponse)))
            foreach (var p in r.GetMembers().OfType<IPropertySymbol>())
                foreach (var x in MitArgumenten(p.Type)) alsFeld.Add(x);
        foreach (var t in typen.Where(t => !t.IsAbstract && t.TypeKind != TypeKind.Interface))
        {
            if (IstT(t, iEvent) && !IstT(t, iQuelle) && !erzeugt.Contains(t))
                ctx.ReportDiagnostic(Diagnostic.Create(AusgangOffen, Location.None, t.Name, IstT(t, iTransient) ? "Ablehnung/Hinweis" : "Event",
                    "kein Decide, keine Funktion, kein Handle und kein Akteur nennt es in seiner Signatur"));
            else if (IstT(t, iResponse) && !erzeugt.Contains(t) && !alsFeld.Contains(t))
                ctx.ReportDiagnostic(Diagnostic.Create(AusgangOffen, Location.None, t.Name, "Response", "kein Reader-Handle nennt sie in seiner Signatur"));
        }

        // GR-GETRAGEN: sobald es Clients gibt, trägt jede Zusage mit Ausgabe ein Client.
        var clients = typen.Where(t => t.TypeKind == TypeKind.Interface && IstT(t, iClient)).ToList();
        if (clients.Count > 0 && iVertrag != null)
        {
            var getragen = new HashSet<ITypeSymbol>(clients.SelectMany(cl => cl.AllInterfaces), SymbolEqualityComparer.Default);
            foreach (var teil in typen.Where(t => t.TypeKind == TypeKind.Interface && Generisch(t, iVertrag) && !IstT(t, iClient) && !getragen.Contains(t)))
                foreach (var m in teil.GetMembers(Abstractions.Akteurvertrag.Auf).OfType<IMethodSymbol>().Where(m => m.Parameters.Length == 1))
                {
                    var aus = Faelle(m.ReturnType).ToList();
                    if (aus.Count > 0)
                        ctx.ReportDiagnostic(Diagnostic.Create(Getragen, Location.None, teil.Name, m.Parameters[0].Type.Name, string.Join(", ", aus.Select(a => a.Name))));
                }
        }

        // GR-ZYKLUS: Projektionen/Reaktionen, die sich über transiente Events wecken (starke Zusammenhangskomponenten).
        var kanten = new Dictionary<INamedTypeSymbol, List<INamedTypeSymbol>>(SymbolEqualityComparer.Default);
        foreach (var k in konsumenten)
            kanten[k.T] = konsumenten.Where(z => k.Aus.Any(a => IstT(a, iTransient) && z.Ein.Contains(a, SymbolEqualityComparer.Default))).Select(z => z.T).ToList();
        foreach (var komp in Komponenten(kanten))
            ctx.ReportDiagnostic(Diagnostic.Create(Zyklus, Location.None, komp[0].Name, string.Join(" → ", komp.Select(k => k.Name))));
    }

    /// <summary>Tarjan: Komponenten mit Kreis (mehr als ein Knoten oder Selbst-Schleife), Namen sortiert.</summary>
    private static List<List<INamedTypeSymbol>> Komponenten(Dictionary<INamedTypeSymbol, List<INamedTypeSymbol>> kanten)
    {
        var index = new Dictionary<INamedTypeSymbol, int>(SymbolEqualityComparer.Default);
        var low = new Dictionary<INamedTypeSymbol, int>(SymbolEqualityComparer.Default);
        var stapel = new Stack<INamedTypeSymbol>();
        var auf = new HashSet<INamedTypeSymbol>(SymbolEqualityComparer.Default);
        var aus = new List<List<INamedTypeSymbol>>();
        var i = 0;
        void Besuche(INamedTypeSymbol v)
        {
            index[v] = low[v] = i++;
            stapel.Push(v); auf.Add(v);
            foreach (var w in kanten[v])
                if (!index.ContainsKey(w)) { Besuche(w); low[v] = System.Math.Min(low[v], low[w]); }
                else if (auf.Contains(w)) low[v] = System.Math.Min(low[v], index[w]);
            if (low[v] != index[v]) return;
            var komp = new List<INamedTypeSymbol>();
            INamedTypeSymbol x;
            do { x = stapel.Pop(); auf.Remove(x); komp.Add(x); } while (!SymbolEqualityComparer.Default.Equals(x, v));
            if (komp.Count > 1 || kanten[v].Contains(v, SymbolEqualityComparer.Default))
                aus.Add(komp.OrderBy(k => k.Name, System.StringComparer.Ordinal).ToList());
        }
        foreach (var v in kanten.Keys.OrderBy(k => k.ToDisplayString(), System.StringComparer.Ordinal))
            if (!index.ContainsKey(v)) Besuche(v);
        return aus;
    }

    private static bool Ist(ITypeSymbol t, INamedTypeSymbol i) =>
        SymbolEqualityComparer.Default.Equals(t.OriginalDefinition, i) || t.AllInterfaces.Any(x => SymbolEqualityComparer.Default.Equals(x.OriginalDefinition, i));

    /// <summary>Die Fälle einer Ausgabe-Signatur: <c>OneOf&lt;A, B&gt;</c> → A, B; Task/IEnumerable abgeschält; void → keine.</summary>
    private static IEnumerable<ITypeSymbol> Faelle(ITypeSymbol t)
    {
        while (t is INamedTypeSymbol { TypeArguments.Length: 1 } h
               && (h.Name == "Task" || h.Name == "ValueTask" || h.Name == "IEnumerable" || h.Name == "IAsyncEnumerable"))
            t = h.TypeArguments[0];
        if (t is INamedTypeSymbol { Name: "OneOf" } o && o.ContainingNamespace?.ToDisplayString() == "Abstractions") return o.TypeArguments;
        if (t.SpecialType == SpecialType.System_Void || t is INamedTypeSymbol { Name: "Task", TypeArguments.Length: 0 }) return Enumerable.Empty<ITypeSymbol>();
        return new[] { t };
    }

    private static IEnumerable<ITypeSymbol> MitArgumenten(ITypeSymbol t)
    {
        yield return t;
        if (t is IArrayTypeSymbol a) foreach (var x in MitArgumenten(a.ElementType)) yield return x;
        if (t is INamedTypeSymbol n) foreach (var arg in n.TypeArguments) foreach (var x in MitArgumenten(arg)) yield return x;
    }

    private static IEnumerable<INamedTypeSymbol> Alle(INamespaceSymbol ns)
    {
        foreach (var t in ns.GetTypeMembers())
            foreach (var x in MitGeschachtelten(t)) yield return x;
        foreach (var sub in ns.GetNamespaceMembers())
            foreach (var t in Alle(sub)) yield return t;
    }

    private static IEnumerable<INamedTypeSymbol> MitGeschachtelten(INamedTypeSymbol t)
    {
        yield return t;
        foreach (var n in t.GetTypeMembers())
            foreach (var x in MitGeschachtelten(n)) yield return x;
    }
}
