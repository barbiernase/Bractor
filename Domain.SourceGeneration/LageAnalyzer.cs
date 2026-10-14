using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using Abstractions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Domain.SourceGeneration;

/// <summary>
/// CQRS098 — jede Primitive liegt im Projekt ihrer Rolle (<c>docs/07-projektstruktur.md</c> §7.2, Tabelle <see cref="Projektstruktur"/>):
/// ein Fluss in der Schreibseite, eine Projektion in den Abläufen, ein Akteur in der Leseseite … ist ein Build-Fehler, nicht still.
/// WAS ein Typ ist, sagt <see cref="Lageart"/> (Marker, Signaturen); WO er hingehört, die Tabelle. Die Rolle des Projekts folgt aus seinem
/// Namen (Wurzel + Suffix). Aktiv in jedem Projekt, das die Domänen-Generatoren einbindet (die Rollen-Projekte einer Anwendung).
/// Ordner, Namespace und Dateiname prüft <c>--check</c> (dort ist das Projektverzeichnis bekannt).
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class LageAnalyzer : DiagnosticAnalyzer
{
    public const string Id = "CQRS098";

    private static readonly DiagnosticDescriptor FalschesProjekt = new(Id, "Typ im falschen Projekt",
        "{0}", "CQRS.Struktur", DiagnosticSeverity.Error, isEnabledByDefault: true);

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArray.Create(FalschesProjekt);

    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        context.RegisterCompilationStartAction(start =>
        {
            var c = start.Compilation;
            var name = c.AssemblyName ?? "";
            var lage = new Lageart(c, EigeneTypen(c.Assembly.GlobalNamespace));
            if (!lage.Bereit) return;
            var wurzel = Projektstruktur.Zerlege(name).Wurzel;
            start.RegisterSymbolAction(ctx =>
            {
                var t = (INamedTypeSymbol)ctx.Symbol;
                if (lage.Von(t) is not { } p) return;
                if (Projektstruktur.PruefeProjekt(wurzel, name, p, t.Name) is not { } befund) return;
                var ort = t.Locations.FirstOrDefault(l => l.IsInSource);
                if (ort != null) ctx.ReportDiagnostic(Diagnostic.Create(FalschesProjekt, ort, befund));
            }, SymbolKind.NamedType);
        });
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
