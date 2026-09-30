using Microsoft.CodeAnalysis;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Infrastructure.SourceGeneration
{
    /// <summary>
    /// Verdrahtungs-Generator für den PULL-Pfad. Findet jede <c>ISubscriber</c>, die zusätzlich
    /// <c>Abstractions.IPullSubscriber</c> implementiert, und emittiert in
    /// <c>Infrastructure</c> (wie GeneratedSubscribers) die komplette, sonst handgeschriebene
    /// Verdrahtung:
    ///   - je Projektion einen per-Stream-Adapter-Kind (<c>IClusterKindContributor</c>),
    ///     der Tracker + Dispatch über einen Fähigkeits-Bereich auflöst (Store-Impl-Typ wird NIE genannt;
    ///     Achse B aus den Schreib-Fähigkeiten der Handles, CQRS052 bei mehreren Stores),
    ///   - eine <c>PullPathRegistration</c> (Receiver + Poller, generisch),
    ///   - <c>PushSubscriberExclusions</c> (koppelt den Push-Subscriber ab),
    ///   - <c>AddGeneratedPullPaths()</c>, das der Host als EINZIGEN Aufruf nutzt.
    ///
    /// KEIN handgeschriebener Domänen-Glue im Framework mehr — die Domäne steckt nur noch in
    /// der Projektion (Domain.Projections) und ihrem Store (Domain.Infrastructure).
    /// </summary>
    [Generator]
    public class PullPathGenerator : ISourceGenerator
    {
        private const string ISubscriberFullName = "Abstractions.ISubscriber";
        private const string IExactlyOnceFullName = "Abstractions.IPullSubscriber";

        /// <summary>CQRS052: die Schreib-Fähigkeiten eines Konsumenten stammen aus mehreren Stores (Achse B mehrdeutig).</summary>
        private static readonly DiagnosticDescriptor MehrereStores = new(
            "CQRS052",
            "Schreib-Fähigkeiten aus mehreren Stores",
            "{0}: die Schreib-Fähigkeiten der Handles gehören zu mehreren Stores ({1}) — ein Konsument schreibt in genau EINEN Store (eine Transaktionsgrenze, eine Marke)",
            "Cqrs.Faehigkeiten",
            DiagnosticSeverity.Error,
            isEnabledByDefault: true);

        public void Initialize(GeneratorInitializationContext context) { }

        public void Execute(GeneratorExecutionContext context)
        {
            var iSubscriber = context.Compilation.GetTypeByMetadataName(ISubscriberFullName);
            var iExactlyOnce = context.Compilation.GetTypeByMetadataName(IExactlyOnceFullName);
            if (iSubscriber == null || iExactlyOnce == null)
                return;

            var projections = new List<INamedTypeSymbol>();
            void Walk(INamespaceSymbol ns)
            {
                foreach (var t in ns.GetTypeMembers())
                {
                    if (t.TypeKind == TypeKind.Class && !t.IsAbstract &&
                        t.AllInterfaces.Contains(iExactlyOnce, SymbolEqualityComparer.Default))
                        projections.Add(t);
                }
                foreach (var child in ns.GetNamespaceMembers())
                    Walk(child);
            }
            Walk(context.Compilation.GlobalNamespace);

            var iWrite = context.Compilation.GetTypeByMetadataName("Abstractions.IWriteStore");
            var klassen = new List<INamedTypeSymbol>();
            void Klassen(INamespaceSymbol ns)
            {
                foreach (var t in ns.GetTypeMembers())
                    if (t.TypeKind == TypeKind.Class && !t.IsAbstract && iWrite != null
                        && t.AllInterfaces.Contains(iWrite, SymbolEqualityComparer.Default)) klassen.Add(t);
                foreach (var child in ns.GetNamespaceMembers()) Klassen(child);
            }
            Klassen(context.Compilation.GlobalNamespace);

            var faehigkeiten = new Dictionary<INamedTypeSymbol, List<INamedTypeSymbol>>(SymbolEqualityComparer.Default);
            foreach (var p in projections)
            {
                var fs = iWrite == null ? new List<INamedTypeSymbol>() : SchreibFaehigkeiten(p, iWrite);
                faehigkeiten[p] = fs;
                // Achse B zur Compile-Zeit: alle Schreib-Fähigkeiten einer Klasse gehören zu EINEM Store
                //   (einer Transaktionsgrenze). Sonst gäbe es zwei Marken für einen Konsumenten.
                var stores = fs
                    .Select(f => klassen.Where(k => k.AllInterfaces.Contains(f, SymbolEqualityComparer.Default)).ToList())
                    .SelectMany(x => x).Distinct<INamedTypeSymbol>(SymbolEqualityComparer.Default).ToList();
                if (stores.Count > 1)
                    context.ReportDiagnostic(Diagnostic.Create(MehrereStores, p.Locations.FirstOrDefault(), p.Name,
                        string.Join(", ", stores.Select(k => k.Name))));
            }

            context.AddSource("GeneratedPullPaths.g.cs",
                GenerateFile(projections.OrderBy(p => p.Name).ToList(), faehigkeiten));
        }

        private static string GenerateFile(List<INamedTypeSymbol> projections, Dictionary<INamedTypeSymbol, List<INamedTypeSymbol>> faehigkeiten)
        {
            var sb = new StringBuilder();
            sb.AppendLine("// <auto-generated/>");
            sb.AppendLine("// Verdrahtung des Pull-Pfads pro IPullSubscriber (PullPathGenerator).");
            sb.AppendLine("using Abstractions;");
            sb.AppendLine("using Core;");
            sb.AppendLine("using Infrastructure.Projections;");
            sb.AppendLine("using Infrastructure.PubSub;");
            sb.AppendLine("using Infrastructure.PubSub.Startup;");
            sb.AppendLine("using Microsoft.Extensions.DependencyInjection;");
            sb.AppendLine("using Proto;");
            sb.AppendLine("using Proto.Cluster;");
            sb.AppendLine("using System;");
            sb.AppendLine("using System.Collections.Generic;");
            sb.AppendLine("using System.Linq;");
            sb.AppendLine("using System.Threading.Tasks;");
            sb.AppendLine();
            sb.AppendLine("namespace Infrastructure.Projections.Generated;");
            sb.AppendLine();

            foreach (var p in projections)
                EmitKind(sb, p, faehigkeiten[p]);

            sb.AppendLine("public static class GeneratedPullPaths");
            sb.AppendLine("{");
            sb.AppendLine("    public static IServiceCollection AddGeneratedPullPaths(this IServiceCollection services)");
            sb.AppendLine("    {");
            if (projections.Count == 0)
            {
                sb.AppendLine("        // Keine IPullSubscriber gefunden.");
            }
            else
            {
                var names = string.Join(", ", projections.Select(p => "\"" + p.Name + "\""));
                sb.AppendLine($"        services.AddSingleton(new PushSubscriberExclusions(new[] {{ {names} }}));");
                sb.AppendLine();
                foreach (var p in projections)
                {
                    sb.AppendLine($"        services.AddSingleton<IClusterKindContributor, {p.Name}PullAdapterKind>();");
                    sb.AppendLine($"        services.AddSingleton(new PullPathRegistration(");
                    sb.AppendLine($"            {p.Name}PullAdapterKind.KindName, {p.Name}PullAdapterKind.KindName, {p.ToDisplayString()}.SubscribedTypes));");
                }
                sb.AppendLine();
                sb.AppendLine("        services.AddHostedService<GenericPullStartupService>();");
            }
            sb.AppendLine("        return services;");
            sb.AppendLine("    }");
            sb.AppendLine("}");

            return sb.ToString();
        }

        /// <summary>
        /// Die SCHREIB-Fähigkeiten (Marker <c>IWriteStore</c>) aller Handles einer Projektion — Achse B liest sich
        /// daraus (nicht mehr aus dem Konstruktor): hat der Konsument Schreib-Fähigkeiten, löst der Bereich den Store
        /// auf, der zugleich <c>IProjectionTracker</c> ist → replaybar. Ohne → emittierend.
        /// </summary>
        private static List<INamedTypeSymbol> SchreibFaehigkeiten(INamedTypeSymbol p, INamedTypeSymbol iWrite)
        {
            var result = new List<INamedTypeSymbol>();
            foreach (var m in p.GetMembers("Handle").OfType<IMethodSymbol>())
                foreach (var par in m.Parameters.Skip(3))
                    if (par.Type is INamedTypeSymbol t && t.TypeKind == TypeKind.Interface
                        && t.AllInterfaces.Contains(iWrite, SymbolEqualityComparer.Default)
                        && !result.Contains(t, SymbolEqualityComparer.Default))
                        result.Add(t);
            return result;
        }

        private static void EmitKind(StringBuilder sb, INamedTypeSymbol p, List<INamedTypeSymbol> schreibFaehigkeiten)
        {
            var fqProjection = p.ToDisplayString();
            var ctor = p.InstanceConstructors
                .Where(c => c.DeclaredAccessibility == Accessibility.Public && !c.IsStatic)
                .OrderByDescending(c => c.Parameters.Length)
                .FirstOrDefault();
            var ctorParams = ctor?.Parameters ?? default;

            sb.AppendLine($"internal sealed class {p.Name}PullAdapterKind : IClusterKindContributor");
            sb.AppendLine("{");
            sb.AppendLine($"    public const string KindName = \"pull-{p.Name}\";");
            sb.AppendLine();
            sb.AppendLine("    public ClusterKind CreateKind(ActorSystem system, IServiceProvider provider)");
            sb.AppendLine("    {");
            sb.AppendLine("        var eventStore = provider.GetRequiredService<IEventStoreRepository>();");
            sb.AppendLine("        var depsSink = provider.GetService<IReadModelDepsSink>();");
            sb.AppendLine("        return new ClusterKind(KindName, Props.FromProducer(() =>");
            sb.AppendLine("            new SignalAdapterActor(eventStore, () =>");
            sb.AppendLine("            {");
            sb.AppendLine("                // Ein Fähigkeits-Bereich je Adapter-Actor: jede Fähigkeit eines Stores liefert DIESELBE");
            sb.AppendLine("                //   Instanz (Co-Commit: Effekte + Marke in einer Transaktion).");
            sb.AppendLine("                var faehigkeiten = provider.GetRequiredService<IFaehigkeitsFabrik>().Oeffne();");

            var argNames = new List<string>();
            if (!ctorParams.IsDefault)
            {
                for (int i = 0; i < ctorParams.Length; i++)
                {
                    var t = ctorParams[i].Type.ToDisplayString();
                    sb.AppendLine($"                var d{i} = provider.GetRequiredService<{t}>();");
                    argNames.Add("d" + i);
                }
            }
            sb.AppendLine($"                var projection = new {fqProjection}({string.Join(", ", argNames)});");
            var holen = schreibFaehigkeiten.Select(f => $"faehigkeiten.Hole<{f.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat)}>()");
            sb.AppendLine($"                var candidates = new object[] {{ {string.Join(", ", holen)} }};");
            sb.AppendLine("                var trackers = candidates.OfType<IProjectionTracker>().Distinct().ToList();");
            sb.AppendLine("                if (trackers.Count > 1)");
            sb.AppendLine($"                    throw new InvalidOperationException(\"{p.Name}: Schreib-Fähigkeiten aus mehreren IProjectionTracker-Stores — höchstens einer erlaubt (Spec 7.6).\");");
            sb.AppendLine();
            sb.AppendLine("                // Achse-B-Schnitt (P4): lösen die Schreib-Fähigkeiten der Handles einen (co-committbaren)");
            sb.AppendLine("                //   IProjectionTracker-Store auf, ist der Konsument REPLAYBAR (Projektion). Sonst ist er EMITTIEREND");
            sb.AppendLine("                //   (Reaktion/Pipeline-Event) und bekommt den best-effort IEmittentenCursor (kein Reset).");
            sb.AppendLine("                var tracker = trackers.FirstOrDefault();");
            sb.AppendLine();
            sb.AppendLine("                // GA-1-Check (P4): eine append-artige Projektion OHNE Co-Commit-Tracker bricht hier");
            sb.AppendLine("                //   (statt still at-least-once doppelte Appends zu schreiben).");
            sb.AppendLine("                GaEinsPruefung.PrüfeCoCommit(projection, tracker, projection.SubscriberId);");
            sb.AppendLine();
            sb.AppendLine("                IEmittentenCursor? emittentenCursor = tracker is null");
            sb.AppendLine("                    ? provider.GetService<IEmittentenCursor>()");
            sb.AppendLine("                    : null;");
            sb.AppendLine();
            sb.AppendLine("                // Schritt A / Spec 8: DIESELBE Ausgabe-Route wie der Push-Actor (HandlerOutputRouter):");
            sb.AppendLine("                //   IEvent → re-publish, ICommand → Reaktion. system.Cluster() hier zur SPAWN-Zeit");
            sb.AppendLine("                //   (die Factory läuft im Actor-Started, Cluster ist fertig — NICHT bei Kind-Registrierung).");
            sb.AppendLine("                //   DetachedEmit hält den virtuellen Adapter-Turn frei (at-least-once, Empfänger dedupliziert).");
            sb.AppendLine("                var router = new HandlerOutputRouter(");
            sb.AppendLine("                    system.Cluster(), provider.GetService<BrokerPublisher>(), projection.SubscriberId);");
            sb.AppendLine("                Func<EventEnvelope, ProjectionWriter, Task> dispatch =");
            sb.AppendLine("                    (e, writer) => projection.DispatchAsync(e, writer,");
            sb.AppendLine("                        DetachedEmit.Wrap(router.EmitFor(e, System.Threading.CancellationToken.None)), faehigkeiten);");
            sb.AppendLine("                return (projection.SubscriberId, tracker, emittentenCursor, dispatch);");
            sb.AppendLine("            }, depsSink)));");
            sb.AppendLine("    }");
            sb.AppendLine("}");
            sb.AppendLine();
        }
    }
}
