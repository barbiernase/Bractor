using Microsoft.CodeAnalysis;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Domain.SourceGeneration
{
    /// <summary>
    /// Der EINE Prozess-Generator im neuen Modell (Spec §4): er emittiert aus jeder
    /// <c>IProzessDefinition</c> den DAG-DESKRIPTOR — die Regeln als Daten — in eine Registry
    /// <c>GeneratedProzessRegeln.Alle</c> (Prozess-Name → <c>ProzessRegeln</c>). KEIN per-Prozess-Aggregat
    /// mehr: der Manager ist generisch, die Regeln parameterisieren ihn. Der Prozess-Name ist der
    /// Definitions-Klassenname (daraus leitet der Korrelations-Router die Korrelation ab).
    ///
    /// Die Infrastruktur-Seite (Manager-Kind, Router, Startup) ist generisch und handgeschrieben — sie
    /// liest nur diese Registry. Deshalb genügt EIN Domain-Generator; kein Infrastructure-Generator nötig.
    /// </summary>
    [Generator]
    public class ProzessRegelnGenerator : ISourceGenerator
    {
        // ★ P1c (TG-3): zwei Prozesse mit gleichem aufgelöstem Namen → gleiche Korrelations-Ableitung
        //   (ProzessId.Für) + stille Überschreibung in der Registry. Fail-fast am Build.
        private static readonly DiagnosticDescriptor ProzessKollision = new DiagnosticDescriptor(
            id: "CQRS012",
            title: "Zwei Prozesse mit gleichem Namen",
            messageFormat: "Der Prozess-Name '{0}' ist mehrdeutig — er wird von mehreren Definitionen belegt ({1}). "
                + "Gib mindestens einer ein explizites [ProzessName(\"...\")], um sie zu unterscheiden.",
            category: "Cqrs",
            defaultSeverity: DiagnosticSeverity.Error,
            isEnabledByDefault: true);

        public void Initialize(GeneratorInitializationContext context) { }

        public void Execute(GeneratorExecutionContext context)
        {
            var comp = context.Compilation;
            var iDef = comp.GetTypeByMetadataName("Abstractions.IProzessDefinition");
            if (iDef == null) return;
            // Pipeline-Flüsse (docs/konzept-editor-pipelines.md §14) laufen auf demselben Dirigenten: ihre übersetzten Regeln
            // stehen in derselben Registry (Name = Klassenname, gleiche Kollisionsprüfung).
            var iPipeline = comp.GetTypeByMetadataName("Abstractions.IPipeline");

            var declared = new List<INamedTypeSymbol>();
            Collect(comp.GlobalNamespace, comp.Assembly, declared);

            var defs = declared
                .Where(t => t.TypeKind == TypeKind.Class && !t.IsAbstract &&
                            (t.AllInterfaces.Contains(iDef, SymbolEqualityComparer.Default) ||
                             (iPipeline != null && t.AllInterfaces.Contains(iPipeline, SymbolEqualityComparer.Default))))
                .OrderBy(t => t.Name)
                .ToList();

            // Die Registry je Assembly in ihrem EIGENEN Namespace ({Assembly}.Prozess) — so kollidieren Domain und Domain.Pipeline
            // (Flüsse, deren Funktionen Fähigkeiten der Leseseite brauchen) nicht (CS0433); Infrastructure vereinigt sie.
            // Domain emittiert immer — auch leer, wenn es gar keine Prozesse gibt.
            if (defs.Count == 0 && context.Compilation.AssemblyName != "Domain") return;
            var ns = (context.Compilation.AssemblyName ?? "Domain") + ".Prozess";

            // ★ P1c (TG-3): Name aus [ProzessName] (falls gesetzt), sonst Klassenname. Kollisionen → Build-Fehler.
            var benannt = defs.Select(d => (Name: ProzessIdentität(d), Def: d)).ToList();
            foreach (var g in benannt.GroupBy(x => x.Name).Where(g => g.Count() > 1))
                context.ReportDiagnostic(Diagnostic.Create(
                    ProzessKollision, g.First().Def.Locations.FirstOrDefault() ?? Location.None,
                    g.Key, string.Join(", ", g.Select(x => x.Def.ToDisplayString()))));

            context.AddSource("GeneratedProzessRegeln.g.cs", Emit(benannt, iPipeline, ns));
        }

        /// <summary>
        /// ★ P1c (TG-3): der Prozess-Name — <c>[ProzessName("…")]</c> falls gesetzt, sonst der Klassenname
        /// (Default, keine Migration).
        /// </summary>
        private static string ProzessIdentität(INamedTypeSymbol def)
        {
            var attr = def.GetAttributes().FirstOrDefault(a =>
                a.AttributeClass?.ToDisplayString() == "Abstractions.ProzessNameAttribute");
            if (attr != null && attr.ConstructorArguments.Length > 0 &&
                attr.ConstructorArguments[0].Value is string s && !string.IsNullOrWhiteSpace(s))
                return s;
            return def.Name;
        }

        private static void Collect(INamespaceSymbol ns, IAssemblySymbol assembly, List<INamedTypeSymbol> acc)
        {
            foreach (var t in ns.GetTypeMembers())
                if (SymbolEqualityComparer.Default.Equals(t.ContainingAssembly, assembly))
                    acc.Add(t);
            foreach (var child in ns.GetNamespaceMembers())
                Collect(child, assembly, acc);
        }

        private static string Emit(List<(string Name, INamedTypeSymbol Def)> defs, INamedTypeSymbol? iPipeline, string ns)
        {
            var sb = new StringBuilder();
            sb.AppendLine("// <auto-generated/>");
            sb.AppendLine("// Regel-Registry pro IProzessDefinition (ProzessRegelnGenerator). Nicht editieren.");
            sb.AppendLine("#nullable enable");
            sb.AppendLine("using System.Collections.Generic;");
            sb.AppendLine("using Abstractions;");
            sb.AppendLine();
            sb.AppendLine($"namespace {ns}");
            sb.AppendLine("{");
            sb.AppendLine("    public static class GeneratedProzessRegeln");
            sb.AppendLine("    {");
            sb.AppendLine("        // Prozess-Name (= Definitions-Klassenname) → seine Regeln (der DAG-Deskriptor).");
            sb.AppendLine("        public static readonly IReadOnlyDictionary<string, ProzessRegeln> Alle =");
            sb.AppendLine("            new Dictionary<string, ProzessRegeln>");
            sb.AppendLine("        {");
            foreach (var (name, d) in defs)
            {
                var istFluss = iPipeline != null && d.AllInterfaces.Contains(iPipeline, SymbolEqualityComparer.Default);
                sb.AppendLine($"            [\"{name}\"] = new {d.ToDisplayString()}().{(istFluss ? "Fluss.Regeln" : "Regeln")},");
            }
            sb.AppendLine("        };");
            sb.AppendLine("    }");
            sb.AppendLine("}");
            return sb.ToString();
        }
    }
}
