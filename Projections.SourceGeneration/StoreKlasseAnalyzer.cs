using System.Collections.Immutable;
using System.Linq;
using Infrastructure.SourceGeneration;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Projections.SourceGeneration;

/// <summary>
/// <b>CQRS109 — die Klasse eines Stores (R14, docs/04 §4.3).</b> Ein Store, in den eine Projektion schreibt (eine Klasse mit
/// Schreib-Fähigkeiten), implementiert GENAU EINE Store-Klasse: <c>IAtomaresZiel</c> (1), <c>IWiederholungsfestesZiel</c> (2) oder
/// <c>INurMindestensEinmal</c> (3) — davon hängt ab, wie der Batch-Leser ihn schreibt. Ein Store der Klasse 2 bekommt den Fortschritt vom Framework
/// erst nach seinen Wirkungen; damit eine Wiederholung dasselbe ergibt, braucht jede Schreib-Fähigkeit die Event-Identität — sie nimmt die
/// <c>Herkunft</c>. Dass ein Treiber seine Klasse einhält, zeigt der allgemeine Store-Test; dass eine Projektion nur mit Erlaubnis in Klasse 3 schreibt,
/// prüft die Komposition (CQRS110).
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class StoreKlasseAnalyzer : DiagnosticAnalyzer
{
    public const string Id = "CQRS109";

    private static readonly DiagnosticDescriptor Klasse = new(
        Id, "Store ohne genau eine Klasse",
        "'{0}' {1}",
        "CQRS.Store", DiagnosticSeverity.Error, isEnabledByDefault: true);

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArray.Create(Klasse);

    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        context.RegisterSymbolAction(Pruefe, SymbolKind.NamedType);
    }

    private static void Pruefe(SymbolAnalysisContext ctx)
    {
        var t = (INamedTypeSymbol)ctx.Symbol;
        if (t.TypeKind != TypeKind.Class || t.IsAbstract) return;
        var schreib = t.AllInterfaces.Where(Projektionsform.IstSchreibFaehigkeit).ToList();
        if (schreib.Count == 0) return;
        var ort = t.Locations.FirstOrDefault();
        var klassen = Projektionsform.StoreKlassen(t);
        if (klassen.Count != 1)
        {
            ctx.ReportDiagnostic(Diagnostic.Create(Klasse, ort, t.Name, klassen.Count == 0
                ? "hat Schreib-Fähigkeiten, aber keine Store-Klasse — ein Store implementiert genau eine: IAtomaresZiel (Wirkung und Fortschritt in einer "
                  + "Transaktion), IWiederholungsfestesZiel (jede Wirkung wiederholbar) oder INurMindestensEinmal"
                : $"implementiert {klassen.Count} Store-Klassen ({string.Join(", ", klassen)}) — ein Store implementiert genau eine"));
            return;
        }
        if (klassen[0] != 2) return;
        foreach (var f in schreib)
            foreach (var m in f.GetMembers().OfType<IMethodSymbol>().Where(m => m.MethodKind == MethodKind.Ordinary))
                if (!m.Parameters.Any(p => Projektionsform.Name(p.Type) == Abstractions.Projektionsvertrag.Herkunft))
                    ctx.ReportDiagnostic(Diagnostic.Create(Klasse, ort, t.Name,
                        $"ist ein Store der Klasse 2 (IWiederholungsfestesZiel), aber die Schreib-Fähigkeit '{f.Name}.{m.Name}' nimmt keine Herkunft — "
                        + "eine wiederholbare Wirkung braucht die Event-Identität (Stream und Version im Schlüssel oder als Bedingung)"));
    }
}
