using System.Collections.Immutable;
using System.Linq;
using Infrastructure.SourceGeneration;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Domain.SourceGeneration;

/// <summary>
/// <b>CQRS105 (R19)</b> — Zustand einer Funktion nur über <c>IZustand&lt;T&gt;</c> im Konstruktor einer Funktions-Klasse (Plan Pub/Sub-Neustrukturierung
/// §2.9, E6). Eine Funktion ist ein Actor, dessen Aktivierung umziehen, passivieren und neu starten kann: Zustand in einem Feld oder einer Eigenschaft
/// ginge dabei still verloren (und eine gleichbleibende Funktion rechnete beim Wiederholen anders — derselbe Eingang, ein anderer Zähler). Geprüft
/// wird an jeder Funktions-Klasse (Baustein und Umsetzung einer Funktion draußen):
/// <list type="bullet">
/// <item>kein veränderliches Feld (Instanz oder statisch, nicht <c>readonly</c>/<c>const</c>), keine Eigenschaft mit <c>set</c>;</item>
/// <item>kein <c>readonly</c>-Feld einer veränderlichen Sammlung (Liste, Wörterbuch, Menge, Array, <c>StringBuilder</c>) — das wäre Zustand durch die
///   Hintertür;</item>
/// <item><c>IZustand&lt;T&gt;</c> steht nur im Konstruktor einer Funktions-Klasse (nicht in einer Umsetzung einer Schnittstelle — draußen lebt der
///   Zustand im Worker, A8 —, nicht in einem Dienst, Aggregat oder sonst wo).</item>
/// </list>
/// Dienste im Konstruktor (gehalten in <c>readonly</c>-Feldern oder Parametern) sind Ressourcen der Aktivierung, kein Zustand. Nur Code-Fakten:
/// Marker, Konstruktor-Parameter, Feld- und Eigenschafts-Symbole.
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class ZustandAnalyzer : DiagnosticAnalyzer
{
    public const string Id = "CQRS105";

    private static readonly DiagnosticDescriptor Regel = new(Id, "Zustand einer Funktion nur über IZustand<T>",
        "'{0}': {1} — Zustand einer Funktion nur über IZustand<T> im Konstruktor einer Funktions-Klasse; er wird mit der Position gesichert und kommt "
        + "beim Umzug mit, alles andere ginge still verloren (R19)",
        "CQRS.Funktion", DiagnosticSeverity.Error, isEnabledByDefault: true);

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArray.Create(Regel);

    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        context.RegisterSymbolAction(Pruefe, SymbolKind.NamedType);
    }

    private static readonly string[] Sammlungen =
    {
        "System.Collections.Generic.List`1", "System.Collections.Generic.Dictionary`2", "System.Collections.Generic.HashSet`1",
        "System.Collections.Generic.Queue`1", "System.Collections.Generic.Stack`1", "System.Collections.Generic.SortedDictionary`2",
        "System.Collections.Generic.SortedSet`1", "System.Collections.Generic.LinkedList`1", "System.Collections.Generic.SortedList`2",
        "System.Collections.Concurrent.ConcurrentDictionary`2", "System.Collections.Concurrent.ConcurrentQueue`1",
        "System.Collections.Concurrent.ConcurrentBag`1", "System.Collections.Concurrent.ConcurrentStack`1", "System.Text.StringBuilder",
        "System.Collections.ArrayList", "System.Collections.Hashtable",
    };

    private static bool Veraenderlich(ITypeSymbol t) =>
        t is IArrayTypeSymbol || (t is INamedTypeSymbol n && Sammlungen.Contains(Funktionsform.Name(n)));

    private static void Pruefe(SymbolAnalysisContext ctx)
    {
        var t = (INamedTypeSymbol)ctx.Symbol;
        if (t.TypeKind != TypeKind.Class) return;
        var baustein = !t.IsAbstract && Funktionsform.IstBaustein(t);
        var umsetzung = !baustein && Funktionsform.Umsetzt(t).Any(Funktionsform.IstBaustein);

        // IZustand<T> außerhalb einer Funktions-Klasse.
        if (!baustein)
            foreach (var c in t.InstanceConstructors.Where(c => !c.IsImplicitlyDeclared))
                foreach (var p in c.Parameters.Where(p => Funktionsform.IstZustand(p.Type)))
                    ctx.ReportDiagnostic(Diagnostic.Create(Regel, p.Locations.FirstOrDefault() ?? t.Locations.FirstOrDefault(), t.Name, umsetzung
                        ? $"die Umsetzung einer Funktion draußen nimmt '{p.Type.ToDisplayString()}' — Zustand hat nur eine Funktions-Klasse (draußen lebt er im Worker)"
                        : $"'{p.Type.ToDisplayString()}' steht im Konstruktor von etwas, das keine Funktion ist"));

        if (!baustein && !umsetzung) return;
        foreach (var m in t.GetMembers().Where(m => !m.IsImplicitlyDeclared))
        {
            switch (m)
            {
                case IFieldSymbol f when f.IsConst:
                    break;
                case IFieldSymbol f when !f.IsReadOnly:
                    ctx.ReportDiagnostic(Diagnostic.Create(Regel, f.Locations.FirstOrDefault(), t.Name,
                        $"das Feld '{f.Name}' ist veränderlich{(f.IsStatic ? " (statisch)" : "")}"));
                    break;
                case IFieldSymbol f when Veraenderlich(f.Type):
                    ctx.ReportDiagnostic(Diagnostic.Create(Regel, f.Locations.FirstOrDefault(), t.Name,
                        $"das Feld '{f.Name}' hält eine veränderliche Sammlung ({f.Type.ToDisplayString()})"));
                    break;
                case IPropertySymbol p when p.SetMethod is { IsInitOnly: false }:
                    ctx.ReportDiagnostic(Diagnostic.Create(Regel, p.Locations.FirstOrDefault(), t.Name, $"die Eigenschaft '{p.Name}' hat einen Setter"));
                    break;
            }
        }
    }
}
