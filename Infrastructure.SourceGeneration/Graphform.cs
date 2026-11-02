// In Projekt: Infrastructure.SourceGeneration (per Link auch in Domain.SourceGeneration und GraphExtractor.Kern)
// Dateiname: Graphform.cs

using Microsoft.CodeAnalysis;
using System.Collections.Generic;
using System.Linq;

namespace Infrastructure.SourceGeneration
{
    /// <summary>
    /// Die EINE Lesart eines Graphen (docs/04 §4.4) für Generator, Analyzer und Extractor — nur Code-Fakten: der Marker <c>IGraph</c>, die Parameter
    /// des Records (je einer ein Knoten: Name = Konfig-Schlüssel, Typ = eine Funktion) und die Signaturen der Funktionen (<see cref="Funktionsform"/>).
    /// Kanten gibt es nicht zum Schreiben: gibt ein Knoten einen Command ohne Ziel aus, bekommt ihn jeder Knoten des Graphen, der ihn nimmt. Ein
    /// Eingangstyp, den kein Knoten des Graphen erzeugt, ist ein KOPF — je Command- bzw. Quell-Typ genau einer. Was kein Knoten nimmt, verlässt den
    /// Graphen (ein Command an ein Aggregat, ein Command ohne Ziel an den Kopf eines anderen Graphen, ein Hinweis). Die Reihenfolge der Parameter
    /// bedeutet nichts.
    /// </summary>
    internal static class Graphform
    {
        private const string A = "Abstractions.";
        public const string IGraph = A + "IGraph";

        /// <summary>Ein Knoten: sein Name (Parameter des Records, Konfig-Schlüssel) und seine Funktion.</summary>
        internal sealed class Knoten
        {
            public string Name = "";
            public INamedTypeSymbol Typ = null!;
            public Funktionsform.Form Funktion = null!;
            public Location Ort = Location.None;
            /// <summary>Die Commands ohne Ziel, Quell-Nachrichten und Hinweise, die der Knoten nimmt (ohne Antwort-Events).</summary>
            public List<INamedTypeSymbol> Eingaenge = new List<INamedTypeSymbol>();
            public List<INamedTypeSymbol> Ausgaben = new List<INamedTypeSymbol>();
        }

        /// <summary>Eine Kante: von einem Knoten über einen Command ohne Ziel zu einem Knoten desselben Graphen.</summary>
        internal sealed class Kante
        {
            public Knoten Von = null!, Nach = null!;
            public INamedTypeSymbol Typ = null!;
        }

        /// <summary>Ein gelesener Graph: Knoten, Kanten, Köpfe (Eingangstyp → Knoten), Ausgänge und Formfehler (Text, Ort).</summary>
        internal sealed class Form
        {
            public INamedTypeSymbol Typ = null!;
            public List<Knoten> Knoten = new List<Knoten>();
            public List<Kante> Kanten = new List<Kante>();
            /// <summary>Die Eingänge des Graphen: Typ → die Knoten, die ihn nehmen (bei Command und Quell-Nachricht genau einer).</summary>
            public List<(INamedTypeSymbol Typ, List<Knoten> Knoten)> Koepfe = new List<(INamedTypeSymbol, List<Knoten>)>();
            /// <summary>Was den Graphen verlässt: je Knoten die Ausgaben, die kein Knoten des Graphen nimmt.</summary>
            public List<(Knoten Von, INamedTypeSymbol Typ)> Ausgaenge = new List<(Knoten, INamedTypeSymbol)>();
            public List<(string Text, Location Ort)> Fehler = new List<(string, Location)>();

            /// <summary>Der Kopf für einen Eingangstyp, oder null.</summary>
            public List<Knoten>? KopfFuer(ITypeSymbol t) => Koepfe.Where(k => Funktionsform.Name(k.Typ) == Funktionsform.Name(t)).Select(k => k.Knoten).FirstOrDefault();
        }

        /// <summary>Ist <paramref name="t"/> ein Graph — ein konkreter Record mit <c>IGraph</c>?</summary>
        public static bool IstGraph(INamedTypeSymbol t) =>
            t.TypeKind == TypeKind.Class && !t.IsAbstract && Funktionsform.Name(t) != IGraph && Funktionsform.Hat(t, IGraph);

        /// <summary>Ein Command ohne Ziel (Kanten und Command-Köpfe), eine Quell-Nachricht (Quell-Kopf), ein Hinweis (Hinweis-Kopf).</summary>
        private static bool IstCommand(ITypeSymbol t) => Nachrichtenform.IstOhneZiel(t);

        /// <summary>Liest den Graphen <paramref name="t"/> (<see cref="IstGraph"/> vorausgesetzt).</summary>
        public static Form Lies(INamedTypeSymbol t)
        {
            var g = new Form { Typ = t };
            var ort = t.Locations.FirstOrDefault() ?? Location.None;
            if (!t.IsRecord) g.Fehler.Add(("ein Graph ist ein Record — seine Parameter sind die Knoten", ort));
            if (t.IsGenericType) g.Fehler.Add(("ein Graph ist nicht generisch", ort));
            var ctor = t.InstanceConstructors.Where(c => c.DeclaredAccessibility == Accessibility.Public && c.Parameters.Length > 0)
                .OrderByDescending(c => c.Parameters.Length).FirstOrDefault();
            if (ctor == null) { g.Fehler.Add(("ein Graph nennt seine Knoten als Parameter des Records (record X(F1 A, F2 B) : IGraph)", ort)); return g; }
            foreach (var p in ctor.Parameters)
            {
                var pOrt = p.Locations.FirstOrDefault() ?? ort;
                if (p.Type is not INamedTypeSymbol f || !Funktionsform.IstFunktion(f))
                {
                    var graph = p.Type is INamedTypeSymbol gg && IstGraph(gg);
                    g.Fehler.Add(($"der Knoten '{p.Name}' ist {(graph ? "ein Graph — ein Graph im Graphen kommt mit dem nächsten Schritt" : $"keine Funktion ('{p.Type.ToDisplayString()}')")} "
                        + "— ein Knoten ist eine Funktion (IFunktion)", pOrt));
                    continue;
                }
                var form = Funktionsform.Lies(f);
                var k = new Knoten { Name = p.Name, Typ = f, Funktion = form, Ort = pOrt };
                foreach (var a in form.Aufs.Where(a => !form.IstAntwort(a)))
                    if (!k.Eingaenge.Any(e => Funktionsform.Name(e) == Funktionsform.Name(a.Eingang))) k.Eingaenge.Add(a.Eingang);
                foreach (var x in form.Aufs.SelectMany(a => a.Ausgaben))
                    if (!k.Ausgaben.Any(e => Funktionsform.Name(e) == Funktionsform.Name(x))) k.Ausgaben.Add(x);
                g.Knoten.Add(k);
            }
            foreach (var doppelt in g.Knoten.GroupBy(k => Funktionsform.Name(k.Typ)).Where(x => x.Count() > 1))
                g.Fehler.Add(($"die Funktion '{doppelt.First().Typ.Name}' steht mehrfach im Graphen ({string.Join(", ", doppelt.Select(k => k.Name))}) — je Funktion "
                    + "ein Knoten (sie bekäme jeden Eingang doppelt)", doppelt.Last().Ort));

            // Kanten: jeder Command ohne Ziel, den ein Knoten ausgibt, geht an jeden Knoten des Graphen, der ihn nimmt.
            foreach (var von in g.Knoten)
                foreach (var x in von.Ausgaben)
                {
                    var nach = g.Knoten.Where(k => IstCommand(x) && k.Eingaenge.Any(e => Funktionsform.Name(e) == Funktionsform.Name(x))).ToList();
                    if (nach.Count == 0) { g.Ausgaenge.Add((von, x)); continue; }
                    foreach (var n in nach) g.Kanten.Add(new Kante { Von = von, Nach = n, Typ = x });
                }

            // Köpfe: Eingangstypen, die kein Knoten des Graphen erzeugt — je Command bzw. Quell-Nachricht genau einer (Hinweise dürfen mehrere hören).
            foreach (var typ in g.Knoten.SelectMany(k => k.Eingaenge).GroupBy(Funktionsform.Name).Select(x => x.First()))
            {
                if (g.Kanten.Any(k => Funktionsform.Name(k.Typ) == Funktionsform.Name(typ))) continue;
                var nehmen = g.Knoten.Where(k => k.Eingaenge.Any(e => Funktionsform.Name(e) == Funktionsform.Name(typ))).ToList();
                if (nehmen.Count > 1 && !Funktionsform.Hat(typ, A + "ITransientEvent"))
                    g.Fehler.Add(($"'{typ.Name}' nehmen {nehmen.Count} Knoten als Eingang des Graphen ({string.Join(", ", nehmen.Select(k => k.Name))}) — je "
                        + "Eingangstyp genau ein Kopf; was mehrere brauchen, gibt ein Kopf weiter", nehmen[1].Ort));
                g.Koepfe.Add((typ, nehmen));
            }
            if (g.Knoten.Count > 0 && g.Koepfe.Count == 0)
                g.Fehler.Add(("der Graph hat keinen Eingang — jeder Knoten nimmt nur, was ein anderer erzeugt (ein Kreis)", ort));

            // Kreisfrei: die Kanten bilden keinen Kreis (ein Lauf endet).
            var kreis = Kreis(g);
            if (kreis != null)
                g.Fehler.Add(($"die Knoten {string.Join(" → ", kreis.Select(k => k.Name))} bilden einen Kreis — ein Graph ist kreisfrei (was über Läufe hinweg "
                    + "gilt, steht im Zustand eines Knotens oder in einem Aggregat)", kreis[0].Ort));
            return g;
        }

        private static List<Knoten>? Kreis(Form g)
        {
            var zustand = new Dictionary<Knoten, int>();
            var pfad = new List<Knoten>();
            List<Knoten>? Besuche(Knoten k)
            {
                zustand[k] = 1;
                pfad.Add(k);
                foreach (var n in g.Kanten.Where(e => e.Von == k).Select(e => e.Nach))
                {
                    if (zustand.TryGetValue(n, out var z) && z == 1) return pfad.SkipWhile(x => x != n).Append(n).ToList();
                    if (!zustand.ContainsKey(n) && Besuche(n) is { } r) return r;
                }
                pfad.RemoveAt(pfad.Count - 1);
                zustand[k] = 2;
                return null;
            }
            foreach (var k in g.Knoten)
                if (!zustand.ContainsKey(k) && Besuche(k) is { } r) return r;
            return null;
        }
    }
}
