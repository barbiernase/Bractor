// In Projekt: Infrastructure.SourceGeneration
// Dateiname: FunktionsBausteinGenerator.cs

using Microsoft.CodeAnalysis;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Infrastructure.SourceGeneration
{
    /// <summary>
    /// Funktionen in Baustein-Form (Plan Pub/Sub-Neustrukturierung §2.2, §3.3, Phase 3): erzeugt <c>GeneratedFunktionsBausteine</c> — je Funktion
    /// Eingänge, Ausgaben, Lese-Fähigkeiten, ob sie gleichbleibend ist (R12), wie sie gebaut wird, den reflexionsfreien Dispatch in Einzel- und
    /// Seiten-Form und den Hinweisplan (Hinweis-Typ → Funktions-Actors; den Weckplan erzeugt der WeckplanGenerator). Alles NUR aus der Signatur (<see cref="Funktionsform"/>):
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
                + "Commands, Events oder Hinweisen als Ausgabe (konkreter Typ, OneOf<…>, T?, Strom, je auch als Task).",
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
            var iDienst = c.GetTypeByMetadataName(Abstractions.Dienstvertrag.Marker);
            var fq = SymbolDisplayFormat.FullyQualifiedFormat;

            var typen = Vertragsassemblies.Typen(c);
            var gut = new List<Funktionsform.Form>();
            foreach (var t in typen.Where(Funktionsform.IstBaustein).OrderBy(t => t.ToDisplayString(), System.StringComparer.Ordinal))
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

            context.AddSource("GeneratedFunktionsBausteine.g.cs", Emit(gut, umsetzungen, iDienst, fq));
        }

        /// <summary>
        /// Die Form des Zustands: frisch, sichern, laden, zurücksetzen — über den Serializer-Kontext der Komposition
        /// (<c>EventJsonSerializerContext</c>, vom Codegen-Prepass um die Zustände ergänzt; fehlt der Typ dort, bricht der Build).
        /// </summary>
        private static string Zustandsform(INamedTypeSymbol z, System.Func<ITypeSymbol, string> T)
        {
            var typ = T(z);
            var huelle = $"global::Infrastructure.Funktionen.Zustand<{typ}>";
            var info = $"global::Infrastructure.Serialization.EventJsonSerializerContext.Default.{z.Name}";
            return $"new FunktionsZustandsform(typeof({typ}), () => new {huelle}(), "
                + $"z => global::System.Text.Json.JsonSerializer.SerializeToUtf8Bytes((({huelle})z).Wert, {info}), "
                + $"(z, d) => (({huelle})z).Wert = global::System.Text.Json.JsonSerializer.Deserialize(d, {info}) ?? new {typ}(), "
                + $"z => (({huelle})z).Wert = new {typ}())";
        }

        private static string Emit(List<Funktionsform.Form> fs, Dictionary<INamedTypeSymbol, List<INamedTypeSymbol>> umsetzungen, INamedTypeSymbol? iDienst,
            SymbolDisplayFormat fq)
        {
            string T(ITypeSymbol t) => t.ToDisplayString(fq);
            var sb = new StringBuilder();
            sb.AppendLine("// <auto-generated/>");
            sb.AppendLine("// Funktionen in Baustein-Form: Eingänge, Ausgaben, Dispatch, Hinweisplan (FunktionsBausteinGenerator). Nicht editieren.");
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
                var dienste = Ctorbindung.Braucht(ctor, iDienst).Select(d => $"typeof({T(d)})");
                sb.AppendLine($"            new FunktionsBaustein(typeof({T(f.Typ)}), \"{f.Typ.Name}\", Draussen: {(f.Draussen ? "true" : "false")}, "
                    + $"Gleichbleibend: {(Funktionsform.Gleichbleibend(f) ? "true" : "false")},");
                sb.AppendLine("                new FunktionsEingang[]");
                sb.AppendLine("                {");
                foreach (var a in f.Aufs)
                    sb.AppendLine($"                    new FunktionsEingang(typeof({T(a.Eingang)}), Hinweis: {(a.Hinweis ? "true" : "false")}, Strom: {(a.Strom ? "true" : "false")}, "
                        + $"MitEvents: {(a.Ausgaben.Any(x => Funktionsform.Hat(x, "Abstractions.IEvent") && !Funktionsform.Hat(x, "Abstractions.ITransientEvent")) ? "true" : "false")}, "
                        + $"new Type[] {{ {string.Join(", ", a.Ausgaben.Select(x => $"typeof({T(x)})"))} }}, "
                        + $"new Type[] {{ {string.Join(", ", a.Faehigkeiten.Select(x => $"typeof({T(x)})"))} }}),");
                sb.AppendLine("                },");
                sb.AppendLine("                new FunktionsUmsetzung[]");
                sb.AppendLine("                {");
                foreach (var k in umsetzungen[f.Typ])
                    sb.AppendLine($"                    new FunktionsUmsetzung(\"{k.Name}\", typeof({T(k)}), (sp, z) => new {T(k)}({Ctorbindung.Argumente(Ctorbindung.Konstruktor(k), fq, "z")})),");
                sb.AppendLine("                },");
                sb.AppendLine($"                new Type[] {{ {string.Join(", ", dienste)} }},");
                sb.AppendLine($"                Rufe{i}, Seite{i}{(f.Zustand != null ? ", Zustand: " + Zustandsform(f.Zustand, T) : "")}{(f.EineAdresse ? ", EineAdresse: true" : "")}),");
            }
            sb.AppendLine("        };");
            sb.AppendLine();

            var hinweise = fs.SelectMany(f => f.Aufs.Where(a => a.Hinweis).Select(a => (Eingang: T(a.Eingang), Kind: Abstractions.Funktionsvertrag.Kind(f.Typ.Name))))
                .GroupBy(x => x.Eingang).OrderBy(g => g.Key, System.StringComparer.Ordinal);
            sb.AppendLine("        /// <summary>Hinweis-Typ → die Funktions-Actors, die ihn hören (verlierbar, höchstens einmal; zugestellt vom Hinweis-Netz).</summary>");
            sb.AppendLine("        public static readonly IReadOnlyDictionary<Type, string[]> Hinweisplan = new Dictionary<Type, string[]>");
            sb.AppendLine("        {");
            foreach (var g in hinweise)
                sb.AppendLine($"            [typeof({g.Key})] = new[] {{ {string.Join(", ", g.Select(x => "\"" + x.Kind + "\"").Distinct())} }},");
            sb.AppendLine("        };");

            for (var i = 0; i < fs.Count; i++)
            {
                var f = fs[i];
                sb.AppendLine();
                sb.AppendLine($"        private static IAsyncEnumerable<IAusgabe> Rufe{i}(object o, IEvent eingang, Herkunft h, IFaehigkeiten? b, CancellationToken ct)");
                sb.AppendLine("        {");
                sb.AppendLine($"            var f = ({T(f.Typ)})o;");
                sb.AppendLine("            switch (eingang)");
                sb.AppendLine("            {");
                foreach (var a in f.Aufs)
                {
                    var argsKeine = string.Join(", ", a.Parameter.Select(p => p.Art switch
                    {
                        "e" => "e",
                        "h" => "h",
                        "ct" => "ct",
                        _ => $"b!.Hole<{T(p.Typ!)}>()",
                    }));
                    if (a.Keine)
                    {
                        // Ohne Ausgabe (nur mit Zustand): der Eingang wirkt allein auf den Zustand.
                        sb.AppendLine(a.Huelle.Length == 0
                            ? $"                case {T(a.Eingang)} e: f.{Abstractions.Funktionsvertrag.Auf}({argsKeine}); return Ausgaben.Leer;"
                            : $"                case {T(a.Eingang)} e: return Ausgaben.KeineAsync(f.{Abstractions.Funktionsvertrag.Auf}({argsKeine}));");
                        continue;
                    }
                    var args = string.Join(", ", a.Parameter.Select(p => p.Art switch
                    {
                        "e" => "e",
                        "h" => "h",
                        "ct" => "ct",
                        _ => $"b!.Hole<{T(p.Typ!)}>()",
                    }));
                    var aufruf = $"f.{Abstractions.Funktionsvertrag.Auf}({args})";
                    var abbildung = a.Abbildung.Length == 0 ? "y => y" : "y => y" + a.Abbildung;
                    string adapter;
                    if (a.Strom) adapter = a.StromAsync || a.Huelle == "Task" ? "StromAsync" : "Strom";
                    else adapter = a.Huelle.Length > 0 ? "EinzelnAsync" : "Einzeln";
                    var mitCt = a.Strom ? ", ct" : "";
                    sb.AppendLine($"                case {T(a.Eingang)} e: return Ausgaben.{adapter}({aufruf}, {abbildung}{mitCt});");
                }
                sb.AppendLine($"                default: throw new NotSupportedException($\"{f.Typ.Name} hört '{{eingang.GetType().Name}}' nicht.\");");
                sb.AppendLine("            }");
                sb.AppendLine("        }");
                sb.AppendLine();
                sb.AppendLine("        /// <summary>Seiten-Form (aus der Einzel-Form gebaut): die Eingänge einer Seite nacheinander, die Ausgaben mit dem Index ihres Eingangs.</summary>");
                sb.AppendLine($"        private static async IAsyncEnumerable<(int Index, IAusgabe Ausgabe)> Seite{i}(object o, IReadOnlyList<(IEvent Eingang, Herkunft Herkunft)> seite,");
                sb.AppendLine("            IFaehigkeiten? b, [EnumeratorCancellation] CancellationToken ct)");
                sb.AppendLine("        {");
                sb.AppendLine("            for (var i = 0; i < seite.Count; i++)");
                sb.AppendLine($"                await foreach (var a in Rufe{i}(o, seite[i].Eingang, seite[i].Herkunft, b, ct).WithCancellation(ct))");
                sb.AppendLine("                    yield return (i, a);");
                sb.AppendLine("        }");
            }
            sb.AppendLine("    }");
            sb.AppendLine("}");
            return sb.ToString();
        }
    }
}
