using System.Collections.Immutable;
using Infrastructure.SourceGeneration;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Domain.SourceGeneration;

/// <summary>
/// <b>CQRS115 (GR-GRAPH)</b> — ein Graph hat die Form, aus der Generator und Laufzeit ihn lesen (<see cref="Graphform"/>, docs/04 §4.4): ein Record mit
/// <c>IGraph</c>, jeder Parameter ein Knoten (eine Funktion, je Funktion höchstens einer), je Command- bzw. Quell-Typ genau ein Kopf, kreisfrei. Schon im
/// Pipeline-Projekt mit Ort; die Regeln über alle Projekte (Empfänger, Funktion ohne Graph) prüft das Gesamtbild.
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class GraphAnalyzer : DiagnosticAnalyzer
{
    public const string Id = "CQRS115";

    private static readonly DiagnosticDescriptor Form = new(Id, "Graph hat nicht die Form",
        "Graph '{0}': {1} (GR-GRAPH)", "CQRS.Graph", DiagnosticSeverity.Error, isEnabledByDefault: true);

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
        if (!Graphform.IstGraph(t)) return;
        foreach (var (text, ort) in Graphform.Lies(t).Fehler)
            ctx.ReportDiagnostic(Diagnostic.Create(Form, ort, t.Name, text));
    }
}
