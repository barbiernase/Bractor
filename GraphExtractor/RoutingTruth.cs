using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace GraphExtractor;

/// <summary>
/// Liest die AUTORITATIVE Routing-Wahrheit aus dem generierten
/// <c>Infrastructure.Mapping.GeneratedCommandRouting</c> (aus der gebauten Infrastructure-Compilation,
/// die den Roslyn-Generator bereits ausgeführt hat).
///
/// Wir parsen bewusst das GENERAT, nicht die Decider erneut: der Sinn ist die Kopplung an genau die
/// Abbildung, die zur Laufzeit routet (<c>HandlerOutputRouter</c>/<c>ProzessManagerActor</c>). Fällt das
/// Generat aus (nicht gebaut), fällt <see cref="TryFromCompilations"/> auf die Decider-Ableitung zurück und
/// meldet das über <see cref="Source"/>.
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

    public static RoutingTruth FromCompilations(IReadOnlyList<Compilation> compilations)
    {
        var truth = new RoutingTruth();

        // GeneratedCommandRouting liegt in der Infrastructure-Compilation als generierter Syntaxbaum.
        foreach (var comp in compilations)
        {
            var symbol = comp.GetTypeByMetadataName("Infrastructure.Mapping.GeneratedCommandRouting");
            if (symbol == null) continue;

            var syntaxRef = symbol.DeclaringSyntaxReferences.FirstOrDefault();
            if (syntaxRef == null) continue;

            var classNode = syntaxRef.GetSyntax();
            var tree = classNode.SyntaxTree;
            var model = comp.GetSemanticModel(tree);

            if (TryParse(classNode, model, truth))
            {
                truth.Source = "GeneratedCommandRouting";
                return truth;
            }
        }

        // Fallback: aus den Decider-Signaturen ableiten (gleiche Regel wie der Generator).
        DeciderFallback.Fill(compilations, truth);
        truth.Source = "decider-fallback";
        return truth;
    }

    /// <summary>
    /// Parst die beiden Dictionary-Initializer. Robust: wir suchen JEDE Zuweisung <c>[typeof(X)] = …</c>
    /// innerhalb der jeweiligen Property und lesen X über das Semantik-Modell (voll aufgelöst).
    /// </summary>
    private static bool TryParse(SyntaxNode classNode, SemanticModel model, RoutingTruth truth)
    {
        var props = classNode.DescendantNodes().OfType<PropertyDeclarationSyntax>().ToList();
        var aggProp = props.FirstOrDefault(p => p.Identifier.Text == "CommandToAggregate");
        var evtProp = props.FirstOrDefault(p => p.Identifier.Text == "CommandToEvents");
        if (aggProp == null && evtProp == null) return false;

        if (aggProp != null)
        {
            foreach (var (cmd, rhs) in Entries(aggProp, model))
            {
                if (rhs is LiteralExpressionSyntax lit && lit.Token.Value is string agg)
                {
                    truth.CommandToAggregate[cmd.Full] = agg;
                    truth.CommandSimpleName[cmd.Full] = cmd.Simple;
                }
            }
        }

        if (evtProp != null)
        {
            foreach (var (cmd, rhs) in Entries(evtProp, model))
            {
                var events = rhs.DescendantNodesAndSelf()
                    .OfType<TypeOfExpressionSyntax>()
                    .Select(t => model.GetTypeInfo(t.Type).Type)
                    .OfType<INamedTypeSymbol>()
                    .Select(Fq)
                    .Where(s => s.Length > 0)
                    .ToList();

                if (events.Count > 0)
                {
                    truth.CommandToEvents[cmd.Full] = events;
                    truth.CommandSimpleName.TryAdd(cmd.Full, cmd.Simple);
                }
            }
        }

        return truth.CommandToAggregate.Count > 0 || truth.CommandToEvents.Count > 0;
    }

    /// <summary>Alle <c>[typeof(Cmd)] = rhs</c>-Zuweisungen einer Property: liefert (Cmd-Typ, rhs-Ausdruck).</summary>
    private static IEnumerable<(TypeName Cmd, ExpressionSyntax Rhs)> Entries(PropertyDeclarationSyntax prop, SemanticModel model)
    {
        foreach (var asg in prop.DescendantNodes().OfType<AssignmentExpressionSyntax>())
        {
            if (asg.Left is not ImplicitElementAccessSyntax access) continue;
            var arg = access.ArgumentList.Arguments.FirstOrDefault()?.Expression;
            if (arg is not TypeOfExpressionSyntax tof) continue;
            if (model.GetTypeInfo(tof.Type).Type is not INamedTypeSymbol cmdSym) continue;

            yield return (new TypeName(Fq(cmdSym), cmdSym.Name), asg.Right);
        }
    }

    private static string Fq(INamedTypeSymbol s) =>
        s.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat).Replace("global::", "");

    private readonly record struct TypeName(string Full, string Simple);
}
