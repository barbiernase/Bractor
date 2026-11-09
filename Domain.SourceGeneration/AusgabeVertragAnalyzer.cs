using System.Collections.Immutable;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Domain.SourceGeneration;

/// <summary>
/// <b>CQRS050 — geschlossener Ausgabe-Vertrag.</b> WAS ein Decider oder Handler erzeugen kann, steht in seiner
/// SIGNATUR — als konkreter Typ oder als <c>OneOf&lt;A, B, …&gt;</c> konkreter Typen. Der Compiler garantiert dann, dass
/// der Rumpf nichts anderes liefern kann; Generatoren (Command→Event-Map, Azyklizitäts-Guard), Graph, Editor
/// und LLM-Kontext lesen die Menge aus dem Typ — ohne Rumpf-Analyse, die grundsätzlich unvollständig bleibt
/// (Variablen mit Interface-Typ, Hilfsmethoden, Schleifen über Listen, Fabriken … sind syntaktisch nicht zu erschöpfen).
///
/// Geprüft (Rolle über Marker/Parameter-Typen, nie über Namen):
///  • Decide — Klasse implementiert <c>IDecider&lt;T&gt;</c>, 1. Parameter <c>ICommand</c>: <c>IEnumerable&lt;X&gt;</c>.
///  • Reader — <c>IReader</c>, <c>Frage</c> mit 1. Parameter <c>IQuery</c>: <c>Task&lt;X&gt;</c>/<c>ValueTask&lt;X&gt;</c>/<c>X</c>.
///  • Frage an ein Aggregat — im Decider, <c>Frage</c> mit 1. Parameter <c>IQuery</c>: <c>X</c> (docs/03 §3.2).
/// Eine Projektion gibt nichts aus (ihr <c>Auf</c> liefert <c>Task</c>, CQRS057).
/// Die Ausgabe einer Funktion (<c>Auf</c>) prüft die Funktionsform (CQRS100).
/// Offen ist X (bzw. ein OneOf-Typargument), wenn es ein Interface, ein Typ-Parameter, <c>object</c> oder eine abstrakte
/// Klasse ist. <c>Task</c>/<c>ValueTask</c>/<c>void</c> ohne Ergebnis sind erlaubt (reine Effekte, z. B. Projektionen).
///
/// <b>CQRS114 — ein Decide gibt nur Events, Hinweise und Fristen aus</b> (docs/03, docs/konzept-akteure.md §3): keinen Command, weder an ein
/// Aggregat noch ohne Ziel. Was nach einem Fakt geschehen soll, stößt ein Client an, der darauf reagiert; Aggregate reden über Graphen und Clients.
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class AusgabeVertragAnalyzer : DiagnosticAnalyzer
{
    public const string OffenId = "CQRS050";

    private static readonly DiagnosticDescriptor Offen = new(
        OffenId,
        "Offener Ausgabe-Vertrag",
        "'{0}' gibt '{1}' zurück — '{2}' ist kein konkreter Typ. Was ein Decider/Handler erzeugen kann, muss in der " +
        "Signatur stehen: ein konkreter Typ oder OneOf<A, B, …> konkreter Typen.",
        "CQRS.Vertrag",
        DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        description: "Die Menge möglicher Ausgaben ist ein Typ-Fakt (vom Compiler garantiert), kein Rumpf-Fakt. " +
                     "Eine offene Signatur (z. B. IAsyncEnumerable<ICommand>) würde Generatoren und Editor auf eine " +
                     "grundsätzlich unvollständige Rumpf-Analyse zurückwerfen.");

    public const string AggregatCommandId = "CQRS114";

    private static readonly DiagnosticDescriptor AggregatCommand = new(
        AggregatCommandId,
        "Decide gibt einen Command aus",
        "'{0}' gibt den Command '{1}' aus — ein Decide gibt nur Events, Hinweise und Fristen aus: gib ein Event aus, das den Fakt nennt, und lass einen "
        + "Client darauf reagieren (IHoert am Akteur), der den Command hineingibt (GR-DECIDE-AUSGABE)",
        "CQRS.Vertrag",
        DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArray.Create(Offen, AggregatCommand);

    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        context.RegisterCompilationStartAction(start =>
        {
            var c = start.Compilation;
            var m = new Marker(
                c.GetTypeByMetadataName("Abstractions.IDecider`1"),
                c.GetTypeByMetadataName("Abstractions.ICommand"),
                c.GetTypeByMetadataName("Abstractions.IEvent"),
                c.GetTypeByMetadataName("Abstractions.IQuery"),
                c.GetTypeByMetadataName("Abstractions.IReader"),
                c.GetTypeByMetadataName("Abstractions.IAggregatCommand"));
            if (m.ICommand == null) return;   // kein CQRS-Vertrag in dieser Compilation
            start.RegisterSymbolAction(ctx => Pruefe(ctx, m), SymbolKind.Method);
        });
    }

    private sealed class Marker
    {
        public readonly INamedTypeSymbol? IDecider, ICommand, IEvent, IQuery, IReader, IAggregatCommand;
        public Marker(INamedTypeSymbol? d, INamedTypeSymbol? c, INamedTypeSymbol? e, INamedTypeSymbol? q, INamedTypeSymbol? r, INamedTypeSymbol? a)
        { IDecider = d; ICommand = c; IEvent = e; IQuery = q; IReader = r; IAggregatCommand = a; }
    }

    private static void Pruefe(SymbolAnalysisContext ctx, Marker m)
    {
        var mm = (IMethodSymbol)ctx.Symbol;
        if (mm.MethodKind != MethodKind.Ordinary || mm.IsStatic || mm.DeclaredAccessibility != Accessibility.Public) return;
        if (mm.Parameters.Length == 0 || mm.ContainingType is not { } typ) return;
        var p0 = mm.Parameters[0].Type;

        bool Hat(INamedTypeSymbol? generischOderMarker) => generischOderMarker != null && typ.AllInterfaces.Any(i =>
            SymbolEqualityComparer.Default.Equals(i.OriginalDefinition, generischOderMarker));
        bool Ist(ITypeSymbol t, INamedTypeSymbol? marker) => marker != null &&
            (SymbolEqualityComparer.Default.Equals(t, marker) || t.AllInterfaces.Any(i => SymbolEqualityComparer.Default.Equals(i, marker)));

        ITypeSymbol? element = null;
        var rt = mm.ReturnType;
        var decide = Hat(m.IDecider) && Ist(p0, m.ICommand);
        if (decide)
            element = Element(rt, enumerable: true, task: false);
        else if (Hat(m.IDecider) && mm.Name == Abstractions.Projektionsvertrag.Frage && Ist(p0, m.IQuery))
            element = IstOhneErgebnis(rt) ? null : rt;   // die Frage an ein Aggregat (docs/03 §3.2): synchron, die Antwort steht in der Signatur
        else if (Ist(typ, m.IReader) && mm.Name == Abstractions.Projektionsvertrag.Frage && Ist(p0, m.IQuery))
            element = Element(rt, enumerable: false, task: true) ?? (IstOhneErgebnis(rt) ? null : rt);
        else return;
        if (element == null) return;   // Task/void: reine Effekte

        if (decide && m.ICommand != null)
            foreach (var fall in Teile(element).Where(t => Ist(t, m.ICommand)))
                ctx.ReportDiagnostic(Diagnostic.Create(AggregatCommand, mm.Locations.FirstOrDefault(), typ.Name + "." + mm.Name + "(" + p0.Name + ")", fall.Name));

        var offen = OffeneTypen(element).FirstOrDefault();
        if (offen == null) return;
        var ort = mm.DeclaringSyntaxReferences.Select(r => r.GetSyntax(ctx.CancellationToken)).OfType<MethodDeclarationSyntax>()
            .FirstOrDefault()?.ReturnType.GetLocation() ?? mm.Locations.FirstOrDefault();
        ctx.ReportDiagnostic(Diagnostic.Create(Offen, ort,
            typ.Name + "." + mm.Name + "(" + p0.Name + ")",
            rt.ToDisplayString(SymbolDisplayFormat.MinimallyQualifiedFormat),
            offen.ToDisplayString(SymbolDisplayFormat.MinimallyQualifiedFormat)));
    }

    /// <summary>Das Element-/Ergebnis-Typargument von <c>IEnumerable/IAsyncEnumerable&lt;X&gt;</c> bzw. <c>Task/ValueTask&lt;X&gt;</c>.</summary>
    private static ITypeSymbol? Element(ITypeSymbol rt, bool enumerable, bool task)
    {
        if (rt is not INamedTypeSymbol { IsGenericType: true, TypeArguments.Length: 1 } n) return null;
        var def = n.OriginalDefinition.ToDisplayString();
        if (enumerable && (def == "System.Collections.Generic.IEnumerable<T>" || def == "System.Collections.Generic.IAsyncEnumerable<T>"))
            return n.TypeArguments[0];
        if (task && (def == "System.Threading.Tasks.Task<TResult>" || def == "System.Threading.Tasks.ValueTask<TResult>"))
            return n.TypeArguments[0];
        return null;
    }

    private static bool IstOhneErgebnis(ITypeSymbol rt) =>
        rt.SpecialType == SpecialType.System_Void
        || rt.ToDisplayString() is "System.Threading.Tasks.Task" or "System.Threading.Tasks.ValueTask";

    /// <summary>Die Fälle von X (bei OneOf: seine Typargumente).</summary>
    private static System.Collections.Generic.IEnumerable<ITypeSymbol> Teile(ITypeSymbol x) =>
        x is INamedTypeSymbol { Name: "OneOf" } o && o.ContainingNamespace?.ToDisplayString() == "Abstractions" ? o.TypeArguments.AsEnumerable() : new[] { x };

    /// <summary>Die nicht-konkreten Typen in X (bei OneOf: in seinen Typargumenten).</summary>
    private static System.Collections.Generic.IEnumerable<ITypeSymbol> OffeneTypen(ITypeSymbol x)
    {
        var teile = Teile(x);
        return teile.Where(t => t.TypeKind is TypeKind.Interface or TypeKind.TypeParameter
                                || t.SpecialType == SpecialType.System_Object
                                || (t.TypeKind == TypeKind.Class && t.IsAbstract));
    }
}
