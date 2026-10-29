using System.Collections.Immutable;
using System.Linq;
using Infrastructure.SourceGeneration;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Domain.SourceGeneration;

/// <summary>
/// <b>CQRS107</b> — der Lebenszyklus (<c>ILebenszyklus</c>, docs/04 §4.4, docs/10 E10) gehört an eine Funktions-Klasse im Host (Baustein oder
/// Umsetzung einer Funktion draußen): nur dort ruft ihn der Funktions-Actor. An einer Schnittstelle wäre er wirkungslos (draußen gehört der Lebenszyklus
/// dem Worker), an einem Dienst ebenso (die Funktion ruft ihren Dienst selbst, in der Reihenfolge, die sie kennt). <c>VorDemSichernAsync</c> ruft der
/// Actor nur bei einer Funktion mit Zustand (<c>IZustand&lt;T&gt;</c>) — ohne Zustand liefe eine Umsetzung davon nie. Nur Code-Fakten: Marker,
/// Interface-Implementierungen, Konstruktor-Parameter.
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class LebenszyklusAnalyzer : DiagnosticAnalyzer
{
    public const string Id = "CQRS107";

    private static readonly DiagnosticDescriptor Regel = new(Id, "Lebenszyklus nur an einer Funktions-Klasse",
        "'{0}': {1} (ILebenszyklus, docs/04 §4.4)", "CQRS.Funktion", DiagnosticSeverity.Error, isEnabledByDefault: true);

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArray.Create(Regel);

    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        context.RegisterSymbolAction(Pruefe, SymbolKind.NamedType);
    }

    private static void Pruefe(SymbolAnalysisContext ctx)
    {
        var t = (INamedTypeSymbol)ctx.Symbol;
        var lz = t.AllInterfaces.FirstOrDefault(i => Funktionsform.Name(i) == Abstractions.Funktionsvertrag.Lebenszyklus);
        if (lz == null) return;
        var ort = t.Locations.FirstOrDefault();
        if (t.TypeKind == TypeKind.Interface)
        {
            ctx.ReportDiagnostic(Diagnostic.Create(Regel, ort, t.Name, "eine Schnittstelle hat keinen Lebenszyklus — draußen gehört er dem Worker, im Host der Umsetzung"));
            return;
        }
        if (t.TypeKind != TypeKind.Class || t.IsAbstract) return;
        var funktion = Funktionsform.IstBaustein(t) || Funktionsform.Umsetzt(t).Any(Funktionsform.IstBaustein);
        if (!funktion)
        {
            ctx.ReportDiagnostic(Diagnostic.Create(Regel, ort, t.Name,
                "nur eine Funktions-Klasse hat einen Lebenszyklus (ein Dienst wird von seiner Funktion geladen, freigegeben über IAsyncDisposable)"));
            return;
        }
        var vorDemSichern = lz.GetMembers("VorDemSichernAsync").OfType<IMethodSymbol>().FirstOrDefault();
        if (vorDemSichern != null && t.FindImplementationForInterfaceMember(vorDemSichern) is IMethodSymbol impl
            && !SymbolEqualityComparer.Default.Equals(impl.ContainingType, lz)
            && !Funktionsform.ZustandsParameter(t).Any())
            ctx.ReportDiagnostic(Diagnostic.Create(Regel, impl.Locations.FirstOrDefault() ?? ort, t.Name,
                "VorDemSichernAsync ohne Zustand wird nie gerufen — gesichert wird nur ein IZustand<T> im Konstruktor"));
    }
}
