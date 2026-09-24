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
    ///   - Stores: je <c>IReadStore&lt;TWrite&gt;</c>-Interface das Paar (TWrite, Read) — die Paarung steht als Typ im
    ///     Code (Marker aus Abstractions), die Namen sind frei; je Seite die konkrete Klasse (Symbol-Query). Ctor-Argumente per Parameter-Inspektion (robust gegen (IDocumentStore)
    ///     vs (IDocumentStore, ILogger&lt;T&gt;)). Lifetime: Write = Co-Commit/Transient; separater Read-Store
    ///     (Postgres) = Singleton; ist Read == Write (eine Klasse) → beide Transient.
    ///   - Reader: je <c>IReader&lt;T&gt;</c>-Implementierer ein <c>AddSingleton</c>.
    ///   - Projektionen: je <c>ISubscriber</c>+<c>IPullSubscriber</c> ein <c>AddSingleton</c>.
    ///   - <c>ProjectionQueryService</c> (nur hier registriert).
    ///
    /// Läuft in der Domain.Infrastructure-Compilation (sieht Store-Klassen + Domain.Projections + Marten).
    /// </summary>
    [Generator]
    public class ProjectionServicesGenerator : ISourceGenerator
    {
        public void Initialize(GeneratorInitializationContext context) { }

        public void Execute(GeneratorExecutionContext context)
        {
            var comp = context.Compilation;
            var iReadModel = comp.GetTypeByMetadataName("Abstractions.IReadModel");
            var iSubscriber = comp.GetTypeByMetadataName("Abstractions.ISubscriber");
            var iPull = comp.GetTypeByMetadataName("Abstractions.IPullSubscriber");
            var iReader = comp.GetTypeByMetadataName("Abstractions.IReader`1");
            var iReadStoreT = comp.GetTypeByMetadataName("Abstractions.IReadStore`1");
            if (iReadModel == null || iSubscriber == null || iPull == null || iReader == null || iReadStoreT == null)
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

            // ── Store-Paare: IReadStore<TWrite> nennt seinen Schreib-Partner als Typ ──
            var paare = new List<(INamedTypeSymbol Write, INamedTypeSymbol Read)>();
            foreach (var i in ifaces)
            {
                var r = i.Interfaces.FirstOrDefault(x => x.IsGenericType
                    && SymbolEqualityComparer.Default.Equals(x.OriginalDefinition, iReadStoreT));
                if (r?.TypeArguments[0] is INamedTypeSymbol w) paare.Add((w, i));
            }
            paare = paare.OrderBy(p => p.Write.ToDisplayString(full), System.StringComparer.Ordinal).ToList();

            INamedTypeSymbol? ConcreteImpl(INamedTypeSymbol i) =>
                classes.FirstOrDefault(c => Impl(c, i));

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

            // Stores je Basis
            foreach (var (writeIface, readIface) in paare)
            {
                var b = writeIface.Name;
                var writeClass = ConcreteImpl(writeIface);
                var readClass = ConcreteImpl(readIface);
                if (writeClass == null || readClass == null) continue;

                sb.AppendLine($"        // ── {b} ──");
                // Write-Store: Co-Commit, Transient
                sb.AppendLine($"        services.AddTransient<{writeClass.ToDisplayString(full)}>(sp => {NewExpr(writeClass, full)});");
                sb.AppendLine($"        services.AddTransient<{writeIface.ToDisplayString(full)}>(sp => sp.GetRequiredService<{writeClass.ToDisplayString(full)}>());");

                if (SymbolEqualityComparer.Default.Equals(readClass, writeClass))
                {
                    // Muster B: eine Klasse bedient read + write (beide Transient)
                    sb.AppendLine($"        services.AddTransient<{readIface.ToDisplayString(full)}>(sp => sp.GetRequiredService<{writeClass.ToDisplayString(full)}>());");
                }
                else
                {
                    // Muster A: separater Postgres-Read-Store, Singleton
                    sb.AppendLine($"        services.AddSingleton<{readClass.ToDisplayString(full)}>(sp => {NewExpr(readClass, full)});");
                    sb.AppendLine($"        services.AddSingleton<{readIface.ToDisplayString(full)}>(sp => sp.GetRequiredService<{readClass.ToDisplayString(full)}>());");
                }
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
