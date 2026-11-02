using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using Infrastructure.SourceGeneration;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Domain.SourceGeneration;

/// <summary>
/// Die Bindung der Konstruktoren (docs/06 §6.5) im Build — was die generierte Bindung (<c>TechnikGenerator</c>, <c>BindeTechnik</c>) voraussetzt.
/// Technik trägt keinen Marker: sie ist, was der Konstruktor einer Funktion bzw. einer Technik-Umsetzung nimmt und weder Konfig-Record noch
/// <c>ILogger&lt;T&gt;</c> noch <c>IZustand&lt;T&gt;</c> ist (<c>Funktionsform.Techniken</c>).
/// <list type="bullet">
/// <item>CQRS089 (GR-TECHNIK) — Technik steht nur im Konstruktor einer Funktion bzw. ihrer Umsetzung oder einer Technik-Umsetzung; als Parameter eines
///   Handles/Decide, als Feld oder Eigenschaft im Fachcode (Aggregat, Projektion, Reader …) wäre sie eine unsichtbare Abhängigkeit — dort sind es
///   Fähigkeiten als Parameter.</item>
/// <item>CQRS090 (GR-TECHNIK-BINDBAR) — der Konstruktor einer Funktion bzw. Umsetzung nimmt nur, was die Umgebung binden kann: Technik (eine
///   Schnittstelle oder Klasse der Anwendung), Konfig-Records, <c>ILogger&lt;T&gt;</c> und ihren Zustand (<c>IZustand&lt;T&gt;</c>). Eine Funktion
///   ruft das Framework, nie ein Halter; ein Typ außerhalb der Anwendung braucht eine eigene Technik-Schnittstelle davor.</item>
/// <item>CQRS091 (GR-TECHNIK-BINDBAR) — ein Konfig-Record hat nur Felder, die aus der Konfig lesbar sind (Text, Zahl, Wahrheitswert, Dauer,
///   Guid, Enum, Text-Liste).</item>
/// <item>CQRS092 (GR-TECHNIK-BINDBAR) — eine Umsetzung ist öffentlich und nicht generisch, sonst kann die generierte Fabrik sie nicht
///   bauen und sie fehlt still in der Tabelle.</item>
/// </list>
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class TechnikAnalyzer : DiagnosticAnalyzer
{
    public const string OrtId = "CQRS089", CtorId = "CQRS090", KonfigFeldId = "CQRS091", BaubarId = "CQRS092";

    private static DiagnosticDescriptor D(string id, string titel, string text) =>
        new(id, titel, text, "CQRS.Funktion", DiagnosticSeverity.Error, isEnabledByDefault: true);

    private static readonly DiagnosticDescriptor Ort = D(OrtId, "Technik außerhalb eines Konstruktors",
        "'{0}' nimmt die Technik '{1}' als {2} — Technik steht nur im Konstruktor einer Funktion bzw. ihrer Umsetzung; "
        + "im Fachcode sind es Fähigkeiten als Parameter (GR-TECHNIK)");
    private static readonly DiagnosticDescriptor Ctor = D(CtorId, "Konstruktor nicht bindbar",
        "Der Konstruktor von '{0}' nimmt '{1}' ({2}) — die Umgebung bindet nur Technik (Schnittstellen und Klassen der Anwendung), Konfig-Records, ILogger<T> und IZustand<T>{3} (GR-TECHNIK-BINDBAR)");
    private static readonly DiagnosticDescriptor KonfigFeld = D(KonfigFeldId, "Konfig-Feld nicht bindbar",
        "Der Konfig-Record '{0}' hat das Feld '{1}' vom Typ '{2}' — aus Konfig:{0} lesbar sind Text, Zahl, Wahrheitswert, Dauer, Guid, Enum und Text-Liste (GR-TECHNIK-BINDBAR)");
    private static readonly DiagnosticDescriptor Baubar = D(BaubarId, "Umsetzung nicht baubar",
        "'{0}' setzt '{1}' um, ist aber {2} — die generierte Bindung baut nur öffentliche, nicht generische Klassen; so fehlt sie still (GR-TECHNIK-BINDBAR)");

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArray.Create(Ort, Ctor, KonfigFeld, Baubar);

    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        context.RegisterCompilationStartAction(start =>
        {
            if (start.Compilation.GetTypeByMetadataName(Funktionsform.IFunktion) == null) return;
            var technik = Funktionsform.Techniken(Vertragsassemblies.Typen(start.Compilation));
            start.RegisterSymbolAction(ctx => Pruefe(ctx, technik), SymbolKind.NamedType);
        });
    }

    /// <summary>Die Technik im Typ selbst oder in seinen Typ-Argumenten (Func&lt;IX&gt;, IEnumerable&lt;IX&gt; …).</summary>
    private static INamedTypeSymbol? TechnikIn(ITypeSymbol? t, Funktionsform.Technikmenge technik)
    {
        if (t is INamedTypeSymbol g && technik.IstTechnik(g)) return g;
        if (t is IArrayTypeSymbol a) return TechnikIn(a.ElementType, technik);
        return t is INamedTypeSymbol n ? n.TypeArguments.Select(x => TechnikIn(x, technik)).FirstOrDefault(x => x != null) : null;
    }

    private static void Pruefe(SymbolAnalysisContext ctx, Funktionsform.Technikmenge technik)
    {
        var t = (INamedTypeSymbol)ctx.Symbol;
        var vertraege = Funktionsform.Umsetzt(t).ToList();
        var technikVon = t.TypeKind == TypeKind.Class
            ? t.AllInterfaces.Where(technik.IstTechnik).Cast<INamedTypeSymbol>().FirstOrDefault() ?? (technik.IstTechnik(t) ? t : null)
            : null;
        // Eine Funktion als Klasse ist ihre eigene Umsetzung; eine Technik-Umsetzung nimmt geschachtelt selbst Technik.
        var istImpl = t.TypeKind == TypeKind.Class && !t.IsAbstract
            && (vertraege.Count > 0 || Funktionsform.IstFunktion(t) || technik.IstUmsetzung(t) || technikVon != null);

        // CQRS092: eine Umsetzung, die die generierte Bindung nicht bauen kann.
        if (istImpl && !Ctorbindung.Baubar(t))
            ctx.ReportDiagnostic(Diagnostic.Create(Baubar, t.Locations.FirstOrDefault(), t.Name,
                vertraege.FirstOrDefault()?.Name ?? technikVon?.Name ?? "IFunktion", t.IsGenericType ? "generisch" : "nicht öffentlich"));

        // CQRS089: Technik an jeder anderen Stelle als im Konstruktor einer Umsetzung (Felder/Eigenschaften dort halten sie nur).
        foreach (var m in t.GetMembers().Where(m => !m.IsImplicitlyDeclared))
        {
            switch (m)
            {
                case IMethodSymbol me when me.MethodKind is MethodKind.Constructor && istImpl:
                    break;
                // Die Methoden einer Technik und ihrer Umsetzungen nehmen, was sie wollen — sie ruft ihr Halter.
                case IMethodSymbol when technik.IstTechnik(t) || technikVon != null:
                    break;
                case IMethodSymbol me when me.MethodKind is MethodKind.Ordinary or MethodKind.Constructor or MethodKind.ExplicitInterfaceImplementation:
                    foreach (var p in me.Parameters)
                        if (TechnikIn(p.Type, technik) is { } d)
                            ctx.ReportDiagnostic(Diagnostic.Create(Ort, p.Locations.FirstOrDefault() ?? me.Locations.FirstOrDefault(), t.Name, d.Name,
                                me.MethodKind == MethodKind.Constructor ? "Konstruktor-Parameter" : $"Parameter von {me.Name}"));
                    break;
                case IFieldSymbol f when !istImpl && TechnikIn(f.Type, technik) is { } d:
                    ctx.ReportDiagnostic(Diagnostic.Create(Ort, f.Locations.FirstOrDefault(), t.Name, d.Name, "Feld"));
                    break;
                case IPropertySymbol p when !istImpl && !technik.IstTechnik(t) && TechnikIn(p.Type, technik) is { } d:
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
            var art = Ctorbindung.ArtVon(p.Type);
            if (art == Ctorbindung.Art.Fremd)
            {
                var zusatz = p.Type is INamedTypeSymbol b && Funktionsform.IstFunktion(b)
                    ? $" — '{b.Name}' ist eine Funktion: sie ruft das Framework, nie ein Halter; was sie ausgibt, kommt als Eingang an"
                    : " — ein Typ außerhalb der Anwendung bekommt eine eigene Technik-Schnittstelle, deren Umsetzung ihn hält";
                ctx.ReportDiagnostic(Diagnostic.Create(Ctor, p.Locations.FirstOrDefault() ?? t.Locations.FirstOrDefault(), t.Name, p.Name, p.Type.ToDisplayString(), zusatz));
                continue;
            }
            if (art != Ctorbindung.Art.Konfig || p.Type is not INamedTypeSymbol rec) continue;
            foreach (var feld in Ctorbindung.Konstruktor(rec)?.Parameters ?? ImmutableArray<IParameterSymbol>.Empty)
                if (!Ctorbindung.FeldBindbar(feld.Type))
                {
                    // Am Konstruktor-Parameter der Umsetzung (der Record kann in einem anderen Projekt liegen; je Umsetzung einmal).
                    ctx.ReportDiagnostic(Diagnostic.Create(KonfigFeld, p.Locations.FirstOrDefault() ?? t.Locations.FirstOrDefault(), rec.Name, feld.Name, feld.Type.ToDisplayString()));
                }
        }
    }
}
