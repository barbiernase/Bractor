using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Domain.SourceGeneration;

/// <summary>
/// <b>CQRS058 — Akteur-Befugnis.</b> <c>IDarf&lt;T&gt;</c> ist EIN Wort für alles, was ein Akteur in das System
/// hineingibt: Command, Query, Trigger, Transient-Event. Alles andere (ein Event aus dem Log, eine Response, ein
/// Wertobjekt …) kann man nicht „dürfen" — das Hören wird aus dem Graphen abgeleitet. Und <c>IDarf</c> gehört an einen
/// <c>IAkteur</c>: an einem anderen Typ würde es niemand lesen. Beides ist ein Build-Fehler (docs/konzept-akteure.md).
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class AkteurAnalyzer : DiagnosticAnalyzer
{
    public const string BefugnisId = "CQRS058", AuftragId = "CQRS060";

    private static readonly DiagnosticDescriptor Befugnis = new(
        BefugnisId, "Ungültige Akteur-Befugnis",
        "'{0}': {1}",
        "CQRS.Akteur", DiagnosticSeverity.Error, isEnabledByDefault: true);

    /// <summary>
    /// CQRS060 — Im Auftrag eines Akteurs über seinen Dienst (<c>IAkteurDienst&lt;A&gt;</c>, z. B. der Classifier-Dienst des
    /// Klassifizierers): der Dienst kommt als Parameter an den Pipeline-Handle (so
    /// steht in der Signatur, WER entscheidet), höchstens einer je Handle, und der Handle gibt nur Commands aus, die dieser
    /// Akteur darf. Im Konstruktor/Feld eines Konsumenten wäre die Verbindung unsichtbar (wie ein Store, CQRS054).
    /// </summary>
    private static readonly DiagnosticDescriptor Auftrag = new(
        AuftragId, "Im Auftrag eines Akteurs",
        "'{0}': {1}",
        "CQRS.Akteur", DiagnosticSeverity.Error, isEnabledByDefault: true);

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArray.Create(Befugnis, Auftrag);

    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        context.RegisterCompilationStartAction(start =>
        {
            var c = start.Compilation;
            INamedTypeSymbol? T(string n) => c.GetTypeByMetadataName(n);
            var iDarf = T("Abstractions.IDarf`1");
            var iAkteur = T("Abstractions.IAkteur");
            if (iDarf == null || iAkteur == null) return;
            var hinein = new[] { T("Abstractions.ICommand"), T("Abstractions.IQuery"), T("Abstractions.IPipelineTrigger"), T("Abstractions.ITransientEvent") }
                .Where(x => x != null).Cast<INamedTypeSymbol>().ToArray();
            start.RegisterSymbolAction(ctx => Pruefe(ctx, iDarf, iAkteur, hinein), SymbolKind.NamedType);
            var k = new Konsumenten(T("Abstractions.ISubscriber"), T("Abstractions.IReader`1"), T("Abstractions.IPipelineHandler"),
                T("Abstractions.PipelineContext"), T("Abstractions.ICommand"));
            var iDienst = T("Abstractions.IAkteurDienst`1");
            if (iDienst == null) return;
            start.RegisterSymbolAction(ctx => PruefeKonsument(ctx, iDienst, k), SymbolKind.NamedType);
            start.RegisterSymbolAction(ctx => PruefeHandle(ctx, iDarf, iDienst, k), SymbolKind.Method);
        });
    }

    private static void Pruefe(SymbolAnalysisContext ctx, INamedTypeSymbol iDarf, INamedTypeSymbol iAkteur, INamedTypeSymbol[] hinein)
    {
        var typ = (INamedTypeSymbol)ctx.Symbol;
        var ort = typ.Locations.FirstOrDefault() ?? Location.None;
        // Ein Akteur ist ein Record (Domänen-Experte), nie ein Dienst-Vertrag: der Dienst gehört einem Akteur (IAkteurDienst<A>).
        if (typ.TypeKind == TypeKind.Interface && typ.ContainingNamespace?.ToDisplayString() != "Abstractions"
            && typ.AllInterfaces.Contains(iAkteur, SymbolEqualityComparer.Default))
            ctx.ReportDiagnostic(Diagnostic.Create(Befugnis, ort, typ.Name,
                "ein Dienst ist kein Akteur — Akteure sind Records (IMensch/IMaschine/IKi); lass den Dienst einem Akteur gehören: IAkteurDienst<TAkteur>"));
        var darf = typ.Interfaces.Where(i => SymbolEqualityComparer.Default.Equals(i.OriginalDefinition, iDarf)).ToList();
        if (darf.Count == 0) return;

        if (!typ.AllInterfaces.Contains(iAkteur, SymbolEqualityComparer.Default))
            ctx.ReportDiagnostic(Diagnostic.Create(Befugnis, ort, typ.Name,
                "IDarf<…> steht nur an einem Akteur — ergänze IAkteur in der Basisliste"));

        foreach (var d in darf)
        {
            var t = d.TypeArguments[0];
            var erlaubt = hinein.Any(h => SymbolEqualityComparer.Default.Equals(t, h)
                || t.AllInterfaces.Contains(h, SymbolEqualityComparer.Default));
            if (!erlaubt || t.TypeKind == TypeKind.Interface || t.IsAbstract)
                ctx.ReportDiagnostic(Diagnostic.Create(Befugnis, ort, typ.Name,
                    $"IDarf<{t.Name}> — dürfen kann man nur, was man hineingibt: einen konkreten Command, eine Query, "
                    + "einen Trigger oder ein Transient-Event (was ein Akteur hört, wird aus dem Graphen abgeleitet)"));
        }
    }

    private sealed class Konsumenten
    {
        public readonly INamedTypeSymbol? Subscriber, Reader, Pipeline, PipeCtx, Command;
        public Konsumenten(INamedTypeSymbol? subscriber, INamedTypeSymbol? reader, INamedTypeSymbol? pipeline,
            INamedTypeSymbol? pipeCtx, INamedTypeSymbol? command)
        { Subscriber = subscriber; Reader = reader; Pipeline = pipeline; PipeCtx = pipeCtx; Command = command; }
    }

    private static bool Hat(ITypeSymbol t, INamedTypeSymbol? i) => i != null &&
        (SymbolEqualityComparer.Default.Equals(t.OriginalDefinition, i) || t.AllInterfaces.Any(x => SymbolEqualityComparer.Default.Equals(x.OriginalDefinition, i)));

    /// <summary>Der Akteur eines Akteur-Dienstes (<c>IAkteurDienst&lt;A&gt;</c> → A), sonst null.</summary>
    private static ITypeSymbol? AkteurVon(ITypeSymbol t, INamedTypeSymbol iDienst) =>
        t.TypeKind != TypeKind.Interface ? null
            : t.AllInterfaces.FirstOrDefault(i => SymbolEqualityComparer.Default.Equals(i.OriginalDefinition, iDienst))?.TypeArguments.FirstOrDefault();

    private static bool IstAkteurDienst(ITypeSymbol t, INamedTypeSymbol iDienst) => AkteurVon(t, iDienst) != null;

    // Akteur-Dienst im Konstruktor/Feld einer Klasse: die Verbindung „wer entscheidet" stünde nicht in der Signatur.
    private static void PruefeKonsument(SymbolAnalysisContext ctx, INamedTypeSymbol iDienst, Konsumenten k)
    {
        var t = (INamedTypeSymbol)ctx.Symbol;
        // In JEDER Klasse (nicht nur Konsumenten): sonst wäre offen, wer den Dienst benutzt (docs/konzept-akteure.md §7.2).
        if (t.TypeKind != TypeKind.Class) return;
        foreach (var ctor in t.InstanceConstructors.Where(x => !x.IsImplicitlyDeclared))
            foreach (var p in ctor.Parameters.Where(p => IstAkteurDienst(p.Type, iDienst)))
                ctx.ReportDiagnostic(Diagnostic.Create(Auftrag, p.Locations.FirstOrDefault(), t.Name,
                    $"der Akteur-Dienst {p.Type.Name} steht im Konstruktor — er gehört als Parameter an den Handle, der in seinem Auftrag entscheidet"));
        foreach (var f in t.GetMembers().OfType<IFieldSymbol>().Where(f => !f.IsImplicitlyDeclared && IstAkteurDienst(f.Type, iDienst)))
            ctx.ReportDiagnostic(Diagnostic.Create(Auftrag, f.Locations.FirstOrDefault(), t.Name,
                $"das Feld '{f.Name}' hält den Akteur-Dienst {f.Type.Name} — er gehört als Parameter an den Handle"));
    }

    // Handle mit Akteur-Parameter: höchstens einer, und nur Commands, die dieser Akteur darf.
    private static void PruefeHandle(SymbolAnalysisContext ctx, INamedTypeSymbol iDarf, INamedTypeSymbol iDienst, Konsumenten k)
    {
        var m = (IMethodSymbol)ctx.Symbol;
        if (m.MethodKind != MethodKind.Ordinary || m.ContainingType is not { } typ || !Hat(typ, k.Pipeline)) return;
        if (m.Parameters.Length < 2 || !SymbolEqualityComparer.Default.Equals(m.Parameters[1].Type, k.PipeCtx)) return;
        var akteure = m.Parameters.Skip(2).Where(p => IstAkteurDienst(p.Type, iDienst)).ToList();
        if (akteure.Count == 0) return;
        var ort = m.Locations.FirstOrDefault() ?? Location.None;
        if (akteure.Count > 1)
        {
            ctx.ReportDiagnostic(Diagnostic.Create(Auftrag, ort, $"{typ.Name}.{m.Name}",
                $"entscheidet im Auftrag mehrerer Akteure ({string.Join(", ", akteure.Select(a => a.Type.Name))}) — höchstens einer je Handle"));
            return;
        }
        var akteur = AkteurVon(akteure[0].Type, iDienst)!;
        var darf = akteur.AllInterfaces.Where(i => SymbolEqualityComparer.Default.Equals(i.OriginalDefinition, iDarf))
            .Select(i => i.TypeArguments[0]).ToImmutableHashSet<ITypeSymbol>(SymbolEqualityComparer.Default);
        foreach (var cmd in Ausgaben(m.ReturnType).Where(t => Hat(t, k.Command) && !darf.Contains(t)))
            ctx.ReportDiagnostic(Diagnostic.Create(Auftrag, ort, $"{typ.Name}.{m.Name}",
                $"gibt {cmd.Name} im Auftrag von {akteur.Name} aus, aber {akteur.Name} darf das nicht — ergänze IDarf<{cmd.Name}> am Akteur oder nimm den Akteur-Parameter weg"));
    }

    /// <summary>Die ausgegebenen Typen aus <c>(Async)Enumerable&lt;OneOf&lt;…&gt;&gt;</c> bzw. <c>(Async)Enumerable&lt;T&gt;</c>.</summary>
    private static IEnumerable<ITypeSymbol> Ausgaben(ITypeSymbol rueckgabe)
    {
        if (rueckgabe is not INamedTypeSymbol { TypeArguments.Length: 1 } en || en.TypeArguments[0] is not INamedTypeSymbol el) yield break;
        if (el.Name == "OneOf") foreach (var a in el.TypeArguments) yield return a;
        else yield return el;
    }
}
