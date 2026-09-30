using System.Collections.Immutable;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Operations;

namespace Domain.SourceGeneration;

/// <summary>
/// <b>Fähigkeiten statt Rumpf-Analyse.</b> Welche Store-Funktionen ein Handle benutzen DARF, steht in seiner Signatur:
/// als Fähigkeits-Parameter (Interface mit Marker <c>IWriteStore</c>/<c>IReadStore</c>, genau EINE Funktion). Der
/// generierte Dispatch löst sie auf. Damit diese Obergrenze dicht ist, darf ein Konsument keinen anderen Weg zum Store haben:
///  • <b>CQRS051</b> — eine Fähigkeit deklariert genau eine Funktion und erbt keine andere Fähigkeit (Bündeln ist Sache
///    des Stores: <c>IStore</c>).
///  • <b>CQRS054</b> — Projektion/Reader/Reaktion/Pipeline halten keinen Store (Fähigkeit, Bündel, Store-Klasse) und keinen
///    <c>IFristplan</c> im Konstruktor, in einem Feld oder einer Eigenschaft (auch nicht statisch). Andere Dienste (Logger,
///    Konfig) bleiben erlaubt.
///  • <b>CQRS055</b> — niemand baut eine Store-Klasse selbst (<c>new …</c>); Stores kommen nur aus der DI.
/// Rollen über Marker, nie über Namen.
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class FaehigkeitAnalyzer : DiagnosticAnalyzer
{
    public const string FormId = "CQRS051", ZustandId = "CQRS054", NeuId = "CQRS055";

    private static readonly DiagnosticDescriptor Form = new(
        FormId, "Fähigkeit mit mehr als einer Funktion",
        "Die Fähigkeit '{0}' {1} — eine Fähigkeit trägt genau EINE Store-Funktion; mehrere bündelt ein Store-Interface (IStore)",
        "CQRS.Faehigkeiten", DiagnosticSeverity.Error, isEnabledByDefault: true);

    private static readonly DiagnosticDescriptor Zustand = new(
        ZustandId, "Store im Konsumenten",
        "'{0}' hält '{1}' ({2}) — ein Konsument bekommt Store-Funktionen nur als Fähigkeits-Parameter eines Handles, " +
        "sonst steht die Obergrenze nicht in der Signatur",
        "CQRS.Faehigkeiten", DiagnosticSeverity.Error, isEnabledByDefault: true);

    private static readonly DiagnosticDescriptor Neu = new(
        NeuId, "Store selbst gebaut",
        "'new {0}' — Stores kommen nur aus der DI (eine Instanz je Fähigkeits-Bereich, sonst bricht der Co-Commit)",
        "CQRS.Faehigkeiten", DiagnosticSeverity.Error, isEnabledByDefault: true);

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArray.Create(Form, Zustand, Neu);

    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        context.RegisterCompilationStartAction(start =>
        {
            var c = start.Compilation;
            var m = new Marker(
                c.GetTypeByMetadataName("Abstractions.IWriteStore"),
                c.GetTypeByMetadataName("Abstractions.IReadStore"),
                c.GetTypeByMetadataName("Abstractions.IStore"),
                c.GetTypeByMetadataName("Abstractions.IFristplan"),
                c.GetTypeByMetadataName("Abstractions.ISubscriber"),
                c.GetTypeByMetadataName("Abstractions.IReader`1"),
                c.GetTypeByMetadataName("Abstractions.IPipelineHandler"));
            if (m.IWrite == null || m.IRead == null) return;   // kein Fähigkeits-Vertrag in dieser Compilation
            start.RegisterSymbolAction(ctx => PruefeTyp(ctx, m), SymbolKind.NamedType);
            start.RegisterOperationAction(ctx => PruefeNeu(ctx, m), OperationKind.ObjectCreation);
        });
    }

    private sealed class Marker
    {
        public readonly INamedTypeSymbol? IWrite, IRead, IStore, IFristplan, ISubscriber, IReader, IPipeline;
        public Marker(INamedTypeSymbol? w, INamedTypeSymbol? r, INamedTypeSymbol? s, INamedTypeSymbol? f,
                      INamedTypeSymbol? sub, INamedTypeSymbol? rd, INamedTypeSymbol? p)
        { IWrite = w; IRead = r; IStore = s; IFristplan = f; ISubscriber = sub; IReader = rd; IPipeline = p; }

        private static bool Hat(ITypeSymbol t, INamedTypeSymbol? i) => i != null &&
            (SymbolEqualityComparer.Default.Equals(t.OriginalDefinition, i)
             || t.AllInterfaces.Any(x => SymbolEqualityComparer.Default.Equals(x.OriginalDefinition, i)));

        /// <summary>Fähigkeit: Interface mit IWriteStore/IReadStore, das kein Bündel (IStore) ist.</summary>
        public bool IstFaehigkeit(ITypeSymbol t) => t.TypeKind == TypeKind.Interface
            && !SymbolEqualityComparer.Default.Equals(t, IWrite) && !SymbolEqualityComparer.Default.Equals(t, IRead)
            && (Hat(t, IWrite) || Hat(t, IRead)) && !Hat(t, IStore);

        /// <summary>Alles, was Store-Zugang gibt: Fähigkeit, Bündel, Marker selbst, Store-Klasse — oder der Fristplan.</summary>
        public bool GibtStoreZugang(ITypeSymbol t) =>
            Hat(t, IWrite) || Hat(t, IRead) || Hat(t, IStore) || Hat(t, IFristplan);

        public bool IstKonsument(INamedTypeSymbol t) =>
            t.TypeKind == TypeKind.Class && (Hat(t, ISubscriber) || Hat(t, IReader) || Hat(t, IPipeline));

        public bool IstStoreKlasse(ITypeSymbol t) =>
            t.TypeKind == TypeKind.Class && (Hat(t, IWrite) || Hat(t, IRead) || Hat(t, IStore));
    }

    private static void PruefeTyp(SymbolAnalysisContext ctx, Marker m)
    {
        var t = (INamedTypeSymbol)ctx.Symbol;

        if (m.IstFaehigkeit(t))
        {
            var fns = t.GetMembers().OfType<IMethodSymbol>().Count(x => x.MethodKind == MethodKind.Ordinary);
            var geerbt = t.AllInterfaces.Where(m.IstFaehigkeit).Select(x => x.Name).ToList();
            if (fns != 1)
                ctx.ReportDiagnostic(Diagnostic.Create(Form, t.Locations.FirstOrDefault(), t.Name, $"deklariert {fns} Funktionen"));
            else if (geerbt.Count > 0)
                ctx.ReportDiagnostic(Diagnostic.Create(Form, t.Locations.FirstOrDefault(), t.Name, "erbt " + string.Join(", ", geerbt)));
            return;
        }

        if (!m.IstKonsument(t)) return;
        foreach (var ctor in t.InstanceConstructors.Where(x => !x.IsImplicitlyDeclared))
            foreach (var p in ctor.Parameters.Where(p => m.GibtStoreZugang(p.Type)))
                ctx.ReportDiagnostic(Diagnostic.Create(Zustand, p.Locations.FirstOrDefault(), t.Name, p.Type.Name, "Konstruktor-Parameter"));
        foreach (var member in t.GetMembers())
        {
            var (typ, art) = member switch
            {
                IFieldSymbol { IsImplicitlyDeclared: false } f => (f.Type, f.IsStatic ? "statisches Feld" : "Feld"),
                IPropertySymbol p => (p.Type, p.IsStatic ? "statische Eigenschaft" : "Eigenschaft"),
                _ => ((ITypeSymbol?)null, ""),
            };
            if (typ != null && m.GibtStoreZugang(typ))
                ctx.ReportDiagnostic(Diagnostic.Create(Zustand, member.Locations.FirstOrDefault(), t.Name, typ.Name, art));
        }
    }

    private static void PruefeNeu(OperationAnalysisContext ctx, Marker m)
    {
        var op = (IObjectCreationOperation)ctx.Operation;
        if (op.Type is { } t && m.IstStoreKlasse(t))
            ctx.ReportDiagnostic(Diagnostic.Create(Neu, op.Syntax.GetLocation(), t.Name));
    }
}
