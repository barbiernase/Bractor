using Microsoft.CodeAnalysis;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Infrastructure.SourceGeneration
{
    /// <summary>
    /// Verdrahtungs-Generator für den Konsum der PROJEKTIONEN (docs/10 §10.3). Findet jede Projektion (<c>IProjektion</c>,
    /// gelesen über <see cref="Projektionsform"/> — dieselbe Lesart wie der Weckplan) und emittiert die komplette Verdrahtung:
    ///   - je Projektion den Dispatch (<c>switch</c> über den Event-Typ → ihr <c>Auf</c> mit Herkunft und Fähigkeiten aus dem Bereich), die gehörten
    ///     Typen und den Store aus einem Fähigkeits-Bereich (<c>GeneratedProjektionen</c>);
    ///   - ein Batch-Kind <c>batch-&lt;Projektion&gt;</c> — ein Leser je Partition, die Naht nach der Store-Klasse (Store-Impl-Typ wird NIE genannt);
    ///   - <c>AddGeneratedPullPaths()</c>: Batch-Kinds, Takt-Registrierung, <c>Projektionseintrag</c> (Neuaufbau, Stand je Id) und der Query-Dienst.
    /// Die Kennung einer Projektion ist ihr Typname (Code-Fakt), nicht mehr ein handgeschriebener String.
    ///
    /// CQRS052 — eine Projektion schreibt in genau EINEN Store (R9): ihre Schreib-Fähigkeiten gehören zu einer Store-Klasse.
    /// CQRS110 — eine Projektion schreibt in einen Store der Klasse 3 ohne die ausdrückliche Erlaubnis <c>[MindestensEinmal]</c> (R14).
    /// </summary>
    [Generator]
    public class PullPathGenerator : ISourceGenerator
    {
        public const string MindestensEinmalId = "CQRS110";

        /// <summary>CQRS052: eine Projektion schreibt in genau einen Store (R9) — nicht in mehrere, nicht in keinen.</summary>
        private static readonly DiagnosticDescriptor MehrereStores = new DiagnosticDescriptor(
            "CQRS052",
            "Eine Projektion schreibt in genau einen Store",
            "{0}: {1} — eine Projektion schreibt in genau EINEN Store (eine Transaktionsgrenze, ein Fortschritt); was auf ein Event hin etwas sendet, ist eine Funktion (IFunktion)",
            "Cqrs.Faehigkeiten",
            DiagnosticSeverity.Error,
            isEnabledByDefault: true);

        private static readonly DiagnosticDescriptor OhneErlaubnis = new DiagnosticDescriptor(
            MindestensEinmalId,
            "Store der Klasse 3 ohne Erlaubnis",
            "{0} schreibt in {1}, einen Store der Klasse 3 (INurMindestensEinmal) — seine Wirkungen gelten nur mindestens einmal; das braucht die ausdrückliche "
                + "Erlaubnis [MindestensEinmal] an der Projektion (R14)",
            "Cqrs.Faehigkeiten",
            DiagnosticSeverity.Error,
            isEnabledByDefault: true);

        public void Initialize(GeneratorInitializationContext context) { }

        public void Execute(GeneratorExecutionContext context)
        {
            if (!Laufzeitprojekt.Ist(context.Compilation)) return;   // nur in der Komposition (docs/07)
            var bild = Leseseitenbild.Lies(context.Compilation);

            var gueltig = new List<(Projektionsform.Projektion P, List<INamedTypeSymbol> Stores)>();
            foreach (var p in bild.Projektionen)
            {
                if (p.Fehler.Count > 0 || p.Doppelt.Count > 0) continue;   // meldet der HandlerFormAnalyzer in der Compilation der Projektion
                var ort = p.Typ.Locations.FirstOrDefault();
                var stores = bild.Stores(p);
                if (stores.Count != 1)
                {
                    context.ReportDiagnostic(Diagnostic.Create(MehrereStores, ort, p.Typ.Name, stores.Count == 0
                        ? "keine Schreib-Fähigkeit eines Stores in den Auf"
                        : "die Schreib-Fähigkeiten gehören zu mehreren Stores (" + string.Join(", ", stores.Select(k => k.Name)) + ")"));
                    continue;
                }
                if (Projektionsform.StoreKlassen(stores[0]).Contains(3) && !p.MindestensEinmal)
                    context.ReportDiagnostic(Diagnostic.Create(OhneErlaubnis, ort, p.Typ.Name, stores[0].Name));
                gueltig.Add((p, stores));
            }

            context.AddSource("GeneratedPullPaths.g.cs", GenerateFile(gueltig.OrderBy(g => g.P.Typ.Name, System.StringComparer.Ordinal).ToList()));
        }

        private static string Fq(ITypeSymbol t) => t.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);

        /// <summary>Der Code-Stand der Projektion aus ihrer Compilation (<c>[assembly: Projektionsstand(typeof(P), "…")]</c>, Domain.SourceGeneration).</summary>
        private static string Hash(INamedTypeSymbol p)
        {
            foreach (var a in p.ContainingAssembly.GetAttributes())
                if (a.AttributeClass != null && Projektionsform.Name(a.AttributeClass) == Abstractions.Projektionsvertrag.Projektionsstand
                    && a.ConstructorArguments.Length == 2 && a.ConstructorArguments[0].Value is INamedTypeSymbol t
                    && SymbolEqualityComparer.Default.Equals(t, p) && a.ConstructorArguments[1].Value is string h)
                    return h;
            return "";
        }

        private static string GenerateFile(List<(Projektionsform.Projektion P, List<INamedTypeSymbol> Stores)> projektionen)
        {
            var sb = new StringBuilder();
            sb.AppendLine("// <auto-generated/>");
            sb.AppendLine("// Verdrahtung der Projektionen (PullPathGenerator).");
            sb.AppendLine("#nullable enable");
            sb.AppendLine("using Abstractions;");
            sb.AppendLine("using Infrastructure.Projections;");
            sb.AppendLine("using Microsoft.Extensions.DependencyInjection;");
            sb.AppendLine("using Microsoft.Extensions.DependencyInjection.Extensions;");
            sb.AppendLine("using Microsoft.Extensions.Logging;");
            sb.AppendLine("using Proto;");
            sb.AppendLine("using Proto.Cluster;");
            sb.AppendLine("using System;");
            sb.AppendLine("using System.Threading;");
            sb.AppendLine("using System.Threading.Tasks;");
            sb.AppendLine();
            sb.AppendLine("namespace Infrastructure.Projections.Generated;");
            sb.AppendLine();

            // ── Dispatch, Typen, Store je Projektion ──
            sb.AppendLine("/// <summary>Je Projektion: die gehörten Typen, der Dispatch auf ihr Auf und ihr Store aus einem Fähigkeits-Bereich.</summary>");
            sb.AppendLine("public static class GeneratedProjektionen");
            sb.AppendLine("{");
            foreach (var (p, _) in projektionen)
            {
                var n = p.Typ.Name;
                var wirker = Abstractions.Projektionsvertrag.Wirker(n);
                sb.AppendLine($"    // ── {n} ──");
                sb.AppendLine($"    public const string {n}_Kennung = \"{Abstractions.Projektionsvertrag.Kennung(n)}\";");
                sb.AppendLine($"    public static readonly Type[] {n}_Typen = {{ {string.Join(", ", p.Aufs.Select(a => $"typeof({Fq(a.Eingang)})"))} }};");
                sb.AppendLine($"    public static IProjektionsZiel {n}_Ziel(IFaehigkeiten f) => Projektionslauf.Ziel({n}_Kennung, {(p.MindestensEinmal ? "true" : "false")}, "
                    + string.Join(", ", p.SchreibFaehigkeiten().Select(c => $"f.Hole<{Fq(c)}>()")) + ");");
                sb.AppendLine($"    public static Task {n}_Dispatch({Fq(p.Typ)} projektion, EventEnvelope e, IFaehigkeiten f, CancellationToken ct)");
                sb.AppendLine("    {");
                sb.AppendLine("        switch (e.Payload)");
                sb.AppendLine("        {");
                foreach (var a in p.Aufs)
                {
                    var args = a.Parameter.Select(x => x.Art switch
                    {
                        "e" => "p",
                        "h" => $"Projektionslauf.Herkunft(e, \"{wirker}\")",
                        "ct" => "ct",
                        _ => $"f.Hole<{Fq(x.Typ!)}>()",
                    });
                    sb.AppendLine($"            case {Fq(a.Eingang)} p: return projektion.{Abstractions.Projektionsvertrag.Auf}({string.Join(", ", args)});");
                }
                sb.AppendLine("            default: return Task.CompletedTask;");
                sb.AppendLine("        }");
                sb.AppendLine("    }");
                sb.AppendLine();
            }
            sb.AppendLine("}");
            sb.AppendLine();

            foreach (var (p, _) in projektionen)
                EmitBatchKind(sb, p.Typ);

            sb.AppendLine("public static class GeneratedPullPaths");
            sb.AppendLine("{");
            sb.AppendLine("    public static IServiceCollection AddGeneratedPullPaths(this IServiceCollection services)");
            sb.AppendLine("    {");
            foreach (var (p, stores) in projektionen)
            {
                var n = p.Typ.Name;
                var typen = string.Join(", ", stores.Select(k => $"typeof({Fq(k)})"));
                sb.AppendLine($"        services.AddSingleton<IClusterKindContributor, {n}BatchKind>();");
                sb.AppendLine($"        services.AddSingleton(new BatchRegistrierung({n}BatchKind.KindName, GeneratedProjektionen.{n}_Kennung, GeneratedProjektionen.{n}_Typen));");
                sb.AppendLine($"        services.AddSingleton(new Projektionseintrag(GeneratedProjektionen.{n}_Kennung, {n}BatchKind.KindName, GeneratedProjektionen.{n}_Typen,");
                sb.AppendLine($"            \"{Hash(p.Typ)}\", new Type[] {{ {typen} }}, GeneratedProjektionen.{n}_Ziel));");
            }
            if (projektionen.Count > 0)
            {
                sb.AppendLine("        services.AddHostedService<BatchKonsumStartupService>();");
                sb.AppendLine("        services.TryAddSingleton<IKonsumRueckstand, KonsumRueckstand>();");
            }
            sb.AppendLine("        // Der Query-Dienst der Leseseite (QueryDienstGenerator).");
            sb.AppendLine("        services.AddSingleton<GeneratedQueryDienst>();");
            sb.AppendLine("        services.AddSingleton<IQueryDienst>(sp => sp.GetRequiredService<GeneratedQueryDienst>());");
            sb.AppendLine("        return services;");
            sb.AppendLine("    }");
            sb.AppendLine("}");
            return sb.ToString();
        }

        /// <summary>Die Konstruktion aus dem Container (Konstruktor-Parameter als Dienste).</summary>
        internal static string Konstruktion(INamedTypeSymbol t, string provider)
        {
            var ctor = t.InstanceConstructors
                .Where(c => c.DeclaredAccessibility == Accessibility.Public && !c.IsStatic)
                .OrderByDescending(c => c.Parameters.Length)
                .FirstOrDefault();
            var args = ctor == null ? "" : string.Join(", ", ctor.Parameters.Select(par => $"{provider}.GetRequiredService<{Fq(par.Type)}>()"));
            return $"new {Fq(t)}({args})";
        }

        /// <summary>Ein Batch-Leser je Partition (docs/04 §4.1); die Naht folgt der Store-Klasse.</summary>
        private static void EmitBatchKind(StringBuilder sb, INamedTypeSymbol p)
        {
            var n = p.Name;
            sb.AppendLine($"internal sealed class {n}BatchKind : IClusterKindContributor");
            sb.AppendLine("{");
            sb.AppendLine($"    public const string KindName = \"{Abstractions.Projektionsvertrag.Kind(n)}\";");
            sb.AppendLine();
            sb.AppendLine("    public ClusterKind CreateKind(ActorSystem system, IServiceProvider provider)");
            sb.AppendLine("    {");
            sb.AppendLine("        var eventStore = provider.GetRequiredService<IEventStoreRepository>();");
            sb.AppendLine("        var deadLetter = provider.GetService<IDeadLetterSink>();");
            sb.AppendLine("        var konsum = provider.GetService<Konsum>() ?? Konsum.Standard;");
            sb.AppendLine($"        var logger = provider.GetService<ILoggerFactory>()?.CreateLogger(\"Konsum.{n}\");");
            sb.AppendLine("        // Typfilter der Seiten: die Speicher-Diskriminatoren der gehörten Typen samt Upcasting-Vorgängern.");
            sb.AppendLine($"        var typen = BatchLeser.TypenAus(GeneratedProjektionen.{n}_Typen);");
            sb.AppendLine("        return new ClusterKind(KindName, Props.FromProducer(() =>");
            sb.AppendLine("            new BatchLeserActor((partition, faehigkeiten) =>");
            sb.AppendLine("            {");
            sb.AppendLine("                // Der Fähigkeits-Bereich der Aktivierung (Rahmen): jede Fähigkeit eines Stores liefert DIESELBE Instanz; frei, wenn die Aktivierung endet.");
            sb.AppendLine($"                var projektion = {Konstruktion(p, "provider")};");
            sb.AppendLine($"                var naht = new Projektionsnaht(GeneratedProjektionen.{n}_Ziel(faehigkeiten), provider.GetService<IFortschrittBeimFramework>());");
            sb.AppendLine("                // „P hat S weiterverarbeitet“ an die Clients — erst beim Spawnen aufgelöst (die Verteilung braucht das fertige Actor-System).");
            sb.AppendLine("                var verteilung = provider.GetService<global::Infrastructure.Verteilen.Verteilung>();");
            sb.AppendLine($"                return new BatchLeser(eventStore, naht, GeneratedProjektionen.{n}_Kennung, partition, konsum, typen,");
            sb.AppendLine($"                    (e, ct) => GeneratedProjektionen.{n}_Dispatch(projektion, e, faehigkeiten, ct),");
            sb.AppendLine("                    deadLetter: deadLetter, logger: logger,");
            sb.AppendLine($"                    verarbeitet: verteilung is null ? null : streams => verteilung.MeldeVerarbeitet(GeneratedProjektionen.{n}_Kennung, streams));");
            sb.AppendLine("            }, provider.GetRequiredService<IFaehigkeitsFabrik>(), logger)));");
            sb.AppendLine("    }");
            sb.AppendLine("}");
            sb.AppendLine();
        }
    }
}
