// In Projekt: Infrastructure.SourceGeneration (per Link auch in Domain.SourceGeneration und GraphExtractor.Kern)
// Dateiname: Funktionsform.cs

using Microsoft.CodeAnalysis;
using System.Collections.Generic;
using System.Linq;

namespace Infrastructure.SourceGeneration
{
    /// <summary>
    /// Die EINE Lesart einer Funktion (docs/10 §10.3) und ihrer Technik für den <c>FunktionsBausteinGenerator</c>, die Bindung der Technik
    /// (<c>TechnikGenerator</c>), die Analyzer (R2, R12), die Lage-Regeln und den Extractor. Nur Code-Fakten: der Marker <c>IFunktion</c>, die
    /// Methoden <c>Auf</c> und ihre Signaturen (Eingang, <c>Herkunft</c>, Lese-Fähigkeiten, Abbruch, Rückgabe), die Konstruktoren (Technik ist, was eine
    /// Funktion dort nimmt, <see cref="Techniken"/>). Verglichen wird über volle Namen, nie über Symbol-Identität — so liest sie auch Typen aus
    /// anderen Compilations.
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
            /// <summary>Der Eingang ist eine Nachricht ohne Marker (eine Folge, ein Zwischenergebnis — Nachrichtenform), kein Event.</summary>
            public bool Nachricht;
            /// <summary>Der Eingang ist eine Quell-Nachricht (<c>IQuellNachricht</c>): sie steht genau einmal im Log, ihr Eingang ist durabel.</summary>
            public bool Quelle;
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
            /// <summary>Ein <c>Auf</c> ohne Ausgabe (<c>void</c>, <c>Task</c>, <c>ValueTask</c>): nur mit Zustand — der Eingang wirkt allein auf ihn (die
            /// Funktion lernt, docs/04 §4.4).</summary>
            public bool Keine;
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
            /// <summary>Der Typ des Zustands (<c>IZustand&lt;T&gt;</c> im Konstruktor einer Klasse, docs/04 §4.4) — null = ohne Zustand.</summary>
            public INamedTypeSymbol? Zustand;
            /// <summary>
            /// Die Funktion steht VOR dem Aggregat (docs/04 §4.4): ihre Eingänge sind Nachrichten ohne Marker, Quell-Nachrichten und Hinweise, ihre
            /// Event-Eingänge Antworten. Sie liest nie das Log; Eingänge bringt ihr die Laufzeit über Wurzel, Parent und Kinder. Jede Funktion mit
            /// Eingängen muss es sein (CQRS100): wer nur Events hört, hätte keinen Einstieg.
            /// </summary>
            public bool VorDemAggregat => Aufs.Any(a => a.Nachricht || a.Hinweis || a.Quelle);

            /// <summary>Ein Event-Eingang einer Funktion vor dem Aggregat ist eine ANTWORT: ein Ziel entschied es auf einen ihrer Commands hin; sie hört
            /// es nur so, nie aus dem Log (dass einer ihrer Commands es entscheiden kann, prüft das Gesamtbild).</summary>
            public bool IstAntwort(Auf a) => VorDemAggregat && !a.Nachricht && !a.Hinweis && !a.Quelle;
        }

        public static string Name(ITypeSymbol t) =>
            (t.OriginalDefinition.ContainingNamespace?.ToDisplayString() ?? "") + "." + t.OriginalDefinition.MetadataName;

        public static bool Hat(ITypeSymbol t, string vollerName) =>
            Name(t) == vollerName || t.AllInterfaces.Any(i => Name(i) == vollerName);

        /// <summary>
        /// Ist <paramref name="t"/> eine Funktion (<c>IFunktion</c>)? Eine Schnittstelle <c>: IFunktion</c>, oder eine konkrete Klasse, die
        /// <c>IFunktion</c> direkt trägt (keine abgeleitete Funktions-Schnittstelle — dann wäre sie deren Umsetzung).
        /// </summary>
        public static bool IstFunktion(INamedTypeSymbol t)
        {
            if (Name(t) == IFunktion || !Hat(t, IFunktion)) return false;
            if (t.TypeKind == TypeKind.Interface) return true;
            return t.TypeKind == TypeKind.Class && !t.IsAbstract && !Umsetzt(t).Any();
        }

        /// <summary>Hat <paramref name="t"/> eine Methode <c>Auf</c> (einen Eingang)?</summary>
        public static bool HatAuf(INamedTypeSymbol t) =>
            t.GetMembers(Abstractions.Funktionsvertrag.Auf).OfType<IMethodSymbol>().Any(m => m.MethodKind == MethodKind.Ordinary);

        /// <summary>
        /// Die Methoden einer Technik, die ihr Halter ruft: öffentliche Instanz-Methoden (an einer Klasse nur die selbst deklarierten, ohne Freigabe).
        /// </summary>
        public static IEnumerable<IMethodSymbol> TechnikMethoden(INamedTypeSymbol t) =>
            t.GetMembers().OfType<IMethodSymbol>().Where(m => m.MethodKind == MethodKind.Ordinary && !m.IsStatic
                && (t.TypeKind == TypeKind.Interface || m.DeclaredAccessibility == Accessibility.Public)
                && !(t.TypeKind == TypeKind.Class && m.Name is "Dispose" or "DisposeAsync"));

        /// <summary>
        /// Die TECHNIK unter <paramref name="typen"/> (docs/10 §10.3, ohne Marker): jeder Typ, den der Konstruktor einer Funktion (ihrer Klasse bzw.
        /// einer Umsetzung ihrer Schnittstelle) oder — geschachtelt — einer Technik-Umsetzung nimmt und der weder Konfig-Record noch
        /// <c>ILogger&lt;T&gt;</c> noch <c>IZustand&lt;T&gt;</c> ist (<see cref="Ctorbindung.Art.Technik"/>). Eine Schnittstelle hat als Umsetzungen die
        /// Klassen unter <paramref name="typen"/>, die sie implementieren; eine Klasse ist ihre eigene. Mit den Klassen, die Technik umsetzen.
        /// </summary>
        public static Technikmenge Techniken(IEnumerable<INamedTypeSymbol> typen)
        {
            var alle = typen.ToList();
            var klassen = alle.Where(k => k.TypeKind == TypeKind.Class && !k.IsAbstract).ToList();
            var m = new Technikmenge();
            var offen = new Queue<INamedTypeSymbol>(klassen.Where(k => IstFunktion(k) || Umsetzt(k).Any()));
            var besucht = new HashSet<string>();
            while (offen.Count > 0)
            {
                var k = offen.Dequeue();
                if (!besucht.Add(Name(k))) continue;
                foreach (var p in Ctorbindung.Konstruktor(k)?.Parameters ?? System.Collections.Immutable.ImmutableArray<IParameterSymbol>.Empty)
                {
                    if (p.Type is not INamedTypeSymbol t || Ctorbindung.ArtVon(t) != Ctorbindung.Art.Technik) continue;
                    if (!m.Typen.ContainsKey(Name(t))) m.Typen[Name(t)] = t.OriginalDefinition;
                    var ums = t.TypeKind == TypeKind.Interface
                        ? klassen.Where(c => c.AllInterfaces.Any(i => Name(i) == Name(t))).ToList()
                        : new List<INamedTypeSymbol> { t };
                    foreach (var u in ums)
                    {
                        if (!m.Umsetzungen.ContainsKey(Name(u))) m.Umsetzungen[Name(u)] = u;
                        offen.Enqueue(u);
                    }
                }
            }
            return m;
        }

        /// <summary>Die Technik einer Anwendung (<see cref="Techniken"/>): die Typen im Konstruktor und die Klassen, die sie umsetzen (voller Name).</summary>
        internal sealed class Technikmenge
        {
            public readonly Dictionary<string, INamedTypeSymbol> Typen = new Dictionary<string, INamedTypeSymbol>();
            public readonly Dictionary<string, INamedTypeSymbol> Umsetzungen = new Dictionary<string, INamedTypeSymbol>();
            public bool IstTechnik(ITypeSymbol t) => Typen.ContainsKey(Name(t));
            public bool IstUmsetzung(ITypeSymbol t) => Umsetzungen.ContainsKey(Name(t));
        }

        /// <summary>Die Funktions-Schnittstellen, die eine Klasse implementiert (sie ist deren Umsetzung im Host).</summary>
        public static IEnumerable<INamedTypeSymbol> Umsetzt(INamedTypeSymbol t) =>
            t.AllInterfaces.Where(i => Name(i) != IFunktion && Hat(i, IFunktion));

        /// <summary>Ist <paramref name="t"/> der Zustand einer Funktion, <c>IZustand&lt;T&gt;</c>?</summary>
        public static bool IstZustand(ITypeSymbol t) => t is INamedTypeSymbol { IsGenericType: true } n && Name(n) == Abstractions.Funktionsvertrag.Zustand;

        /// <summary>Die Parameter des Konstruktors, die einen Zustand nehmen (<c>IZustand&lt;T&gt;</c>).</summary>
        public static IEnumerable<IParameterSymbol> ZustandsParameter(INamedTypeSymbol t) =>
            Ctorbindung.Konstruktor(t)?.Parameters.Where(p => IstZustand(p.Type)) ?? Enumerable.Empty<IParameterSymbol>();

        /// <summary>
        /// Der Zustand einer Klasse (<c>IZustand&lt;T&gt;</c> im Konstruktor, docs/04 §4.4) — einer Funktion oder einer Technik-Umsetzung: T oder null; Formfehler (mehr als einer, T nicht serialisierbar ohne Reflection) in <paramref name="fehler"/>.
        /// </summary>
        public static INamedTypeSymbol? ZustandVon(INamedTypeSymbol t, List<(string Text, Location Ort)> fehler)
        {
            var ort = t.Locations.FirstOrDefault() ?? Location.None;
            var zustaende = ZustandsParameter(t).ToList();
            if (zustaende.Count > 1)
            {
                fehler.Add(("der Konstruktor nimmt mehr als einen IZustand<T> — eine Funktion hat höchstens einen Zustand (fasse ihn in einer Klasse zusammen)",
                    zustaende[1].Locations.FirstOrDefault() ?? ort));
                return null;
            }
            if (zustaende.Count == 0) return null;
            var z = ((INamedTypeSymbol)zustaende[0].Type).TypeArguments[0] as INamedTypeSymbol;
            if (z == null || z.TypeKind != TypeKind.Class || z.IsGenericType || z.IsAbstract || z.ContainingType != null)
            {
                fehler.Add(($"der Zustand '{zustaende[0].Type.ToDisplayString()}' ist keine eigene, nicht generische, nicht geschachtelte Klasse — "
                    + "nur so entsteht sein Serializer ohne Reflection", zustaende[0].Locations.FirstOrDefault() ?? ort));
                return null;
            }
            return z;
        }


        /// <summary>Liest die Form einer Funktion (<see cref="IstFunktion"/> vorausgesetzt).</summary>
        public static Form Lies(INamedTypeSymbol t)
        {
            var f = new Form
            {
                Typ = t, Draussen = t.TypeKind == TypeKind.Interface,
            };
            var ort = t.Locations.FirstOrDefault() ?? Location.None;
            if (!f.Draussen) f.Zustand = ZustandVon(t, f.Fehler);
            // Eine Funktions-Schnittstelle hat nur Auf: das Framework ruft sie — eine weitere Methode riefe niemand (Technik ist eine eigene Schnittstelle).
            if (f.Draussen)
                foreach (var m in t.GetMembers().OfType<IMethodSymbol>().Where(m => m.MethodKind == MethodKind.Ordinary && !m.IsStatic
                             && m.Name != Abstractions.Funktionsvertrag.Auf))
                    f.Fehler.Add(($"die Schnittstelle hat neben {Abstractions.Funktionsvertrag.Auf} die Methode '{m.Name}' — eine Funktion hat Eingänge (das "
                        + "Framework ruft sie); was ihre Umsetzung ruft, ist Technik: eine eigene Schnittstelle in ihrem Konstruktor", m.Locations.FirstOrDefault() ?? ort));
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
                var auf = LiesAuf(m, mOrt, f.Fehler, f.Zustand != null);
                if (auf == null) continue;
                var schluessel = Name(auf.Eingang);
                if (gesehen.ContainsKey(schluessel)) { f.Doppelt.Add((auf.Eingang, mOrt)); continue; }
                gesehen[schluessel] = auf;
                f.Aufs.Add(auf);
            }
            // Eine Funktion steht vor dem Aggregat: wer nur Events hört, hätte keinen Einstieg — ein Event hört sie nur als Antwort auf einen eigenen Command.
            if (f.Aufs.Count > 0 && !f.VorDemAggregat)
                f.Fehler.Add(("sie hört nur Events — eine Funktion liest nie das Log: sie hört eine Folge oder ein Zwischenergebnis (schlichte Records), "
                    + "eine Quell-Nachricht oder einen Hinweis; ein Event nur als Antwort auf einen eigenen Command", ort));
            // Sie schreibt nie ins Log: ihre Ausgaben sind Commands, Zwischenergebnisse und Hinweise.
            foreach (var a in f.Aufs)
                foreach (var x in a.Ausgaben.Where(x => Hat(x, A + "IEvent") && !Hat(x, A + "ITransientEvent")))
                    f.Fehler.Add(($"die Ausgabe '{x.Name}' ist ein Event — eine Funktion schreibt nie ins Log: sie gibt Commands, "
                        + "Zwischenergebnisse (schlichte Records) und Hinweise aus", a.Methode.Locations.FirstOrDefault() ?? ort));
            if (gesehen.Count == 0 && f.Doppelt.Count == 0 && f.Fehler.Count == 0)
                f.Fehler.Add(($"'{t.Name}' hat kein {Abstractions.Funktionsvertrag.Auf} — eine Funktion hört mindestens einen Eingang; Technik hinter einer "
                    + "Funktion trägt keinen Marker (sie steht nur im Konstruktor ihres Halters)", ort));
            return f;
        }

        private static Auf? LiesAuf(IMethodSymbol m, Location ort, List<(string, Location)> fehler, bool mitZustand)
        {
            if (m.Parameters.Length == 0 || m.Parameters[0].Type is not INamedTypeSymbol eingang)
            {
                fehler.Add(("ein Auf nimmt als ersten Parameter seinen Eingang (eine Nachricht ohne Marker, ein Event, eine Quell-Nachricht oder einen Hinweis)", ort));
                return null;
            }
            var nachricht = Nachrichtenform.IstSchlicht(eingang);
            if (!nachricht && (eingang.TypeKind == TypeKind.Interface || eingang.IsAbstract || !Hat(eingang, A + "IEvent")))
            {
                fehler.Add(($"der Eingang '{eingang.Name}' ist keine Nachricht ohne Marker (Folge, Zwischenergebnis), kein konkretes Event, keine Quell-Nachricht "
                    + "und kein Hinweis", ort));
                return null;
            }
            var auf = new Auf { Methode = m, Eingang = eingang, Hinweis = Hat(eingang, A + "ITransientEvent"), Nachricht = nachricht,
                Quelle = Hat(eingang, A + "IQuellNachricht") };
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

            // Ohne Ausgabe (void, Task, ValueTask): nur mit Zustand — der Eingang wirkt allein auf ihn (die Funktion lernt).
            if (m.ReturnsVoid || m.ReturnType is INamedTypeSymbol { IsGenericType: false } k
                && (Name(k) == "System.Threading.Tasks.Task" || Name(k) == "System.Threading.Tasks.ValueTask"))
            {
                if (!mitZustand)
                {
                    fehler.Add(("ein Auf gibt etwas aus (void und Task ohne Ergebnis sind keine Funktion — nur eine Funktion mit Zustand darf aus einem Eingang "
                        + "allein lernen)", ort));
                    return null;
                }
                auf.Keine = true;
                auf.Huelle = m.ReturnsVoid ? "" : ((INamedTypeSymbol)m.ReturnType).Name;
                auf.Element = m.ReturnType;
                return auf;
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
                    || !(Hat(a, A + "ICommand") || Hat(a, A + "IEvent") || Nachrichtenform.IstSchlicht(a)))
                {
                    fehler.Add(($"die Ausgabe '{x.ToDisplayString()}' ist kein konkreter Command, kein Zwischenergebnis (schlichter Record), kein konkretes Event "
                        + "und kein Hinweis — was eine Funktion ausgibt, steht vollständig in der Signatur", ort));
                    return null;
                }
                if (Hat(a, A + "IQuellNachricht"))
                {
                    fehler.Add(($"die Ausgabe '{a.Name}' ist eine Quell-Nachricht — Quell-Nachrichten bringen Quellen und Clients ins Log, nicht Funktionen", ort));
                    return null;
                }
                if (auf.Hinweis && Hat(a, A + "IEvent") && !Hat(a, A + "ITransientEvent"))
                {
                    fehler.Add(($"die Ausgabe '{a.Name}' ist ein Event, der Eingang '{eingang.Name}' ein Hinweis — aus einem Hinweis (höchstens einmal, nicht im "
                        + "Log) gehen nur Commands und Hinweise hervor; was fachlich zählt, verlässt das Hinweis-Netz als Command an ein Aggregat (docs/04 §4.4)", ort));
                    return null;
                }
                auf.Ausgaben.Add(a);
            }
            if (auf.Ausgaben.Count == 0) { fehler.Add(("ein Auf gibt etwas aus (void und Task ohne Ergebnis sind keine Funktion)", ort)); return null; }
            return auf;
        }

        private static bool IstOneOf(INamedTypeSymbol t) => t.IsGenericType && Name(t).StartsWith(A + "OneOf`", System.StringComparison.Ordinal);
    }
}
