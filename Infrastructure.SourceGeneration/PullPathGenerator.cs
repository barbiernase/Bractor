using Microsoft.CodeAnalysis;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Infrastructure.SourceGeneration
{
    /// <summary>
    /// Verdrahtungs-Generator für den PULL-Pfad. Findet jede <c>ISubscriber</c>, die zusätzlich
    /// <c>Abstractions.IPullSubscriber</c> implementiert, und emittiert die komplette, sonst handgeschriebene
    /// Verdrahtung. Achse B folgt aus den Schreib-Fähigkeiten der Handles (CQRS052 bei mehreren Stores):
    ///   - REPLAYBAR (Projektion, Schreib-Fähigkeiten): ein Batch-Kind <c>batch-&lt;Typ&gt;</c> je Projektion — ein Leser je
    ///     Partition, der Store aus einem Fähigkeits-Bereich ist die Batch-Naht (Store-Impl-Typ wird NIE genannt), dazu eine
    ///     <c>BatchRegistrierung</c> (Signal → Partition, Poll je Partition) und der <c>ProjektionsStand</c>;
    ///   - EMITTIEREND (Reaktion, keine Schreib-Fähigkeit): ein Adapter-Kind <c>pull-&lt;Typ&gt;</c> je Stream mit Emittenten-Cursor
    ///     und eine <c>PullPathRegistration</c> (Receiver + Poller);
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
            if (!Laufzeitprojekt.Ist(context.Compilation)) return;   // nur in der Komposition (docs/07)
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

            // Die Store-Klasse(n) hinter den Schreib-Fähigkeiten — Teil des Projektions-Stands (konzept-werkbank §12.3 E9). Nur replaybare Konsumenten
            // (mit Store) bekommen einen Stand; emittierende werden nie neu aufgebaut.
            var storesJe = projections.ToDictionary(p => p, p => faehigkeiten[p]
                .SelectMany(f => klassen.Where(k => k.AllInterfaces.Contains(f, SymbolEqualityComparer.Default)))
                .Distinct<INamedTypeSymbol>(SymbolEqualityComparer.Default).ToList(), (IEqualityComparer<INamedTypeSymbol>)SymbolEqualityComparer.Default);

            context.AddSource("GeneratedPullPaths.g.cs",
                GenerateFile(projections.OrderBy(p => p.Name).ToList(), faehigkeiten, storesJe));
        }

        private static string GenerateFile(List<INamedTypeSymbol> projections, Dictionary<INamedTypeSymbol, List<INamedTypeSymbol>> faehigkeiten,
            Dictionary<INamedTypeSymbol, List<INamedTypeSymbol>> storesJe)
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
            sb.AppendLine("using Microsoft.Extensions.DependencyInjection.Extensions;");
            sb.AppendLine("using Microsoft.Extensions.Logging;");
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
            {
                if (storesJe[p].Count > 0) EmitBatchKind(sb, p, faehigkeiten[p]);
                else EmitKind(sb, p);
            }

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
                var batch = false;
                var proStream = false;
                foreach (var p in projections)
                {
                    var stores = storesJe[p];
                    if (stores.Count > 0)
                    {
                        batch = true;
                        var hash = p.GetMembers("CodeHash").OfType<IFieldSymbol>().Any(f => f.IsConst) ? $"{p.ToDisplayString()}.CodeHash" : "\"\"";
                        var typen = string.Join(", ", stores.Select(k => $"typeof({k.ToDisplayString()})"));
                        sb.AppendLine($"        services.AddSingleton<IClusterKindContributor, {p.Name}BatchKind>();");
                        sb.AppendLine($"        services.AddSingleton(sp => new BatchRegistrierung({p.Name}BatchKind.KindName,");
                        sb.AppendLine($"            sp.GetRequiredService<{p.ToDisplayString()}>().SubscriberId, {p.ToDisplayString()}.SubscribedTypes));");
                        sb.AppendLine($"        services.AddSingleton(sp => new ProjektionsStand({p.Name}BatchKind.KindName,");
                        sb.AppendLine($"            sp.GetRequiredService<{p.ToDisplayString()}>().SubscriberId, {hash}, new Type[] {{ {typen} }}));");
                    }
                    else
                    {
                        proStream = true;
                        sb.AppendLine($"        services.AddSingleton<IClusterKindContributor, {p.Name}PullAdapterKind>();");
                        sb.AppendLine($"        services.AddSingleton(new PullPathRegistration(");
                        sb.AppendLine($"            {p.Name}PullAdapterKind.KindName, {p.Name}PullAdapterKind.KindName, {p.ToDisplayString()}.SubscribedTypes));");
                    }
                }
                sb.AppendLine();
                if (batch)
                {
                    sb.AppendLine("        services.AddHostedService<BatchKonsumStartupService>();");
                    sb.AppendLine("        services.TryAddSingleton<IKonsumRueckstand, KonsumRueckstand>();");
                }
                if (proStream)
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

        /// <summary>Die Konstruktion der Projektion bzw. Reaktion aus dem Container (Konstruktor-Parameter als Dienste).</summary>
        private static void EmitKonstruktion(StringBuilder sb, INamedTypeSymbol p, string einzug)
        {
            var ctor = p.InstanceConstructors
                .Where(c => c.DeclaredAccessibility == Accessibility.Public && !c.IsStatic)
                .OrderByDescending(c => c.Parameters.Length)
                .FirstOrDefault();
            var ctorParams = ctor?.Parameters ?? default;
            var argNames = new List<string>();
            if (!ctorParams.IsDefault)
            {
                for (int i = 0; i < ctorParams.Length; i++)
                {
                    sb.AppendLine($"{einzug}var d{i} = provider.GetRequiredService<{ctorParams[i].Type.ToDisplayString()}>();");
                    argNames.Add("d" + i);
                }
            }
            sb.AppendLine($"{einzug}var projection = new {p.ToDisplayString()}({string.Join(", ", argNames)});");
        }

        /// <summary>Replaybar: ein Batch-Leser je Partition (docs/04 §4.1); der Store der Schreib-Fähigkeiten ist die Batch-Naht.</summary>
        private static void EmitBatchKind(StringBuilder sb, INamedTypeSymbol p, List<INamedTypeSymbol> schreibFaehigkeiten)
        {
            sb.AppendLine($"internal sealed class {p.Name}BatchKind : IClusterKindContributor");
            sb.AppendLine("{");
            sb.AppendLine($"    public const string KindName = \"batch-{p.Name}\";");
            sb.AppendLine();
            sb.AppendLine("    public ClusterKind CreateKind(ActorSystem system, IServiceProvider provider)");
            sb.AppendLine("    {");
            sb.AppendLine("        var eventStore = provider.GetRequiredService<IEventStoreRepository>();");
            sb.AppendLine("        var depsSink = provider.GetService<IReadModelDepsSink>();");
            sb.AppendLine("        var deadLetter = provider.GetService<IDeadLetterSink>();");
            sb.AppendLine("        var konsum = provider.GetService<Konsum>() ?? Konsum.Standard;");
            sb.AppendLine($"        var logger = provider.GetService<ILoggerFactory>()?.CreateLogger(\"Konsum.{p.Name}\");");
            sb.AppendLine("        // Typfilter der Seiten: die Speicher-Diskriminatoren der abonnierten Typen samt Upcasting-Vorgängern.");
            sb.AppendLine($"        var typen = BatchLeser.TypenAus({p.ToDisplayString()}.SubscribedTypes);");
            sb.AppendLine("        return new ClusterKind(KindName, Props.FromProducer(() =>");
            sb.AppendLine("            new BatchLeserActor(partition =>");
            sb.AppendLine("            {");
            sb.AppendLine("                // Ein Fähigkeits-Bereich je Partitions-Leser: jede Fähigkeit eines Stores liefert DIESELBE Instanz");
            sb.AppendLine("                //   (Co-Commit: Effekte, Guards und Partition in einer Transaktion).");
            sb.AppendLine("                var faehigkeiten = provider.GetRequiredService<IFaehigkeitsFabrik>().Oeffne();");
            EmitKonstruktion(sb, p, "                ");
            var holen = schreibFaehigkeiten.Select(f => $"faehigkeiten.Hole<{f.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat)}>()");
            sb.AppendLine($"                var trackers = new object[] {{ {string.Join(", ", holen)} }}.OfType<IProjectionTracker>().Distinct().ToList();");
            sb.AppendLine("                if (trackers.Count != 1)");
            sb.AppendLine($"                    throw new InvalidOperationException(\"{p.Name}: die Schreib-Fähigkeiten müssen genau einen IProjectionTracker-Store auflösen (gefunden: \" + trackers.Count + \") — docs/04 §4.2, CQRS052.\");");
            sb.AppendLine();
            sb.AppendLine("                // GA-1-Check (P4): eine append-artige Projektion OHNE Co-Commit-Tracker bricht hier");
            sb.AppendLine("                //   (statt still at-least-once doppelte Appends zu schreiben).");
            sb.AppendLine("                GaEinsPruefung.PrüfeCoCommit(projection, trackers[0], projection.SubscriberId);");
            sb.AppendLine();
            sb.AppendLine("                Func<EventEnvelope, ProjectionWriter, Task> dispatch =");
            sb.AppendLine("                    (e, writer) => projection.DispatchAsync(e, writer, BatchLeser.KeineAusgabe, faehigkeiten);");
            sb.AppendLine("                var leser = new BatchLeser(eventStore, trackers[0], projection.SubscriberId, partition, konsum, typen, dispatch,");
            sb.AppendLine("                    depsSink, deadLetter, logger);");
            sb.AppendLine("                return (leser, (IDisposable?)faehigkeiten);");
            sb.AppendLine("            }, logger)));");
            sb.AppendLine("    }");
            sb.AppendLine("}");
            sb.AppendLine();
        }

        /// <summary>Emittierend: ein Adapter je Stream mit Emittenten-Cursor (durabel, ab Cursor, kein Reset; E8).</summary>
        private static void EmitKind(StringBuilder sb, INamedTypeSymbol p)
        {
            sb.AppendLine($"internal sealed class {p.Name}PullAdapterKind : IClusterKindContributor");
            sb.AppendLine("{");
            sb.AppendLine($"    public const string KindName = \"pull-{p.Name}\";");
            sb.AppendLine();
            sb.AppendLine("    public ClusterKind CreateKind(ActorSystem system, IServiceProvider provider)");
            sb.AppendLine("    {");
            sb.AppendLine("        var eventStore = provider.GetRequiredService<IEventStoreRepository>();");
            sb.AppendLine("        return new ClusterKind(KindName, Props.FromProducer(() =>");
            sb.AppendLine("            new SignalAdapterActor(eventStore, () =>");
            sb.AppendLine("            {");
            sb.AppendLine("                var faehigkeiten = provider.GetRequiredService<IFaehigkeitsFabrik>().Oeffne();");
            EmitKonstruktion(sb, p, "                ");
            sb.AppendLine();
            sb.AppendLine("                // Achse B (P4): ohne Schreib-Fähigkeit ist der Konsument EMITTIEREND (Reaktion) und bekommt den");
            sb.AppendLine("                //   IEmittentenCursor (durabel, ab Cursor, kein Reset).");
            sb.AppendLine("                var emittentenCursor = provider.GetService<IEmittentenCursor>();");
            sb.AppendLine();
            sb.AppendLine("                // DIESELBE Ausgabe-Route wie der Push-Actor (HandlerOutputRouter):");
            sb.AppendLine("                //   IEvent → re-publish, ICommand → Reaktion. system.Cluster() hier zur SPAWN-Zeit");
            sb.AppendLine("                //   (die Factory läuft im Actor-Started, Cluster ist fertig — NICHT bei Kind-Registrierung).");
            sb.AppendLine("                //   Die Commands einer Weckung laufen parallel und tragen das gebundene Token der Weckung; der Adapter wartet");
            sb.AppendLine("                //   ihre Ausgänge ab, bevor der Cursor vorrückt (G-2, P1-6).");
            sb.AppendLine("                var router = new HandlerOutputRouter(");
            sb.AppendLine($"                    system.Cluster(), provider.GetService<BrokerPublisher>(), projection.SubscriberId, wirker: global::Abstractions.WirkerId.FuerReaktion(\"{p.Name}\"));");
            sb.AppendLine("                Func<EventEnvelope, ProjectionWriter, EmitAusgaenge, Task> dispatch =");
            sb.AppendLine("                    (e, writer, ausgaenge) => projection.DispatchAsync(e, writer, router.EmitFor(e, ausgaenge), faehigkeiten);");
            sb.AppendLine("                return (projection.SubscriberId, emittentenCursor, dispatch);");
            sb.AppendLine("            }, provider.GetService<IReadModelDepsSink>(), provider.GetService<global::Infrastructure.Lebenszyklus.Passivierung>())));");
            sb.AppendLine("    }");
            sb.AppendLine("}");
            sb.AppendLine();
        }
    }
}
