// In Projekt: Infrastructure.SourceGeneration
// Dateiname: FunktionsBausteinGenerator.cs

using Microsoft.CodeAnalysis;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Infrastructure.SourceGeneration
{
    /// <summary>
    /// Funktionen in Baustein-Form (docs/04 §4.4, docs/10 §10.3): erzeugt <c>GeneratedFunktionsBausteine</c> — je Funktion Eingänge, Ausgaben,
    /// Lese-Fähigkeiten, wie sie gebaut wird und ihre reflexionsfreie Rechnung (die Kanten liest der <c>GraphGenerator</c> aus den Graphen). Alles
    /// NUR aus der Signatur (<see cref="Funktionsform"/>):
    /// <code>public sealed class Mahnwesen(IMailversand mail) : IFunktion { public OneOf&lt;EroeffneMahnung&gt;? Auf(ZahlungUeberfaellig e, Herkunft h) … }</code>
    /// <code>public interface IBonitaetsPruefung : IFunktion { OneOf&lt;BonitaetBestaetigt, BonitaetAbgelehnt&gt; Auf(KreditBeantragt e); }</code>
    ///
    /// CQRS100 — die Funktion hat nicht die Form (Eingang, Parameter, Ausgaben, beide Formen in einem Typ).
    /// CQRS101 — zwei <c>Auf</c> für denselben Eingangstyp (R2; als Analyzer schon im Domänen-Projekt).
    /// Beides bricht den Build; eine fehlerhafte Funktion fehlt sonst still im Dispatch.
    /// </summary>
    [Generator]
    public class FunktionsBausteinGenerator : ISourceGenerator
    {
        public const string FormId = "CQRS100", DoppeltId = "CQRS101";

        private static readonly DiagnosticDescriptor FormFehler = new DiagnosticDescriptor(
            id: FormId,
            title: "Funktion hat nicht die Form",
            messageFormat: "Funktion '{0}': {1}. Form: je Eingang ein Auf(TEingang e[, Herkunft h][, Lese-Fähigkeiten…][, CancellationToken]) mit konkreten "
                + "Commands oder Hinweisen als Ausgabe (konkreter Typ, OneOf<…> oder Task<…> davon, IEnumerable<…>, IAsyncEnumerable<…>).",
            category: "CQRS.Funktion",
            defaultSeverity: DiagnosticSeverity.Error,
            isEnabledByDefault: true);

        internal static readonly DiagnosticDescriptor Doppelt = new DiagnosticDescriptor(
            id: DoppeltId,
            title: "Zwei Auf für denselben Eingang",
            messageFormat: "'{0}' hat mehr als ein {2} für '{1}' — je Baustein und Eingangstyp höchstens eines, sonst wäre unklar, was der Eingang auslöst (R2)",
            category: "CQRS.Funktion",
            defaultSeverity: DiagnosticSeverity.Error,
            isEnabledByDefault: true);

        public void Initialize(GeneratorInitializationContext context) { }

        public void Execute(GeneratorExecutionContext context)
        {
            if (!Laufzeitprojekt.Ist(context.Compilation)) return;   // nur in der Komposition (docs/07)
            var c = context.Compilation;
            if (c.GetTypeByMetadataName(Funktionsform.IFunktion) == null) return;
            var fq = SymbolDisplayFormat.FullyQualifiedFormat;

            var typen = Vertragsassemblies.Typen(c);
            var gut = new List<Funktionsform.Form>();
            foreach (var t in typen.Where(Funktionsform.IstFunktion).OrderBy(t => t.ToDisplayString(), System.StringComparer.Ordinal))
            {
                var f = Funktionsform.Lies(t);
                foreach (var (text, ort) in f.Fehler) context.ReportDiagnostic(Diagnostic.Create(FormFehler, ort, t.Name, text));
                foreach (var (e, ort) in f.Doppelt)
                    context.ReportDiagnostic(Diagnostic.Create(Doppelt, ort, t.Name, e.Name, Abstractions.Funktionsvertrag.Auf));
                if (!f.Draussen && !Ctorbindung.Baubar(t))
                {
                    context.ReportDiagnostic(Diagnostic.Create(FormFehler, t.Locations.FirstOrDefault() ?? Location.None, t.Name,
                        "die Klasse ist nicht öffentlich oder generisch — die generierte Fabrik kann sie nicht bauen"));
                    continue;
                }
                if (f.Fehler.Count == 0 && f.Doppelt.Count == 0 && f.Aufs.Count > 0) gut.Add(f);
            }

            // Die Umsetzungen im Host: eine Klasse ist sich selbst die Umsetzung; eine Schnittstelle hat ihre öffentlichen, konkreten Implementierungen.
            var klassen = Ctorbindung.Klassen(c);
            var umsetzungen = gut.ToDictionary(f => f.Typ, f => f.Draussen
                ? klassen.Where(k => Ctorbindung.Baubar(k) && k.AllInterfaces.Contains(f.Typ, SymbolEqualityComparer.Default))
                    .OrderBy(k => k.ToDisplayString(), System.StringComparer.Ordinal).ToList()
                : new List<INamedTypeSymbol> { f.Typ }, (IEqualityComparer<INamedTypeSymbol>)SymbolEqualityComparer.Default);

            context.AddSource("GeneratedFunktionsBausteine.g.cs", Emit(gut, umsetzungen, fq));
        }

        private static string Emit(List<Funktionsform.Form> fs, Dictionary<INamedTypeSymbol, List<INamedTypeSymbol>> umsetzungen,
            SymbolDisplayFormat fq)
        {
            string T(ITypeSymbol t) => t.ToDisplayString(fq);
            var sb = new StringBuilder();
            sb.AppendLine("// <auto-generated/>");
            sb.AppendLine("// Funktionen in Baustein-Form: Eingänge, Ausgaben, Rechnung (FunktionsBausteinGenerator). Nicht editieren.");
            sb.AppendLine("#nullable enable");
            sb.AppendLine("using System;");
            sb.AppendLine("using System.Collections.Generic;");
            sb.AppendLine("using System.Runtime.CompilerServices;");
            sb.AppendLine("using System.Threading;");
            sb.AppendLine("using System.Threading.Tasks;");
            sb.AppendLine("using Abstractions;");
            sb.AppendLine();
            sb.AppendLine("namespace Infrastructure.Funktionen");
            sb.AppendLine("{");
            sb.AppendLine("    public static class GeneratedFunktionsBausteine");
            sb.AppendLine("    {");
            sb.AppendLine("        /// <summary>Alle Funktionen in Baustein-Form.</summary>");
            sb.AppendLine("        public static readonly IReadOnlyList<FunktionsBaustein> Alle = new FunktionsBaustein[]");
            sb.AppendLine("        {");
            for (var i = 0; i < fs.Count; i++)
            {
                var f = fs[i];
                var ctor = f.Draussen ? null : Ctorbindung.Konstruktor(f.Typ);
                var dienste = Ctorbindung.Braucht(ctor).Select(d => $"typeof({T(d)})");
                sb.AppendLine($"            new FunktionsBaustein(typeof({T(f.Typ)}), \"{f.Typ.Name}\", Draussen: {(f.Draussen ? "true" : "false")},");
                sb.AppendLine("                new FunktionsEingang[]");
                sb.AppendLine("                {");
                foreach (var a in f.Aufs)
                    sb.AppendLine($"                    new FunktionsEingang(typeof({T(a.Eingang)}), Hinweis: {(a.Hinweis ? "true" : "false")}, Strom: {(a.Strom ? "true" : "false")}, "
                        + $"new Type[] {{ {string.Join(", ", a.Ausgaben.Select(x => $"typeof({T(x)})"))} }}, "
                        + $"new Type[] {{ {string.Join(", ", a.Faehigkeiten.Select(x => $"typeof({T(x)})"))} }}),");
                sb.AppendLine("                },");
                sb.AppendLine("                new FunktionsUmsetzung[]");
                sb.AppendLine("                {");
                foreach (var k in umsetzungen[f.Typ])
                    sb.AppendLine($"                    new FunktionsUmsetzung(\"{k.Name}\", typeof({T(k)}), (sp, z) => new {T(k)}({Ctorbindung.Argumente(Ctorbindung.Konstruktor(k), fq, "z")})),");
                sb.AppendLine("                },");
                sb.AppendLine($"                new Type[] {{ {string.Join(", ", dienste)} }},");
                sb.AppendLine($"                Rechne{i}{(f.Zustand != null ? ", Zustand: " + Ctorbindung.Zustandsform(f.Zustand, fq) : "")}),");
            }
            sb.AppendLine("        };");
            sb.AppendLine();

            for (var i = 0; i < fs.Count; i++)
            {
                sb.AppendLine();
                EmitRechne(sb, fs[i], i, T);
            }
            sb.AppendLine("    }");
            sb.AppendLine("}");
            return sb.ToString();
        }

        /// <summary>
        /// <c>Rechne{i}</c> nimmt den Eingang als Objekt und liefert die Ausgaben als Objekte (Commands, Hinweise) — reflexionsfrei,
        /// je Eingangstyp ein Fall.
        /// </summary>
        private static void EmitRechne(StringBuilder sb, Funktionsform.Form f, int i, System.Func<ITypeSymbol, string> T)
        {
            sb.AppendLine($"        private static IAsyncEnumerable<object> Rechne{i}(object o, object eingang, Herkunft h, IFaehigkeiten? b, CancellationToken ct)");
            sb.AppendLine("        {");
            sb.AppendLine($"            var f = ({T(f.Typ)})o;");
            sb.AppendLine("            switch (eingang)");
            sb.AppendLine("            {");
            foreach (var a in f.Aufs)
            {
                var args = string.Join(", ", a.Parameter.Select(p => p.Art switch
                {
                    "e" => "e",
                    "h" => "h",
                    "ct" => "ct",
                    _ => $"b!.Hole<{T(p.Typ!)}>()",
                }));
                var aufruf = $"f.{Abstractions.Funktionsvertrag.Auf}({args})";
                var abbildung = "y => (object?)y" + a.Abbildung;
                string adapter;
                if (a.Strom) adapter = a.StromAsync ? "StromAsync" : "Strom";
                else adapter = a.Huelle.Length > 0 ? "EinzelnAsync" : "Einzeln";
                var mitCt = a.Strom ? ", ct" : "";
                sb.AppendLine($"                case {T(a.Eingang)} e: return Ausgaben.{adapter}({aufruf}, {abbildung}{mitCt});");
            }
            sb.AppendLine($"                default: throw new NotSupportedException($\"{f.Typ.Name} hört '{{eingang.GetType().Name}}' nicht.\");");
            sb.AppendLine("            }");
            sb.AppendLine("        }");
        }
    }
}
