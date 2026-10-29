// In Projekt: Infrastructure.SourceGeneration (per Link auch in Domain.SourceGeneration, Projections.SourceGeneration und GraphExtractor.Kern)
// Dateiname: Projektionsform.cs

using Microsoft.CodeAnalysis;
using System.Collections.Generic;
using System.Linq;

namespace Infrastructure.SourceGeneration
{
    /// <summary>
    /// Die EINE Lesart von Projektion und Reader (docs/10 §10.3) — für die Batch-Kinds und den Weckplan (eine Erkennung für
    /// beide, FB-12), den Query-Dienst, die Analyzer, die Lage-Regeln und den Extractor. Nur Code-Fakten: die Marker <c>IProjektion</c> und
    /// <c>IReader</c>, die Methoden <c>Auf</c> bzw. <c>Frage</c> und ihre Signaturen, die Store-Klasse eines Stores. Verglichen wird über volle
    /// Namen, nie über Symbol-Identität — so liest sie auch Typen aus anderen Compilations. Dazu die Frage an ein Aggregat (§2.6): die
    /// <c>Frage</c> im Decider — eine Query hat genau ein Ziel, einen Reader oder ein Aggregat (R4).
    /// </summary>
    internal static class Projektionsform
    {
        private const string A = "Abstractions.";

        /// <summary>Ein <c>Auf</c> einer Projektion: <c>Task Auf(TEvent e[, Herkunft h], Schreib-Fähigkeiten…[, CancellationToken ct])</c>.</summary>
        internal sealed class Auf
        {
            public IMethodSymbol Methode = null!;
            public INamedTypeSymbol Eingang = null!;
            /// <summary>Die Parameter in Reihenfolge: <c>e</c> (Eingang), <c>h</c> (Herkunft), <c>ct</c> (Abbruch) oder <c>faehigkeit</c>.</summary>
            public List<(string Art, INamedTypeSymbol? Typ)> Parameter = new List<(string, INamedTypeSymbol?)>();
            public List<INamedTypeSymbol> Faehigkeiten = new List<INamedTypeSymbol>();
            public bool MitHerkunft;
        }

        internal sealed class Projektion
        {
            public INamedTypeSymbol Typ = null!;
            public List<Auf> Aufs = new List<Auf>();
            public List<(string Text, Location Ort)> Fehler = new List<(string, Location)>();
            /// <summary>Eingangstypen mit mehr als einem <c>Auf</c> (R2).</summary>
            public List<(INamedTypeSymbol Eingang, Location Ort)> Doppelt = new List<(INamedTypeSymbol, Location)>();
            /// <summary><c>[MindestensEinmal]</c>: ausdrücklich erlaubt, in einen Store der Klasse 3 zu schreiben (R14).</summary>
            public bool MindestensEinmal;

            /// <summary>Die Schreib-Fähigkeiten aller <c>Auf</c>, ohne Doppelte, in Reihenfolge des ersten Auftretens.</summary>
            public List<INamedTypeSymbol> SchreibFaehigkeiten() => Aufs.SelectMany(a => a.Faehigkeiten)
                .GroupBy(Name).Select(g => g.First()).ToList();
        }

        /// <summary>Eine <c>Frage</c> eines Readers: <c>Frage(TQuery q, Lese-Fähigkeiten…[, CancellationToken ct])</c> → Antwort, <c>OneOf&lt;…&gt;</c>,
        /// auch als <c>Task</c>/<c>ValueTask</c>.</summary>
        internal sealed class Frage
        {
            public IMethodSymbol Methode = null!;
            public INamedTypeSymbol Query = null!;
            public List<(string Art, INamedTypeSymbol? Typ)> Parameter = new List<(string, INamedTypeSymbol?)>();
            public List<INamedTypeSymbol> Faehigkeiten = new List<INamedTypeSymbol>();
            /// <summary><c>""</c>, <c>"Task"</c> oder <c>"ValueTask"</c>.</summary>
            public string Huelle = "";
            /// <summary>Die Rückgabe ist ein <c>OneOf&lt;…&gt;</c> (dann <c>.Value</c>).</summary>
            public bool OneOf;
            /// <summary>Die möglichen Antwort-Typen (ein konkreter Typ oder die Typen des <c>OneOf</c>).</summary>
            public List<INamedTypeSymbol> Antworten = new List<INamedTypeSymbol>();
            /// <summary>Nur an ein Aggregat: das <c>Id&lt;TAggregat&gt;</c>-Glied der Query — daran geht sie an ihr Aggregat.</summary>
            public string? IdGlied;
        }

        /// <summary>Die Fragen eines Aggregats (§2.6): je Query eine <c>Frage(TQuery q)</c> im Decider, synchron, nur lesend auf seinen Zustand.</summary>
        internal sealed class Aggregatfragen
        {
            public INamedTypeSymbol Decider = null!;
            public INamedTypeSymbol State = null!;
            public List<Frage> Fragen = new List<Frage>();
            public List<(string Text, Location Ort)> Fehler = new List<(string, Location)>();
            public List<(INamedTypeSymbol Query, Location Ort)> Doppelt = new List<(INamedTypeSymbol, Location)>();
        }

        internal sealed class Reader
        {
            public INamedTypeSymbol Typ = null!;
            public List<Frage> Fragen = new List<Frage>();
            public List<(string Text, Location Ort)> Fehler = new List<(string, Location)>();
            public List<(INamedTypeSymbol Query, Location Ort)> Doppelt = new List<(INamedTypeSymbol, Location)>();
        }

        public static string Name(ITypeSymbol t) =>
            (t.OriginalDefinition.ContainingNamespace?.ToDisplayString() ?? "") + "." + t.OriginalDefinition.MetadataName;

        public static bool Hat(ITypeSymbol t, string vollerName) =>
            Name(t) == vollerName || t.AllInterfaces.Any(i => Name(i) == vollerName);

        /// <summary>Eine Projektion: eine konkrete Klasse mit <c>IProjektion</c>.</summary>
        public static bool IstProjektion(INamedTypeSymbol t) =>
            t.TypeKind == TypeKind.Class && !t.IsAbstract && Hat(t, Abstractions.Projektionsvertrag.IProjektion);

        /// <summary>Ein Reader: eine konkrete Klasse mit <c>IReader</c>.</summary>
        public static bool IstReader(INamedTypeSymbol t) =>
            t.TypeKind == TypeKind.Class && !t.IsAbstract && Hat(t, Abstractions.Projektionsvertrag.IReader);

        /// <summary>Eine Schreib-Fähigkeit: ein Interface mit <c>IWriteStore</c> (ohne <c>IReadStore</c>).</summary>
        public static bool IstSchreibFaehigkeit(ITypeSymbol t) =>
            t is INamedTypeSymbol n && n.TypeKind == TypeKind.Interface && Hat(n, A + "IWriteStore") && !Hat(n, A + "IReadStore");

        /// <summary>Eine Lese-Fähigkeit: ein Interface mit <c>IReadStore</c> (ohne <c>IWriteStore</c>).</summary>
        public static bool IstLeseFaehigkeit(ITypeSymbol t) =>
            t is INamedTypeSymbol n && n.TypeKind == TypeKind.Interface && Hat(n, A + "IReadStore") && !Hat(n, A + "IWriteStore");

        /// <summary>Die Store-Klassen (1, 2, 3), die ein Typ implementiert — ein Store implementiert genau eine (R14).</summary>
        public static List<int> StoreKlassen(ITypeSymbol t)
        {
            var k = new List<int>();
            for (var i = 0; i < Abstractions.Projektionsvertrag.Klassen.Length; i++)
                if (Hat(t, Abstractions.Projektionsvertrag.Klassen[i])) k.Add(i + 1);
            return k;
        }

        public static Projektion LiesProjektion(INamedTypeSymbol t)
        {
            var p = new Projektion
            {
                Typ = t,
                MindestensEinmal = t.GetAttributes().Any(a => a.AttributeClass != null && Name(a.AttributeClass) == Abstractions.Projektionsvertrag.MindestensEinmal),
            };
            var ort = t.Locations.FirstOrDefault() ?? Location.None;
            if (t.IsGenericType) p.Fehler.Add(("eine Projektion ist nicht generisch", ort));
            var gesehen = new HashSet<string>();
            foreach (var m in t.GetMembers(Abstractions.Projektionsvertrag.Auf).OfType<IMethodSymbol>().Where(m => m.MethodKind == MethodKind.Ordinary))
            {
                var mOrt = m.Locations.FirstOrDefault() ?? ort;
                if (m.DeclaredAccessibility != Accessibility.Public || m.IsStatic || m.IsGenericMethod)
                {
                    p.Fehler.Add(($"'{m.Name}' ist {(m.IsStatic ? "statisch" : m.IsGenericMethod ? "generisch" : "nicht öffentlich")} — ein Auf ist eine öffentliche, "
                        + "nicht generische Instanz-Methode", mOrt));
                    continue;
                }
                var auf = LiesAuf(m, mOrt, p.Fehler);
                if (auf == null) continue;
                if (!gesehen.Add(Name(auf.Eingang))) { p.Doppelt.Add((auf.Eingang, mOrt)); continue; }
                p.Aufs.Add(auf);
            }
            if (p.Aufs.Count == 0 && p.Doppelt.Count == 0 && p.Fehler.Count == 0)
                p.Fehler.Add(($"'{t.Name}' hat kein {Abstractions.Projektionsvertrag.Auf} — eine Projektion hört mindestens ein Event", ort));
            return p;
        }

        private static Auf? LiesAuf(IMethodSymbol m, Location ort, List<(string, Location)> fehler)
        {
            if (m.Parameters.Length == 0 || m.Parameters[0].Type is not INamedTypeSymbol eingang
                || eingang.TypeKind == TypeKind.Interface || eingang.IsAbstract || !Hat(eingang, A + "IEvent") || Hat(eingang, A + "ITransientEvent"))
            {
                fehler.Add(("ein Auf einer Projektion nimmt als ersten Parameter ein konkretes, persistentes Event (kein Hinweis — die stehen nie im Log)", ort));
                return null;
            }
            var auf = new Auf { Methode = m, Eingang = eingang };
            auf.Parameter.Add(("e", eingang));
            var mitAbbruch = false;
            foreach (var par in m.Parameters.Skip(1))
            {
                var typ = par.Type as INamedTypeSymbol;
                if (typ != null && Name(typ) == Abstractions.Projektionsvertrag.Herkunft && !auf.MitHerkunft) { auf.MitHerkunft = true; auf.Parameter.Add(("h", typ)); }
                else if (typ != null && Name(typ) == "System.Threading.CancellationToken" && !mitAbbruch) { mitAbbruch = true; auf.Parameter.Add(("ct", typ)); }
                else if (typ != null && IstSchreibFaehigkeit(typ)) { auf.Faehigkeiten.Add(typ); auf.Parameter.Add(("faehigkeit", typ)); }
                else
                {
                    fehler.Add(($"der Parameter '{par.Name}' ist weder Herkunft noch CancellationToken noch eine Schreib-Fähigkeit (Interface mit IWriteStore) — "
                        + "eine Projektion schreibt nur über die Fähigkeiten ihres Stores", par.Locations.FirstOrDefault() ?? ort));
                    return null;
                }
            }
            if (!(m.ReturnType is INamedTypeSymbol { IsGenericType: false } r && Name(r) == "System.Threading.Tasks.Task"))
            {
                fehler.Add(($"'{m.Name}' gibt '{m.ReturnType.ToDisplayString()}' zurück — eine Projektion gibt nichts aus, ihr Auf liefert Task", ort));
                return null;
            }
            return auf;
        }

        public static Reader LiesReader(INamedTypeSymbol t)
        {
            var r = new Reader { Typ = t };
            var ort = t.Locations.FirstOrDefault() ?? Location.None;
            if (t.IsGenericType) r.Fehler.Add(("ein Reader ist nicht generisch", ort));
            var gesehen = new HashSet<string>();
            foreach (var m in t.GetMembers(Abstractions.Projektionsvertrag.Frage).OfType<IMethodSymbol>().Where(m => m.MethodKind == MethodKind.Ordinary))
            {
                var mOrt = m.Locations.FirstOrDefault() ?? ort;
                if (m.DeclaredAccessibility != Accessibility.Public || m.IsStatic || m.IsGenericMethod)
                {
                    r.Fehler.Add(($"'{m.Name}' ist {(m.IsStatic ? "statisch" : m.IsGenericMethod ? "generisch" : "nicht öffentlich")} — eine Frage ist eine öffentliche, "
                        + "nicht generische Instanz-Methode", mOrt));
                    continue;
                }
                var f = LiesFrage(m, mOrt, r.Fehler);
                if (f == null) continue;
                if (!gesehen.Add(Name(f.Query))) { r.Doppelt.Add((f.Query, mOrt)); continue; }
                r.Fragen.Add(f);
            }
            if (r.Fragen.Count == 0 && r.Doppelt.Count == 0 && r.Fehler.Count == 0)
                r.Fehler.Add(($"'{t.Name}' hat keine {Abstractions.Projektionsvertrag.Frage} — ein Reader beantwortet mindestens eine Query", ort));
            return r;
        }

        private static Frage? LiesFrage(IMethodSymbol m, Location ort, List<(string, Location)> fehler)
        {
            if (m.Parameters.Length == 0 || m.Parameters[0].Type is not INamedTypeSymbol query || query.TypeKind == TypeKind.Interface || !Hat(query, A + "IQuery"))
            {
                fehler.Add(("eine Frage nimmt als ersten Parameter ihre Query (ein konkreter Typ mit IQuery)", ort));
                return null;
            }
            var f = new Frage { Methode = m, Query = query };
            f.Parameter.Add(("q", query));
            var mitAbbruch = false;
            foreach (var par in m.Parameters.Skip(1))
            {
                var typ = par.Type as INamedTypeSymbol;
                if (typ != null && Name(typ) == "System.Threading.CancellationToken" && !mitAbbruch) { mitAbbruch = true; f.Parameter.Add(("ct", typ)); }
                else if (typ != null && IstLeseFaehigkeit(typ)) { f.Faehigkeiten.Add(typ); f.Parameter.Add(("faehigkeit", typ)); }
                else
                {
                    fehler.Add(($"der Parameter '{par.Name}' ist weder CancellationToken noch eine Lese-Fähigkeit (Interface mit IReadStore) — ein Reader liest nur",
                        par.Locations.FirstOrDefault() ?? ort));
                    return null;
                }
            }
            return LiesAntwort(m, f, ort, fehler) ? f : null;
        }

        /// <summary>Die Rückgabe einer Frage: Antwort oder <c>OneOf&lt;…&gt;</c> konkreter Typen mit <c>IQueryResponse</c>, auch als Task.</summary>
        private static bool LiesAntwort(IMethodSymbol m, Frage f, Location ort, List<(string, Location)> fehler)
        {
            var rueck = m.ReturnType as INamedTypeSymbol;
            if (rueck != null && rueck.IsGenericType && rueck.TypeArguments.Length == 1
                && (Name(rueck) == "System.Threading.Tasks.Task`1" || Name(rueck) == "System.Threading.Tasks.ValueTask`1"))
            {
                f.Huelle = rueck.Name;
                rueck = rueck.TypeArguments[0] as INamedTypeSymbol;
            }
            if (rueck == null || m.ReturnsVoid || Name(rueck) == "System.Threading.Tasks.Task" || Name(rueck) == "System.Threading.Tasks.ValueTask")
            {
                fehler.Add(($"'{m.Name}' gibt keine Antwort zurück — eine Frage liefert eine Antwort oder OneOf<…> (auch als Task)", ort));
                return false;
            }
            if (rueck.IsGenericType && Name(rueck).StartsWith(A + "OneOf`"))
            {
                f.OneOf = true;
                f.Antworten.AddRange(rueck.TypeArguments.OfType<INamedTypeSymbol>());
            }
            else f.Antworten.Add(rueck);
            foreach (var a in f.Antworten)
                if (a.TypeKind == TypeKind.Interface || a.IsAbstract || !Hat(a, A + "IQueryResponse"))
                {
                    fehler.Add(($"die Antwort '{a.ToDisplayString()}' ist kein konkreter Typ mit IQueryResponse — eine Antwort reist über die Leitung", ort));
                    return false;
                }
            return true;
        }

        /// <summary>Ein Decider: eine Klasse mit <c>IDecider&lt;TState&gt;</c> — dann ihr State, sonst <c>null</c>.</summary>
        public static INamedTypeSymbol? StateVonDecider(INamedTypeSymbol t)
        {
            if (t.TypeKind != TypeKind.Class) return null;
            var d = t.AllInterfaces.FirstOrDefault(i => i.IsGenericType && Name(i) == A + "IDecider`1");
            return d?.TypeArguments[0] as INamedTypeSymbol;
        }

        /// <summary>
        /// Die Fragen eines Deciders (§2.6): <c>Frage(TQuery q)</c> → Antwort oder <c>OneOf&lt;…&gt;</c>, synchron (der Aggregat-Actor antwortet im Turn
        /// aus seinem Zustand), ohne Fähigkeiten. Die Query trägt genau ein <c>Id&lt;TAggregat&gt;</c>-Glied — daran wird sie geroutet.
        /// </summary>
        public static Aggregatfragen LiesAggregatfragen(INamedTypeSymbol decider, INamedTypeSymbol state)
        {
            var a = new Aggregatfragen { Decider = decider, State = state };
            var ort = decider.Locations.FirstOrDefault() ?? Location.None;
            var gesehen = new HashSet<string>();
            foreach (var m in decider.GetMembers(Abstractions.Projektionsvertrag.Frage).OfType<IMethodSymbol>().Where(m => m.MethodKind == MethodKind.Ordinary))
            {
                var mOrt = m.Locations.FirstOrDefault() ?? ort;
                if (m.DeclaredAccessibility != Accessibility.Public || m.IsStatic || m.IsGenericMethod)
                {
                    a.Fehler.Add(($"'{m.Name}' ist {(m.IsStatic ? "statisch" : m.IsGenericMethod ? "generisch" : "nicht öffentlich")} — eine Frage ist eine öffentliche, "
                        + "nicht generische Instanz-Methode", mOrt));
                    continue;
                }
                if (m.Parameters.Length != 1 || m.Parameters[0].Type is not INamedTypeSymbol query || query.TypeKind == TypeKind.Interface || !Hat(query, A + "IQuery"))
                {
                    a.Fehler.Add(("eine Frage an ein Aggregat nimmt genau einen Parameter, ihre Query (ein konkreter Typ mit IQuery) — sie liest nur seinen Zustand", mOrt));
                    continue;
                }
                var f = new Frage { Methode = m, Query = query };
                f.Parameter.Add(("q", query));
                if (!LiesAntwort(m, f, mOrt, a.Fehler)) continue;
                if (f.Huelle != "")
                {
                    a.Fehler.Add(($"'{m.Name}' gibt einen {f.Huelle} zurück — eine Frage an ein Aggregat antwortet synchron aus seinem Zustand (im Turn des Actors)", mOrt));
                    continue;
                }
                var ids = Glieder(query).Where(g => IstId(g.Typ) && Name(((INamedTypeSymbol)g.Typ).TypeArguments[0]) == Name(state)).Select(g => g.Name).ToList();
                if (ids.Count != 1)
                {
                    a.Fehler.Add(($"die Query '{query.Name}' trägt {(ids.Count == 0 ? "kein Glied" : ids.Count + " Glieder")} Id<{state.Name}> — eine Frage an ein Aggregat "
                        + "geht an genau eine Instanz, die ein Glied Id<TAggregat> nennt", mOrt));
                    continue;
                }
                f.IdGlied = ids[0];
                if (!gesehen.Add(Name(query))) { a.Doppelt.Add((query, mOrt)); continue; }
                a.Fragen.Add(f);
            }
            return a;
        }

        // ─────────────────────────────────────────────────────────────
        // Was eine Antwort zeigt: ihre Id<T>-Felder (docs/10 §10.4)
        // ─────────────────────────────────────────────────────────────

        /// <summary>Ist <paramref name="t"/> eine typisierte Id (<c>Id&lt;T&gt;</c>)?</summary>
        public static bool IstId(ITypeSymbol t) => t is INamedTypeSymbol n && n.IsGenericType && Name(n) == A + "Id`1";

        /// <summary>Ist <paramref name="t"/> eine Sammlung (Array oder <c>IEnumerable&lt;X&gt;</c>, nicht <c>string</c>)? Dann ihr Element.</summary>
        public static ITypeSymbol? Element(ITypeSymbol t)
        {
            if (t is IArrayTypeSymbol arr) return arr.ElementType;
            if (t.SpecialType == SpecialType.System_String) return null;
            var e = t is INamedTypeSymbol n && n.IsGenericType && Name(n) == "System.Collections.Generic.IEnumerable`1"
                ? n
                : t.AllInterfaces.FirstOrDefault(i => i.IsGenericType && Name(i) == "System.Collections.Generic.IEnumerable`1");
            if (e == null) return null;
            // Wörterbücher: die Werte (die Schlüssel sind selten Ids, und KeyValuePair hat sie im Feld Value).
            return e.TypeArguments[0];
        }

        /// <summary>Wird ein Typ nach Ids durchsucht? Eigene Typen der Anwendung (nicht aus System/Microsoft), keine Enums, keine Ids selbst.</summary>
        public static bool Durchsuchbar(ITypeSymbol t)
        {
            if (t is not INamedTypeSymbol n || n.TypeKind == TypeKind.Enum || n.TypeKind == TypeKind.Interface || IstId(n)) return false;
            if (n.IsGenericType && Name(n) == "System.Nullable`1") return false;
            var ns = n.ContainingNamespace?.ToDisplayString() ?? "";
            return !(ns == "System" || ns.StartsWith("System.") || ns.StartsWith("Microsoft.")) && (n.TypeKind == TypeKind.Class || n.TypeKind == TypeKind.Struct);
        }

        /// <summary>Die lesbaren Instanz-Eigenschaften und -Felder eines Typs (samt Basistypen), in Deklarations-Reihenfolge.</summary>
        public static IEnumerable<(string Name, ITypeSymbol Typ)> Glieder(INamedTypeSymbol t)
        {
            var gesehen = new HashSet<string>();
            for (var b = t; b != null && b.SpecialType != SpecialType.System_Object; b = b.BaseType)
                foreach (var m in b.GetMembers())
                {
                    if (m.IsStatic || m.DeclaredAccessibility != Accessibility.Public || !gesehen.Add(m.Name)) continue;
                    if (m is IPropertySymbol p && p.GetMethod != null && !p.IsIndexer && p.Name != "EqualityContract") yield return (p.Name, p.Type);
                    else if (m is IFieldSymbol f && !f.IsImplicitlyDeclared) yield return (f.Name, f.Type);
                }
        }
    }
}
