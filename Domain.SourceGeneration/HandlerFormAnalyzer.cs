using System.Collections.Immutable;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Domain.SourceGeneration;

/// <summary>
/// <b>CQRS057 — Handler-Form.</b> Extractor/Editor erkennen einen Handler an seinen TYPEN (Eingang + Kontext), die
/// Dispatch-Generatoren rufen nur Methoden namens <c>Handle</c> in genau dieser Form auf:
///  • Projektion/Reaktion (<c>ISubscriber</c>): <c>Handle(TEvent, IAggregateEnvelope, ProjectionWriter, Fähigkeit…)</c>
///  • Reader (<c>IReader&lt;T&gt;</c>):          <c>Handle(TQuery, IMessageEnvelope, ReadContext, Fähigkeit…)</c>
///  • Pipeline (<c>IPipelineHandler</c>):       <c>Handle(TEingang, PipelineContext, Fähigkeit…)</c>
/// Weicht ein erkannter Handler davon ab (anderer Name, Kontext an falscher Stelle, Extra-Parameter ohne
/// Fähigkeits-Marker), würde der Editor ihn zeigen, die Laufzeit ihn aber still übergehen. Das ist hier ein Build-Fehler.
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class HandlerFormAnalyzer : DiagnosticAnalyzer
{
    public const string FormId = "CQRS057";

    /// <summary>Der Methoden-Name, den alle Dispatch-Generatoren aufrufen (Framework-Vertrag).</summary>
    private const string Handle = "Handle";

    private static readonly DiagnosticDescriptor Form = new(
        FormId, "Handler wird nicht dispatcht",
        "'{0}.{1}' sieht aus wie ein Handler für '{2}', wird aber nie aufgerufen: {3}. Erwartet: {4}",
        "CQRS.Vertrag", DiagnosticSeverity.Error, isEnabledByDefault: true);

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArray.Create(Form);

    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        context.RegisterCompilationStartAction(start =>
        {
            var c = start.Compilation;
            INamedTypeSymbol? T(string n) => c.GetTypeByMetadataName(n);
            var m = new M
            {
                ISubscriber = T("Abstractions.ISubscriber"), IReader = T("Abstractions.IReader`1"), IPipeline = T("Abstractions.IPipelineHandler"),
                IEvent = T("Abstractions.IEvent"), IQuery = T("Abstractions.IQuery"),
                IAggEnv = T("Abstractions.IAggregateEnvelope"), IMsgEnv = T("Abstractions.IMessageEnvelope"),
                Writer = T("Core.ProjectionWriter"), ReadCtx = T("Abstractions.ReadContext"), PipeCtx = T("Abstractions.PipelineContext"),
                IWrite = T("Abstractions.IWriteStore"), IRead = T("Abstractions.IReadStore"), IAkteur = T("Abstractions.IAkteur"),
            };
            if (m.ISubscriber == null && m.IReader == null && m.IPipeline == null) return;
            start.RegisterSymbolAction(ctx => Pruefe(ctx, m), SymbolKind.Method);
        });
    }

    private sealed class M
    {
        public INamedTypeSymbol? ISubscriber, IReader, IPipeline, IEvent, IQuery, IAggEnv, IMsgEnv, Writer, ReadCtx, PipeCtx, IWrite, IRead, IAkteur;
    }

    private static bool Gleich(ITypeSymbol a, INamedTypeSymbol? b) => b != null && SymbolEqualityComparer.Default.Equals(a, b);
    private static bool Hat(ITypeSymbol t, INamedTypeSymbol? i) => i != null &&
        (SymbolEqualityComparer.Default.Equals(t.OriginalDefinition, i) || t.AllInterfaces.Any(x => SymbolEqualityComparer.Default.Equals(x.OriginalDefinition, i)));

    private static void Pruefe(SymbolAnalysisContext ctx, M m)
    {
        var mm = (IMethodSymbol)ctx.Symbol;
        if (mm.MethodKind != MethodKind.Ordinary || mm.IsStatic || mm.DeclaredAccessibility != Accessibility.Public
            || mm.Parameters.Length == 0 || mm.ContainingType is not { TypeKind: TypeKind.Class } typ) return;
        var ps = mm.Parameters;
        var p0 = ps[0].Type;

        // Rolle + erwartete Form — erkannt an denselben TYPEN wie im Extractor (Eingang + Kontext irgendwo dahinter).
        INamedTypeSymbol? k1, k2; int fähigAb; string erwartet;
        if (Hat(typ, m.ISubscriber) && Hat(p0, m.IEvent) && ps.Skip(1).Any(p => Gleich(p.Type, m.IAggEnv)))
        { k1 = m.IAggEnv; k2 = m.Writer; fähigAb = 3; erwartet = "Handle(TEvent, IAggregateEnvelope, ProjectionWriter, Fähigkeit…)"; }
        else if (Hat(typ, m.IReader) && Hat(p0, m.IQuery))
        { k1 = m.IMsgEnv; k2 = m.ReadCtx; fähigAb = 3; erwartet = "Handle(TQuery, IMessageEnvelope, ReadContext, Fähigkeit…)"; }
        else if (Hat(typ, m.IPipeline) && ps.Skip(1).Any(p => Gleich(p.Type, m.PipeCtx)))
        { k1 = m.PipeCtx; k2 = null; fähigAb = 2; erwartet = "Handle(TEingang, PipelineContext, Fähigkeit…)"; }
        else return;

        string? grund = null;
        if (mm.Name != Handle) grund = $"der Methodenname ist '{mm.Name}', der Dispatch ruft nur '{Handle}'";
        else if (ps.Length < fähigAb || !Gleich(ps[1].Type, k1) || (k2 != null && !Gleich(ps[2].Type, k2)))
            grund = "der Framework-Kontext steht nicht an der erwarteten Stelle";
        else if (ps.Skip(fähigAb).FirstOrDefault(p => !(p.Type.TypeKind == TypeKind.Interface && (Hat(p.Type, m.IWrite) || Hat(p.Type, m.IRead)
                     // Akteur-Dienst (IAkteur): nur am Pipeline-Handle — dort entscheidet der Handle in seinem Auftrag.
                     || k1 == m.PipeCtx && Hat(p.Type, m.IAkteur)))) is { } fremd)
            grund = $"der Parameter '{fremd.Name}' ({fremd.Type.Name}) ist keine Fähigkeit (IWriteStore/IReadStore)"
                + (Hat(fremd.Type, m.IAkteur) ? " — ein Akteur-Dienst ist nur am Pipeline-Handle erlaubt" : " — Dienste gehören in den Konstruktor");
        if (grund == null) return;

        ctx.ReportDiagnostic(Diagnostic.Create(Form, mm.Locations.FirstOrDefault(), typ.Name, mm.Name, p0.Name, grund, erwartet));
    }
}
