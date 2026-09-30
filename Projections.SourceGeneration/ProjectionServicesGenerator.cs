using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using Microsoft.CodeAnalysis;

namespace Projections.SourceGeneration
{
    /// <summary>
    /// Generiert <c>AddGeneratedProjectionServices()</c> — die DI-Verdrahtung aller Projektions-Komponenten,
    /// die früher von Hand in <c>DomainServiceExtension.AddDomainProjectionServices()</c> stand (der eine
    /// steile Boilerplate-Herd: ~6 Registrierungen + 1 Marten-Schema-Block PRO Projektion).
    ///
    /// Alles typ-/interface-getrieben entdeckt (Namen sind unzuverlässig: „Projection" vs „Projektion"):
    ///   - Marten-Schema: je <c>IReadModel</c> ein uniformer <c>Schema.For&lt;T&gt;()</c>-Block.
    ///   - Stores: jede Klasse mit Fähigkeiten (Marker <c>IWriteStore</c>/<c>IReadStore</c>) SCOPED — eine Instanz je
    ///     Fähigkeits-Bereich —, umgeleitet unter jeder Fähigkeit und dem Bündel (<c>IStore</c>). Ctor-Argumente per
    ///     Parameter-Inspektion. Eine Fähigkeit mit zwei Klassen ist CQRS053.
    ///   - Reader: je <c>IReader&lt;T&gt;</c>-Implementierer ein <c>AddSingleton</c>.
    ///   - Projektionen: je <c>ISubscriber</c>+<c>IPullSubscriber</c> ein <c>AddSingleton</c>.
    ///   - <c>ProjectionQueryService</c> (nur hier registriert).
    ///
    /// Läuft in der Domain.Infrastructure-Compilation (sieht Store-Klassen + Domain.Projections + Marten).
    /// </summary>
    [Generator]
    public class ProjectionServicesGenerator : ISourceGenerator
    {
        /// <summary>CQRS053: eine Fähigkeit wird von mehreren Store-Klassen implementiert — die DI wüsste nicht, welche.</summary>
        private static readonly DiagnosticDescriptor MehrdeutigeFaehigkeit = new(
            "CQRS053",
            "Fähigkeit mehrdeutig",
            "Die Fähigkeit {0} wird von mehreren Store-Klassen implementiert ({1}) — genau eine Klasse je Fähigkeit",
            "Cqrs.Faehigkeiten",
            DiagnosticSeverity.Error,
            isEnabledByDefault: true);

        public void Initialize(GeneratorInitializationContext context) { }

        public void Execute(GeneratorExecutionContext context)
        {
            var comp = context.Compilation;
            var iReadModel = comp.GetTypeByMetadataName("Abstractions.IReadModel");
            var iSubscriber = comp.GetTypeByMetadataName("Abstractions.ISubscriber");
            var iPull = comp.GetTypeByMetadataName("Abstractions.IPullSubscriber");
            var iReader = comp.GetTypeByMetadataName("Abstractions.IReader`1");
            var iWrite = comp.GetTypeByMetadataName("Abstractions.IWriteStore");
            var iRead = comp.GetTypeByMetadataName("Abstractions.IReadStore");
            var iStore = comp.GetTypeByMetadataName("Abstractions.IStore");
            if (iReadModel == null || iSubscriber == null || iPull == null || iReader == null
                || iWrite == null || iRead == null || iStore == null)
                return;

            // Nur Typen aus Assemblies, die den Vertrag (Abstractions) referenzieren — der Vertrag selbst und
            // fremde Bibliotheken fallen heraus. Die Rolle entscheiden danach allein die Marker.
            var vertrag = iReadModel.ContainingAssembly;
            var classes = new List<INamedTypeSymbol>();
            var ifaces = new List<INamedTypeSymbol>();
            var assemblies = new List<IAssemblySymbol> { comp.Assembly };
            assemblies.AddRange(comp.SourceModule.ReferencedAssemblySymbols);
            foreach (var asm in assemblies)
                if (!SymbolEqualityComparer.Default.Equals(asm, vertrag)
                    && (SymbolEqualityComparer.Default.Equals(asm, comp.Assembly)
                        || asm.Modules.Any(m => m.ReferencedAssemblySymbols.Any(r => SymbolEqualityComparer.Default.Equals(r, vertrag)))))
                    Collect(asm.GlobalNamespace, classes, ifaces);

            var full = SymbolDisplayFormat.FullyQualifiedFormat;
            bool Impl(INamedTypeSymbol t, INamedTypeSymbol i) => t.AllInterfaces.Contains(i, SymbolEqualityComparer.Default);

            // ── ReadModels (Marten-Schema) ──
            var readModels = classes
                .Where(c => Impl(c, iReadModel))
                .OrderBy(c => c.Name, System.StringComparer.Ordinal)
                .ToList();

            // ── Stores: jede Klasse, die Fähigkeiten (IWriteStore/IReadStore) implementiert. Registriert wird sie
            //    SCOPED — eine Instanz je Fähigkeits-Bereich (Pull-Actor / Pipeline-Actor / Query) — und unter JEDER
            //    ihrer Fähigkeiten plus dem Bündel (IStore) auf dieselbe Instanz umgeleitet → Co-Commit bleibt. ──
            bool IstFaehigkeit(INamedTypeSymbol i) => Impl(i, iWrite) || Impl(i, iRead);
            var stores = classes
                .Where(c => c.AllInterfaces.Any(IstFaehigkeit))
                .OrderBy(c => c.ToDisplayString(full), System.StringComparer.Ordinal)
                .ToList();
            foreach (var f in stores.SelectMany(c => c.AllInterfaces.Where(IstFaehigkeit)).Distinct<INamedTypeSymbol>(SymbolEqualityComparer.Default))
            {
                var impls = stores.Where(c => Impl(c, f)).ToList();
                if (impls.Count > 1)
                    context.ReportDiagnostic(Diagnostic.Create(MehrdeutigeFaehigkeit, impls[0].Locations.FirstOrDefault(),
                        f.Name, string.Join(", ", impls.Select(c => c.Name))));
            }

            // ── Reader + Projektionen ──
            var readers = classes
                .Where(c => c.AllInterfaces.Any(i => i.IsGenericType &&
                       SymbolEqualityComparer.Default.Equals(i.OriginalDefinition, iReader)))
                .OrderBy(c => c.Name, System.StringComparer.Ordinal)
                .ToList();
            var projections = classes
                .Where(c => Impl(c, iSubscriber) && Impl(c, iPull))
                .OrderBy(c => c.Name, System.StringComparer.Ordinal)
                .ToList();

            var projectionQueryService = comp.GetTypeByMetadataName("Domain.Projections.ProjectionQueryService");

            // ── Emit ──
            var sb = new StringBuilder();
            sb.AppendLine("// <auto-generated/> — ProjectionServicesGenerator. Ersetzt DomainServiceExtension.");
            sb.AppendLine("#nullable enable");
            sb.AppendLine("using Marten;");
            sb.AppendLine("using Microsoft.Extensions.DependencyInjection;");
            sb.AppendLine();
            sb.AppendLine("namespace Domain.Infrastructure.Generated;");
            sb.AppendLine();
            sb.AppendLine("/// <summary>Generierte DI-Verdrahtung aller Projektions-Komponenten (Stores/Reader/Projektionen + Marten-Schema).</summary>");
            sb.AppendLine("public static class GeneratedProjectionServices");
            sb.AppendLine("{");
            sb.AppendLine("    public static IServiceCollection AddGeneratedProjectionServices(this IServiceCollection services)");
            sb.AppendLine("    {");

            // Marten-Schema
            sb.AppendLine("        services.ConfigureMarten(options =>");
            sb.AppendLine("        {");
            foreach (var rm in readModels)
            {
                sb.AppendLine($"            options.Schema.For<{rm.ToDisplayString(full)}>()");
                sb.AppendLine("                .DatabaseSchemaName(\"rm\")");
                sb.AppendLine("                .Identity(x => x.Id)");
                sb.AppendLine("                .UseOptimisticConcurrency(false);");
            }
            sb.AppendLine("        });");
            sb.AppendLine();

            // Stores: eine Instanz je Bereich, unter jeder Fähigkeit + dem Bündel
            foreach (var store in stores)
            {
                var fq = store.ToDisplayString(full);
                sb.AppendLine($"        // ── {store.Name} ──");
                sb.AppendLine($"        services.AddScoped<{fq}>(sp => {NewExpr(store, full)});");
                foreach (var i in store.AllInterfaces
                             .Where(i => IstFaehigkeit(i) || Impl(i, iStore))
                             .OrderBy(i => i.ToDisplayString(full), System.StringComparer.Ordinal))
                    sb.AppendLine($"        services.AddScoped<{i.ToDisplayString(full)}>(sp => sp.GetRequiredService<{fq}>());");
                sb.AppendLine();
            }

            // Reader
            foreach (var r in readers)
                sb.AppendLine($"        services.AddSingleton<{r.ToDisplayString(full)}>();");
            sb.AppendLine();

            // Projektionen
            foreach (var p in projections)
                sb.AppendLine($"        services.AddSingleton<{p.ToDisplayString(full)}>();");

            // Query-Service (nur hier registriert)
            if (projectionQueryService != null)
            {
                sb.AppendLine();
                sb.AppendLine($"        services.AddSingleton<{projectionQueryService.ToDisplayString(full)}>();");
            }

            sb.AppendLine();
            sb.AppendLine("        return services;");
            sb.AppendLine("    }");
            sb.AppendLine("}");

            context.AddSource("GeneratedProjectionServices.g.cs", sb.ToString());
        }

        /// <summary>`new Class(sp.GetRequiredService&lt;P1&gt;(), …)` aus dem öffentlichen Ctor mit den meisten Parametern.</summary>
        private static string NewExpr(INamedTypeSymbol type, SymbolDisplayFormat full)
        {
            var ctor = type.InstanceConstructors
                .Where(c => c.DeclaredAccessibility == Accessibility.Public)
                .OrderByDescending(c => c.Parameters.Length)
                .FirstOrDefault();
            var args = ctor == null
                ? ""
                : string.Join(", ", ctor.Parameters.Select(p => $"sp.GetRequiredService<{p.Type.ToDisplayString(full)}>()"));
            return $"new {type.ToDisplayString(full)}({args})";
        }

        private static void Collect(INamespaceSymbol ns, List<INamedTypeSymbol> classes, List<INamedTypeSymbol> ifaces)
        {
            foreach (var t in ns.GetTypeMembers())
            {
                if (t.TypeKind == TypeKind.Class && !t.IsAbstract && !t.IsStatic) classes.Add(t);
                else if (t.TypeKind == TypeKind.Interface) ifaces.Add(t);
            }
            foreach (var sub in ns.GetNamespaceMembers())
                Collect(sub, classes, ifaces);
        }
    }
}
