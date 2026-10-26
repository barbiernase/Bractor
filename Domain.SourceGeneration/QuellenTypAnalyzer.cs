using System.Collections.Immutable;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Domain.SourceGeneration;

/// <summary>
/// CQRS070 — die Form eines Quellen-Typs (docs/konzept-werkbank.md §3.2): ein Typ, der von einer Treiber-Basis erbt
/// (<c>DateiGemeldet</c>, <see cref="Abstractions.Treibervertrag"/>), ist ein <c>sealed record</c> OHNE eigene Member und ohne
/// Parameterliste — der Treiber befüllt nur die Felder seiner Basis, eigene Felder könnte er nicht setzen. Die Domäne benennt die
/// Quelle nur als Typ: <c>public sealed record DateiErkannt : DateiGemeldet;</c>
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class QuellenTypAnalyzer : DiagnosticAnalyzer
{
    public const string FormId = "CQRS070";

    private static readonly DiagnosticDescriptor Form = new(
        FormId,
        "Quellen-Typ hat nicht die feste Form",
        "Quellen-Typ '{0}' ({1}-Treiber): {2}. Form: public sealed record {0} : {3}; — ohne eigene Member, ohne Parameterliste. "
        + "Die Felder befüllt der Treiber; die Werte (Pfad, Muster, Intervall …) stehen in der Konfig (Quellen:{0}).",
        "CQRS.Quelle",
        DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArray.Create(Form);

    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        context.RegisterCompilationStartAction(start =>
        {
            var basen = Abstractions.Treibervertrag.Basen
                .Select(b => (Basis: start.Compilation.GetTypeByMetadataName(b[0]), Treiber: b[1]))
                .Where(b => b.Basis != null).Select(b => (b.Basis!, b.Treiber)).ToList();
            if (basen.Count == 0) return;
            start.RegisterSymbolAction(ctx => { foreach (var (basis, treiber) in basen) Pruefe(ctx, basis, treiber); }, SymbolKind.NamedType);
        });
    }

    private static void Pruefe(SymbolAnalysisContext ctx, INamedTypeSymbol basis, string treiber)
    {
        var t = (INamedTypeSymbol)ctx.Symbol;
        if (t.IsAbstract || SymbolEqualityComparer.Default.Equals(t, basis)) return;
        var erbt = false;
        for (var b = t.BaseType; b != null && !erbt; b = b.BaseType) erbt = SymbolEqualityComparer.Default.Equals(b, basis);
        if (!erbt) return;

        string? grund = null;
        if (!t.IsRecord) grund = "kein record";
        else if (!t.IsSealed) grund = "nicht sealed";
        else if (!SymbolEqualityComparer.Default.Equals(t.BaseType, basis)) grund = $"erbt nicht direkt von {basis.Name}";
        else
        {
            foreach (var r in t.DeclaringSyntaxReferences)
            {
                if (r.GetSyntax(ctx.CancellationToken) is not RecordDeclarationSyntax d) continue;
                if (d.ParameterList != null) { grund = "hat eine Parameterliste"; break; }
                if (d.Members.Count > 0) { grund = "hat eigene Member (" + string.Join(", ", d.Members.Select(Name)) + ")"; break; }
            }
        }
        if (grund != null)
            ctx.ReportDiagnostic(Diagnostic.Create(Form, t.Locations.FirstOrDefault(), t.Name, treiber, grund, basis.Name));
    }

    private static string Name(MemberDeclarationSyntax m) => m switch
    {
        PropertyDeclarationSyntax p => p.Identifier.Text,
        MethodDeclarationSyntax me => me.Identifier.Text,
        FieldDeclarationSyntax f => string.Join(", ", f.Declaration.Variables.Select(v => v.Identifier.Text)),
        _ => m.Kind().ToString(),
    };
}
