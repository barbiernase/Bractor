// In Projekt: Infrastructure.SourceGeneration
// Dateiname: WeckplanGenerator.cs

using Microsoft.CodeAnalysis;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Infrastructure.SourceGeneration
{
    /// <summary>
    /// Der WECKPLAN (Plan Pub/Sub-Neustrukturierung §2.3, Phase 6): je Event-Typ, wen ein Commit weckt — die Batch-Leser der Projektionen, die ihn
    /// hören (<c>batch-&lt;Projektion&gt;</c>, je Partition), die Funktions-Actors, die ihn aus dem Log hören (<c>funktion-&lt;F&gt;</c>, je Partition
    /// bzw. die eine Adresse), und die Uhr (<c>FristGesetzt</c>). Alles NUR aus den Signaturen:
    /// <code>public Task Handle(BildErfasst e, IAggregateEnvelope env, ProjectionWriter w, ISchreibeBild s)   → batch-BildProjektion</code>
    /// <code>public OneOf&lt;Mahne&gt; Auf(Ueberfaellig e, Herkunft h)                                         → funktion-Mahnwesen</code>
    /// Der Wecker am Flush (<c>Infrastructure.Wecken.Wecker</c>) schlägt hier nach und weckt gezielt; der Takt je Partition bleibt das Netz.
    ///
    /// CQRS108 — ein Hörer aus dem Log hört einen Typ, den der Plan nicht führen kann: kein konkretes, persistentes Event (eine Schnittstelle, eine
    /// abstrakte Klasse, ein Hinweis). Der Wecker sucht nach dem Typ des geschriebenen Events — ein solcher Hörer würde nie geweckt (und der
    /// Batch-Leser fände ihn im Log nicht). Dass jeder registrierte Hörer mit jedem gehörten Typ im Plan steht, prüft der Wecker beim Start.
    /// </summary>
    [Generator]
    public class WeckplanGenerator : ISourceGenerator
    {
        public const string NichtImPlanId = "CQRS108";

        /// <summary>Das Cluster-Kind der Uhr (<c>Infrastructure.Uhr.UhrActor.KindName</c>) und das Event, das sie weckt.</summary>
        private const string UhrKind = "uhr", FristGesetzt = "Abstractions.FristGesetzt";

        private static readonly DiagnosticDescriptor NichtImPlan = new DiagnosticDescriptor(
            id: NichtImPlanId,
            title: "Gehörter Typ kann nicht im Weckplan stehen",
            messageFormat: "'{0}' hört '{1}' aus dem Log, aber {2} — der Weckplan führt nur konkrete, persistente Events (der Wecker sucht nach dem Typ des "
                + "geschriebenen Events); dieser Hörer würde nie geweckt",
            category: "CQRS.Weckplan",
            defaultSeverity: DiagnosticSeverity.Error,
            isEnabledByDefault: true);

        public void Initialize(GeneratorInitializationContext context) { }

        public void Execute(GeneratorExecutionContext context)
        {
            if (!Laufzeitprojekt.Ist(context.Compilation)) return;   // nur in der Komposition (docs/07)
            var fq = SymbolDisplayFormat.FullyQualifiedFormat;
            var eintraege = new List<(INamedTypeSymbol Typ, string Kind, bool EineAdresse)>();

            foreach (var t in Vertragsassemblies.Typen(context.Compilation).OrderBy(t => t.ToDisplayString(), System.StringComparer.Ordinal))
            {
                // Projektionen: Handle(TEvent, IAggregateEnvelope, ProjectionWriter, Fähigkeiten…) — der Batch-Leser je Partition.
                if (t.TypeKind == TypeKind.Class && !t.IsAbstract && Funktionsform.Hat(t, "Abstractions.IPullSubscriber"))
                {
                    foreach (var m in t.GetMembers("Handle").OfType<IMethodSymbol>())
                    {
                        if (m.Parameters.Length < 3 || Funktionsform.Name(m.Parameters[1].Type) != "Abstractions.IAggregateEnvelope"
                            || Funktionsform.Name(m.Parameters[2].Type) != "Core.ProjectionWriter") continue;
                        if (Pruefe(context, t, m.Parameters[0].Type, m.Locations.FirstOrDefault()) is { } e) eintraege.Add((e, "batch-" + t.Name, false));
                    }
                }

                // Funktionen: Auf mit einem Event aus dem Log (Hinweise gehen übers Hinweis-Netz, nicht über den Weckplan).
                if (Funktionsform.IstBaustein(t))
                {
                    var f = Funktionsform.Lies(t);
                    if (f.Fehler.Count > 0 || f.Doppelt.Count > 0) continue;   // CQRS100/101 meldet der Baustein-Generator
                    foreach (var a in f.Aufs.Where(a => !a.Hinweis))
                        if (Pruefe(context, t, a.Eingang, a.Methode.Locations.FirstOrDefault()) is { } e)
                            eintraege.Add((e, Abstractions.Funktionsvertrag.Kind(t.Name), f.EineAdresse));
                }
            }

            // Die Uhr: eine gesetzte Frist kann sofort fällig sein — sie wartet nicht auf ihren Takt.
            if (context.Compilation.GetTypeByMetadataName(FristGesetzt) is { } frist) eintraege.Add((frist, UhrKind, true));

            context.AddSource("GeneratedWeckplan.g.cs", Emit(eintraege, fq));
        }

        /// <summary>Der gehörte Typ, wenn er im Plan stehen kann; sonst CQRS108 und null.</summary>
        private static INamedTypeSymbol? Pruefe(GeneratorExecutionContext context, INamedTypeSymbol hoerer, ITypeSymbol typ, Location? ort)
        {
            string? grund = null;
            if (typ is not INamedTypeSymbol n || n.TypeKind == TypeKind.Interface) grund = "der Typ ist eine Schnittstelle";
            else if (n.IsAbstract) grund = "der Typ ist abstrakt";
            else if (Funktionsform.Hat(n, "Abstractions.ITransientEvent")) grund = "der Typ ist ein Hinweis (ITransientEvent) und steht nie im Log";
            else if (!Funktionsform.Hat(n, "Abstractions.IEvent")) grund = "der Typ ist kein Event";
            if (grund == null) return (INamedTypeSymbol)typ;
            context.ReportDiagnostic(Diagnostic.Create(NichtImPlan, ort ?? hoerer.Locations.FirstOrDefault() ?? Location.None, hoerer.Name,
                typ.ToDisplayString(), grund));
            return null;
        }

        private static string Emit(List<(INamedTypeSymbol Typ, string Kind, bool EineAdresse)> eintraege, SymbolDisplayFormat fq)
        {
            var sb = new StringBuilder();
            sb.AppendLine("// <auto-generated/>");
            sb.AppendLine("// Der Weckplan: Event-Typ → wen ein Commit weckt (WeckplanGenerator). Nicht editieren.");
            sb.AppendLine("#nullable enable");
            sb.AppendLine("using System;");
            sb.AppendLine("using System.Collections.Generic;");
            sb.AppendLine();
            sb.AppendLine("namespace Infrastructure.Wecken");
            sb.AppendLine("{");
            sb.AppendLine("    public static class GeneratedWeckplan");
            sb.AppendLine("    {");
            sb.AppendLine("        /// <summary>Je Event-Typ die Ziele, die ein Commit dieses Typs weckt (Cluster-Kind; eine Adresse oder die Partition des Streams).</summary>");
            sb.AppendLine("        public static readonly IReadOnlyDictionary<Type, Weckziel[]> Plan = new Dictionary<Type, Weckziel[]>");
            sb.AppendLine("        {");
            foreach (var g in eintraege.GroupBy(e => e.Typ.ToDisplayString(fq)).OrderBy(g => g.Key, System.StringComparer.Ordinal))
            {
                var ziele = g.Select(e => (e.Kind, e.EineAdresse)).Distinct().OrderBy(z => z.Kind, System.StringComparer.Ordinal)
                    .Select(z => $"new Weckziel(\"{z.Kind}\"{(z.EineAdresse ? ", EineAdresse: true" : "")})");
                sb.AppendLine($"            [typeof({g.Key})] = new[] {{ {string.Join(", ", ziele)} }},");
            }
            sb.AppendLine("        };");
            sb.AppendLine("    }");
            sb.AppendLine("}");
            return sb.ToString();
        }
    }
}
