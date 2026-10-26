using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Domain.SourceGeneration;

/// <summary>
/// Regeln der Grammatik und Graph-Diagnosen, die früher nur der Editor-Validator bzw. der Extractor meldete — jetzt im Build (Werkbank P7.4,
/// „Prüfen = Bauen“). Alles aus Signaturen und Symbolen der eigenen Compilation, nie aus Rümpfen:
/// <list type="bullet">
/// <item>CQRS079 (MISSING-APPLY) — ein Aggregat entscheidet ein persistentes Event, sein Applier faltet es nicht (der generierte
///   Fold würfe beim Falten).</item>
/// <item>CQRS080 (GR-FALTUNG, Warnung) — ein Applier faltet ein Event, das sein Aggregat in keinem Decide erzeugt.</item>
/// <item>CQRS081 (ENUM-ZERO, Warnung) — ein Enum-Wert 0 geht auf dem Proto-Wire als Default verloren (1-basiert empfohlen).</item>
/// <item>CQRS083 (GR-ZUSTAND, Regel Z, Warnung) — eine Projektion hält Zustand in Instanzfeldern; Gedächtnis gehört in ein Aggregat bzw.
///   Lesemodell.</item>
/// <item>CQRS013 (R7) — ein Command ohne typisiertes Ziel: er implementiert <c>ICommand</c>, aber nicht genau ein <c>ICommand&lt;TAggregat&gt;</c>
///   (etwa mit einer handgeschriebenen <c>Ziel</c>-Guid). Das Ziel eines Commands steht im Typ (Plan §2.5).</item>
/// </list>
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class ModellAnalyzer : DiagnosticAnalyzer
{
    public const string ApplyFehltId = "CQRS079", FaltungId = "CQRS080", EnumNullId = "CQRS081", ZustandId = "CQRS083",
        ZielId = "CQRS013";

    private static DiagnosticDescriptor D(string id, string titel, string text, DiagnosticSeverity s) =>
        new(id, titel, text, "CQRS.Modell", s, isEnabledByDefault: true);

    private static readonly DiagnosticDescriptor ApplyFehlt = D(ApplyFehltId, "Entschiedenes Event ohne Apply",
        "Aggregat '{0}' entscheidet '{1}' (Decide({2})), sein Applier faltet es nicht — Apply({1}) fehlt, das Falten würfe (MISSING-APPLY)",
        DiagnosticSeverity.Error);
    private static readonly DiagnosticDescriptor Faltung = D(FaltungId, "Gefaltetes Event, das das Aggregat nicht entscheidet",
        "Aggregat '{0}' faltet '{1}', erzeugt es aber in keinem Decide — ein Event faltet das Aggregat, das es entscheidet (GR-FALTUNG)",
        DiagnosticSeverity.Warning);
    private static readonly DiagnosticDescriptor EnumNull = D(EnumNullId, "Enum-Wert 0",
        "Enum '{0}' hat den Wert 0 ('{1}') — er geht auf dem Proto-Wire als Default verloren; 1-basiert nummerieren (ENUM-ZERO)",
        DiagnosticSeverity.Warning);
    private static readonly DiagnosticDescriptor Zustand = D(ZustandId, "Projektion hält Zustand",
        "'{0}' hält Zustand in {1} — eine Projektion ist ein zustandsloser Übersetzer; Gedächtnis gehört in ein Aggregat bzw. Lesemodell (GR-ZUSTAND, Regel Z)",
        DiagnosticSeverity.Warning);

    private static readonly DiagnosticDescriptor Ziel = D(ZielId, "Command ohne typisiertes Ziel",
        "Command '{0}' nennt sein Ziel nicht im Typ — ein Command implementiert genau ein ICommand<TAggregat> mit Id<TAggregat> AggregateId (R7)",
        DiagnosticSeverity.Error);

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArray.Create(ApplyFehlt, Faltung, EnumNull, Zustand, Ziel);

    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        context.RegisterCompilationStartAction(start =>
        {
            var c = start.Compilation;
            var v = new Vertrag(c);
            if (v.IEvent == null) return;
            start.RegisterSymbolAction(ctx => Pruefe(ctx, v), SymbolKind.NamedType);
        });
    }

    private sealed class Vertrag
    {
        public readonly INamedTypeSymbol? IEvent, ITransient, ICommand, ICommandZiel, IState, IDecider, IApplier, ISubscriber;
        public Vertrag(Compilation c)
        {
            INamedTypeSymbol? G(string n) => c.GetTypeByMetadataName("Abstractions." + n);
            IEvent = G("IEvent"); ITransient = G("ITransientEvent"); ICommand = G("ICommand"); IState = G("IState");
            IDecider = G("IDecider`1"); IApplier = G("IApplier`1"); ISubscriber = G("ISubscriber");
            ICommandZiel = c.GetTypeByMetadataName(Abstractions.Idvertrag.CommandMetadatenName);
        }
        public bool Ist(ITypeSymbol t, INamedTypeSymbol? i) => i != null && (SymbolEqualityComparer.Default.Equals(t, i) || t.AllInterfaces.Contains(i, SymbolEqualityComparer.Default));
    }

    private static void Pruefe(SymbolAnalysisContext ctx, Vertrag v)
    {
        var t = (INamedTypeSymbol)ctx.Symbol;
        if (t.TypeKind == TypeKind.Enum) { PruefeEnum(ctx, t); return; }
        if (v.Ist(t, v.IState) && !t.IsAbstract) PruefeAggregat(ctx, t, v);
        if (t.TypeKind is TypeKind.Class or TypeKind.Struct && !t.IsAbstract && v.Ist(t, v.ICommand) && v.ICommandZiel != null
            && t.AllInterfaces.Count(i => SymbolEqualityComparer.Default.Equals(i.OriginalDefinition, v.ICommandZiel)) != 1)
            ctx.ReportDiagnostic(Diagnostic.Create(Ziel, t.Locations.FirstOrDefault(), t.Name));
        if (t.TypeKind == TypeKind.Class && !t.IsAbstract && v.Ist(t, v.ISubscriber)) PruefeKonsument(ctx, t, v);
    }

    private static void PruefeEnum(SymbolAnalysisContext ctx, INamedTypeSymbol e)
    {
        foreach (var f in e.GetMembers().OfType<IFieldSymbol>())
            if (f.HasConstantValue && System.Convert.ToInt64(f.ConstantValue) == 0)
                ctx.ReportDiagnostic(Diagnostic.Create(EnumNull, f.Locations.FirstOrDefault(), e.Name, f.Name));
    }

    /// <summary>Decide-Ausgänge gegen Apply-Eingänge desselben State (Decider/Applier dürfen getrennte Klassen sein, auch verschachtelt).</summary>
    private static void PruefeAggregat(SymbolAnalysisContext ctx, INamedTypeSymbol state, Vertrag v)
    {
        var teile = Typen.Vertragsnutzer(ctx.Compilation).Where(x => SymbolEqualityComparer.Default.Equals(x.ContainingAssembly, ctx.Compilation.Assembly)).ToList();
        bool Fuer(INamedTypeSymbol x, INamedTypeSymbol? g) => g != null && x.AllInterfaces.Any(i =>
            SymbolEqualityComparer.Default.Equals(i.OriginalDefinition, g) && SymbolEqualityComparer.Default.Equals(i.TypeArguments[0], state));
        var decides = teile.Where(x => Fuer(x, v.IDecider)).SelectMany(x => x.GetMembers(Abstractions.Aggregatvertrag.Decide).OfType<IMethodSymbol>())
            .Where(m => m.Parameters.Length > 0).ToList();
        var applies = teile.Where(x => Fuer(x, v.IApplier)).SelectMany(x => x.GetMembers(Abstractions.Aggregatvertrag.Apply).OfType<IMethodSymbol>())
            .Where(m => m.Parameters.Length > 0).ToList();
        if (decides.Count == 0) return;
        var gefaltet = new HashSet<ITypeSymbol>(applies.Select(m => m.Parameters[0].Type), SymbolEqualityComparer.Default);
        var entschieden = new HashSet<ITypeSymbol>(SymbolEqualityComparer.Default);
        foreach (var d in decides)
            foreach (var e in Typen.Faelle(d.ReturnType))
            {
                entschieden.Add(e);
                if (v.Ist(e, v.IEvent) && !v.Ist(e, v.ITransient) && !gefaltet.Contains(e))
                    ctx.ReportDiagnostic(Diagnostic.Create(ApplyFehlt, d.Locations.FirstOrDefault(), state.Name, e.Name, d.Parameters[0].Type.Name));
            }
        foreach (var a in applies.Where(a => !entschieden.Contains(a.Parameters[0].Type)))
            ctx.ReportDiagnostic(Diagnostic.Create(Faltung, a.Locations.FirstOrDefault(), state.Name, a.Parameters[0].Type.Name));
    }

    private static void PruefeKonsument(SymbolAnalysisContext ctx, INamedTypeSymbol t, Vertrag v)
    {
        // Regel Z: Instanzfelder, die Zustand halten — nicht readonly, oder readonly mit einem Referenztyp, der nicht injiziert (Ctor) und kein string ist.
        var injiziert = new HashSet<ITypeSymbol>(t.InstanceConstructors.SelectMany(c => c.Parameters).Select(p => p.Type), SymbolEqualityComparer.Default);
        var felder = t.GetMembers().OfType<IFieldSymbol>()
            .Where(f => !f.IsStatic && !f.IsConst && !f.IsImplicitlyDeclared && f.AssociatedSymbol == null)
            .Where(f => !f.IsReadOnly || (!f.Type.IsValueType && f.Type.SpecialType != SpecialType.System_String && !injiziert.Contains(f.Type)))
            .ToList();
        if (felder.Count > 0)
            ctx.ReportDiagnostic(Diagnostic.Create(Zustand, felder[0].Locations.FirstOrDefault(), t.Name, string.Join(", ", felder.Select(f => f.Name))));
    }
}
