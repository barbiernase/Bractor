// In Projekt: Infrastructure.SourceGeneration
// Dateiname: FunktionsGenerator.cs

using Microsoft.CodeAnalysis;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Infrastructure.SourceGeneration
{
    /// <summary>
    /// Katalog-Funktionen (<c>Abstractions.IFunktion</c>): erzeugt <c>GeneratedFunktionen</c> — den reflexionsfreien Dispatch
    /// Auftrag → Funktion → Ergebnis-Event, dazu die Tabellen Auftrag→Funktion und Funktion→Ergebnisse (für den
    /// Azyklizitäts-Guard und den Boot-Check der Bindungen). Alles NUR aus der Signatur:
    /// <code>interface IX : IFunktion { Task&lt;OneOf&lt;E1, E2&gt;&gt; RufeAsync(XAuftrag a, IAusfuehrung x); }</code>
    /// mit <c>XAuftrag : IAuftrag&lt;IX&gt;</c> und persistenten Ergebnis-Events E1, E2 (kein ITransientEvent).
    /// Nach <c>IAusfuehrung</c> dürfen LESE-Fähigkeiten folgen (<c>ISucheX s</c>: ein Interface mit <c>IReadStore</c>) — der Dispatch
    /// löst sie je Aufruf aus einem Fähigkeits-Bereich auf, wie bei einem Handle. Schreiben bleibt dem Aggregat (über ein Command).
    ///
    /// CQRS068 — Funktion hat nicht die feste Form (genau eine Methode RufeAsync(Auftrag, IAusfuehrung) → Task&lt;OneOf&lt;Events&gt;&gt;).
    /// CQRS069 — ein Auftrag gehört zu mehr als einer Funktion (der Dispatch wäre mehrdeutig).
    /// Beides bricht den Build; eine fehlerhafte Funktion fehlt sonst still im Dispatch.
    /// </summary>
    [Generator]
    public class FunktionsGenerator : ISourceGenerator
    {
        public const string Methode = Abstractions.Funktionsvertrag.Methode;

        private static readonly DiagnosticDescriptor Form = new DiagnosticDescriptor(
            id: "CQRS068",
            title: "Funktion hat nicht die feste Form",
            messageFormat: "Funktion '{0}': {1}. Form: genau eine Methode Task<OneOf<Ergebnis…>> RufeAsync(TAuftrag auftrag, IAusfuehrung x) "
                + "mit TAuftrag : IAuftrag<{0}> und persistenten Ergebnis-Events (kein ITransientEvent).",
            category: "CQRS.Funktion",
            defaultSeverity: DiagnosticSeverity.Error,
            isEnabledByDefault: true);

        private static readonly DiagnosticDescriptor Mehrdeutig = new DiagnosticDescriptor(
            id: "CQRS069",
            title: "Auftrag gehört zu mehreren Funktionen",
            messageFormat: "Der Auftrag '{0}' ist Eingang mehrerer Funktionen ({1}) — der Dispatch wäre mehrdeutig. Jede Funktion bekommt ihren eigenen Auftrag.",
            category: "CQRS.Funktion",
            defaultSeverity: DiagnosticSeverity.Error,
            isEnabledByDefault: true);

        public void Initialize(GeneratorInitializationContext context) { }

        public void Execute(GeneratorExecutionContext context)
        {
            var c = context.Compilation;
            var iFunktion = c.GetTypeByMetadataName("Abstractions.IFunktion");
            var iAuftragT = c.GetTypeByMetadataName("Abstractions.IAuftrag`1");
            var iAusfuehrung = c.GetTypeByMetadataName("Abstractions.IAusfuehrung");
            var iEvent = c.GetTypeByMetadataName("Abstractions.IEvent");
            var iTransient = c.GetTypeByMetadataName("Abstractions.ITransientEvent");
            var iReadStore = c.GetTypeByMetadataName("Abstractions.IReadStore");
            var task1 = c.GetTypeByMetadataName("System.Threading.Tasks.Task`1");
            if (iFunktion == null || iAuftragT == null || iAusfuehrung == null || iEvent == null || iTransient == null || task1 == null)
                return;
            var fq = SymbolDisplayFormat.FullyQualifiedFormat;

            var schnitt = new List<INamedTypeSymbol>();
            CollectInterfaces(c.GlobalNamespace, schnitt);
            var funktionen = schnitt
                .Where(t => t.AllInterfaces.Contains(iFunktion, SymbolEqualityComparer.Default))
                .OrderBy(t => t.ToDisplayString(), System.StringComparer.Ordinal)
                .ToList();

            var gut = new List<(INamedTypeSymbol Funktion, INamedTypeSymbol Auftrag, List<INamedTypeSymbol> Ergebnisse, List<INamedTypeSymbol> Faehigkeiten)>();
            foreach (var f in funktionen)
            {
                var ort = f.Locations.FirstOrDefault() ?? Location.None;
                void Fehler(string grund) => context.ReportDiagnostic(Diagnostic.Create(Form, ort, f.Name, grund));

                var methoden = f.GetMembers().OfType<IMethodSymbol>().Where(m => m.MethodKind == MethodKind.Ordinary).ToList();
                if (methoden.Count != 1 || methoden[0].Name != Methode)
                {
                    Fehler(methoden.Count == 1 ? $"die Methode heißt '{methoden[0].Name}' statt {Methode}" : $"{methoden.Count} Methoden statt genau einer");
                    continue;
                }
                var m = methoden[0];
                if (m.Parameters.Length < 2 || m.Parameters[0].Type is not INamedTypeSymbol auftrag ||
                    !SymbolEqualityComparer.Default.Equals(m.Parameters[1].Type, iAusfuehrung))
                {
                    Fehler("die Parameter müssen (TAuftrag auftrag, IAusfuehrung x, Lese-Fähigkeit…) sein");
                    continue;
                }
                var faehigkeiten = m.Parameters.Skip(2).Select(p => p.Type as INamedTypeSymbol).ToList();
                var keineLese = m.Parameters.Skip(2).FirstOrDefault(p => p.Type is not INamedTypeSymbol { TypeKind: TypeKind.Interface } t ||
                    iReadStore == null || !t.AllInterfaces.Contains(iReadStore, SymbolEqualityComparer.Default));
                if (keineLese != null)
                {
                    Fehler($"der Parameter '{keineLese.Name}' ist keine Lese-Fähigkeit (Interface mit IReadStore) — eine Funktion liest höchstens, sie schreibt nie");
                    continue;
                }
                var gehoert = auftrag.AllInterfaces.Any(i =>
                    SymbolEqualityComparer.Default.Equals(i.OriginalDefinition, iAuftragT) &&
                    SymbolEqualityComparer.Default.Equals(i.TypeArguments[0], f));
                if (!gehoert || auftrag.TypeKind == TypeKind.Interface || auftrag.IsAbstract)
                {
                    Fehler($"der Auftrag '{auftrag.Name}' muss ein konkreter Typ mit IAuftrag<{f.Name}> sein");
                    continue;
                }
                if (m.ReturnType is not INamedTypeSymbol rt || !SymbolEqualityComparer.Default.Equals(rt.OriginalDefinition, task1) ||
                    rt.TypeArguments[0] is not INamedTypeSymbol oneOf || oneOf.Name != "OneOf" ||
                    oneOf.ContainingNamespace?.ToDisplayString() != "Abstractions")
                {
                    Fehler("die Rückgabe muss Task<OneOf<…>> sein");
                    continue;
                }
                var ergebnisse = oneOf.TypeArguments.OfType<INamedTypeSymbol>().ToList();
                var falsch = ergebnisse.FirstOrDefault(e =>
                    e.TypeKind == TypeKind.Interface || e.IsAbstract ||
                    !e.AllInterfaces.Contains(iEvent, SymbolEqualityComparer.Default) ||
                    e.AllInterfaces.Contains(iTransient, SymbolEqualityComparer.Default));
                if (falsch != null || ergebnisse.Count != oneOf.TypeArguments.Length)
                {
                    Fehler($"das Ergebnis '{falsch?.Name ?? "?"}' ist kein konkretes, persistentes Event");
                    continue;
                }
                gut.Add((f, auftrag, ergebnisse, faehigkeiten!));
            }

            // CQRS069: ein Auftrag-Typ als Eingang mehrerer Funktionen → mehrdeutiger Dispatch.
            foreach (var g in gut.GroupBy(x => x.Auftrag, SymbolEqualityComparer.Default).Where(g => g.Count() > 1))
            {
                foreach (var x in g)
                    context.ReportDiagnostic(Diagnostic.Create(Mehrdeutig, x.Funktion.Locations.FirstOrDefault() ?? Location.None,
                        x.Auftrag.Name, string.Join(", ", g.Select(y => y.Funktion.Name))));
            }
            var eindeutig = gut.GroupBy(x => x.Auftrag, SymbolEqualityComparer.Default).Where(g => g.Count() == 1).Select(g => g.First()).ToList();

            context.AddSource("GeneratedFunktionen.g.cs", Emit(eindeutig, fq));
        }

        private static string Emit(List<(INamedTypeSymbol Funktion, INamedTypeSymbol Auftrag, List<INamedTypeSymbol> Ergebnisse, List<INamedTypeSymbol> Faehigkeiten)> fs, SymbolDisplayFormat fq)
        {
            var sb = new StringBuilder();
            sb.AppendLine("// <auto-generated/>");
            sb.AppendLine("// Katalog-Funktionen: Dispatch Auftrag → Funktion → Ergebnis-Event (FunktionsGenerator). Nicht editieren.");
            sb.AppendLine("#nullable enable");
            sb.AppendLine("using System;");
            sb.AppendLine("using System.Collections.Generic;");
            sb.AppendLine("using System.Threading.Tasks;");
            sb.AppendLine("using Abstractions;");
            sb.AppendLine("using Microsoft.Extensions.DependencyInjection;");
            sb.AppendLine();
            sb.AppendLine("namespace Infrastructure.Funktionen");
            sb.AppendLine("{");
            sb.AppendLine("    public static class GeneratedFunktionen");
            sb.AppendLine("    {");
            sb.AppendLine("        /// <summary>Auftrag-Typ → die EINE Funktion, deren Eingang er ist.</summary>");
            sb.AppendLine("        public static readonly IReadOnlyDictionary<Type, Type> AuftragZuFunktion = new Dictionary<Type, Type>");
            sb.AppendLine("        {");
            foreach (var x in fs)
                sb.AppendLine($"            [typeof({x.Auftrag.ToDisplayString(fq)})] = typeof({x.Funktion.ToDisplayString(fq)}),");
            sb.AppendLine("        };");
            sb.AppendLine();
            sb.AppendLine("        /// <summary>Funktion → ihre Ergebnis-Events (die OneOf-Fälle der Signatur).</summary>");
            sb.AppendLine("        public static readonly IReadOnlyDictionary<Type, Type[]> Ergebnisse = new Dictionary<Type, Type[]>");
            sb.AppendLine("        {");
            foreach (var x in fs)
                sb.AppendLine($"            [typeof({x.Funktion.ToDisplayString(fq)})] = new Type[] {{ {string.Join(", ", x.Ergebnisse.Select(e => $"typeof({e.ToDisplayString(fq)})"))} }},");
            sb.AppendLine("        };");
            sb.AppendLine();
            sb.AppendLine("        /// <summary>Funktion → ihre Lese-Fähigkeiten (Parameter nach IAusfuehrung). Eine solche Funktion läuft nur im Host.</summary>");
            sb.AppendLine("        public static readonly IReadOnlyDictionary<Type, Type[]> Faehigkeiten = new Dictionary<Type, Type[]>");
            sb.AppendLine("        {");
            foreach (var x in fs.Where(x => x.Faehigkeiten.Count > 0))
                sb.AppendLine($"            [typeof({x.Funktion.ToDisplayString(fq)})] = new Type[] {{ {string.Join(", ", x.Faehigkeiten.Select(e => $"typeof({e.ToDisplayString(fq)})"))} }},");
            sb.AppendLine("        };");
            sb.AppendLine();
            sb.AppendLine("        /// <summary>Führt den Auftrag mit der gebundenen Funktion aus (aus der DI) und liefert den gewählten OneOf-Fall.</summary>");
            sb.AppendLine("        public static Task<IEvent> RufeAsync(IServiceProvider sp, IAuftrag auftrag, IAusfuehrung x)");
            sb.AppendLine("        {");
            sb.AppendLine("            switch (auftrag)");
            sb.AppendLine("            {");
            for (int i = 0; i < fs.Count; i++)
                sb.AppendLine($"                case {fs[i].Auftrag.ToDisplayString(fq)} a{i}: return Rufe{i}(sp, a{i}, x);");
            sb.AppendLine("                default: throw new NotSupportedException($\"Kein Funktions-Dispatch für den Auftrag '{auftrag.GetType().Name}'.\");");
            sb.AppendLine("            }");
            sb.AppendLine("        }");
            for (int i = 0; i < fs.Count; i++)
            {
                sb.AppendLine();
                sb.AppendLine($"        private static async Task<IEvent> Rufe{i}(IServiceProvider sp, {fs[i].Auftrag.ToDisplayString(fq)} a, IAusfuehrung x)");
                if (fs[i].Faehigkeiten.Count == 0)
                {
                    sb.AppendLine($"            => (IEvent)(await sp.GetRequiredService<{fs[i].Funktion.ToDisplayString(fq)}>().{Methode}(a, x)).Value;");
                    continue;
                }
                // Lese-Fähigkeiten: je Aufruf ein Bereich (wie je Handle-Aufruf), danach freigegeben.
                var args = string.Concat(fs[i].Faehigkeiten.Select(t => $", b.Hole<{t.ToDisplayString(fq)}>()"));
                sb.AppendLine("        {");
                sb.AppendLine("            using var b = sp.GetRequiredService<IFaehigkeitsFabrik>().Oeffne();");
                sb.AppendLine($"            return (IEvent)(await sp.GetRequiredService<{fs[i].Funktion.ToDisplayString(fq)}>().{Methode}(a, x{args})).Value;");
                sb.AppendLine("        }");
            }
            sb.AppendLine("    }");
            sb.AppendLine("}");
            return sb.ToString();
        }

        private static void CollectInterfaces(INamespaceSymbol ns, List<INamedTypeSymbol> results)
        {
            foreach (var type in ns.GetTypeMembers())
                CollectNested(type, results);
            foreach (var sub in ns.GetNamespaceMembers())
                CollectInterfaces(sub, results);
        }

        private static void CollectNested(INamedTypeSymbol type, List<INamedTypeSymbol> results)
        {
            if (type.TypeKind == TypeKind.Interface && type.ContainingNamespace?.ToDisplayString() != "Abstractions")
                results.Add(type);
            foreach (var nested in type.GetTypeMembers())
                CollectNested(nested, results);
        }
    }
}
