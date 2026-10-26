// In Projekt: Infrastructure.SourceGeneration (per Link auch in Domain.SourceGeneration und GraphExtractor.Kern)
// Dateiname: Funktionsform.cs

using Microsoft.CodeAnalysis;
using System.Collections.Generic;
using System.Linq;

namespace Infrastructure.SourceGeneration
{
    /// <summary>
    /// Die EINE Lesart einer Funktion in Baustein-Form (Plan Pub/Sub-Neustrukturierung §3.3) — für den <c>FunktionsBausteinGenerator</c>, die
    /// Analyzer (R2, R12), die Lage-Regeln und den Extractor. Nur Code-Fakten: der Marker <c>IFunktion</c>, die Methoden <c>Auf</c> und ihre
    /// Signaturen (Eingang, <c>Herkunft</c>, Lese-Fähigkeiten, Abbruch, Rückgabe). Verglichen wird über volle Namen, nie über Symbol-Identität —
    /// so liest sie auch Typen aus anderen Compilations.
    /// </summary>
    internal static class Funktionsform
    {
        private const string A = "Abstractions.";
        public const string IFunktion = A + "IFunktion";

        /// <summary>Was ein <c>Auf</c> ausgibt: Hülle (Task/ValueTask), Strom (sync/async) und die Abbildung eines Elements auf die Ausgabe.</summary>
        internal sealed class Auf
        {
            public IMethodSymbol Methode = null!;
            public INamedTypeSymbol Eingang = null!;
            public bool Hinweis;
            /// <summary>Die Parameter in Reihenfolge: <c>e</c> (Eingang), <c>h</c> (Herkunft), <c>ct</c> (Abbruch) oder eine Lese-Fähigkeit.</summary>
            public List<(string Art, INamedTypeSymbol? Typ, string Name)> Parameter = new List<(string, INamedTypeSymbol?, string)>();
            public List<INamedTypeSymbol> Faehigkeiten = new List<INamedTypeSymbol>();
            public bool MitHerkunft;
            /// <summary><c>""</c>, <c>"Task"</c> oder <c>"ValueTask"</c>.</summary>
            public string Huelle = "";
            public bool Strom;
            public bool StromAsync;
            /// <summary>Das Element der Rückgabe (bei einem Strom sein Element).</summary>
            public ITypeSymbol Element = null!;
            /// <summary>Wie ein Element zur Ausgabe wird: <c>""</c> (es selbst), <c>".Value"</c> (OneOf), <c>"?.Value"</c> (OneOf?).</summary>
            public string Abbildung = "";
            public List<INamedTypeSymbol> Ausgaben = new List<INamedTypeSymbol>();
        }

        /// <summary>Eine gelesene Funktion: die Methoden <c>Auf</c> und die Formfehler (Text, Ort).</summary>
        internal sealed class Form
        {
            public INamedTypeSymbol Typ = null!;
            /// <summary>Als Schnittstelle deklariert (läuft draußen oder mit einer Implementierung im Host).</summary>
            public bool Draussen;
            public List<Auf> Aufs = new List<Auf>();
            public List<(string Text, Location Ort)> Fehler = new List<(string, Location)>();
            /// <summary>Eingangstypen mit mehr als einem <c>Auf</c> (R2).</summary>
            public List<(INamedTypeSymbol Eingang, Location Ort)> Doppelt = new List<(INamedTypeSymbol, Location)>();
        }

        public static string Name(ITypeSymbol t) =>
            (t.OriginalDefinition.ContainingNamespace?.ToDisplayString() ?? "") + "." + t.OriginalDefinition.MetadataName;

        public static bool Hat(ITypeSymbol t, string vollerName) =>
            Name(t) == vollerName || t.AllInterfaces.Any(i => Name(i) == vollerName);

        private static bool HatAuf(INamedTypeSymbol t) =>
            t.GetMembers(Abstractions.Funktionsvertrag.Auf).OfType<IMethodSymbol>().Any(m => m.MethodKind == MethodKind.Ordinary);

        private static bool HatRufe(INamedTypeSymbol t) =>
            t.GetMembers(Abstractions.Funktionsvertrag.Methode).OfType<IMethodSymbol>().Any(m => m.MethodKind == MethodKind.Ordinary);

        /// <summary>Eine Schnittstelle der Katalog-Form (<c>RufeAsync</c>, bis Phase 4)?</summary>
        public static bool IstKatalog(INamedTypeSymbol t) =>
            t.TypeKind == TypeKind.Interface && Name(t) != IFunktion && Hat(t, IFunktion) && !HatAuf(t);

        /// <summary>
        /// Ist <paramref name="t"/> eine Funktion in Baustein-Form? Eine Schnittstelle <c>: IFunktion</c> mit <c>Auf</c>, oder eine konkrete Klasse,
        /// die <c>IFunktion</c> direkt trägt (keine abgeleitete Funktions-Schnittstelle — dann wäre sie deren Implementierung).
        /// </summary>
        public static bool IstBaustein(INamedTypeSymbol t)
        {
            if (Name(t) == IFunktion || !Hat(t, IFunktion)) return false;
            if (t.TypeKind == TypeKind.Interface) return HatAuf(t);
            return t.TypeKind == TypeKind.Class && !t.IsAbstract && !Umsetzt(t).Any();
        }

        /// <summary>Die Funktions-Schnittstellen (beider Formen), die eine Klasse implementiert.</summary>
        public static IEnumerable<INamedTypeSymbol> Umsetzt(INamedTypeSymbol t) =>
            t.AllInterfaces.Where(i => Name(i) != IFunktion && Hat(i, IFunktion));

        /// <summary>Liest die Form einer Funktion (<see cref="IstBaustein"/> vorausgesetzt).</summary>
        public static Form Lies(INamedTypeSymbol t)
        {
            var f = new Form { Typ = t, Draussen = t.TypeKind == TypeKind.Interface };
            var ort = t.Locations.FirstOrDefault() ?? Location.None;
            if (f.Draussen && HatRufe(t))
                f.Fehler.Add(($"'{t.Name}' hat {Abstractions.Funktionsvertrag.Auf} und {Abstractions.Funktionsvertrag.Methode} — eine Funktion hat genau eine Form", ort));
            if (f.Draussen)
                foreach (var m in t.GetMembers().OfType<IMethodSymbol>().Where(m => m.MethodKind == MethodKind.Ordinary && m.Name != Abstractions.Funktionsvertrag.Auf))
                    f.Fehler.Add(($"die Schnittstelle hat die Methode '{m.Name}' — eine Funktion draußen hat nur {Abstractions.Funktionsvertrag.Auf}", m.Locations.FirstOrDefault() ?? ort));
            if (!f.Draussen && t.IsGenericType)
                f.Fehler.Add(("eine Funktion ist nicht generisch", ort));

            var gesehen = new Dictionary<string, Auf>();
            foreach (var m in t.GetMembers(Abstractions.Funktionsvertrag.Auf).OfType<IMethodSymbol>().Where(m => m.MethodKind == MethodKind.Ordinary))
            {
                var mOrt = m.Locations.FirstOrDefault() ?? ort;
                if (!f.Draussen && (m.DeclaredAccessibility != Accessibility.Public || m.IsStatic))
                {
                    f.Fehler.Add(($"'{m.Name}' ist {(m.IsStatic ? "statisch" : "nicht öffentlich")} — ein Auf ist eine öffentliche Instanz-Methode", mOrt));
                    continue;
                }
                if (m.IsGenericMethod) { f.Fehler.Add(("ein Auf ist nicht generisch", mOrt)); continue; }
                var auf = LiesAuf(m, mOrt, f.Fehler);
                if (auf == null) continue;
                var schluessel = Name(auf.Eingang);
                if (gesehen.ContainsKey(schluessel)) { f.Doppelt.Add((auf.Eingang, mOrt)); continue; }
                gesehen[schluessel] = auf;
                f.Aufs.Add(auf);
            }
            return f;
        }

        private static Auf? LiesAuf(IMethodSymbol m, Location ort, List<(string, Location)> fehler)
        {
            if (m.Parameters.Length == 0 || m.Parameters[0].Type is not INamedTypeSymbol eingang)
            {
                fehler.Add(("ein Auf nimmt als ersten Parameter seinen Eingang (ein Event, eine Quell-Nachricht oder einen Hinweis)", ort));
                return null;
            }
            if (eingang.TypeKind == TypeKind.Interface || eingang.IsAbstract || !Hat(eingang, A + "IEvent"))
            {
                fehler.Add(($"der Eingang '{eingang.Name}' ist kein konkretes Event, keine Quell-Nachricht und kein Hinweis", ort));
                return null;
            }
            var auf = new Auf { Methode = m, Eingang = eingang, Hinweis = Hat(eingang, A + "ITransientEvent") };
            auf.Parameter.Add(("e", eingang, m.Parameters[0].Name));
            var mitAbbruch = false;
            foreach (var p in m.Parameters.Skip(1))
            {
                var typ = p.Type as INamedTypeSymbol;
                if (typ != null && Name(typ) == Abstractions.Funktionsvertrag.Herkunft && !auf.MitHerkunft)
                {
                    auf.MitHerkunft = true;
                    auf.Parameter.Add(("h", typ, p.Name));
                }
                else if (typ != null && Name(typ) == "System.Threading.CancellationToken" && !mitAbbruch)
                {
                    mitAbbruch = true;
                    auf.Parameter.Add(("ct", typ, p.Name));
                }
                else if (typ != null && typ.TypeKind == TypeKind.Interface && Hat(typ, A + "IReadStore") && !Hat(typ, A + "IWriteStore"))
                {
                    auf.Faehigkeiten.Add(typ);
                    auf.Parameter.Add(("faehigkeit", typ, p.Name));
                }
                else
                {
                    fehler.Add(($"der Parameter '{p.Name}' ist weder Herkunft noch CancellationToken noch eine Lese-Fähigkeit (Interface mit IReadStore) — "
                        + "eine Funktion liest höchstens, sie schreibt nie selbst", p.Locations.FirstOrDefault() ?? ort));
                    return null;
                }
            }

            // Rückgabe: [Task<|ValueTask<] ( IEnumerable<X> | IAsyncEnumerable<X> | X ) mit X = konkreter Typ | OneOf<…> | OneOf<…>?
            ITypeSymbol r = m.ReturnType;
            if (r is INamedTypeSymbol { IsGenericType: true } h && (Name(h) == "System.Threading.Tasks.Task`1" || Name(h) == "System.Threading.Tasks.ValueTask`1"))
            {
                auf.Huelle = h.Name;
                r = h.TypeArguments[0];
            }
            if (r is INamedTypeSymbol { IsGenericType: true } s && Name(s) == "System.Collections.Generic.IEnumerable`1")
            {
                auf.Strom = true;
                r = s.TypeArguments[0];
            }
            else if (r is INamedTypeSymbol { IsGenericType: true } sa && Name(sa) == "System.Collections.Generic.IAsyncEnumerable`1")
            {
                if (auf.Huelle.Length > 0) { fehler.Add(("ein asynchroner Strom steht nicht in einem Task", ort)); return null; }
                auf.Strom = auf.StromAsync = true;
                r = sa.TypeArguments[0];
            }
            if (auf.Huelle == "ValueTask" && auf.Strom) { fehler.Add(("ein Strom steht höchstens in einem Task, nicht in einem ValueTask", ort)); return null; }
            auf.Element = r;
            IEnumerable<ITypeSymbol> faelle;
            if (r is INamedTypeSymbol { IsGenericType: true } n && Name(n) == "System.Nullable`1" && n.TypeArguments[0] is INamedTypeSymbol o && IstOneOf(o))
            {
                auf.Abbildung = "?.Value";
                faelle = o.TypeArguments;
            }
            else if (r is INamedTypeSymbol o2 && IstOneOf(o2))
            {
                auf.Abbildung = ".Value";
                faelle = o2.TypeArguments;
            }
            else faelle = new[] { r };

            foreach (var x in faelle)
            {
                if (x is not INamedTypeSymbol a || a.TypeKind == TypeKind.Interface || a.IsAbstract || a.TypeKind == TypeKind.TypeParameter
                    || !(Hat(a, A + "ICommand") || Hat(a, A + "IEvent")))
                {
                    fehler.Add(($"die Ausgabe '{x.ToDisplayString()}' ist kein konkreter Command, kein konkretes Event und kein Hinweis — "
                        + "was eine Funktion ausgibt, steht vollständig in der Signatur", ort));
                    return null;
                }
                if (Hat(a, A + "IQuellNachricht"))
                {
                    fehler.Add(($"die Ausgabe '{a.Name}' ist eine Quell-Nachricht — Quell-Nachrichten bringen Quellen und Clients ins Log, nicht Funktionen", ort));
                    return null;
                }
                auf.Ausgaben.Add(a);
            }
            if (auf.Ausgaben.Count == 0) { fehler.Add(("ein Auf gibt etwas aus (void und Task ohne Ergebnis sind keine Funktion)", ort)); return null; }
            return auf;
        }

        private static bool IstOneOf(INamedTypeSymbol t) => t.IsGenericType && Name(t).StartsWith(A + "OneOf`", System.StringComparison.Ordinal);

        /// <summary>
        /// Nachweislich gleichbleibend (R12, aus der Signatur): eine Klasse ohne Konstruktor-Parameter, deren <c>Auf</c> keine Lese-Fähigkeit nimmt.
        /// Dass ihr Rumpf weder Uhr noch Zufall nutzt, prüft CQRS102.
        /// </summary>
        public static bool Gleichbleibend(Form f) =>
            !f.Draussen && f.Aufs.All(a => a.Faehigkeiten.Count == 0)
            && (Ctorbindung.Konstruktor(f.Typ)?.Parameters.Length ?? 0) == 0;
    }
}
