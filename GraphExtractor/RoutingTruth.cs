using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace GraphExtractor;

/// <summary>
/// Liest die AUTORITATIVE Routing-Wahrheit aus dem GENERAT des Framework-Generators — dieselbe Abbildung, die zur
/// Laufzeit routet. Das Generat wird über den Vertrags-Anker <c>[RoutingTabelle(Art)]</c> an seinen Properties erkannt
/// (weder Klassen-/Property-Namen noch eine Syntaxform werden vorausgesetzt); die Einträge <c>[typeof(Command)] = …</c>
/// werden per Symbol gelesen. Fehlt es (nicht gebaut), fällt <see cref="FromCompilations"/> auf die Decider-Ableitung
/// zurück und meldet das über <see cref="Source"/>.
/// </summary>
public sealed class RoutingTruth
{
    /// <summary>Command-FullName → Aggregat-Name.</summary>
    public Dictionary<string, string> CommandToAggregate { get; } = new();

    /// <summary>Command-FullName → produzierte (persistierte) Event-FullNames.</summary>
    public Dictionary<string, List<string>> CommandToEvents { get; } = new();

    /// <summary>Command-FullName → einfacher Name (für Node-Ids).</summary>
    public Dictionary<string, string> CommandSimpleName { get; } = new();

    public string Source { get; private set; } = "unbekannt";

    /// <summary>Enthält die Compilation eine generierte Command→Aggregat-Tabelle?</summary>
    public static bool HatRouting(Compilation comp) => RoutingProperties(comp).Any(x => x.Art == Art.Aggregat);

    public static RoutingTruth FromCompilations(IReadOnlyList<Compilation> compilations)
    {
        var truth = new RoutingTruth();
        foreach (var comp in compilations)
        {
            var props = RoutingProperties(comp).ToList();
            if (!props.Any(x => x.Art == Art.Aggregat)) continue;
            foreach (var (prop, model, art) in props)
                foreach (var (cmd, rhs) in Entries(prop, model))
                {
                    truth.CommandSimpleName.TryAdd(cmd.Full, cmd.Simple);
                    if (art == Art.Aggregat && model.GetConstantValue(rhs) is { HasValue: true, Value: string agg })
                        truth.CommandToAggregate[cmd.Full] = agg;
                    else if (art == Art.Events)
                    {
                        var events = rhs.DescendantNodesAndSelf().OfType<TypeOfExpressionSyntax>()
                            .Select(t => model.GetTypeInfo(t.Type).Type).OfType<INamedTypeSymbol>()
                            .Select(Fq).Where(x => x.Length > 0).ToList();
                        if (events.Count > 0) truth.CommandToEvents[cmd.Full] = events;
                    }
                }
            truth.Source = "GeneratedCommandRouting";
            return truth;
        }

        DeciderFallback.Fill(compilations, truth);
        truth.Source = "decider-fallback";
        return truth;
    }

    private enum Art { Aggregat, Events }

    private static IEnumerable<(PropertyDeclarationSyntax Prop, SemanticModel Model, Art Art)> RoutingProperties(Compilation comp)
    {
        var anker = comp.GetTypeByMetadataName(Vertrag.RoutingTabelleAttribute);
        if (anker == null) yield break;
        foreach (var tree in comp.SyntaxTrees.Where(Projektlage.IstGeneriert))
        {
            var model = comp.GetSemanticModel(tree);
            foreach (var prop in tree.GetRoot().DescendantNodes().OfType<PropertyDeclarationSyntax>().Where(p => p.AttributeLists.Count > 0))
            {
                if (model.GetDeclaredSymbol(prop) is not IPropertySymbol ps) continue;
                var attr = ps.GetAttributes().FirstOrDefault(a => a.AttributeClass?.ToDisplayString() == Vertrag.RoutingTabelleAttribute);
                if (attr?.ConstructorArguments.FirstOrDefault().Value is not int art) continue;
                if (art == (int)Abstractions.RoutingArt.CommandZuAggregat) yield return (prop, model, Art.Aggregat);
                else if (art == (int)Abstractions.RoutingArt.CommandZuEvents) yield return (prop, model, Art.Events);
            }
        }
    }

    private readonly record struct Eintrag(TypeName Cmd, ExpressionSyntax Rhs, INamedTypeSymbol Symbol)
    {
        public void Deconstruct(out TypeName cmd, out ExpressionSyntax rhs) { cmd = Cmd; rhs = Rhs; }
    }

    /// <summary>Alle <c>[typeof(X)] = rhs</c>-Zuweisungen einer Property: liefert (X, rhs-Ausdruck).</summary>
    private static IEnumerable<Eintrag> Entries(PropertyDeclarationSyntax prop, SemanticModel model)
    {
        foreach (var asg in prop.DescendantNodes().OfType<AssignmentExpressionSyntax>())
        {
            if (asg.Left is not ImplicitElementAccessSyntax access) continue;
            if (access.ArgumentList.Arguments.FirstOrDefault()?.Expression is not TypeOfExpressionSyntax tof) continue;
            if (model.GetTypeInfo(tof.Type).Type is not INamedTypeSymbol sym) continue;
            yield return new Eintrag(new TypeName(Fq(sym), sym.Name), asg.Right, sym);
        }
    }

    private static string Fq(INamedTypeSymbol s) =>
        s.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat).Replace("global::", "");

    private readonly record struct TypeName(string Full, string Simple);
}
