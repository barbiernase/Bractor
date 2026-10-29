using System.Collections.Immutable;
using System.Linq;
using Infrastructure.SourceGeneration;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Domain.SourceGeneration;

/// <summary>
/// <b>CQRS057 — Form von Projektion und Reader</b> (docs/10 §10.3, R1, R2). Generatoren, Weckplan und Extractor lesen beide
/// über dieselbe Lesart (<see cref="Projektionsform"/>) und rufen nur diese Formen auf:
///  • Projektion (<c>IProjektion</c>): <c>Task Auf(TEvent e[, Herkunft h], Schreib-Fähigkeiten…[, CancellationToken ct])</c>
///  • Reader (<c>IReader</c>): <c>Frage(TQuery q, Lese-Fähigkeiten…[, CancellationToken ct])</c> → Antwort bzw. <c>OneOf&lt;…&gt;</c>, auch als Task
///  • Aggregat (Frage im Decider, docs/03 §3.2): <c>Frage(TQuery q)</c> → Antwort bzw. <c>OneOf&lt;…&gt;</c>, synchron; die Query nennt ihr Aggregat mit
///    genau einem Glied <c>Id&lt;TAggregat&gt;</c>
/// Weicht eine Methode davon ab (falsche Parameter, falsche Rückgabe, ein anderer Name für einen Eingang; zwei für denselben Eingang meldet CQRS101), würde der
/// Editor sie zeigen, die Laufzeit sie aber still übergehen. Das ist hier ein Build-Fehler.
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class HandlerFormAnalyzer : DiagnosticAnalyzer
{
    public const string FormId = "CQRS057";

    private static readonly DiagnosticDescriptor Form = new(
        FormId, "Projektion, Reader oder Frage an ein Aggregat in falscher Form",
        "'{0}': {1}",
        "CQRS.Vertrag", DiagnosticSeverity.Error, isEnabledByDefault: true);

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArray.Create(Form);

    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        context.RegisterSymbolAction(Pruefe, SymbolKind.NamedType);
    }

    private static void Pruefe(SymbolAnalysisContext ctx)
    {
        var t = (INamedTypeSymbol)ctx.Symbol;
        if (Projektionsform.IstProjektion(t))
        {
            var p = Projektionsform.LiesProjektion(t);
            foreach (var (text, ort) in p.Fehler) ctx.ReportDiagnostic(Diagnostic.Create(Form, ort, t.Name, text));
            Fremde(ctx, t, "Abstractions.IEvent", Abstractions.Projektionsvertrag.Auf);
        }
        else if (Projektionsform.IstReader(t))
        {
            var r = Projektionsform.LiesReader(t);
            foreach (var (text, ort) in r.Fehler) ctx.ReportDiagnostic(Diagnostic.Create(Form, ort, t.Name, text));
            Fremde(ctx, t, "Abstractions.IQuery", Abstractions.Projektionsvertrag.Frage);
        }
        else if (Projektionsform.StateVonDecider(t) is { } state)
        {
            var a = Projektionsform.LiesAggregatfragen(t, state);
            foreach (var (text, ort) in a.Fehler) ctx.ReportDiagnostic(Diagnostic.Create(Form, ort, state.Name, text));
            foreach (var (query, ort) in a.Doppelt)
                ctx.ReportDiagnostic(Diagnostic.Create(Form, ort, state.Name, $"zwei Fragen nehmen '{query.Name}' — je Query eine Frage"));
            Fremde(ctx, t, "Abstractions.IQuery", Abstractions.Projektionsvertrag.Frage);
        }
    }

    /// <summary>Eine öffentliche Methode, die einen Eingang nimmt, aber anders heißt — sie würde nie aufgerufen.</summary>
    private static void Fremde(SymbolAnalysisContext ctx, INamedTypeSymbol t, string eingang, string name)
    {
        foreach (var m in t.GetMembers().OfType<IMethodSymbol>())
            if (m.MethodKind == MethodKind.Ordinary && !m.IsStatic && m.DeclaredAccessibility == Accessibility.Public && m.Name != name
                && m.Parameters.Length > 0 && Projektionsform.Hat(m.Parameters[0].Type, eingang))
                ctx.ReportDiagnostic(Diagnostic.Create(Form, m.Locations.FirstOrDefault(), t.Name,
                    $"'{m.Name}' nimmt '{m.Parameters[0].Type.Name}', wird aber nie aufgerufen — der Dispatch ruft nur '{name}'"));
    }
}
