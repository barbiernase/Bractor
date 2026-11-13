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
        public const string IQuellart = A + "IQuellart";
        public const string IMeldung = A + "IMeldung";

        /// <summary>Der Eingang eines <c>Auf</c> (genau einer je Auf): Parametername und der Typ, den er nimmt.</summary>
        internal sealed class Port
        {
            public string Name = "";
            public List<INamedTypeSymbol> Typen = new List<INamedTypeSymbol>();
            public Location Ort = Location.None;
        }

        /// <summary>Was ein <c>Auf</c> nimmt und ausgibt: Eingangsports, Hülle (Task), Strom (sync/async) und die Abbildung eines Elements auf die Ausgabe.</summary>
        internal sealed class Auf
        {
            public IMethodSymbol Methode = null!;
            /// <summary>Die Eingangsports (Entscheidung 4): je Command-Parameter einer; ein Antwort-Event nur allein.</summary>
            public List<Port> Ports = new List<Port>();
            /// <summary>Der erste Typ des ersten Ports — bei einem einzelnen Eingang (Antwort, ein Command) der Eingang.</summary>
            public INamedTypeSymbol Eingang => Ports[0].Typen[0];
            /// <summary>Alle Typen, die das <c>Auf</c> nimmt (über alle Ports).</summary>
            public IEnumerable<INamedTypeSymbol> Eingaenge => Ports.SelectMany(p => p.Typen);
            /// <summary>Die Eingänge sind Commands ohne Ziel (Nachrichtenform): aus einem Decide, von einem anderen Knoten oder einem Akteur.</summary>
            public bool Command;
            /// <summary>Die Parameter in Reihenfolge: <c>p</c> (ein Port, Nummer in <see cref="Ports"/>), <c>h</c> (Herkunft), <c>ct</c> (Abbruch) oder eine Lese-Fähigkeit.</summary>
            public List<(string Art, INamedTypeSymbol? Typ, string Name, int Port)> Parameter = new List<(string, INamedTypeSymbol?, string, int)>();
            public List<INamedTypeSymbol> Faehigkeiten = new List<INamedTypeSymbol>();
            public bool MitHerkunft;
            /// <summary><c>""</c> oder <c>"Task"</c>.</summary>
            public string Huelle = "";
            public bool Strom;
            public bool StromAsync;
            /// <summary>Das Element der Rückgabe (bei einem Strom sein Element).</summary>
            public ITypeSymbol Element = null!;
            /// <summary>Wie ein Element zur Ausgabe wird: <c>""</c> (es selbst) oder <c>".Value"</c> (OneOf).</summary>
            public string Abbildung = "";
            public List<INamedTypeSymbol> Ausgaben = new List<INamedTypeSymbol>();
            /// <summary>Ist die Ausgabe eine Wahl (ein <c>OneOf</c> mit mehr als einem Fall: jeder Fall kommt 0- oder 1-mal)?</summary>
            public bool Wahl => Abbildung.Length > 0 && Ausgaben.Count > 1;
        }

        /// <summary>Eine gelesene Funktion: die Methoden <c>Auf</c> und die Formfehler (Text, Ort).</summary>
        internal sealed class Form
        {
            public INamedTypeSymbol Typ = null!;
            public List<Auf> Aufs = new List<Auf>();
            public List<(string Text, Location Ort)> Fehler = new List<(string, Location)>();
            /// <summary>Eingangstypen mit mehr als einem <c>Auf</c> (R2).</summary>
            public List<(INamedTypeSymbol Eingang, Location Ort)> Doppelt = new List<(INamedTypeSymbol, Location)>();
            /// <summary>Der Typ des Zustands (<c>IZustand&lt;T&gt;</c> im Konstruktor einer Klasse, docs/04 §4.4) — null = ohne Zustand.</summary>
            public INamedTypeSymbol? Zustand;
            /// <summary>Hat die Funktion einen Einstieg — einen Command, oder lauscht sie? Wer nur Events hört, hätte keinen
            /// (CQRS100): eine Funktion liest nie das Log.</summary>
            public bool MitEinstieg => Lausche != null || Aufs.Any(a => a.Command);

            /// <summary>Ein Event-Eingang ist eine ANTWORT: ein Ziel entschied es auf einen ihrer Commands hin; sie hört es nur so, nie aus dem Log
            /// (dass einer ihrer Commands es entscheiden kann, prüft das Gesamtbild).</summary>
            public bool IstAntwort(Auf a) => !a.Command;

            /// <summary>
            /// Die Methode <c>Lausche(CancellationToken)</c> einer QUELLART (<c>IQuellart</c>, docs/04 §4.7) — ohne Ports, ihr Strom gibt je Element eine
            /// Meldung (<c>IMeldung</c>); null = keine Quellart. Sie steht nicht in <see cref="Aufs"/>; die Rechnung ruft sie unter der Nummer
            /// <c>Aufs.Count</c>.
            /// </summary>
            public Auf? Lausche;

            /// <summary>Eine Quellart (<c>IQuellart</c>): sie lauscht und steht nie in einem Graphen; ihr Akteur gibt hinein, was sie meldet.</summary>
            public bool Quellart;
        }

        /// <summary>Der Name einer Funktion (Baustein, Wirker, Spur): der Klassenname, bei einer generischen mit ihren Typargumenten
        /// (<c>Bildverkleinerung&lt;Vorschau&gt;</c>).</summary>
        public static string Anzeigename(ITypeSymbol t) =>
            t is INamedTypeSymbol { IsGenericType: true } g ? g.Name + "<" + string.Join(",", g.TypeArguments.Select(Anzeigename)) + ">" : t.Name;

        public static string Name(ITypeSymbol t) =>
            (t.OriginalDefinition.ContainingNamespace?.ToDisplayString() ?? "") + "." + t.OriginalDefinition.MetadataName;

        public static bool Hat(ITypeSymbol t, string vollerName) =>
            Name(t) == vollerName || t.AllInterfaces.Any(i => Name(i) == vollerName);

        /// <summary>
        /// Ist <paramref name="t"/> eine Funktion (<c>IFunktion</c>)? Eine konkrete Klasse, die <c>IFunktion</c> trägt — oder eine Schnittstelle
        /// <c>: IFunktion</c>, die die Form als Fehler meldet (eine Funktion ist eine Klasse im Host; was draußen gerechnet wird, ist Arbeit eines Akteurs).
        /// </summary>
        public static bool IstFunktion(INamedTypeSymbol t)
        {
            if (Name(t) == IFunktion || !Hat(t, IFunktion)) return false;
            if (t.TypeKind == TypeKind.Interface) return t.Interfaces.Any(i => Name(i) == IFunktion);
            return t.TypeKind == TypeKind.Class && !t.IsAbstract && !t.AllInterfaces.Any(i => Name(i) != IFunktion && Hat(i, IFunktion));
        }

        /// <summary>
        /// Ist <paramref name="t"/> eine QUELLART (<c>IQuellart</c>, docs/04 §4.7)? Eine konkrete Klasse mit <c>Lausche</c>: sie meldet, was sie draußen findet;
        /// eine Übersetzung der Akteur-Seite macht daraus eine Nachricht, die der Akteur eines Eingangs darf. Nie ein Knoten eines Graphen.
        /// </summary>
        public static bool IstQuellart(INamedTypeSymbol t) =>
            Name(t) != IQuellart && t.TypeKind == TypeKind.Class && !t.IsAbstract && Hat(t, IQuellart);

        /// <summary>Ist <paramref name="t"/> eine Meldung einer Quellart (<c>IMeldung</c>)?</summary>
        public static bool IstMeldung(ITypeSymbol t) => Name(t) != IMeldung && Hat(t, IMeldung);

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
            var offen = new Queue<INamedTypeSymbol>(klassen.Where(k => IstFunktion(k) || IstQuellart(k)));
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


        /// <summary>Liest die Form einer Funktion bzw. einer Quellart (<see cref="IstFunktion"/> oder <see cref="IstQuellart"/> vorausgesetzt).</summary>
        public static Form Lies(INamedTypeSymbol t)
        {
            var f = new Form { Typ = t, Quellart = Hat(t, IQuellart) };
            var ort = t.Locations.FirstOrDefault() ?? Location.None;
            // Eine Funktion ist eine Klasse im Host: was draußen gerechnet wird, rechnet ein Client, der auf ein Fakt reagiert (IHoert).
            if (t.TypeKind == TypeKind.Interface)
            {
                f.Fehler.Add(("eine Funktion ist eine Klasse im Host — eine Funktions-Schnittstelle gibt es nicht; was draußen gerechnet wird, rechnet "
                    + "ein Client: er hört ein Fakt (IHoert) und gibt seine Commands über das IDarf seines Akteurs hinein", ort));
                return f;
            }
            f.Zustand = ZustandVon(t, f.Fehler);
            // Eine generische Funktion (Bildverkleinerung<TFormat>) ist erlaubt: die offene Definition ist eine Blaupause, ein Knoten nennt sie mit
            //   Typargument — das unterscheidet gleiche Funktionen mit verschiedenem Ausgang (Entscheidung 4).

            var gesehen = new Dictionary<string, Auf>();
            foreach (var m in t.GetMembers(Abstractions.Funktionsvertrag.Auf).OfType<IMethodSymbol>().Where(m => m.MethodKind == MethodKind.Ordinary))
            {
                var mOrt = m.Locations.FirstOrDefault() ?? ort;
                if (m.DeclaredAccessibility != Accessibility.Public || m.IsStatic)
                {
                    f.Fehler.Add(($"'{m.Name}' ist {(m.IsStatic ? "statisch" : "nicht öffentlich")} — ein Auf ist eine öffentliche Instanz-Methode", mOrt));
                    continue;
                }
                if (m.IsGenericMethod) { f.Fehler.Add(("ein Auf ist nicht generisch", mOrt)); continue; }
                var auf = LiesAuf(m, mOrt, f.Fehler);
                if (auf == null) continue;
                // R2: je Typ höchstens ein Port über alle Auf eines Knotens (sonst wäre unklar, was die Nachricht auslöst).
                var doppelt = auf.Eingaenge.FirstOrDefault(e => gesehen.ContainsKey(Voll(e)));
                if (doppelt != null) { f.Doppelt.Add((doppelt, mOrt)); continue; }
                foreach (var e in auf.Eingaenge) gesehen[Voll(e)] = auf;
                f.Aufs.Add(auf);
            }
            foreach (var m in t.GetMembers(Abstractions.Funktionsvertrag.Lausche).OfType<IMethodSymbol>().Where(m => m.MethodKind == MethodKind.Ordinary))
            {
                var mOrt = m.Locations.FirstOrDefault() ?? ort;
                if (!f.Quellart)
                {
                    f.Fehler.Add(("eine Funktion lauscht nicht — was draußen gefunden wird, meldet eine Quellart (IQuellart); sie steht in keinem Graphen, "
                        + "ihr Akteur gibt über eine Übersetzung hinein, was sie meldet", mOrt));
                    continue;
                }
                if (f.Lausche != null) { f.Fehler.Add(("eine Quellart lauscht genau einmal (ein Lausche)", mOrt)); continue; }
                f.Lausche = LiesLausche(m, mOrt, f.Fehler);
            }
            if (f.Quellart && f.Aufs.Count > 0)
                f.Fehler.Add(("eine Quellart lauscht nur (Lausche) und nimmt keine Eingänge — was sie meldet, übersetzt die Akteur-Seite in eine Nachricht "
                    + "des Akteurs", f.Aufs[0].Methode.Locations.FirstOrDefault() ?? ort));
            if (f.Quellart && f.Lausche == null && f.Fehler.Count == 0)
                f.Fehler.Add(($"'{t.Name}' ist eine Quellart ohne {Abstractions.Funktionsvertrag.Lausche} — sie lauscht und meldet je Element eine Meldung "
                    + "(IMeldung)", ort));
            // Eine Funktion liest nie das Log: wer nur Events hört, hätte keinen Einstieg — ein Event hört sie nur als Antwort auf einen eigenen Command.
            if (f.Aufs.Count > 0 && !f.MitEinstieg)
                f.Fehler.Add(("sie hört nur Events — eine Funktion liest nie das Log: sie nimmt einen Command (ohne Ziel); ein Event nur als "
                    + "Antwort auf einen eigenen Command", ort));
            if (gesehen.Count == 0 && f.Lausche == null && f.Doppelt.Count == 0 && f.Fehler.Count == 0)
                f.Fehler.Add(($"'{t.Name}' hat kein {Abstractions.Funktionsvertrag.Auf} — eine Funktion hört mindestens einen Eingang; Technik hinter einer "
                    + "Funktion trägt keinen Marker (sie steht nur im Konstruktor ihres Halters)", ort));
            return f;
        }

        /// <summary>Der volle Name samt Typargumenten — unterscheidet <c>BildVerkleinert&lt;Vorschau&gt;</c> von <c>BildVerkleinert&lt;Gross&gt;</c>.</summary>
        public static string Voll(ITypeSymbol t) => t.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);

        /// <summary>
        /// Ein Command ohne Ziel als Port oder Ausgabe im Graphen: wie <see cref="Nachrichtenform.IstOhneZiel"/>, dazu ein generischer Command
        /// (<c>BildVerkleinert&lt;TFormat&gt;</c>) — er reist nur zwischen Knoten, nie über die Leitung.
        /// </summary>
        public static bool IstCommandOhneZiel(ITypeSymbol t) =>
            Nachrichtenform.IstOhneZiel(t)
            || t is INamedTypeSymbol { TypeKind: TypeKind.Class, IsAbstract: false, IsGenericType: true } g && Hat(g, A + "ICommand") && !Hat(g, A + "IAggregatCommand");

        /// <summary>
        /// <c>IAsyncEnumerable&lt;T&gt; Lausche(CancellationToken ct)</c>: öffentlich, ein Abbruch als einziger Parameter, ein asynchroner Strom von
        /// Meldungen (<c>IMeldung</c>, auch als OneOf). Was sie meldet, steht vollständig in der Signatur.
        /// </summary>
        private static Auf? LiesLausche(IMethodSymbol m, Location ort, List<(string, Location)> fehler)
        {
            const string form = "eine Quellart lauscht so: public async IAsyncEnumerable<T> Lausche([EnumeratorCancellation] CancellationToken ct) — T eine "
                + "Meldung (IMeldung: ein Record mit der Kennung seines Elements; auch als OneOf)";
            if (m.DeclaredAccessibility != Accessibility.Public || m.IsStatic || m.IsGenericMethod || m.Parameters.Length != 1
                || Name(m.Parameters[0].Type) != "System.Threading.CancellationToken"
                || m.ReturnType is not INamedTypeSymbol { IsGenericType: true } r || Name(r) != "System.Collections.Generic.IAsyncEnumerable`1")
            {
                fehler.Add((form, ort));
                return null;
            }
            var auf = new Auf { Methode = m, Strom = true, StromAsync = true, Element = r.TypeArguments[0] };
            auf.Parameter.Add(("ct", (INamedTypeSymbol)m.Parameters[0].Type, m.Parameters[0].Name, -1));
            IEnumerable<ITypeSymbol> faelle = new[] { auf.Element };
            if (auf.Element is INamedTypeSymbol o && IstOneOf(o)) { auf.Abbildung = ".Value"; faelle = o.TypeArguments; }
            foreach (var x in faelle)
            {
                if (x is not INamedTypeSymbol a || !IstMeldung(a) || a.IsAbstract)
                {
                    fehler.Add((form + $" — '{x.ToDisplayString()}' ist keiner", ort));
                    return null;
                }
                auf.Ausgaben.Add(a);
            }
            return auf;
        }

        private static Auf? LiesAuf(IMethodSymbol m, Location ort, List<(string, Location)> fehler)
        {
            var auf = new Auf { Methode = m };
            var mitAbbruch = false;
            foreach (var p in m.Parameters)
            {
                var pOrt = p.Locations.FirstOrDefault() ?? ort;
                var typ = p.Type as INamedTypeSymbol;
                if (typ != null && Name(typ) == Abstractions.Funktionsvertrag.Herkunft && !auf.MitHerkunft)
                {
                    auf.MitHerkunft = true;
                    auf.Parameter.Add(("h", typ, p.Name, -1));
                }
                else if (typ != null && Name(typ) == "System.Threading.CancellationToken" && !mitAbbruch)
                {
                    mitAbbruch = true;
                    auf.Parameter.Add(("ct", typ, p.Name, -1));
                }
                else if (typ != null && typ.TypeKind == TypeKind.Interface && Hat(typ, A + "IReadStore") && !Hat(typ, A + "IWriteStore"))
                {
                    auf.Faehigkeiten.Add(typ);
                    auf.Parameter.Add(("faehigkeit", typ, p.Name, -1));
                }
                else if (LiesPort(p, pOrt, fehler) is { } port)
                {
                    auf.Parameter.Add(("p", null, p.Name, auf.Ports.Count));
                    auf.Ports.Add(port);
                }
                else return null;
            }
            if (auf.Ports.Count == 0)
            {
                fehler.Add(("ein Auf nimmt eine Nachricht (einen Command ohne Ziel; allein auch ein Event als Antwort)", ort));
                return null;
            }
            if (auf.Ports.Count > 1)
            {
                fehler.Add(($"ein Auf nimmt genau eine Nachricht, nicht {auf.Ports.Count} ({string.Join(", ", auf.Ports.Select(x => "'" + x.Name + "'"))}) — "
                    + "es rechnet, sobald sie ankommt. Was danach gebraucht wird, trägt die Nachricht mit; sonst nimmt je Nachricht ein eigenes Auf sie an, "
                    + "und der Zustand des Knotens oder ein Aggregat führt sie zusammen", auf.Ports[1].Ort));
                return null;
            }
            auf.Command = IstCommandOhneZiel(auf.Ports[0].Typen[0]);

            // Rückgabe: T | OneOf<…> | IEnumerable<…> | IAsyncEnumerable<…>, T und OneOf auch in Task<…>. Kein void, kein T?, kein ValueTask: was
            //   „nichts“ sein kann, ist ein Strom (leer = nichts), was einer von mehreren ist, ein OneOf (Entscheidung 4).
            ITypeSymbol r = m.ReturnType;
            if (m.ReturnsVoid || r is INamedTypeSymbol { IsGenericType: false } k && (Name(k) == "System.Threading.Tasks.Task" || Name(k) == "System.Threading.Tasks.ValueTask"))
            {
                fehler.Add(("ein Auf gibt etwas aus — void und Task ohne Ergebnis sind keine Funktion (was nichts ausgeben kann, ist ein Strom)", ort));
                return null;
            }
            if (r is INamedTypeSymbol { IsGenericType: true } v && Name(v) == "System.Threading.Tasks.ValueTask`1")
            {
                fehler.Add(("ein Auf gibt keinen ValueTask zurück — asynchron ist es ein Task<…> oder ein IAsyncEnumerable<…>", ort));
                return null;
            }
            if (r is INamedTypeSymbol { IsGenericType: true } h && Name(h) == "System.Threading.Tasks.Task`1")
            {
                auf.Huelle = "Task";
                r = h.TypeArguments[0];
            }
            if (r is INamedTypeSymbol { IsGenericType: true } s && Name(s) == "System.Collections.Generic.IEnumerable`1")
            {
                if (auf.Huelle.Length > 0) { fehler.Add(("ein Strom steht nicht in einem Task — asynchron ist er ein IAsyncEnumerable<…>", ort)); return null; }
                auf.Strom = true;
                r = s.TypeArguments[0];
            }
            else if (r is INamedTypeSymbol { IsGenericType: true } sa && Name(sa) == "System.Collections.Generic.IAsyncEnumerable`1")
            {
                if (auf.Huelle.Length > 0) { fehler.Add(("ein asynchroner Strom steht nicht in einem Task", ort)); return null; }
                auf.Strom = auf.StromAsync = true;
                r = sa.TypeArguments[0];
            }
            if (r.NullableAnnotation == NullableAnnotation.Annotated || r is INamedTypeSymbol { IsGenericType: true } nl && Name(nl) == "System.Nullable`1")
            {
                fehler.Add(($"die Ausgabe '{r.ToDisplayString()}' ist nullable — was nichts sein kann, ist ein Strom (IEnumerable<…>, leer = nichts)", ort));
                return null;
            }
            auf.Element = r;
            IEnumerable<ITypeSymbol> faelle;
            if (r is INamedTypeSymbol o && IstOneOf(o))
            {
                auf.Abbildung = ".Value";
                faelle = o.TypeArguments;
            }
            else faelle = new[] { r };

            foreach (var x in faelle)
            {
                if (x is not INamedTypeSymbol a || a.TypeKind == TypeKind.Interface || a.IsAbstract || a.TypeKind == TypeKind.TypeParameter
                    || !(IstCommandOhneZiel(a) || Hat(a, A + "IAggregatCommand") || Hat(a, A + "ITransientEvent")))
                {
                    fehler.Add(($"die Ausgabe '{x.ToDisplayString()}' ist kein konkreter Command und kein Hinweis — eine Funktion schreibt nie ins Log; was sie "
                        + "ausgibt, steht vollständig in der Signatur", ort));
                    return null;
                }
                auf.Ausgaben.Add(a);
            }
            if (auf.Ausgaben.Count == 0) { fehler.Add(("ein Auf gibt etwas aus", ort)); return null; }
            return auf;
        }

        /// <summary>
        /// Der Eingang aus einem Parameter: ein Command ohne Ziel als <c>T</c>, allein auch ein konkretes Event (als Antwort). Ein Auf rechnet, sobald
        /// seine Nachricht ankommt — eine Liste, ein <c>OneOf</c> oder ein <c>T?</c> als Eingang ist ein Formfehler (warten, sammeln und zusammenführen
        /// steht im Logik-Code: im Zustand des Knotens oder in einem Aggregat). Ein Hinweis ist kein Eingang: Hinweise gehen nur an Akteure draußen
        /// (Entscheidung 14).
        /// </summary>
        private static Port? LiesPort(IParameterSymbol p, Location ort, List<(string, Location)> fehler)
        {
            var port = new Port { Name = p.Name, Ort = ort };
            var t = p.Type;
            if (t is INamedTypeSymbol { IsGenericType: true } l && Name(l) == "System.Collections.Generic.IReadOnlyList`1")
            {
                fehler.Add(($"'{p.Name}' nimmt eine Liste — ein Auf nimmt eine Nachricht und rechnet, sobald sie ankommt; wer Elemente zählt oder "
                    + "einsammelt, tut es im Zustand des Knotens oder in einem Aggregat (Zähl-Aggregat)", ort));
                return null;
            }
            if (t is INamedTypeSymbol o && IstOneOf(o))
            {
                fehler.Add(($"'{p.Name}' nimmt ein OneOf — ein Auf nimmt genau einen Typ; nimm je Fall ein eigenes Auf", ort));
                return null;
            }
            if (t.NullableAnnotation == NullableAnnotation.Annotated && t.IsReferenceType)
            {
                fehler.Add(($"'{p.Name}' ist nullable — ein Auf rechnet, wenn seine Nachricht ankommt, ob eine kommt, entscheidet der Erzeuger; nimm "
                    + $"'{t.WithNullableAnnotation(NullableAnnotation.NotAnnotated).ToDisplayString()} {p.Name}'", ort));
                return null;
            }
            if (t is not INamedTypeSymbol n)
            {
                fehler.Add(($"der Parameter '{p.Name}' ist kein Eingang", ort));
                return null;
            }
            var command = IstCommandOhneZiel(n);
            if (!command && Hat(n, A + "ITransientEvent"))
            {
                fehler.Add(($"der Parameter '{p.Name}' ist der Hinweis '{n.Name}' — eine Funktion hört keine Hinweise (sie gehen nur an Akteure draußen); "
                    + "was ein Knoten braucht, ist ein Command ohne Ziel", ort));
                return null;
            }
            if (!command && (n.TypeKind == TypeKind.Interface || n.IsAbstract || !Hat(n, A + "IEvent")))
            {
                fehler.Add(($"der Parameter '{p.Name}' ('{p.Type.ToDisplayString()}') ist weder Eingang (ein Command ohne Ziel; allein auch ein konkretes "
                    + "Event als Antwort) noch Herkunft, CancellationToken "
                    + "oder Lese-Fähigkeit (Interface mit IReadStore)"
                    + (Hat(n, A + "IAggregatCommand") ? " — ein Command an ein Aggregat nimmt nur sein Aggregat" : ""), ort));
                return null;
            }
            port.Typen.Add(n);
            return port;
        }

        private static bool IstOneOf(INamedTypeSymbol t) => t.IsGenericType && Name(t).StartsWith(A + "OneOf`", System.StringComparison.Ordinal);
    }
}
