using Microsoft.CodeAnalysis;

namespace GraphExtractor;

/// <summary>
/// Fallback, falls <c>GeneratedCommandRouting</c> nicht vorliegt: leitet Command→Aggregat und
/// Command→Events aus den <c>IDecider&lt;TState&gt;.Decide(TCommand)</c>-Signaturen ab — dieselbe Regel,
/// die der <c>CommandAggregateMapGenerator</c> anwendet. So bleibt der Graph auch ohne gebaute
/// Infrastructure vollständig; die Provenienz meldet dann „decider-fallback".
/// </summary>
public static class DeciderFallback
{
    public static void Fill(IReadOnlyList<Compilation> comps, RoutingTruth truth)
    {
        INamedTypeSymbol? iDecider = null, iCommand = null, iEvent = null, iTransient = null;
        foreach (var c in comps)
        {
            iDecider ??= c.GetTypeByMetadataName("Abstractions.IDecider`1");
            iCommand ??= c.GetTypeByMetadataName("Abstractions.ICommand");
            iEvent ??= c.GetTypeByMetadataName("Abstractions.IEvent");
            iTransient ??= c.GetTypeByMetadataName("Abstractions.ITransientEvent");
        }
        if (iDecider == null || iCommand == null || iEvent == null) return;

        foreach (var type in Sym.AllConcreteTypes(comps))
        {
            var deciderIface = type.AllInterfaces.FirstOrDefault(i =>
                i.OriginalDefinition.Fq() == iDecider.Fq());
            if (deciderIface == null || deciderIface.TypeArguments.Length != 1) continue;
            if (deciderIface.TypeArguments[0] is not INamedTypeSymbol state) continue;
            var aggName = AggregatName(state);

            foreach (var method in type.GetMembers("Decide").OfType<IMethodSymbol>())
            {
                if (method.Parameters.Length < 1) continue;
                if (method.Parameters[0].Type is not INamedTypeSymbol cmd) continue;
                if (!Sym.Implements(cmd, iCommand)) continue;

                truth.CommandToAggregate[cmd.Fq()] = aggName;
                truth.CommandSimpleName[cmd.Fq()] = cmd.Name;

                var events = ProducedEvents(method.ReturnType, iEvent, iTransient).ToList();
                if (events.Count > 0)
                    truth.CommandToEvents[cmd.Fq()] = events;
            }
        }
    }

    private static string AggregatName(INamedTypeSymbol state)
    {
        var attr = state.GetAttributes().FirstOrDefault(a =>
            a.AttributeClass?.Fq() == "Abstractions.AggregatNameAttribute");
        if (attr is { ConstructorArguments.Length: > 0 } && attr.ConstructorArguments[0].Value is string s && !string.IsNullOrWhiteSpace(s))
            return s;
        return state.Name;
    }

    private static IEnumerable<string> ProducedEvents(ITypeSymbol returnType, INamedTypeSymbol iEvent, INamedTypeSymbol? iTransient)
    {
        if (returnType is not INamedTypeSymbol enumerable || enumerable.TypeArguments.Length != 1) yield break;
        if (enumerable.TypeArguments[0] is not INamedTypeSymbol element) yield break;

        var candidates = element.TypeArguments.Length > 0
            ? element.TypeArguments
            : new[] { (ITypeSymbol)element }.ToImmutableArrayCompat();

        foreach (var ta in candidates)
        {
            if (ta is not INamedTypeSymbol et) continue;
            if (Sym.Implements(et, iEvent) && !Sym.Implements(et, iTransient))
                yield return et.Fq();
        }
    }

    private static System.Collections.Immutable.ImmutableArray<ITypeSymbol> ToImmutableArrayCompat(this ITypeSymbol[] arr)
        => System.Collections.Immutable.ImmutableArray.Create(arr);
}
