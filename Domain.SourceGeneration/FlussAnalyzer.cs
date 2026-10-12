using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Domain.SourceGeneration;

/// <summary>
/// Die Form einer Pipeline als Fluss (docs/konzept-editor-pipelines.md §14) im Build — was früher nur der Editor-Validator bzw. erst der
/// <c>PipelineBauer</c> beim Start prüfte (Werkbank P7.4, „Prüfen = Bauen“). Gelesen wird das Lambda von <c>PipelineFluss.Definiere</c>:
/// jede Anweisung ist ein Knoten (Variable = Name), sein Kern-Aufruf (Quelle/Auf/Rufe/Sende/Warte/Je, außen <c>.Zeitlimit</c>/<c>.Oder</c>)
/// bestimmt Art und Ausgänge — aus Signaturen, nie aus Rümpfen:
/// <list type="bullet">
/// <item>CQRS073 (GR-FLUSS-QUELLE) — ein Fluss beginnt an genau einer Quelle (<c>p.Quelle&lt;T&gt;()</c> oder <c>p.Auf&lt;E&gt;()</c>).</item>
/// <item>CQRS074 (GR-FLUSS-WARTE) — ein Warten (<c>quelle.Strom().Warte&lt;…&gt;()</c>) braucht ein Zeitlimit.</item>
/// <item>CQRS075 (GR-FLUSS-ZEITLIMIT) — <c>k.BeiZeitlimit()</c> nur an einem Knoten mit Zeitlimit.</item>
/// <item>CQRS076 (GR-FLUSS-DRAHT) — <c>k.Bei&lt;E&gt;()</c> nur mit einem Ausgang von k: Ergebnis der Funktion (OneOf ihrer Signatur), Event aus
///   dem Decide des Commands (eine Ablehnung kommt am ✕-Port, <c>BeiAbgelehnt()</c>) bzw. eines der Events, auf die k wartet.</item>
/// <item>CQRS077 (GR-FLUSS-WARTET, Warnung) — ∧ wartet auf einen Ausgang, neben dem der Vorgänger weitere Fälle hat, die nirgends hinführen.</item>
/// <item>CQRS078 (GR-COMMAND) — ein Fluss sendet einen Command, den kein sichtbares Aggregat entscheidet (der Send verpufft).</item>
/// </list>
/// Sichtbar ist die eigene Compilation samt ihren Referenzen (Aggregate und Funktionen liegen nie „unterhalb“ des Flusses, der sie ruft).
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class FlussAnalyzer : DiagnosticAnalyzer
{
    public const string QuelleId = "CQRS073", WarteId = "CQRS074", ZeitlimitId = "CQRS075", DrahtId = "CQRS076", WartetId = "CQRS077",
        OhneDeciderId = "CQRS078";

    private static DiagnosticDescriptor D(string id, string titel, string text, DiagnosticSeverity s = DiagnosticSeverity.Error) =>
        new(id, titel, text, "CQRS.Fluss", s, isEnabledByDefault: true);

    private static readonly DiagnosticDescriptor Quelle = D(QuelleId, "Ein Fluss beginnt an genau einer Quelle",
        "Fluss '{0}' hat {1} Quellen — ein Fluss beginnt an genau einer: p.Quelle<Nachricht>() oder p.Auf<Event>() (GR-FLUSS-QUELLE)");
    private static readonly DiagnosticDescriptor Warte = D(WarteId, "Warten ohne Zeitlimit",
        "Fluss '{0}': der Knoten '{1}' wartet ohne Zeitlimit — kommt keines der Events, bliebe der Vorgang offen; .Zeitlimit(…) anhängen (GR-FLUSS-WARTE)");
    private static readonly DiagnosticDescriptor Zeitlimit = D(ZeitlimitId, "⏳-Port ohne Zeitlimit",
        "Fluss '{0}': '{1}.BeiZeitlimit()' — '{1}' hat kein Zeitlimit, der ⏳-Port feuert nie (GR-FLUSS-ZEITLIMIT)");
    private static readonly DiagnosticDescriptor Draht = D(DrahtId, "Fall außerhalb der Ausgänge",
        "Fluss '{0}': '{1}.Bei<{2}>()' — {2} ist kein Ausgang von '{1}' ({3}); der Draht feuerte nie (GR-FLUSS-DRAHT)");
    private static readonly DiagnosticDescriptor Wartet = D(WartetId, "∧ wartet auf einen Weg, der nicht immer liefert",
        "Fluss '{0}': ∧ wartet auf '{1}.{2}' — liefert '{1}' stattdessen {3}, wartet der Knoten {4}; den anderen Fall verdrahten oder bewusst offen lassen (GR-FLUSS-WARTET)",
        DiagnosticSeverity.Warning);
    private static readonly DiagnosticDescriptor OhneDecider = D(OhneDeciderId, "Fluss sendet einen Command ohne Decider",
        "Fluss '{0}' sendet '{1}', aber kein Aggregat entscheidet ihn (IDecider<TState>.Decide) — der Send verpufft (GR-COMMAND)");

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics =>
        ImmutableArray.Create(Quelle, Warte, Zeitlimit, Draht, Wartet, OhneDecider);

    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        context.RegisterCompilationStartAction(start =>
        {
            var c = start.Compilation;
            var iDecider = c.GetTypeByMetadataName("Abstractions.IDecider`1");
            var iTransient = c.GetTypeByMetadataName("Abstractions.ITransientEvent");
            if (c.GetTypeByMetadataName("Abstractions.PipelineFluss") == null || iDecider == null) return;
            var welt = new System.Lazy<Welt>(() => Welt.Aus(c, iDecider, iTransient), isThreadSafe: true);
            start.RegisterSyntaxNodeAction(ctx => Pruefe(ctx, welt.Value), SyntaxKind.InvocationExpression);
        });
    }

    /// <summary>Was ein Fluss von außen sieht: je Command die Ausgänge seines Decide (Events und Ablehnungen getrennt).</summary>
    private sealed class Welt
    {
        public readonly Dictionary<string, (List<ITypeSymbol> Events, List<ITypeSymbol> Ablehnungen)> Decide = new();

        public static Welt Aus(Compilation c, INamedTypeSymbol iDecider, INamedTypeSymbol? iTransient)
        {
            var w = new Welt();
            foreach (var t in Typen.Vertragsnutzer(c))
            {
                if (!t.AllInterfaces.Any(i => SymbolEqualityComparer.Default.Equals(i.OriginalDefinition, iDecider))) continue;
                foreach (var m in t.GetMembers(Abstractions.Aggregatvertrag.Decide).OfType<IMethodSymbol>())
                {
                    if (m.Parameters.Length == 0) continue;
                    var schluessel = m.Parameters[0].Type.ToDisplayString();
                    if (!w.Decide.TryGetValue(schluessel, out var aus)) w.Decide[schluessel] = aus = (new List<ITypeSymbol>(), new List<ITypeSymbol>());
                    foreach (var e in Typen.Faelle(m.ReturnType))
                        (iTransient != null && e.AllInterfaces.Contains(iTransient, SymbolEqualityComparer.Default) ? aus.Ablehnungen : aus.Events).Add(e);
                }
            }
            return w;
        }
    }

    /// <summary>Ein Knoten im Lambda: Art (Verb), Typ-Argument, Zeitlimit, Ausgänge (null = unbekannt), die Anweisung.</summary>
    private sealed class Knoten
    {
        public string Name = "", Verb = "";
        public ITypeSymbol? Typ;
        public bool HatZeitlimit;
        public List<ITypeSymbol>? Ausgaenge, Ablehnungen;
        public SyntaxNode Ort = null!;
    }

    private static void Pruefe(SyntaxNodeAnalysisContext ctx, Welt welt)
    {
        var inv = (InvocationExpressionSyntax)ctx.Node;
        if (ctx.SemanticModel.GetSymbolInfo(inv, ctx.CancellationToken).Symbol is not IMethodSymbol def
            || def.Name != "Definiere" || def.ContainingType?.Name != "PipelineFluss" || !IstDsl(def)
            || inv.ArgumentList.Arguments.FirstOrDefault()?.Expression is not LambdaExpressionSyntax lambda) return;
        var model = ctx.SemanticModel;
        var fluss = inv.FirstAncestorOrSelf<BaseTypeDeclarationSyntax>()?.Identifier.Text ?? "?";

        // Die Knoten: je Anweisung ihr Kern-Aufruf, außen Zeitlimit/Oder abgeschält.
        var knoten = new Dictionary<string, Knoten>();
        var anweisungen = lambda.Body is BlockSyntax block ? block.Statements.ToList() : new List<StatementSyntax>();
        foreach (var st in anweisungen)
        {
            string? name = null;
            ExpressionSyntax? ausdruck = null;
            if (st is LocalDeclarationStatementSyntax ld && ld.Declaration.Variables.Count == 1 && ld.Declaration.Variables[0].Initializer != null)
            { name = ld.Declaration.Variables[0].Identifier.Text; ausdruck = ld.Declaration.Variables[0].Initializer!.Value; }
            else if (st is ExpressionStatementSyntax es) ausdruck = es.Expression;
            if (ausdruck == null) continue;
            var k = new Knoten { Name = name ?? "", Ort = st };
            while (ausdruck is InvocationExpressionSyntax aussen && aussen.Expression is MemberAccessExpressionSyntax ma
                   && model.GetSymbolInfo(aussen).Symbol is IMethodSymbol am && IstDsl(am) && (am.Name == "Zeitlimit" || am.Name == "Oder"))
            {
                if (am.Name == "Zeitlimit") k.HatZeitlimit = true;
                ausdruck = ma.Expression;
            }
            if (ausdruck is not InvocationExpressionSyntax kern || model.GetSymbolInfo(kern).Symbol is not IMethodSymbol km || !IstDsl(km)) continue;
            k.Verb = km.Name;
            k.Typ = km.TypeArguments.FirstOrDefault();
            switch (km.Name)
            {
                case "Rufe":
                    k.Ausgaenge = k.Typ is INamedTypeSymbol f ? FunktionsErgebnisse(f) : null;
                    break;
                case "Sende":
                    if (k.Typ != null && welt.Decide.TryGetValue(k.Typ.ToDisplayString(), out var d)) { k.Ausgaenge = d.Events; k.Ablehnungen = d.Ablehnungen; }
                    break;
                case "Warte":
                    k.Ausgaenge = km.TypeArguments.ToList();
                    if (!k.HatZeitlimit)
                        ctx.ReportDiagnostic(Diagnostic.Create(Warte, st.GetLocation(), fluss, name ?? "Warte"));
                    break;
            }
            if (name != null) knoten[name] = k;
        }

        // Quellen zählen (jede Anweisung, auch ohne Variable).
        var quellen = lambda.DescendantNodes().OfType<InvocationExpressionSyntax>()
            .Count(i => model.GetSymbolInfo(i).Symbol is IMethodSymbol m && IstDsl(m) && m.ContainingType?.Name == "PipelineBauer"
                        && (m.Name == "Quelle" || m.Name == "Auf"));
        if (quellen != 1) ctx.ReportDiagnostic(Diagnostic.Create(Quelle, inv.GetLocation(), fluss, quellen));

        // Drähte: k.Bei<E>(), k.BeiZeitlimit(); welche Fälle je Knoten irgendwo verdrahtet sind (für ∧ wartet).
        var verdrahtet = new HashSet<(string, string)>();
        foreach (var d in lambda.DescendantNodes().OfType<InvocationExpressionSyntax>())
        {
            if (d.Expression is not MemberAccessExpressionSyntax { Expression: IdentifierNameSyntax von }
                || model.GetSymbolInfo(d).Symbol is not IMethodSymbol m || !IstDsl(m) || !knoten.TryGetValue(von.Identifier.Text, out var k)) continue;
            if (m.Name == "Bei" && m.TypeArguments.FirstOrDefault() is { } fall)
            {
                verdrahtet.Add((k.Name, fall.ToDisplayString()));
                if (k.Ausgaenge != null && !k.Ausgaenge.Any(a => SymbolEqualityComparer.Default.Equals(a, fall)))
                {
                    var warum = k.Ablehnungen != null && k.Ablehnungen.Any(a => SymbolEqualityComparer.Default.Equals(a, fall))
                        ? "eine Ablehnung kommt am ✕-Port: " + k.Name + ".BeiAbgelehnt()"
                        : k.Ausgaenge.Count == 0 ? "keine Ausgänge" : "Ausgänge: " + string.Join(", ", k.Ausgaenge.Select(a => a.Name));
                    ctx.ReportDiagnostic(Diagnostic.Create(Draht, d.GetLocation(), fluss, k.Name, fall.Name, warum));
                }
            }
            else if (m.Name == "BeiZeitlimit" && !k.HatZeitlimit)
                ctx.ReportDiagnostic(Diagnostic.Create(Zeitlimit, d.GetLocation(), fluss, k.Name));
            else if (m.Name == "BeiAbgelehnt" && k.Verb == "Warte")
                ctx.ReportDiagnostic(Diagnostic.Create(Draht, d.GetLocation(), fluss, k.Name, "SchrittAbgelehnt",
                    "ein Warten hat keinen ✕-Port, nur seine Events und ⏳"));
        }

        // Sende<C> ohne Decider (jede Sende-Form der DSL: am Draht, am ∧, im Je, am Sammeln).
        foreach (var s in lambda.DescendantNodes().OfType<InvocationExpressionSyntax>())
            if (model.GetSymbolInfo(s).Symbol is IMethodSymbol m && IstDsl(m) && m.Name == "Sende" && m.TypeArguments.FirstOrDefault() is { } cmd
                && cmd.TypeKind != TypeKind.Error && !welt.Decide.ContainsKey(cmd.ToDisplayString()))
                ctx.ReportDiagnostic(Diagnostic.Create(OhneDecider, s.GetLocation(), fluss, cmd.Name));

        // ∧ (p.Alle(…)) wartet auf einen Fall, neben dem der Vorgänger weitere hat, die nirgends hinführen — je (Knoten, Fall) einmal gemeldet.
        var gemeldet = new HashSet<(string, string)>();
        foreach (var alle in lambda.DescendantNodes().OfType<InvocationExpressionSyntax>())
        {
            if (model.GetSymbolInfo(alle).Symbol is not IMethodSymbol am || !IstDsl(am) || am.Name != "Alle" || alle.ArgumentList.Arguments.Count < 2) continue;
            foreach (var arg in alle.ArgumentList.Arguments)
            {
                if (arg.Expression is not InvocationExpressionSyntax bei || bei.Expression is not MemberAccessExpressionSyntax { Expression: IdentifierNameSyntax von }
                    || model.GetSymbolInfo(bei).Symbol is not IMethodSymbol bm || bm.Name != "Bei" || !knoten.TryGetValue(von.Identifier.Text, out var k)
                    || k.Ausgaenge == null || bm.TypeArguments.FirstOrDefault() is not { } fall) continue;
                var offen = k.Ausgaenge.Where(a => !SymbolEqualityComparer.Default.Equals(a, fall) && !verdrahtet.Contains((k.Name, a.ToDisplayString())))
                    .Select(a => a.Name).ToList();
                if (offen.Count > 0 && gemeldet.Add((k.Name, fall.ToDisplayString())))
                    ctx.ReportDiagnostic(Diagnostic.Create(Wartet, arg.GetLocation(), fluss, k.Name, fall.Name, string.Join("/", offen),
                        k.HatZeitlimit ? "bis zum Zeitlimit" : "für immer"));
            }
        }
    }

    /// <summary>Die Ergebnisse einer Katalog-Funktion: die Fälle der Rückgabe ihrer (einen) Methode — OneOf bzw. Task&lt;OneOf&gt;.</summary>
    private static List<ITypeSymbol>? FunktionsErgebnisse(INamedTypeSymbol f)
    {
        var m = f.GetMembers().OfType<IMethodSymbol>().FirstOrDefault(x => x.MethodKind == MethodKind.Ordinary && x.IsAbstract);
        return m == null ? null : Typen.Faelle(m.ReturnType).ToList();
    }

    private static bool IstDsl(IMethodSymbol m) => m.ContainingType?.ContainingNamespace?.ToDisplayString() == "Abstractions";
}

/// <summary>Gemeinsame Symbol-Helfer der Modell-Analyzer.</summary>
internal static class Typen
{
    /// <summary>Die Fälle einer Ausgabe-Signatur: <c>OneOf&lt;A, B&gt;</c> → A, B; <c>Task&lt;…&gt;</c>/<c>IEnumerable&lt;…&gt;</c> abgeschält; sonst der Typ selbst.</summary>
    public static IEnumerable<ITypeSymbol> Faelle(ITypeSymbol t)
    {
        while (t is INamedTypeSymbol { TypeArguments.Length: 1 } h
               && (h.Name == "Task" || h.Name == "ValueTask" || h.Name == "IEnumerable" || h.Name == "IAsyncEnumerable"))
            t = h.TypeArguments[0];
        if (t is INamedTypeSymbol { Name: "OneOf" } o && o.ContainingNamespace?.ToDisplayString() == "Abstractions") return o.TypeArguments;
        if (t.SpecialType == SpecialType.System_Void || t is INamedTypeSymbol { Name: "Task", TypeArguments.Length: 0 }) return Enumerable.Empty<ITypeSymbol>();
        return new[] { t };
    }

    /// <summary>Alle Typen der eigenen Assembly und jeder referenzierten, die den Vertrag (Abstractions) nutzt — ohne BCL-Metadaten.</summary>
    public static IEnumerable<INamedTypeSymbol> Vertragsnutzer(Compilation c)
    {
        var asms = new List<IAssemblySymbol> { c.Assembly };
        asms.AddRange(c.SourceModule.ReferencedAssemblySymbols.Where(a =>
            a.Name != "Abstractions" && a.Modules.Any(m => m.ReferencedAssemblySymbols.Any(r => r.Name == "Abstractions"))));
        foreach (var a in asms)
            foreach (var t in Alle(a.GlobalNamespace))
                yield return t;
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
