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
/// (Decide, Handles, Funktionen, Akteure, Clients) — Rümpfe werden nicht gebraucht (R1: der Ausgang steht in der Signatur).
/// <list type="bullet">
/// <item>CQRS084 (GR-QUERY) — eine Query beantwortet genau ein Reader.</item>
/// <item>CQRS085 (GR-MODUL-AUSGANG-OFFEN, UNUSED-EVENT; Warnung) — ein Event, eine Ablehnung oder eine Response, die niemand erzeugt.</item>
/// <item>CQRS106 (R20, GR-HINWEIS-NETZ; Fehler) — eine Funktion hört einen Hinweis, den im Hinweis-Netz niemand erzeugt (kein Decide, keine Funktion
///   nennt ihn in der Signatur, kein Client sendet ihn): ihre Adresse bekäme ihn nie (Plan Pub/Sub-Neustrukturierung §2.9).</item>
/// <item>CQRS086 (GR-MODUL-EINGANG-OFFEN; Warnung) — ein Command ohne Decider, eine Query ohne Reader.</item>
/// <item>CQRS087 (GR-GETRAGEN; Warnung) — eine Funktion als Schnittstelle ohne Umsetzung im Host, die kein Client trägt (<c>ITraegt</c>): sie läuft
///   nur mit einer Bindung von Hand (R17), sonst ist sie ein Boot-Fehler.</item>
/// </list>
/// Die Befunde haben keinen Quell-Ort in dieser Compilation (die Typen liegen in den Domänen-Projekten); jede Meldung beginnt deshalb mit dem
/// einfachen Namen des Typs in Anführungszeichen — daran hängt die Werkbank sie an die Karte.
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class GesamtbildAnalyzer : DiagnosticAnalyzer
{
    public const string QueryId = "CQRS084", AusgangOffenId = "CQRS085", EingangOffenId = "CQRS086", GetragenId = "CQRS087", HinweisOhneErzeugerId = "CQRS106";

    private static DiagnosticDescriptor D(string id, string titel, string text, DiagnosticSeverity s) =>
        new(id, titel, text, "CQRS.Gesamtbild", s, isEnabledByDefault: true);

    private static readonly DiagnosticDescriptor Query = D(QueryId, "Query mit mehreren Readern",
        "'{0}': die Query beantworten {1} Reader ({2}) — genau einer (GR-QUERY)", DiagnosticSeverity.Error);
    private static readonly DiagnosticDescriptor AusgangOffen = D(AusgangOffenId, "Ausgang ohne Erzeuger",
        "'{0}' ({1}) erzeugt niemand — {2} (GR-MODUL-AUSGANG-OFFEN)", DiagnosticSeverity.Warning);
    private static readonly DiagnosticDescriptor EingangOffen = D(EingangOffenId, "Eingang ohne Konsument",
        "'{0}' ({1}) nimmt niemand an — {2} (GR-MODUL-EINGANG-OFFEN)", DiagnosticSeverity.Warning);
    private static readonly DiagnosticDescriptor Getragen = D(GetragenId, "Funktion ohne Träger",
        "'{0}': die Funktion hat keine Umsetzung im Host und kein Client trägt sie (ITraegt<{0}>) — sie läuft nur mit einer Bindung von Hand (GR-GETRAGEN)",
        DiagnosticSeverity.Warning);

    private static readonly DiagnosticDescriptor HinweisOhneErzeuger = D(HinweisOhneErzeugerId, "Gehörter Hinweis ohne Erzeuger",
        "Die Funktion '{0}' hört den Hinweis '{1}', aber im Hinweis-Netz erzeugt ihn niemand — kein Decide und keine Funktion nennt ihn in der Signatur, kein Client sendet ihn "
        + "(R20, GR-HINWEIS-NETZ)", DiagnosticSeverity.Error);

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArray.Create(Query, AusgangOffen, EingangOffen, Getragen, HinweisOhneErzeuger);

    /// <summary>Die Rollen-Marker, deren Implementierung eine Assembly zur Domäne macht (wie die Projektlage des Extractors).</summary>
    private static readonly string[] DomaenenRollen =
    {
        "IState", "IDecider`1", "IApplier`1", "ICommand", "IEvent", "IQuery", "IQueryResponse", "IReadModel", "IWriteStore", "IReadStore",
        "IProjektion", "IReader", "IFunktion", "IQuellNachricht", "IWertobjekt",
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
        if (!Laufzeitprojekt.Ist(c)) return;   // das Gesamtbild sieht nur die Komposition (alle Rollen-Projekte, docs/07)
        INamedTypeSymbol? G(string n) => c.GetTypeByMetadataName("Abstractions." + n);
        var (iEvent, iTransient, iCommand, iQuery, iResponse) = (G("IEvent"), G("ITransientEvent"), G("ICommand"), G("IQuery"), G("IQueryResponse"));
        var (iDecider, iFunktion, iAkteur, iDarf) = (G("IDecider`1"), G("IFunktion"), G("IAkteur"), G("IDarf`1"));
        var (iClient, iSendet, iTraegt, iQuelle) = (G("IClientVertrag"), G("ISendet`1"), G("ITraegt`1"), G("IQuellNachricht"));
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
        var imNetz = new HashSet<ITypeSymbol>(SymbolEqualityComparer.Default);   // was ins Hinweis-Netz gelangt: Ausgaben von Decide und Funktionen
        var entschieden = new HashSet<ITypeSymbol>(SymbolEqualityComparer.Default);
        var readerJeQuery = new Dictionary<ITypeSymbol, List<string>>(SymbolEqualityComparer.Default);

        foreach (var t in typen.Where(t => !t.IsAbstract || t.TypeKind == TypeKind.Interface))
        {
            if (t.TypeKind == TypeKind.Class && Generisch(t, iDecider))
                foreach (var m in Methoden(t, Abstractions.Aggregatvertrag.Decide))
                {
                    entschieden.Add(m.Parameters[0].Type);
                    erzeugt.UnionWith(Faelle(m.ReturnType));
                    imNetz.UnionWith(Faelle(m.ReturnType));
                }
            // Ein Reader (Projektionsform, eine Lesart mit Generator und Extractor): die Antworten jeder Frage.
            if (Projektionsform.IstReader(t))
                foreach (var f in Projektionsform.LiesReader(t).Fragen)
                {
                    if (!readerJeQuery.TryGetValue(f.Query, out var l)) readerJeQuery[f.Query] = l = new List<string>();
                    l.Add(t.Name);
                    erzeugt.UnionWith(f.Antworten);
                }
            if (t.TypeKind == TypeKind.Interface && iFunktion != null && IstT(t, iFunktion) && !SymbolEqualityComparer.Default.Equals(t, iFunktion))
                foreach (var m in t.GetMembers().OfType<IMethodSymbol>().Where(m => m.IsAbstract))
                {
                    erzeugt.UnionWith(Faelle(m.ReturnType));
                    imNetz.UnionWith(Faelle(m.ReturnType));
                }
            // Eine Funktion als Klasse (Baustein-Form): die Fälle jedes Auf.
            if (t.TypeKind == TypeKind.Class && Funktionsform.IstBaustein(t))
                foreach (var a in Funktionsform.Lies(t).Aufs)
                {
                    erzeugt.UnionWith(a.Ausgaben);
                    imNetz.UnionWith(a.Ausgaben);
                }
            if (IstT(t, iAkteur))
                erzeugt.UnionWith(t.AllInterfaces.Where(i => iDarf != null && SymbolEqualityComparer.Default.Equals(i.OriginalDefinition, iDarf)).Select(i => i.TypeArguments[0]));
            if (t.TypeKind == TypeKind.Interface && IstT(t, iClient))
            {
                var gesendet = t.AllInterfaces.Where(i => iSendet != null && SymbolEqualityComparer.Default.Equals(i.OriginalDefinition, iSendet))
                    .Select(i => i.TypeArguments[0]).ToList();
                erzeugt.UnionWith(gesendet);
                imNetz.UnionWith(gesendet);   // ein Hinweis, den ein Client sendet, geht ins Hinweis-Netz
            }
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
                    "kein Decide, keine Funktion und kein Akteur nennt es in seiner Signatur"));
            else if (IstT(t, iResponse) && !erzeugt.Contains(t) && !alsFeld.Contains(t))
                ctx.ReportDiagnostic(Diagnostic.Create(AusgangOffen, Location.None, t.Name, "Response", "keine Frage eines Readers nennt sie in ihrer Signatur"));
        }

        // R20: jeder Hinweis, den eine Funktion hört, hat einen Erzeuger im Hinweis-Netz (sonst bekäme ihre Adresse ihn nie).
        foreach (var f in typen.Where(t => (t.TypeKind == TypeKind.Interface || (t.TypeKind == TypeKind.Class && !t.IsAbstract)) && Funktionsform.IstBaustein(t)))
            foreach (var a in Funktionsform.Lies(f).Aufs.Where(a => a.Hinweis && !imNetz.Contains(a.Eingang)))
                ctx.ReportDiagnostic(Diagnostic.Create(HinweisOhneErzeuger, a.Methode.Locations.FirstOrDefault() ?? Location.None, f.Name, a.Eingang.Name));

        // GR-GETRAGEN: eine Funktion als Schnittstelle braucht eine Umsetzung im Host oder einen Träger (sonst nur eine Bindung von Hand).
        var getragen = new HashSet<ITypeSymbol>(typen.Where(t => t.TypeKind == TypeKind.Interface && IstT(t, iClient))
            .SelectMany(cl => cl.AllInterfaces.Where(i => iTraegt != null && SymbolEqualityComparer.Default.Equals(i.OriginalDefinition, iTraegt)))
            .Select(i => i.TypeArguments[0]), SymbolEqualityComparer.Default);
        var umgesetzt = new HashSet<ITypeSymbol>(typen.Where(t => t.TypeKind == TypeKind.Class && !t.IsAbstract).SelectMany(Funktionsform.Umsetzt),
            SymbolEqualityComparer.Default);
        foreach (var f in typen.Where(t => t.TypeKind == TypeKind.Interface && Funktionsform.IstBaustein(t) && !getragen.Contains(t) && !umgesetzt.Contains(t)))
            ctx.ReportDiagnostic(Diagnostic.Create(Getragen, Location.None, f.Name));
    }

    private static bool Ist(ITypeSymbol t, INamedTypeSymbol i) =>
        SymbolEqualityComparer.Default.Equals(t.OriginalDefinition, i) || t.AllInterfaces.Any(x => SymbolEqualityComparer.Default.Equals(x.OriginalDefinition, i));

    /// <summary>Die Fälle einer Ausgabe-Signatur: <c>OneOf&lt;A, B&gt;</c> → A, B; Task/IEnumerable abgeschält; void → keine.</summary>
    private static IEnumerable<ITypeSymbol> Faelle(ITypeSymbol t)
    {
        while (t is INamedTypeSymbol { TypeArguments.Length: 1 } h
               && (h.Name == "Task" || h.Name == "ValueTask" || h.Name == "IEnumerable" || h.Name == "IAsyncEnumerable"))
            t = h.TypeArguments[0];
        if (t is INamedTypeSymbol { Name: "Nullable", TypeArguments.Length: 1 } n) t = n.TypeArguments[0];
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
