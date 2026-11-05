using System.Collections.Immutable;
using Infrastructure.SourceGeneration;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Domain.SourceGeneration;

/// <summary>
/// <b>CQRS115 (GR-GRAPH, GR-KANTE, GR-KOPF, GR-EINGANG)</b> — ein Graph hat die Form, aus der Generator und Laufzeit ihn lesen (<see cref="Graphform"/>,
/// docs/04 §4.4): ein Record mit <c>IGraph</c>, jeder Parameter ein Knoten (eine Funktion — auch mehrfach — oder ein Graph als Schachtel mit Signatur)
/// mit <c>[Kopf]</c> oder <c>[Von(nameof(…))]</c>; jede genannte Quelle speist einen Port, jeder Port hat eine Quelle oder kommt an einem Kopf von außen,
/// je Eingangstyp genau ein Kopf, kreisfrei über die Knoten; die Erzeuger eines Eingangs auf einer Ebene der Unterläufe (bis die Laufzeit ohne Lauf
/// auskommt). Dass ein Auf genau eine Nachricht nimmt, prüft die Funktionsform (CQRS100). Schon im Pipeline-Projekt mit Ort; die Regeln über alle Projekte (Empfänger, Funktion ohne
/// Graph) prüft das Gesamtbild.
/// <para><b>CQRS119 (GR-UEBERSETZUNG)</b> — jeder Parameter der Nachricht, die eine Übersetzung erzeugt, hat eine Quelle (<c>[Feld]</c>, gleicher Name
/// mit implizit zuweisbarem Typ, Standardwert); ein <c>[Feld]</c> nennt ein Ziel und eine Quelle, die es gibt, und steht nur an einer Übersetzung.
/// <b>CQRS121</b> — eine Übersetzung nimmt einen Command ohne Ziel und erzeugt einen Command (<see cref="Uebersetzungsform"/>).</para>
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class GraphAnalyzer : DiagnosticAnalyzer
{
    public const string Id = "CQRS115";

    private static readonly DiagnosticDescriptor Form = new(Id, "Graph hat nicht die Form",
        "Graph '{0}': {1} (GR-GRAPH, GR-KANTE, GR-KOPF, GR-EINGANG)", "CQRS.Graph", DiagnosticSeverity.Error, isEnabledByDefault: true);

    private static readonly DiagnosticDescriptor Zuordnung = new(Uebersetzungsform.Zuordnung, "Übersetzung ohne vollständige Zuordnung",
        "Graph '{0}': {1} (GR-UEBERSETZUNG)", "CQRS.Graph", DiagnosticSeverity.Error, isEnabledByDefault: true);

    private static readonly DiagnosticDescriptor Nachricht = new(Uebersetzungsform.Nachricht, "Übersetzung nimmt bzw. erzeugt keine Nachricht",
        "Graph '{0}': {1} (GR-UEBERSETZUNG)", "CQRS.Graph", DiagnosticSeverity.Error, isEnabledByDefault: true);

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArray.Create(Form, Zuordnung, Nachricht);

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
        var g = Graphform.Lies(t);
        foreach (var (text, ort) in g.Fehler)
            ctx.ReportDiagnostic(Diagnostic.Create(Form, ort, t.Name, text));
        foreach (var (id, text, ort) in g.Regelfehler)
            ctx.ReportDiagnostic(Diagnostic.Create(id == Uebersetzungsform.Nachricht ? Nachricht : Zuordnung, ort, t.Name, text));
    }
}
