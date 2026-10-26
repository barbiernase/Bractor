using System.Collections.Immutable;
using System.Linq;
using Infrastructure.SourceGeneration;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Domain.SourceGeneration;

/// <summary>
/// CQRS101 (R2, Plan Pub/Sub-Neustrukturierung §3.6) — je Baustein und Eingangstyp höchstens EINE Methode, die ihn nimmt: ein <c>Auf</c> einer Funktion,
/// ein <c>Handle</c> einer Projektion oder eines Readers. Zwei Überladungen für denselben Eingang (etwa mit und ohne <c>Herkunft</c>)
/// machten unklar, was der Eingang auslöst — der Dispatch fände zwei Fälle. Schon im Domänen-Projekt, nicht erst beim Generieren in der Komposition.
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class EinEingangAnalyzer : DiagnosticAnalyzer
{
    public const string Id = "CQRS101";

    private static readonly DiagnosticDescriptor Doppelt = new(Id, "Zwei Methoden für denselben Eingang",
        "'{0}' hat mehr als ein {2} für '{1}' — je Baustein und Eingangstyp höchstens eines, sonst wäre unklar, was der Eingang auslöst (R2)",
        "CQRS.Funktion", DiagnosticSeverity.Error, isEnabledByDefault: true);

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArray.Create(Doppelt);

    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        context.RegisterSymbolAction(Pruefe, SymbolKind.NamedType);
    }

    private static void Pruefe(SymbolAnalysisContext ctx)
    {
        var t = (INamedTypeSymbol)ctx.Symbol;
        if (Funktionsform.IstBaustein(t))
        {
            foreach (var (eingang, ort) in Funktionsform.Lies(t).Doppelt)
                ctx.ReportDiagnostic(Diagnostic.Create(Doppelt, ort, t.Name, eingang.Name, Abstractions.Funktionsvertrag.Auf));
            return;
        }
        if (t.TypeKind != TypeKind.Class || !(Funktionsform.Hat(t, "Abstractions.ISubscriber") || t.AllInterfaces.Any(i => Funktionsform.Name(i) == "Abstractions.IReader`1")))
            return;
        foreach (var g in t.GetMembers("Handle").OfType<IMethodSymbol>()
                     .Where(m => m.MethodKind == MethodKind.Ordinary && m.Parameters.Length > 0)
                     .GroupBy(m => Funktionsform.Name(m.Parameters[0].Type))
                     .Where(g => g.Count() > 1))
            foreach (var m in g.Skip(1))
                ctx.ReportDiagnostic(Diagnostic.Create(Doppelt, m.Locations.FirstOrDefault() ?? Location.None, t.Name, m.Parameters[0].Type.Name, "Handle"));
    }
}
