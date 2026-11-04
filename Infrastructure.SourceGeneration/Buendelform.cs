// In Projekt: Infrastructure.SourceGeneration (per Link auch in GraphExtractor.Kern)
// Dateiname: Buendelform.cs

using Microsoft.CodeAnalysis;
using System.Collections.Generic;
using System.Linq;

namespace Infrastructure.SourceGeneration
{
    /// <summary>
    /// Der SCHLÜSSEL eines Bündels (<c>Buendel&lt;A, B, …&gt;</c> als Eingang eines <c>Auf</c>, docs/10 §10.3) — nur aus Code-Fakten: welches
    /// Aggregat ein Event faltet (sein <c>Apply</c> im Applier, GR-FALTUNG) und welche <c>Id&lt;T&gt;</c>-Felder es trägt. Je Event die Menge
    /// seiner Ids: der Stream (das Aggregat, das es faltet) und jedes <c>Id&lt;T&gt;</c>-Feld. Der Schlüssel ist die eine Id, die alle Events
    /// gemeinsam haben; keine gemeinsame = global (je Typ das neueste Event); mehrere = <c>[Ueber&lt;T&gt;]</c> am Parameter wählt. Woher ein Event
    /// seinen Schlüssel nimmt: aus dem Stream, wenn <c>T</c> es faltet, sonst aus seinem einzigen Feld <c>Id&lt;T&gt;</c>. Dieselbe Lesart für
    /// den Generator der Komposition (CQRS118) und den Extractor.
    /// </summary>
    internal static class Buendelform
    {
        internal const string Stream = "stream", Feld = "feld", Global = "global";

        internal sealed class Glied
        {
            public INamedTypeSymbol Typ = null!;
            /// <summary><see cref="Stream"/>, <see cref="Feld"/> oder <see cref="Global"/>.</summary>
            public string Art = Global;
            /// <summary>Bei <see cref="Feld"/> der Name des Feldes <c>Id&lt;T&gt;</c>.</summary>
            public string? Feldname;
        }

        internal sealed class Schluessel
        {
            /// <summary>Das Aggregat <c>T</c> des Schlüssels <c>Id&lt;T&gt;</c>; null = global.</summary>
            public INamedTypeSymbol? Aggregat;
            public List<Glied> Glieder = new List<Glied>();
            /// <summary>Gesetzt: der Schlüssel lässt sich nicht ableiten (CQRS118).</summary>
            public string? Fehler;
        }

        /// <summary>Welches Aggregat ein Event faltet (voller Name des Events → State), aus den Appliern (<c>IApplier&lt;TState&gt;</c>, <c>Apply(E)</c>).</summary>
        public static Dictionary<string, INamedTypeSymbol> Faltung(IEnumerable<INamedTypeSymbol> typen)
        {
            var faltung = new Dictionary<string, INamedTypeSymbol>();
            foreach (var t in typen)
            {
                if (t.TypeKind != TypeKind.Class) continue;
                var applier = t.AllInterfaces.FirstOrDefault(i => i.IsGenericType && Projektionsform.Name(i) == "Abstractions.IApplier`1");
                if (applier?.TypeArguments[0] is not INamedTypeSymbol state) continue;
                foreach (var m in t.GetMembers("Apply").OfType<IMethodSymbol>())
                    if (m.Parameters.Length == 1 && m.Parameters[0].Type is INamedTypeSymbol e)
                        faltung[Projektionsform.Name(e)] = state;
            }
            return faltung;
        }

        /// <summary>Die <c>Id&lt;T&gt;</c>-Felder eines Events (direkt, nicht geschachtelt): Feldname → <c>T</c>.</summary>
        public static List<(string Name, INamedTypeSymbol Aggregat)> IdFelder(INamedTypeSymbol e) =>
            Projektionsform.Glieder(e)
                .Where(g => Projektionsform.IstId(g.Typ) && ((INamedTypeSymbol)g.Typ).TypeArguments[0] is INamedTypeSymbol)
                .Select(g => (g.Name, (INamedTypeSymbol)((INamedTypeSymbol)g.Typ).TypeArguments[0]))
                .ToList();

        public static Schluessel Leite(Projektionsform.Auf auf, IReadOnlyDictionary<string, INamedTypeSymbol> faltung)
        {
            var s = new Schluessel();
            // Je Event die Ids, die es nennt: der Stream (das Aggregat, das es faltet) und seine Id<T>-Felder.
            var ids = auf.Glieder.Select(g =>
            {
                var menge = new Dictionary<string, INamedTypeSymbol>();
                if (faltung.TryGetValue(Projektionsform.Name(g), out var st)) menge[Projektionsform.Name(st)] = st;
                foreach (var (_, a) in IdFelder(g)) menge[Projektionsform.Name(a)] = a;
                return menge;
            }).ToList();

            INamedTypeSymbol? aggregat;
            if (auf.Ueber != null)
            {
                var name = Projektionsform.Name(auf.Ueber);
                var ohne = auf.Glieder.Where((g, i) => !ids[i].ContainsKey(name)).Select(g => g.Name).ToList();
                if (ohne.Count > 0)
                {
                    s.Fehler = $"[Ueber<{auf.Ueber.Name}>]: {string.Join(", ", ohne)} {(ohne.Count == 1 ? "wird" : "werden")} weder von {auf.Ueber.Name} entschieden "
                        + $"noch {(ohne.Count == 1 ? "trägt es" : "tragen sie")} ein Feld Id<{auf.Ueber.Name}>";
                    return s;
                }
                aggregat = auf.Ueber;
            }
            else
            {
                var gemeinsam = ids.Skip(1).Aggregate(new HashSet<string>(ids[0].Keys), (m, x) => { m.IntersectWith(x.Keys); return m; });
                if (gemeinsam.Count > 1)
                {
                    s.Fehler = $"die Events haben mehrere Ids gemeinsam ({string.Join(", ", gemeinsam.OrderBy(x => x, System.StringComparer.Ordinal).Select(x => "Id<" + x.Split('.').Last() + ">"))}) — "
                        + "[Ueber<T>] am Parameter wählt den Schlüssel";
                    return s;
                }
                aggregat = gemeinsam.Count == 1 ? ids[0][gemeinsam.First()] : null;
            }

            s.Aggregat = aggregat;
            foreach (var g in auf.Glieder)
            {
                if (aggregat == null) { s.Glieder.Add(new Glied { Typ = g, Art = Global }); continue; }
                if (faltung.TryGetValue(Projektionsform.Name(g), out var st) && Projektionsform.Name(st) == Projektionsform.Name(aggregat))
                {
                    s.Glieder.Add(new Glied { Typ = g, Art = Stream });
                    continue;
                }
                var felder = IdFelder(g).Where(f => Projektionsform.Name(f.Aggregat) == Projektionsform.Name(aggregat)).Select(f => f.Name).ToList();
                if (felder.Count != 1)
                {
                    s.Fehler = $"{g.Name} trägt {felder.Count} Felder Id<{aggregat.Name}> ({string.Join(", ", felder)}) — das Bündel nimmt den Schlüssel aus genau einem";
                    return s;
                }
                s.Glieder.Add(new Glied { Typ = g, Art = Feld, Feldname = felder[0] });
            }
            return s;
        }
    }
}
