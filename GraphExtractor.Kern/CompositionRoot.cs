using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.FindSymbols;

namespace GraphExtractor;

// ════════════════════════════════════════════════════════════════════════════
//  Composition-Root-Extraktion (die dritte, sonst unsichtbare Ebene) — SEMANTISCH und ohne Namenswissen.
//
//  Kein Datei-, Pfad-, Projekt-, Methoden- oder Konfig-Key-Name ist hier bekannt. Alles wird über den
//  Framework-Vertrag (Typ-Anker, Vertrag.cs) und die Form des Codes gefunden:
//    • Dienst         = DI-Registrierung (Microsoft-API) <Vertrag, Impl> mit Vertrag aus einer Domänen-Assembly.
//    • HostSetting    = bewusst NICHT mehr extrahiert: die Herkunft eines Werts ist Datenfluss durch Ausdrücke (ein
//                       Rumpf-Fakt, nicht verlässlich) — die Liste bleibt leer, der Editor kann Settings nur entwerfen.
//  Fehlt eine Quelle, bleibt die Liste leer — nichts wird erfunden.
// ════════════════════════════════════════════════════════════════════════════

public sealed class CompositionRoot
{
    public List<DienstBinding> Dienste { get; } = new();
    public List<HostSetting> HostSettings { get; } = new();
}

public sealed record DienstBinding(string Name, string Vertrag, string? Impl, bool Extern);
/// <summary>
/// Ein Betriebs-Parameter. <see cref="Konfig"/>/<see cref="Feld"/> = in welches Feld welches Konfigurations-Records
/// er fließt (die Kante HostSetting → Konfig).
/// </summary>
public sealed record HostSetting(string Name, string Typ, string Default, string EnvKey, string? Konfig = null, string? Feld = null);

public sealed class CompositionRootExtractor
{
    private readonly Projektlage _lage;
    private readonly List<(Compilation Comp, SyntaxTree Tree)> _hostQuellen, _domänenQuellen;

    public CompositionRootExtractor(Solution solution, Projektlage lage, RoutingTruth routing, DomainModel dom)
    {
        _lage = lage;
        _hostQuellen = lage.Hosts.SelectMany(h => h.Compilation.SyntaxTrees.Where(t => !lage.Quelltexte.IstGeneriert(t)).Select(t => (h.Compilation, t))).ToList();
        _domänenQuellen = lage.DomänenCompilations.SelectMany(c => c.SyntaxTrees.Where(t => !lage.Quelltexte.IstGeneriert(t)).Select(t => (c, t))).ToList();
    }

    public Task<CompositionRoot> ExtractAsync()
    {
        var cr = new CompositionRoot();
        ExtractServices(cr);
        return Task.FromResult(cr);
    }

    // ── Dienste: DI-Registrierung <Vertrag, Impl> (Microsoft-API) mit Domänen-Vertrag ──
    private void ExtractServices(CompositionRoot cr)
    {
        foreach (var (comp, tree) in _domänenQuellen.Concat(_hostQuellen))
        {
            var model = comp.GetSemanticModel(tree);
            foreach (var inv in tree.GetRoot().DescendantNodes().OfType<InvocationExpressionSyntax>())
            {
                if (DiRegistrierung(inv, model) is not { TypeArguments.Length: >= 1 } m) continue;
                if (m.TypeArguments[0] is not INamedTypeSymbol { TypeKind: TypeKind.Interface } vertrag
                    || !_lage.DomänenAssemblies.Contains(vertrag.ContainingAssembly?.Name ?? "")) continue;
                if (cr.Dienste.Any(d => d.Vertrag == vertrag.Name)) continue;
                // Implementierung: zweites Typ-Argument; sonst der Typ, den die Fabrik liefert bzw. die übergebene Instanz hat.
                var impl = m.TypeArguments.Length > 1 ? m.TypeArguments[1]
                    : inv.ArgumentList.Arguments.Select(a => a.Expression)
                        .Select(e => e is LambdaExpressionSyntax { Body: ExpressionSyntax b } ? b : e)
                        .Select(e => model.GetTypeInfo(e).Type).FirstOrDefault(t => t is INamedTypeSymbol { TypeKind: TypeKind.Class or TypeKind.Struct });
                cr.Dienste.Add(new DienstBinding(Name: impl?.Name ?? vertrag.Name, Vertrag: vertrag.Name, Impl: impl?.Name, Extern: impl == null));
            }
        }
    }

    private static IMethodSymbol? DiRegistrierung(InvocationExpressionSyntax inv, SemanticModel model)
    {
        if (model.GetSymbolInfo(inv).Symbol is not IMethodSymbol m) return null;
        var basis = m.ReducedFrom ?? m;
        var typ = basis.ContainingType?.ToDisplayString();
        return typ == Vertrag.DiErweiterungen || typ == Vertrag.DiTryErweiterungen ? m : null;
    }
}
