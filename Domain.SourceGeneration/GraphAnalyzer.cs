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
/// <para>Eine ÜBERSETZUNG ist eine eigene Klasse der Anwendung (<c>ITranslate&lt;TSource, TTarget&gt;</c>), kein Knoten. <b>CQRS119 (GR-UEBERSETZUNG)</b> —
/// ihr Konstruktor nimmt genau die Quelle; ob <c>Translate()</c> das Ziel vollständig baut, prüft der Compiler.
/// <b>CQRS121</b> — sie nimmt einen Command ohne Ziel oder ein Event (eine Antwort) und erzeugt einen Command (<see cref="Uebersetzungsform"/>).</para>
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class GraphAnalyzer : DiagnosticAnalyzer
{
    public const string Id = "CQRS115";

    private static readonly DiagnosticDescriptor Form = new(Id, "Graph hat nicht die Form",
        "Graph '{0}': {1} (GR-GRAPH, GR-KANTE, GR-KOPF, GR-EINGANG)", "CQRS.Graph", DiagnosticSeverity.Error, isEnabledByDefault: true);

    private static readonly DiagnosticDescriptor Zuordnung = new(Uebersetzungsform.Zuordnung, "Übersetzung nimmt nicht ihre Quelle",
        "Übersetzung '{0}': {1} (GR-UEBERSETZUNG)", "CQRS.Graph", DiagnosticSeverity.Error, isEnabledByDefault: true);

    private static readonly DiagnosticDescriptor Nachricht = new(Uebersetzungsform.Nachricht, "Übersetzung nimmt bzw. erzeugt keine Nachricht",
        "Übersetzung '{0}': {1} (GR-UEBERSETZUNG)", "CQRS.Graph", DiagnosticSeverity.Error, isEnabledByDefault: true);

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
        if (Uebersetzungsform.IstUebersetzung(t))
        {
            foreach (var (id, text, ort) in Uebersetzungsform.Lies(t).Fehler)
                ctx.ReportDiagnostic(Diagnostic.Create(id == Uebersetzungsform.Nachricht ? Nachricht : Zuordnung, ort, t.Name, text));
            return;
        }
        if (!Graphform.IstGraph(t)) return;
        foreach (var (text, ort) in Graphform.Lies(t).Fehler)
            ctx.ReportDiagnostic(Diagnostic.Create(Form, ort, t.Name, text));
    }
}
