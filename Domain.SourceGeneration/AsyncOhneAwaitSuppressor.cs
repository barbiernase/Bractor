// In Projekt: Domain.SourceGeneration
// Dateiname: AsyncOhneAwaitSuppressor.cs

using System.Collections.Immutable;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Domain.SourceGeneration
{
    /// <summary>
    /// Ein <c>Auf</c> bzw. <c>Lausche</c> einer Funktion ist immer asynchron (docs/04 §4.4: <c>Task&lt;T&gt;</c>, <c>Task&lt;OneOf&lt;…&gt;&gt;</c> oder
    /// <c>IAsyncEnumerable&lt;…&gt;</c>) — auch wenn es nur rechnet. Damit eine reine Rechnung so schlicht bleibt wie
    /// <c>public async Task&lt;Merkmal&gt; Auf(Messwert m) =&gt; new(m.Stelle, m.Wert);</c>, unterdrückt dieser Suppressor dort die Warnung „async ohne
    /// await“ (CS1998). Überall sonst bleibt sie.
    /// </summary>
    [DiagnosticAnalyzer(LanguageNames.CSharp)]
    public sealed class AsyncOhneAwaitSuppressor : DiagnosticSuppressor
    {
        private static readonly SuppressionDescriptor Regel = new("CQRS-ASYNC-AUF", "CS1998",
            "Ein Auf einer Funktion ist immer asynchron — auch eine reine Rechnung ohne await (docs/04 §4.4).");

        public override ImmutableArray<SuppressionDescriptor> SupportedSuppressions => ImmutableArray.Create(Regel);

        public override void ReportSuppressions(SuppressionAnalysisContext context)
        {
            foreach (var d in context.ReportedDiagnostics)
            {
                var baum = d.Location.SourceTree;
                if (baum == null) continue;
                var modell = context.GetSemanticModel(baum);
                var knoten = baum.GetRoot(context.CancellationToken).FindNode(d.Location.SourceSpan);
                var symbol = modell.GetDeclaredSymbol(knoten, context.CancellationToken) as IMethodSymbol
                    ?? knoten.AncestorsAndSelf().Select(k => modell.GetDeclaredSymbol(k, context.CancellationToken)).OfType<IMethodSymbol>().FirstOrDefault();
                if (symbol == null || symbol.Name is not (Abstractions.Funktionsvertrag.Auf or Abstractions.Funktionsvertrag.Lausche)) continue;
                if (symbol.ContainingType?.AllInterfaces.Any(i => i.ToDisplayString() == "Abstractions.IFunktion") == true)
                    context.ReportSuppression(Suppression.Create(Regel, d));
            }
        }
    }
}
