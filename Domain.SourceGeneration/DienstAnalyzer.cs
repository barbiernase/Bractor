using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using Infrastructure.SourceGeneration;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Domain.SourceGeneration;

/// <summary>
/// Die Dienst-Bindung (Plan E11) im Build — was die generierte Bindung (<c>DienstGenerator</c>, <c>BindeDienste</c>) voraussetzt:
/// <list type="bullet">
/// <item>CQRS089 (GR-DIENST) — ein Dienst (<c>IDienst</c>) steht nur im Konstruktor einer Funktions- oder Dienst-Implementierung; als Parameter
///   eines Handles/Decide, als Feld oder Eigenschaft im Fachcode (Aggregat, Projektion, Reaktion, Reader …) wäre er eine unsichtbare
///   Abhängigkeit — dort sind es Fähigkeiten als Parameter.</item>
/// <item>CQRS090 (GR-DIENST-BINDBAR) — der Konstruktor einer Funktions-/Dienst-Implementierung nimmt nur, was die Umgebung binden kann:
///   Dienste, Konfig-Records, <c>ILogger&lt;T&gt;</c>.</item>
/// <item>CQRS091 (GR-DIENST-BINDBAR) — ein Konfig-Record hat nur Felder, die aus der Konfig lesbar sind (Text, Zahl, Wahrheitswert, Dauer,
///   Guid, Enum, Text-Liste).</item>
/// <item>CQRS092 (GR-DIENST-BINDBAR) — eine Implementierung ist öffentlich und nicht generisch, sonst kann die generierte Fabrik sie nicht
///   bauen und sie fehlt still in der Tabelle.</item>
/// </list>
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class DienstAnalyzer : DiagnosticAnalyzer
{
    public const string OrtId = "CQRS089", CtorId = "CQRS090", KonfigFeldId = "CQRS091", BaubarId = "CQRS092";

    private static DiagnosticDescriptor D(string id, string titel, string text) =>
        new(id, titel, text, "CQRS.Dienst", DiagnosticSeverity.Error, isEnabledByDefault: true);

    private static readonly DiagnosticDescriptor Ort = D(OrtId, "Dienst außerhalb einer Implementierung",
        "'{0}' nimmt den Dienst '{1}' als {2} — ein Dienst steht nur im Konstruktor einer Funktions- oder Dienst-Implementierung; "
        + "im Fachcode sind es Fähigkeiten als Parameter (GR-DIENST)");
    private static readonly DiagnosticDescriptor Ctor = D(CtorId, "Konstruktor nicht bindbar",
        "Der Konstruktor von '{0}' nimmt '{1}' ({2}) — die Umgebung bindet nur Dienste (IDienst), Konfig-Records und ILogger<T> (GR-DIENST-BINDBAR)");
    private static readonly DiagnosticDescriptor KonfigFeld = D(KonfigFeldId, "Konfig-Feld nicht bindbar",
        "Der Konfig-Record '{0}' hat das Feld '{1}' vom Typ '{2}' — aus Konfig:{0} lesbar sind Text, Zahl, Wahrheitswert, Dauer, Guid, Enum und Text-Liste (GR-DIENST-BINDBAR)");
    private static readonly DiagnosticDescriptor Baubar = D(BaubarId, "Implementierung nicht baubar",
        "'{0}' implementiert '{1}', ist aber {2} — die generierte Bindung baut nur öffentliche, nicht generische Klassen; so fehlt sie still (GR-DIENST-BINDBAR)");

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArray.Create(Ort, Ctor, KonfigFeld, Baubar);

    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        context.RegisterCompilationStartAction(start =>
        {
            var iDienst = start.Compilation.GetTypeByMetadataName(Abstractions.Dienstvertrag.Marker);
            var iFunktion = start.Compilation.GetTypeByMetadataName("Abstractions.IFunktion");
            if (iDienst == null || iFunktion == null) return;
            start.RegisterSymbolAction(ctx => Pruefe(ctx, iDienst, iFunktion), SymbolKind.NamedType);
        });
    }

    private static bool IstDienst(ITypeSymbol? t, INamedTypeSymbol iDienst) =>
        t is INamedTypeSymbol { TypeKind: TypeKind.Interface } i && !SymbolEqualityComparer.Default.Equals(i, iDienst)
        && i.AllInterfaces.Contains(iDienst, SymbolEqualityComparer.Default);

    /// <summary>Der Dienst im Typ selbst oder in seinen Typ-Argumenten (Func&lt;IX&gt;, IEnumerable&lt;IX&gt; …).</summary>
    private static INamedTypeSymbol? DienstIn(ITypeSymbol? t, INamedTypeSymbol iDienst)
    {
        if (IstDienst(t, iDienst)) return (INamedTypeSymbol)t!;
        if (t is IArrayTypeSymbol a) return DienstIn(a.ElementType, iDienst);
        return t is INamedTypeSymbol n ? n.TypeArguments.Select(x => DienstIn(x, iDienst)).FirstOrDefault(x => x != null) : null;
    }

    private static void Pruefe(SymbolAnalysisContext ctx, INamedTypeSymbol iDienst, INamedTypeSymbol iFunktion)
    {
        var t = (INamedTypeSymbol)ctx.Symbol;
        var vertraege = t.AllInterfaces.Where(i => IstDienst(i, iDienst)
            || (!SymbolEqualityComparer.Default.Equals(i, iFunktion) && i.AllInterfaces.Contains(iFunktion, SymbolEqualityComparer.Default))).ToList();
        var istImpl = t.TypeKind == TypeKind.Class && !t.IsAbstract && vertraege.Count > 0;

        // CQRS092: eine Implementierung, die die generierte Bindung nicht bauen kann.
        if (istImpl && !Ctorbindung.Baubar(t))
            ctx.ReportDiagnostic(Diagnostic.Create(Baubar, t.Locations.FirstOrDefault(), t.Name, vertraege[0].Name,
                t.IsGenericType ? "generisch" : "nicht öffentlich"));

        // CQRS089: ein Dienst an jeder anderen Stelle als im Konstruktor einer Implementierung (Felder/Eigenschaften dort halten ihn nur).
        foreach (var m in t.GetMembers().Where(m => !m.IsImplicitlyDeclared))
        {
            switch (m)
            {
                case IMethodSymbol me when me.MethodKind is MethodKind.Constructor && istImpl:
                    break;
                case IMethodSymbol me when me.MethodKind is MethodKind.Ordinary or MethodKind.Constructor or MethodKind.ExplicitInterfaceImplementation:
                    foreach (var p in me.Parameters)
                        if (DienstIn(p.Type, iDienst) is { } d)
                            ctx.ReportDiagnostic(Diagnostic.Create(Ort, p.Locations.FirstOrDefault() ?? me.Locations.FirstOrDefault(), t.Name, d.Name,
                                me.MethodKind == MethodKind.Constructor ? "Konstruktor-Parameter" : $"Parameter von {me.Name}"));
                    break;
                case IFieldSymbol f when !istImpl && DienstIn(f.Type, iDienst) is { } d:
                    ctx.ReportDiagnostic(Diagnostic.Create(Ort, f.Locations.FirstOrDefault(), t.Name, d.Name, "Feld"));
                    break;
                case IPropertySymbol p when !istImpl && DienstIn(p.Type, iDienst) is { } d:
                    ctx.ReportDiagnostic(Diagnostic.Create(Ort, p.Locations.FirstOrDefault(), t.Name, d.Name, "Eigenschaft"));
                    break;
            }
        }
        if (!istImpl) return;

        // CQRS090/091: was der Konstruktor nimmt — und ob die Konfig-Records lesbar sind.
        var k = Ctorbindung.Konstruktor(t);
        if (k == null) return;
        foreach (var p in k.Parameters)
        {
            var art = Ctorbindung.ArtVon(p.Type, iDienst);
            if (art == Ctorbindung.Art.Fremd)
            {
                ctx.ReportDiagnostic(Diagnostic.Create(Ctor, p.Locations.FirstOrDefault() ?? t.Locations.FirstOrDefault(), t.Name, p.Name, p.Type.ToDisplayString()));
                continue;
            }
            if (art != Ctorbindung.Art.Konfig || p.Type is not INamedTypeSymbol rec) continue;
            foreach (var feld in Ctorbindung.Konstruktor(rec)?.Parameters ?? ImmutableArray<IParameterSymbol>.Empty)
                if (!Ctorbindung.FeldBindbar(feld.Type))
                {
                    // Am Konstruktor-Parameter der Implementierung (der Record kann in einem anderen Projekt liegen; je Implementierung einmal).
                    ctx.ReportDiagnostic(Diagnostic.Create(KonfigFeld, p.Locations.FirstOrDefault() ?? t.Locations.FirstOrDefault(), rec.Name, feld.Name, feld.Type.ToDisplayString()));
                }
        }
    }
}
