using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using Microsoft.CodeAnalysis;
using Domain.SourceGeneration;

namespace Projections.SourceGeneration
{
    /// <summary>
    /// Generiert <c>AddGeneratedProjectionServices()</c> — die DI-Verdrahtung aller Projektions-Komponenten,
    /// die früher von Hand in <c>DomainServiceExtension.AddDomainProjectionServices()</c> stand (der eine
    /// steile Boilerplate-Herd: ~6 Registrierungen + 1 Marten-Schema-Block PRO Projektion).
    ///
    /// Alles typ-/interface-getrieben entdeckt (Namen sind unzuverlässig: „Projection" vs „Projektion"):
    ///   - Marten-Schema: je <c>IReadModel</c> ein uniformer <c>Schema.For&lt;T&gt;()</c>-Block; Optimistic Concurrency nur
    ///     für <c>IGeteiltesReadModel</c> (aus mehreren Streams beschrieben).
    ///   - Stores: jede Klasse mit Fähigkeiten (Marker <c>IWriteStore</c>/<c>IReadStore</c>) SCOPED — eine Instanz je
    ///     Fähigkeits-Bereich —, umgeleitet unter jeder Fähigkeit und dem Bündel (<c>IStore</c>). Ctor-Argumente per
    ///     Parameter-Inspektion. Eine Fähigkeit mit zwei Klassen ist CQRS053.
    ///   - Marten-Identität: ein Read-Model mit typisierter Id (<c>Id&lt;T&gt; Id</c>) über seine generierte Speicher-Identität <c>Schluessel</c>
    ///     (ReadModelSchluesselGenerator), sonst <c>Id</c>; jede <c>Id&lt;X&gt;</c> in einem Read-Model als Werttyp für LINQ.
    /// Projektionen, Reader und der Query-Dienst sind Sache der Komposition (PullPathGenerator, QueryDienstGenerator).
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
            var iGeteilt = comp.GetTypeByMetadataName("Abstractions.IGeteiltesReadModel");
            var iWrite = comp.GetTypeByMetadataName("Abstractions.IWriteStore");
            var iRead = comp.GetTypeByMetadataName("Abstractions.IReadStore");
            var iStore = comp.GetTypeByMetadataName("Abstractions.IStore");
            if (iReadModel == null || iGeteilt == null || iWrite == null || iRead == null || iStore == null)
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

            // ── Emit ──
            var sb = new StringBuilder();
            sb.AppendLine("// <auto-generated/> — ProjectionServicesGenerator. Ersetzt DomainServiceExtension.");
            sb.AppendLine("#nullable enable");
            sb.AppendLine("using Marten;");
            sb.AppendLine("using Microsoft.Extensions.DependencyInjection;");
            sb.AppendLine();
            sb.AppendLine($"namespace {comp.AssemblyName}.Generated;");
            sb.AppendLine();
            sb.AppendLine("/// <summary>Generierte DI-Verdrahtung der Stores und das Marten-Schema der Read-Models.</summary>");
            sb.AppendLine("public static class GeneratedProjectionServices");
            sb.AppendLine("{");
            sb.AppendLine("    public static IServiceCollection AddGeneratedProjectionServices(this IServiceCollection services)");
            sb.AppendLine("    {");

            // Marten-Schema (eine Methode, die auch Test-Stores ohne DI aufrufen)
            sb.AppendLine("        services.ConfigureMarten(RegistriereSchema);");
            sb.AppendLine();
            var schema = new StringBuilder();
            schema.AppendLine("    /// <summary>Das Marten-Schema der Read-Models — für den Host über <c>ConfigureMarten</c>, für Stores ohne DI (Tests) direkt.</summary>");
            schema.AppendLine("    public static void RegistriereSchema(StoreOptions options)");
            schema.AppendLine("    {");
            // Optimistic Concurrency nur für GETEILTE Dokumente (aus mehreren Streams beschrieben): alle übrigen haben
            // per Schreib-Regel der Store-Basis genau einen Schreiber (ihren Stream) — dort wäre die Prüfung reine Last.
            var idTypen = new SortedSet<string>(System.StringComparer.Ordinal);
            foreach (var rm in readModels)
            {
                var typisiert = rm.GetMembers("Id").OfType<IPropertySymbol>().Any(p => IstId(p.Type));
                schema.AppendLine($"        options.Schema.For<{rm.ToDisplayString(full)}>()");
                schema.AppendLine("            .DatabaseSchemaName(\"rm\")");
                schema.AppendLine($"            .Identity(x => x.{(typisiert ? Abstractions.Projektionsvertrag.Schluessel : "Id")})");
                schema.AppendLine($"            .UseOptimisticConcurrency({(Impl(rm, iGeteilt) ? "true" : "false")});");
                foreach (var m in rm.GetMembers().OfType<IPropertySymbol>())
                    foreach (var id in IdsIn(m.Type)) idTypen.Add(id.ToDisplayString(full));
            }
            // Ids in Read-Models als Werttyp (eine UUID) — so übersetzt LINQ einen Vergleich auf ein Id-Feld.
            foreach (var id in idTypen)
                schema.AppendLine($"        options.RegisterValueType(typeof({id}));");
            schema.AppendLine("    }");

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


            // Stand der Leseseite (docs/04 §4.3): Code-Hash je Store-Klasse (normalisierte Syntax aller Teile) und die Read-Model-Typen —
            // daraus entscheidet der Host einer Sim-Umgebung beim Start, ob die Projektionen neu aufgebaut werden.
            sb.AppendLine();
            foreach (var store in stores)
                sb.AppendLine($"        services.AddSingleton(new global::Abstractions.StoreStand(typeof({store.ToDisplayString(full)}), \"{Kodeform.Hash(store)}\"));");
            foreach (var rm in readModels)
                sb.AppendLine($"        services.AddSingleton(new global::Abstractions.ReadModelTyp(typeof({rm.ToDisplayString(full)})));");

            sb.AppendLine();
            sb.AppendLine("        return services;");
            sb.AppendLine("    }");
            sb.AppendLine();
            sb.Append(schema.ToString());

            sb.AppendLine("}");

            context.AddSource("GeneratedProjectionServices.g.cs", sb.ToString());
        }

        private static bool IstId(ITypeSymbol t) => t is INamedTypeSymbol n && n.IsGenericType
            && n.OriginalDefinition.ToDisplayString() == "Abstractions.Id<T>";

        /// <summary>Die <c>Id&lt;X&gt;</c> in einem Feld-Typ: direkt, als <c>X?</c> oder als Element einer Sammlung.</summary>
        private static IEnumerable<ITypeSymbol> IdsIn(ITypeSymbol t)
        {
            if (IstId(t)) { yield return t; yield break; }
            if (t is INamedTypeSymbol n && n.IsGenericType)
                foreach (var a in n.TypeArguments)
                    foreach (var id in IdsIn(a)) yield return id;
            if (t is IArrayTypeSymbol arr)
                foreach (var id in IdsIn(arr.ElementType)) yield return id;
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
