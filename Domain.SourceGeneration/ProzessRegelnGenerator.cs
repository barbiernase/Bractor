using Abstractions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
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

        // ★ Werkbank P4 (E4): eine Prozess-Regel heißt nach ihren Typ-Argumenten (A+B→C) — die Wirker-Id im Event-Header und die
        //   Karte im Editor. Zwei gleiche Namen in einem Prozess wären in der Spur nicht unterscheidbar.
        private static readonly DiagnosticDescriptor RegelKollision = new DiagnosticDescriptor(
            id: "CQRS072",
            title: "Zwei Prozess-Regeln mit gleichem Namen",
            messageFormat: "Der Prozess '{0}' hat die Regel '{1}' mehrfach — eine Regel heißt nach Auslöser, Und-Events und Ziel. "
                + "Fasse die Regeln zusammen oder unterscheide sie im Auslöser bzw. Ziel.",
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

            // ★ Werkbank P4: die Namen für die Spur aus dem Quelltext (Fluss: Knoten-Variablen; Prozess: E4).
            var namen = new Dictionary<string, List<string>>();
            foreach (var (name, d) in benannt)
            {
                var istFluss = iPipeline != null && d.AllInterfaces.Contains(iPipeline, SymbolEqualityComparer.Default);
                var liste = istFluss ? KnotenNamen(comp, d) : RegelNamen(comp, d);
                namen[name] = liste;
                if (!istFluss)
                    foreach (var g in liste.GroupBy(x => x).Where(g => g.Count() > 1))
                        context.ReportDiagnostic(Diagnostic.Create(RegelKollision, d.Locations.FirstOrDefault() ?? Location.None, name, g.Key));
            }

            context.AddSource("GeneratedProzessRegeln.g.cs", Emit(benannt, iPipeline, ns, namen));
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

        private static readonly HashSet<string> KnotenVerben = new HashSet<string> { "Quelle", "Auf", "Rufe", "Sende", "Je", "Warte" };

        /// <summary>
        /// Die Knoten eines Flusses in Deklarations-Reihenfolge: jede Anweisung im <c>PipelineFluss.Definiere</c>-Lambda, deren Kern-Aufruf
        /// ein Knoten-Verb der DSL ist (<c>.Zeitlimit(…)</c>/<c>.Oder(…)</c> abgeschält) — Name = Variable bzw. die Regel aus <see cref="WirkerId"/>.
        /// Derselbe Weg wie der Extractor (<c>FlussLeser</c>).
        /// </summary>
        private static List<string> KnotenNamen(Compilation comp, INamedTypeSymbol def)
        {
            var variablen = new List<string?>();
            var typen = new List<string>();
            foreach (var lambda in DefiniereLambdas(comp, def, "Fluss", "PipelineFluss"))
            {
                if (!(lambda.Body is BlockSyntax block)) continue;
                var model = comp.GetSemanticModel(lambda.SyntaxTree);
                foreach (var st in block.Statements)
                {
                    string? name = null;
                    ExpressionSyntax? ausdruck = null;
                    if (st is LocalDeclarationStatementSyntax ld && ld.Declaration.Variables.Count == 1 && ld.Declaration.Variables[0].Initializer != null)
                    { name = ld.Declaration.Variables[0].Identifier.Text; ausdruck = ld.Declaration.Variables[0].Initializer!.Value; }
                    else if (st is ExpressionStatementSyntax es) ausdruck = es.Expression;
                    if (ausdruck == null) continue;
                    while (ausdruck is InvocationExpressionSyntax inv && inv.Expression is MemberAccessExpressionSyntax ma
                           && model.GetSymbolInfo(inv).Symbol is IMethodSymbol m && IstDsl(m) && (m.Name == "Zeitlimit" || m.Name == "Oder"))
                        ausdruck = ma.Expression;
                    if (!(ausdruck is InvocationExpressionSyntax kern) || !(model.GetSymbolInfo(kern).Symbol is IMethodSymbol k)
                        || !IstDsl(k) || !KnotenVerben.Contains(k.Name)) continue;
                    variablen.Add(name);
                    typen.Add(k.TypeArguments.FirstOrDefault()?.Name ?? "");
                }
            }
            return WirkerId.KnotenNamen(variablen, typen).ToList();
        }

        /// <summary>
        /// Die Regeln eines Prozesses in Laufzeit-Reihenfolge (jedes <c>Sende</c>/<c>SendeJe</c>/<c>Rufe</c> eines Regel-Bauers hängt eine
        /// Regel an) mit ihrem Namen nach E4 — Bedingung aus den Typ-Argumenten des Bauers (<c>RegelBauer&lt;A, B&gt;</c>), beim Count-Join
        /// (<c>RegelBauerSammel&lt;…, S&gt;</c>) das letzte als Sammel-Event, Ziel = Typ-Argument des Aufrufs.
        /// </summary>
        private static List<string> RegelNamen(Compilation comp, INamedTypeSymbol def)
        {
            var namen = new List<string>();
            foreach (var lambda in DefiniereLambdas(comp, def, "Regeln", "Prozess"))
            {
                var model = comp.GetSemanticModel(lambda.SyntaxTree);
                foreach (var inv in lambda.DescendantNodes().OfType<InvocationExpressionSyntax>().OrderBy(i => i.Span.End))
                {
                    if (!(model.GetSymbolInfo(inv).Symbol is IMethodSymbol m) || !IstDsl(m)
                        || (m.Name != "Sende" && m.Name != "SendeJe" && m.Name != "Rufe")) continue;
                    var bauer = m.ContainingType;
                    if (bauer.Name != "RegelBauer" && bauer.Name != "RegelBauerSammel") continue;
                    var args = bauer.TypeArguments.Select(t => t.Name).ToList();
                    string? sammel = null;
                    if (bauer.Name == "RegelBauerSammel") { sammel = args[args.Count - 1]; args.RemoveAt(args.Count - 1); }
                    namen.Add(WirkerId.Regel(args, sammel, m.TypeArguments.FirstOrDefault()?.Name ?? ""));
                }
            }
            return namen;
        }

        /// <summary>
        /// Die Lambdas der <c>Definiere</c>-Aufrufe (am Typ <paramref name="dslTyp"/>) in der Implementierung der Interface-Property
        /// <paramref name="property"/> — über die Implementierung, nicht den Namen am Typ (auch explizit: <c>ProzessRegeln IProzessDefinition.Regeln</c>).
        /// </summary>
        private static IEnumerable<LambdaExpressionSyntax> DefiniereLambdas(Compilation comp, INamedTypeSymbol def, string property, string dslTyp)
        {
            var impls = def.AllInterfaces
                .Where(i => i.ContainingNamespace?.ToDisplayString() == "Abstractions" && (i.Name == "IProzessDefinition" || i.Name == "IPipeline"))
                .SelectMany(i => i.GetMembers(property))
                .Select(m => def.FindImplementationForInterfaceMember(m))
                .OfType<IPropertySymbol>()
                .Distinct<IPropertySymbol>(SymbolEqualityComparer.Default);
            foreach (var p in impls)
                foreach (var r in p.DeclaringSyntaxReferences)
                {
                    var syntax = r.GetSyntax();
                    var model = comp.GetSemanticModel(syntax.SyntaxTree);
                    foreach (var inv in syntax.DescendantNodes().OfType<InvocationExpressionSyntax>())
                        if (model.GetSymbolInfo(inv).Symbol is IMethodSymbol m && m.Name == "Definiere" && m.ContainingType?.Name == dslTyp && IstDsl(m)
                            && inv.ArgumentList.Arguments.FirstOrDefault()?.Expression is LambdaExpressionSyntax l)
                            yield return l;
                }
        }

        private static bool IstDsl(IMethodSymbol m) => m.ContainingType?.ContainingNamespace?.ToDisplayString() == "Abstractions";

        private static void Collect(INamespaceSymbol ns, IAssemblySymbol assembly, List<INamedTypeSymbol> acc)
        {
            foreach (var t in ns.GetTypeMembers())
                if (SymbolEqualityComparer.Default.Equals(t.ContainingAssembly, assembly))
                    acc.Add(t);
            foreach (var child in ns.GetNamespaceMembers())
                Collect(child, assembly, acc);
        }

        private static string Emit(List<(string Name, INamedTypeSymbol Def)> defs, INamedTypeSymbol? iPipeline, string ns,
            Dictionary<string, List<string>> namen)
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
                var liste = string.Join(", ", namen[name].Select(n => "\"" + n + "\""));
                sb.AppendLine($"            [\"{name}\"] = (({(istFluss ? "IPipeline" : "IProzessDefinition")})new {d.ToDisplayString()}()).{(istFluss ? "Fluss.Regeln" : "Regeln")}.Benannt(\"{name}\"{(liste.Length > 0 ? ", " + liste : "")}),");
            }
            sb.AppendLine("        };");
            sb.AppendLine("    }");
            sb.AppendLine("}");
            return sb.ToString();
        }
    }
}
