using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using Abstractions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Domain.SourceGeneration;

/// <summary>
/// CQRS122 — die Grenze zwischen Katalog und Domäne (docs/plan-ohne-pipeline.md): die Domäne referenziert die Kataloge, deren Graphen sie bindet, aber
/// nur ihre Übersetzungen (<c>IUebersetzung&lt;TEin, TAus&gt;</c>) nennen deren Typen. Zustand, <c>Decide</c>, <c>Apply</c>, Commands an Aggregate,
/// Events, Hinweise, Wertobjekte und Queries an Aggregate samt Antworten sprechen die Sprache der Domäne — ein Katalog-Typ in ihren Feldern bzw.
/// Signaturen ist ein Build-Fehler. So bleibt ein Katalog austauschbar: was ihn ersetzt, berührt nur Übersetzungen. Ein Command ohne Ziel der Domäne
/// (die Nachricht eines Clients an eine Kopf-Übersetzung) und der Speicher (er setzt Fähigkeiten eines Katalogs um) sind frei.
/// WAS ein Typ ist, sagt <see cref="Lageart"/>; ob ein Typ aus einem Katalog kommt, sein Assembly (<c>&lt;Name&gt;.Katalog</c>, <see cref="Projektstruktur"/>).
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class KatalogGrenzeAnalyzer : DiagnosticAnalyzer
{
    public const string Id = "CQRS122";

    private static readonly DiagnosticDescriptor KatalogTyp = new(Id, "Katalog-Typ in der Sprache der Domäne",
        "'{0}' ({1}) nennt den Katalog-Typ {2} aus {3} — Zustand, Decide, Apply, Commands an Aggregate, Events, Hinweise und Aggregat-Queries "
        + "sprechen die Sprache der Domäne; was aus einem Katalog kommt, macht eine Übersetzung (IUebersetzung<TEin, TAus>) zu einem Typ der Domäne",
        "CQRS.Struktur", DiagnosticSeverity.Error, isEnabledByDefault: true);

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArray.Create(KatalogTyp);

    /// <summary>Die Primitive, die keinen Katalog-Typ nennen dürfen.</summary>
    private static readonly HashSet<Projektstruktur.Primitive> Domaenensprache = new()
    {
        Projektstruktur.Primitive.Aggregat, Projektstruktur.Primitive.Decider, Projektstruktur.Primitive.Applier, Projektstruktur.Primitive.Command,
        Projektstruktur.Primitive.Event, Projektstruktur.Primitive.Hinweis, Projektstruktur.Primitive.Wertobjekt,
        Projektstruktur.Primitive.AggregatQuery, Projektstruktur.Primitive.AggregatAntwort,
    };

    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        context.RegisterCompilationStartAction(start =>
        {
            var c = start.Compilation;
            if (Projektstruktur.IstKatalog(c.AssemblyName ?? "")) return;
            var lage = new Lageart(c, EigeneTypen(c.Assembly.GlobalNamespace));
            if (!lage.Bereit) return;
            start.RegisterSymbolAction(ctx =>
            {
                var t = (INamedTypeSymbol)ctx.Symbol;
                if (lage.Von(t) is not { } p || !Domaenensprache.Contains(p)) return;
                var ort = t.Locations.FirstOrDefault(l => l.IsInSource);
                if (ort == null) return;
                var gemeldet = new HashSet<string>();
                foreach (var genannt in Genannte(t, p).SelectMany(Teile))
                {
                    var katalog = genannt.ContainingAssembly?.Name ?? "";
                    if (!Projektstruktur.IstKatalog(katalog) || !gemeldet.Add(genannt.ToDisplayString())) continue;
                    ctx.ReportDiagnostic(Diagnostic.Create(KatalogTyp, ort, t.Name, Projektstruktur.Text(p), genannt.ToDisplayString(), katalog));
                }
            }, SymbolKind.NamedType);
        });
    }

    /// <summary>Was ein Typ nennt: die Typen seiner Felder und Eigenschaften, bei Decider und Applier die Signaturen ihrer Methoden.</summary>
    private static IEnumerable<ITypeSymbol> Genannte(INamedTypeSymbol t, Projektstruktur.Primitive p)
    {
        foreach (var m in t.GetMembers())
        {
            if (m.IsImplicitlyDeclared) continue;
            switch (m)
            {
                case IPropertySymbol e: yield return e.Type; break;
                case IFieldSymbol f: yield return f.Type; break;
                case IMethodSymbol me when p is Projektstruktur.Primitive.Decider or Projektstruktur.Primitive.Applier
                                          && me.MethodKind == MethodKind.Ordinary && me.Locations.Any(l => l.IsInSource):
                    yield return me.ReturnType;
                    foreach (var a in me.Parameters) yield return a.Type;
                    break;
            }
        }
    }

    /// <summary>Ein Typ und alles, woraus er gebaut ist (Typargumente, Elementtyp eines Arrays).</summary>
    private static IEnumerable<ITypeSymbol> Teile(ITypeSymbol t)
    {
        yield return t;
        if (t is IArrayTypeSymbol a)
            foreach (var x in Teile(a.ElementType)) yield return x;
        if (t is INamedTypeSymbol n)
            foreach (var arg in n.TypeArguments)
                foreach (var x in Teile(arg)) yield return x;
    }

    private static IEnumerable<INamedTypeSymbol> EigeneTypen(INamespaceSymbol ns)
    {
        foreach (var t in ns.GetTypeMembers())
        {
            yield return t;
            foreach (var n in t.GetTypeMembers()) yield return n;
        }
        foreach (var u in ns.GetNamespaceMembers())
            foreach (var t in EigeneTypen(u)) yield return t;
    }
}
