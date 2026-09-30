using System.Collections.Immutable;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Domain.SourceGeneration;

/// <summary>
/// <b>CQRS050 — geschlossener Ausgabe-Vertrag.</b> WAS ein Decider oder Handler erzeugen kann, steht in seiner
/// SIGNATUR — als konkreter Typ oder als <c>OneOf&lt;A, B, …&gt;</c> konkreter Typen. Der Compiler garantiert dann, dass
/// der Rumpf nichts anderes liefern kann; Generatoren (Command→Event-Map, Azyklizitäts-Guard), Graph, Editor, Simulation
/// und LLM-Kontext lesen die Menge aus dem Typ — ohne Rumpf-Analyse, die grundsätzlich unvollständig bleibt
/// (Variablen mit Interface-Typ, Hilfsmethoden, Schleifen über Listen, Fabriken … sind syntaktisch nicht zu erschöpfen).
///
/// Geprüft (Rolle über Marker/Parameter-Typen, nie über Namen):
///  • Decide — Klasse implementiert <c>IDecider&lt;T&gt;</c>, 1. Parameter <c>ICommand</c>: <c>IEnumerable&lt;X&gt;</c>.
///  • Projektion/Reaktion — <c>ISubscriber</c>, Parameter <c>IEvent</c> + <c>IAggregateEnvelope</c>.
///  • Reader — <c>IReader&lt;T&gt;</c>, 1. Parameter <c>IQuery</c>: <c>Task&lt;X&gt;</c>/<c>ValueTask&lt;X&gt;</c>/<c>X</c>.
///  • Pipeline — <c>IPipelineHandler</c>, Parameter <c>PipelineContext</c>.
/// Offen ist X (bzw. ein OneOf-Typargument), wenn es ein Interface, ein Typ-Parameter, <c>object</c> oder eine abstrakte
/// Klasse ist. <c>Task</c>/<c>ValueTask</c>/<c>void</c> ohne Ergebnis sind erlaubt (reine Effekte, z. B. Projektionen).
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

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArray.Create(Offen);

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
                c.GetTypeByMetadataName("Abstractions.ISubscriber"),
                c.GetTypeByMetadataName("Abstractions.IAggregateEnvelope"),
                c.GetTypeByMetadataName("Abstractions.IReader`1"),
                c.GetTypeByMetadataName("Abstractions.IPipelineHandler"),
                c.GetTypeByMetadataName("Abstractions.PipelineContext"));
            if (m.ICommand == null) return;   // kein CQRS-Vertrag in dieser Compilation
            start.RegisterSymbolAction(ctx => Pruefe(ctx, m), SymbolKind.Method);
        });
    }

    private sealed class Marker
    {
        public readonly INamedTypeSymbol? IDecider, ICommand, IEvent, IQuery, ISubscriber, IAggEnvelope, IReader, IPipelineHandler, PipelineContext;
        public Marker(INamedTypeSymbol? d, INamedTypeSymbol? c, INamedTypeSymbol? e, INamedTypeSymbol? q, INamedTypeSymbol? s,
                      INamedTypeSymbol? env, INamedTypeSymbol? r, INamedTypeSymbol? p, INamedTypeSymbol? ctx)
        { IDecider = d; ICommand = c; IEvent = e; IQuery = q; ISubscriber = s; IAggEnvelope = env; IReader = r; IPipelineHandler = p; PipelineContext = ctx; }
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
        bool HatParam(INamedTypeSymbol? pt) => pt != null && mm.Parameters.Any(p => SymbolEqualityComparer.Default.Equals(p.Type, pt));

        ITypeSymbol? element = null;
        var rt = mm.ReturnType;
        if (Hat(m.IDecider) && Ist(p0, m.ICommand))
            element = Element(rt, enumerable: true, task: false);
        else if (Hat(m.ISubscriber) && Ist(p0, m.IEvent) && HatParam(m.IAggEnvelope))
            element = Element(rt, enumerable: true, task: false);
        else if (Hat(m.IReader) && Ist(p0, m.IQuery))
            element = Element(rt, enumerable: false, task: true) ?? (IstOhneErgebnis(rt) ? null : rt);
        else if (Hat(m.IPipelineHandler) && HatParam(m.PipelineContext))
            element = Element(rt, enumerable: true, task: true);
        else return;
        if (element == null) return;   // Task/void: reine Effekte

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

    /// <summary>Die nicht-konkreten Typen in X (bei OneOf: in seinen Typargumenten).</summary>
    private static System.Collections.Generic.IEnumerable<ITypeSymbol> OffeneTypen(ITypeSymbol x)
    {
        var teile = x is INamedTypeSymbol { Name: "OneOf" } o && o.ContainingNamespace?.ToDisplayString() == "Abstractions"
            ? o.TypeArguments.AsEnumerable()
            : new[] { x };
        return teile.Where(t => t.TypeKind is TypeKind.Interface or TypeKind.TypeParameter
                                || t.SpecialType == SpecialType.System_Object
                                || (t.TypeKind == TypeKind.Class && t.IsAbstract));
    }
}
