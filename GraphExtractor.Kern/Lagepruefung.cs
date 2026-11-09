using Domain.SourceGeneration;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using P = Abstractions.Projektstruktur;

namespace GraphExtractor;

/// <summary>
/// Die LAGE-PRÜFUNG (<c>docs/07-projektstruktur.md</c>, Teil von <c>--check</c>): die Projekt- und Ordnerstruktur der Anwendung gegen die eine
/// Tabelle <see cref="P"/> — die Domänen-Projekte sind Rollen-Projekte der Wurzel, ihre Referenzen folgen der Richtung, der Codegen
/// referenziert alle Codegen-Projekte, und jede Primitive liegt in Projekt, Bereichs-Ordner, Namespace und Datei ihrer Rolle.
/// WAS ein Typ ist, sagt <see cref="Lageart"/> (dieselbe Lesart wie der Analyzer CQRS098). Falscher Ort = Befund, nie still.
/// </summary>
public static class Lagepruefung
{
    public static List<string> Pruefe(Solution solution, Projektlage lage, string wurzel = P.StandardWurzel)
    {
        var befunde = new List<string>();
        var domaene = lage.Analyse.Where(a => lage.DomänenAssemblies.Contains(a.Compilation.AssemblyName ?? "")).ToList();

        // ── Projekte: jedes Domänen-Projekt ist ein Rollen-Projekt der Wurzel oder ein Katalog; Referenzen nur in Richtung der Tabelle ──
        foreach (var (projekt, _) in domaene)
            if (!P.IstProjekt(wurzel, projekt.AssemblyName) && !P.IstKatalog(projekt.AssemblyName))
                befunde.Add($"Projekt {projekt.AssemblyName} deklariert Domänen-Typen, ist aber kein Rollen-Projekt von {wurzel} ({string.Join(", ", P.Projekte(wurzel))}; docs/07 §7.2)");
        foreach (var projekt in solution.Projects.Where(p => P.IstProjekt(wurzel, p.AssemblyName)))
        {
            var erlaubt = P.Von(P.Zerlege(projekt.AssemblyName).Rolle).Referenziert.Select(r => P.Projekt(wurzel, r)).ToHashSet(StringComparer.Ordinal);
            foreach (var r in projekt.ProjectReferences.Select(r => solution.GetProject(r.ProjectId)?.AssemblyName).OfType<string>())
                if (P.IstProjekt(wurzel, r) && !erlaubt.Contains(r))
                    befunde.Add($"{projekt.AssemblyName} referenziert {r} — erlaubt sind nur {(erlaubt.Count == 0 ? "keine Rollen-Projekte" : string.Join(", ", erlaubt))} (docs/07 §7.2)");
                else if (P.IstKatalog(r) && !P.NutzenKataloge.Contains(P.Zerlege(projekt.AssemblyName).Rolle) && !Ueber(projekt, r))
                    befunde.Add($"{projekt.AssemblyName} referenziert den Katalog {r} — die Domäne bleibt rein, sie kennt keinen Katalog; Kataloge nutzen nur "
                        + $"{string.Join(", ", P.NutzenKataloge.Select(x => P.Projekt(wurzel, x)))} (docs/07 §7.2)");
        }
        // Der Katalog kommt nur über ein Rollen-Projekt, das ihn nutzen darf (die Übersetzungen) — MSBuild reicht Referenzen
        //   transitiv weiter, gemeint ist hier nur die eigene.
        bool Ueber(Project projekt, string katalog) => projekt.ProjectReferences.Select(x => solution.GetProject(x.ProjectId)).OfType<Project>()
            .Any(x => P.IstProjekt(wurzel, x.AssemblyName) && P.NutzenKataloge.Contains(P.Zerlege(x.AssemblyName).Rolle)
                && x.ProjectReferences.Any(y => solution.GetProject(y.ProjectId)?.AssemblyName == katalog));
        // ── Kataloge: sie referenzieren nur Abstractions, das Framework und andere Kataloge, nie eine Anwendung ──
        foreach (var katalog in solution.Projects.Where(p => P.IstKatalog(p.AssemblyName)))
            foreach (var r in katalog.ProjectReferences.Select(r => solution.GetProject(r.ProjectId)).OfType<Project>())
                if (!P.IstKatalog(r.AssemblyName) && !P.KatalogDarfReferenzieren.Contains(r.AssemblyName) && !IstGenerator(r))
                    befunde.Add($"Der Katalog {katalog.AssemblyName} referenziert {r.AssemblyName} — ein Katalog kennt nur {string.Join(", ", P.KatalogDarfReferenzieren)} "
                        + "und andere Kataloge, nie eine Anwendung (docs/07 §7.2)");
        if (solution.Projects.FirstOrDefault(p => p.AssemblyName == P.CodegenWerkzeug) is { } codegen)
        {
            var refs = codegen.ProjectReferences.Select(r => solution.GetProject(r.ProjectId)?.AssemblyName).OfType<string>().ToHashSet(StringComparer.Ordinal);
            foreach (var fehlt in P.CodegenProjekte(wurzel).Where(p => solution.Projects.Any(x => x.AssemblyName == p) && !refs.Contains(p)))
                befunde.Add($"{P.CodegenWerkzeug} referenziert {fehlt} nicht — der Codegen-Prepass liefe vor dessen Build (docs/07 §7.5)");
        }

        // ── Typen: Projekt, Ordner, Namespace, Datei ──
        var deklarationen = domaene.SelectMany(a => Deklarationen(a.Projekt, a.Compilation, lage.Quelltexte)).ToList();
        var typen = deklarationen.Select(d => d.Typ).Distinct(SymbolEqualityComparer.Default).Cast<INamedTypeSymbol>().ToList();
        var lageart = new Lageart(domaene.First().Compilation, typen);
        foreach (var (projekt, typ, datei) in deklarationen)
        {
            if (lageart.Von(typ) is not { } p || !(P.IstProjekt(wurzel, projekt.AssemblyName) || P.IstKatalog(projekt.AssemblyName))) continue;
            if (P.PruefeProjekt(wurzel, projekt.AssemblyName, p, typ.Name) is { } b) befunde.Add(b);
            var verzeichnis = Path.GetDirectoryName(projekt.FilePath!)!;
            var rel = Path.GetRelativePath(verzeichnis, datei).Replace('\\', '/');
            var name = p is P.Primitive.Decider or P.Primitive.Applier ? Lageart.Aggregat(typ) + "." + typ.Name : typ.Name;
            befunde.AddRange(P.PruefeOrt(projekt.AssemblyName, rel, typ.ContainingNamespace.ToDisplayString(), p, name, Lageart.Aggregat(typ),
                p == P.Primitive.StoreFaehigkeit ? Lageart.Buendel(typ, typen) : null));
        }
        return befunde.Distinct(StringComparer.Ordinal).ToList();
    }

    /// <summary>Ein Generator- bzw. Analyzer-Projekt des Frameworks (als <c>OutputItemType="Analyzer"</c> eingebunden): sein Code ist im Katalog nicht sichtbar.</summary>
    private static bool IstGenerator(Project p) => p.AssemblyName.EndsWith(".SourceGeneration", StringComparison.Ordinal);

    /// <summary>Jede handgeschriebene Typ-Deklaration (auch jedes partial-Teil) mit ihrer Datei.</summary>
    private static IEnumerable<(Project Projekt, INamedTypeSymbol Typ, string Datei)> Deklarationen(Project projekt, Compilation c, Quelltexte quelltexte)
    {
        foreach (var baum in c.SyntaxTrees.Where(t => !quelltexte.IstGeneriert(t) && t.FilePath.Length > 0))
        {
            var modell = c.GetSemanticModel(baum);
            foreach (var d in baum.GetRoot().DescendantNodes().OfType<BaseTypeDeclarationSyntax>())
                if (modell.GetDeclaredSymbol(d) is INamedTypeSymbol t) yield return (projekt, t, baum.FilePath);
        }
    }
}
