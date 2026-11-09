// In Projekt: Infrastructure.SourceGeneration (per Link auch in Domain.SourceGeneration und GraphExtractor.Kern)
// Dateiname: Graphform.cs

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System.Collections.Generic;
using System.Linq;

namespace Infrastructure.SourceGeneration
{
    /// <summary>
    /// Die EINE Lesart eines Graphen (docs/04 §4.4) für Generator, Analyzer und Extractor — nur Code-Fakten: der Marker <c>IGraph</c>, die Parameter
    /// des Records (je einer ein Knoten: Name = Konfig-Schlüssel, Typ = eine Funktion, ein Graph oder eine Übersetzung), ihre Attribute <c>[Von(nameof(…))]</c> und
    /// <c>[Kopf]</c> und die Signaturen der Funktionen (<see cref="Funktionsform"/>). Jede innere Kante steht am nehmenden Knoten: eine genannte Quelle
    /// speist die Ports, deren Typ sie ausgibt; aus Typen allein entsteht keine Kante. Ein <c>[Kopf]</c> nimmt von außen, was keine genannte Quelle
    /// speist — je Eingangstyp genau einer. Was ein Knoten ausgibt und kein Knoten über <c>Von</c> von ihm nimmt, verlässt den Graphen. Ein Graph als
    /// Knoten wird aufgefaltet (seine Knoten heißen <c>Aufbereitung.Vorschau</c>) und bleibt nach außen eine Schachtel: <c>Von</c> an ihm speist seine
    /// Köpfe, <c>[Kopf]</c> an ihm macht seine Köpfe zu Köpfen des äußeren, <c>Von(nameof(Schachtel))</c> meint seine Ausgänge — das ist seine SIGNATUR.
    /// Eine Funktion darf mehrfach im Graphen stehen; die Reihenfolge der Parameter bedeutet nichts.
    /// <para>Jedes <c>Auf</c> nimmt genau eine Nachricht (<see cref="Funktionsform"/>) und rechnet je Ankunft; Zusammenführen und Einsammeln stehen
    /// im Logik-Code. Eine Kante ist gesichert, mit <c>Verlierbar = true</c> an ihrem <c>[Von]</c> verlierbar.</para>
    /// <para>Ein Graph ist domänenneutral und liegt in einem Katalog; eine ÜBERSETZUNG ist kein Knoten, sie lebt in der Domäne
    /// (<c>ITranslate&lt;TSource, TTarget&gt;</c>, <see cref="Uebersetzungsform"/>) und nimmt, was ein laufender Graph ausgibt, nach seinem Typ.</para>
    /// </summary>
    internal static class Graphform
    {
        private const string A = "Abstractions.";
        public const string IGraph = A + "IGraph";
        public const string Von = A + "VonAttribute";
        public const string Kopf = A + "KopfAttribute";

        /// <summary>Ein Knoten (aufgefaltet): sein Pfad (Konfig-Schlüssel, <c>Aufbereitung.Vorschau</c>), seine Funktion und seine Operationen.</summary>
        internal sealed class Knoten
        {
            public string Name = "";
            public INamedTypeSymbol Typ = null!;
            public Funktionsform.Form Funktion = null!;
            public Location Ort = Location.None;
            /// <summary>Die Graphen, in denen der Knoten liegt (Pfade, außen zuerst; leer = im Graphen selbst).</summary>
            public List<string> Bereich = new List<string>();
            /// <summary>Die Operationen (je <c>Auf</c> mit Command-Eingang eine, in Reihenfolge der Funktion).</summary>
            public List<Operation> Operationen = new List<Operation>();
            /// <summary>Die Commands ohne Ziel, die der Knoten nimmt (ohne Antwort-Events).</summary>
            public IEnumerable<INamedTypeSymbol> Eingaenge => Operationen.SelectMany(o => o.Auf.Eingaenge);
            /// <summary>Alles, was der Knoten ausgeben kann (auch aus den Antworten auf seine Commands).</summary>
            public List<INamedTypeSymbol> Ausgaben = new List<INamedTypeSymbol>();
        }

        /// <summary>Eine Operation eines Knotens: sein <c>Auf</c> (Nummer in der Funktion) und was sie ausgibt (welche Typen als Strom).</summary>
        internal sealed class Operation
        {
            public Knoten Knoten = null!;
            public Funktionsform.Auf Auf = null!;
            /// <summary>Die Nummer des <c>Auf</c> in der Funktion (Index in <see cref="Funktionsform.Form.Aufs"/>) — so ruft die Rechnung es.</summary>
            public int Nummer;
            /// <summary>Was die Operation ausgeben kann: ihre Fälle und, gibt sie Commands an Aggregate, die Fälle der Antworten darauf.</summary>
            public List<INamedTypeSymbol> Ausgaben = new List<INamedTypeSymbol>();
            /// <summary>Die Ausgaben, die als Strom kommen (je Element ein Unterlauf).</summary>
            public HashSet<string> Stroeme = new HashSet<string>();
            /// <summary>Die Ausgaben, die eine Wahl sind (ein Fall eines OneOf mit mehreren Fällen: 0 oder 1).</summary>
            public HashSet<string> Wahlen = new HashSet<string>();
        }

        /// <summary>Eine Kante: von einer Operation über einen Command ohne Ziel zu einem Port eines Knotens desselben Graphen.</summary>
        internal sealed class Kante
        {
            public Knoten Von = null!, Nach = null!;
            public Operation VonOperation = null!, NachOperation = null!;
            public Funktionsform.Port Port = null!;
            public INamedTypeSymbol Typ = null!;
            /// <summary>Kommt der Typ beim Erzeuger als Strom (je Element eine Nachricht)?</summary>
            public bool Strom;
            /// <summary>Darf eine Nachricht auf dieser Kante verloren gehen (<c>[Von(…, Verlierbar = true)]</c>)? Sonst gesichert.</summary>
            public bool Verlierbar;
        }

        /// <summary>
        /// Ein Glied eines Graph-Records — ein Parameter, wie er dasteht: Name, Typ, <c>[Kopf]</c>, die Namen aus <c>[Von]</c> (Parameter desselben
        /// Records) und was es ist: ein Funktions-Knoten oder ein Graph im Graphen.
        /// </summary>
        internal sealed class Glied
        {
            public string Name = "";
            /// <summary>Der Pfad im aufgefalteten Graphen (<c>Aufbereitung.Vorschau</c>).</summary>
            public string Pfad = "";
            public ITypeSymbol Typ = null!;
            public bool Kopf;
            public List<string> Von = new List<string>();
            /// <summary>Die Namen aus <c>[Von]</c>, deren Kanten verlierbar sind (<c>Verlierbar = true</c>).</summary>
            public HashSet<string> Verlierbar = new HashSet<string>();
            public Location Ort = Location.None;
            public Knoten? Knoten;
            public Schachtel? Innen;
        }

        /// <summary>Ein gelesener Graph: Knoten (aufgefaltet), Kanten, Köpfe (Eingangstyp → Knoten), Ausgänge, Signatur und Formfehler (Text, Ort).</summary>
        internal sealed class Form
        {
            public INamedTypeSymbol Typ = null!;
            /// <summary>Die Parameter des Records, wie sie dastehen (mit <c>[Kopf]</c> und <c>[Von]</c>).</summary>
            public List<Glied> Glieder = new List<Glied>();
            public List<Knoten> Knoten = new List<Knoten>();
            public List<Kante> Kanten = new List<Kante>();
            /// <summary>Die Eingänge des Graphen: Typ → der Knoten mit <c>[Kopf]</c>, der ihn nimmt (eine Liste mit genau einem).</summary>
            public List<(INamedTypeSymbol Typ, List<Knoten> Knoten)> Koepfe = new List<(INamedTypeSymbol, List<Knoten>)>();
            /// <summary>Was den Graphen verlässt: je Knoten die Ausgaben, die kein Knoten über <c>Von</c> von ihm nimmt.</summary>
            public List<(Knoten Von, INamedTypeSymbol Typ)> Ausgaenge = new List<(Knoten, INamedTypeSymbol)>();
            /// <summary>Die Signatur: was der Graph nimmt (seine Köpfe) und was er nach außen gibt (Commands ohne Ziel, die kein innerer Knoten nimmt).</summary>
            public List<INamedTypeSymbol> SignaturEin = new List<INamedTypeSymbol>(), SignaturAus = new List<INamedTypeSymbol>();
            /// <summary>Der Quellknoten (eine Funktion, die lauscht): er ist der Kopf des Graphen, jedes seiner Elemente startet einen Lauf; null = keiner.</summary>
            public Knoten? Quelle;
            public List<(string Text, Location Ort)> Fehler = new List<(string, Location)>();
            /// <summary>Ohne Formfehler — nur so erzeugt der Generator den Graphen.</summary>
            public bool Gueltig => Fehler.Count == 0;

            /// <summary>Der Kopf für einen Eingangstyp, oder null.</summary>
            public List<Knoten>? KopfFuer(ITypeSymbol t) => Koepfe.Where(k => Funktionsform.Voll(k.Typ) == Funktionsform.Voll(t)).Select(k => k.Knoten).FirstOrDefault();
        }

        /// <summary>Ist <paramref name="t"/> ein Graph — ein konkreter Record mit <c>IGraph</c>?</summary>
        public static bool IstGraph(INamedTypeSymbol t) =>
            t.TypeKind == TypeKind.Class && !t.IsAbstract && Funktionsform.Name(t) != IGraph && Funktionsform.Hat(t, IGraph);

        private static string V(ITypeSymbol t) => Funktionsform.Voll(t);

        /// <summary>
        /// Ein Graph beim Auffalten: seine Glieder, seine Funktions-Knoten und inneren Graphen, die Ports, die von außen nehmen (seine Köpfe), und die
        /// Ausgaben, die ihn verlassen (je Erzeuger) — als Schachtel gesehen seine Signatur.
        /// </summary>
        internal sealed class Schachtel
        {
            public string Pfad = "";
            public List<Glied> Glieder = new List<Glied>();
            public List<Knoten> Eigene = new List<Knoten>();
            public List<Schachtel> Innere = new List<Schachtel>();
            public List<(Knoten Knoten, Operation Op, Funktionsform.Port Port)> Offen = new List<(Knoten, Operation, Funktionsform.Port)>();
            public List<(Knoten Knoten, Operation Op, INamedTypeSymbol Typ)> Aus = new List<(Knoten, Operation, INamedTypeSymbol)>();
            public IEnumerable<Knoten> Alle => Eigene.Concat(Innere.SelectMany(i => i.Alle));
        }

        /// <summary>Liest den Graphen <paramref name="t"/> (<see cref="IstGraph"/> vorausgesetzt).</summary>
        public static Form Lies(INamedTypeSymbol t)
        {
            var g = new Form { Typ = t };
            var wurzel = Falte(t, "", new List<string>(), new HashSet<string>(), g);
            if (wurzel == null) return g;
            g.Glieder = wurzel.Glieder;
            g.Knoten = wurzel.Alle.ToList();

            // Ausgänge: was ein Knoten ausgibt und kein Knoten über Von von ihm nimmt (Commands an Aggregate und Hinweise immer).
            foreach (var von in g.Knoten)
                foreach (var x in von.Ausgaben)
                    if (!(Funktionsform.IstCommandOhneZiel(x) && g.Kanten.Any(e => e.Von == von && V(e.Typ) == V(x)))
                        && !g.Ausgaenge.Any(a => a.Von == von && V(a.Typ) == V(x)))
                        g.Ausgaenge.Add((von, x));

            // Köpfe: die Ports der Wurzel, die von außen nehmen, je Typ.
            foreach (var (k, _, port) in wurzel.Offen)
                foreach (var typ in port.Typen)
                {
                    var da = g.KopfFuer(typ);
                    if (da == null) g.Koepfe.Add((typ, new List<Knoten> { k }));
                    else if (!da.Contains(k)) da.Add(k);
                }
            g.SignaturEin = g.Koepfe.Select(k => k.Typ).ToList();
            g.SignaturAus = wurzel.Aus.Select(a => a.Typ).GroupBy(V).Select(x => x.First()).ToList();
            var quellen = g.Knoten.Where(k => k.Funktion.Lausche != null).ToList();
            g.Quelle = quellen.FirstOrDefault();
            if (quellen.Count > 1)
                g.Fehler.Add(($"der Graph hat {quellen.Count} Quellknoten ({string.Join(", ", quellen.Select(k => k.Name))}) — eine Quelle je Graph: sie ist sein Kopf",
                    quellen[1].Ort));
            if (g.Quelle != null && g.Koepfe.Count > 0)
                g.Fehler.Add(($"der Graph hat den Quellknoten {g.Quelle.Name} und dazu Köpfe ({string.Join(", ", g.Koepfe.Select(k => k.Typ.Name))}) — ein Graph "
                    + "mit Quelle hat sonst keinen Kopf (was von draußen kommt, nimmt ein eigener Graph)", g.Quelle.Ort));
            if (g.Knoten.Count > 0 && g.Koepfe.Count == 0 && g.Quelle == null)
                g.Fehler.Add(("der Graph hat keinen Kopf und keine Quelle — von außen nimmt nur ein Knoten mit [Kopf]", OrtVon(t)));

            var kreis = Kreis(g);
            if (kreis != null)
            {
                g.Fehler.Add(($"die Knoten {string.Join(" → ", kreis.Select(k => k.Name))} bilden einen Kreis — ein Graph ist kreisfrei (was über Läufe hinweg "
                    + "gilt, steht im Zustand eines Knotens oder in einem Aggregat; ein neuer Lauf ist ein Command an den eigenen Kopf)", kreis[0].Ort));
                return g;
            }
            return g;
        }

        private static Location OrtVon(ISymbol s) => s.Locations.FirstOrDefault() ?? Location.None;

        /// <summary>
        /// Faltet den Graphen <paramref name="t"/> unter dem Pfad <paramref name="pfad"/> auf: Funktions-Knoten mit ihren Operationen, innere Graphen
        /// rekursiv; dann die Kanten zwischen seinen Gliedern (<see cref="Verdrahte"/>). Fehler gehen an <paramref name="g"/>.
        /// </summary>
        private static Schachtel? Falte(INamedTypeSymbol t, string pfad, List<string> bereich, HashSet<string> offen, Form g)
        {
            var ort = OrtVon(t);
            if (!offen.Add(V(t)))
            {
                g.Fehler.Add(($"der Graph '{t.Name}' enthält sich selbst — ein Graph im Graphen ist eine Schachtel, kein Kreis", ort));
                return null;
            }
            if (!t.IsRecord) g.Fehler.Add(("ein Graph ist ein Record — seine Parameter sind die Knoten", ort));
            if (t.IsGenericType) g.Fehler.Add(("ein Graph ist nicht generisch", ort));
            var ctor = t.InstanceConstructors.Where(c => c.DeclaredAccessibility == Accessibility.Public && c.Parameters.Length > 0)
                .OrderByDescending(c => c.Parameters.Length).FirstOrDefault();
            var s = new Schachtel { Pfad = pfad };
            if (ctor == null)
            {
                g.Fehler.Add(("ein Graph nennt seine Knoten als Parameter des Records (record X(F1 A, F2 B) : IGraph)", ort));
                offen.Remove(V(t));
                return s;
            }
            var namen = new HashSet<string>(ctor.Parameters.Select(p => p.Name));
            foreach (var p in ctor.Parameters)
            {
                var pOrt = p.Locations.FirstOrDefault() ?? ort;
                var name = pfad.Length == 0 ? p.Name : pfad + "." + p.Name;
                var glied = new Glied { Name = p.Name, Pfad = name, Typ = p.Type, Ort = pOrt };
                LiesAttribute(p, glied, namen, t, g);
                if (p.Type is INamedTypeSymbol u && Uebersetzungsform.Ist(u))
                {
                    g.Fehler.Add(($"der Knoten '{p.Name}' ist eine Übersetzung ({u.Name}) — eine Übersetzung ist kein Knoten eines Graphen: der Graph bleibt "
                        + "domänenneutral, die Übersetzung lebt in der Domäne und nimmt seine Nachricht nach ihrem Typ", pOrt));
                    continue;
                }
                if (p.Type is INamedTypeSymbol gg && IstGraph(gg))
                {
                    var innen = Falte(gg, name, bereich.Concat(new[] { name }).ToList(), offen, g);
                    if (innen == null) continue;
                    glied.Innen = innen;
                    s.Innere.Add(innen);
                    s.Glieder.Add(glied);
                    continue;
                }
                if (p.Type is not INamedTypeSymbol f || !Funktionsform.IstFunktion(f) || f.TypeArguments.Any(a => a.TypeKind == TypeKind.TypeParameter))
                {
                    g.Fehler.Add(($"der Knoten '{p.Name}' ist keine Funktion und kein Graph ('{p.Type.ToDisplayString()}') — ein Knoten "
                        + "ist eine Funktion (IFunktion, eine generische mit Typargument) oder ein anderer Graph", pOrt));
                    continue;
                }
                glied.Knoten = LiesKnoten(f, name, bereich, pOrt);
                s.Eigene.Add(glied.Knoten);
                s.Glieder.Add(glied);
            }
            Verdrahte(s, t, g);
            offen.Remove(V(t));
            return s;
        }

        /// <summary>
        /// <c>[Kopf]</c> und <c>[Von(…)]</c> eines Parameters (Entscheidung 8): jedes Argument ist <c>nameof</c> eines anderen Parameters desselben Records,
        /// keiner doppelt. Das <c>nameof</c> sieht nur, wer den Quelltext hat (der Analyzer im Pipeline-Projekt); aus Metadaten zählen die Werte.
        /// </summary>
        private static void LiesAttribute(IParameterSymbol p, Glied glied, HashSet<string> namen, INamedTypeSymbol t, Form g)
        {
            foreach (var a in p.GetAttributes())
            {
                var n = a.AttributeClass == null ? "" : Funktionsform.Name(a.AttributeClass);
                if (n == Kopf) { glied.Kopf = true; continue; }
                if (n != Von) continue;
                var werte = Werte(a);
                var verlierbar = IstVerlierbar(a);
                var syntax = a.ApplicationSyntaxReference?.GetSyntax() as AttributeSyntax;
                var aOrt = syntax?.GetLocation() ?? glied.Ort;
                if (syntax?.ArgumentList != null)
                    foreach (var arg in syntax.ArgumentList.Arguments)
                        if (arg.NameEquals?.Name.Identifier.ValueText == "Verlierbar") continue;
                        else if (!(arg.NameEquals == null && arg.NameColon == null
                              && arg.Expression is InvocationExpressionSyntax { Expression: IdentifierNameSyntax { Identifier.ValueText: "nameof" } } inv
                              && inv.ArgumentList.Arguments.Count == 1 && inv.ArgumentList.Arguments[0].Expression is IdentifierNameSyntax))
                            g.Fehler.Add(($"[Von] an '{p.Name}' nennt {arg} — jedes Argument ist nameof eines anderen Knotens desselben Graphen "
                                + "(nameof(Einlesen)), nie ein String-Literal", arg.GetLocation()));
                if (werte.Count == 0)
                    g.Fehler.Add(($"[Von] an '{p.Name}' nennt keinen Knoten — ohne Quelle fällt es weg (ein Knoten, der von außen nimmt, trägt [Kopf])", aOrt));
                foreach (var w in werte)
                {
                    if (w == null) continue;
                    if (w == p.Name)
                        g.Fehler.Add(($"[Von] an '{p.Name}' nennt den Knoten selbst — ein Knoten bekommt nie von sich (ein Kreis)", aOrt));
                    else if (!namen.Contains(w))
                        g.Fehler.Add(($"[Von] an '{p.Name}' nennt '{w}' — kein Knoten des Graphen {t.Name} (Von nennt Parameter desselben Records)", aOrt));
                    else if (glied.Von.Contains(w))
                        g.Fehler.Add(($"[Von] an '{p.Name}' nennt '{w}' doppelt", aOrt));
                    else
                    {
                        glied.Von.Add(w);
                        if (verlierbar) glied.Verlierbar.Add(w);
                    }
                }
            }
        }

        private static bool IstVerlierbar(AttributeData a) =>
            a.NamedArguments.Any(x => x.Key == "Verlierbar" && x.Value.Value is true);

        private static List<string?> Werte(AttributeData a) =>
            a.ConstructorArguments.Length == 1 && a.ConstructorArguments[0].Kind == TypedConstantKind.Array
                ? a.ConstructorArguments[0].Values.Select(v => v.Value as string).ToList()
                : new List<string?>();

        /// <summary>
        /// <c>[Kopf]</c>, die Namen aus <c>[Von]</c> eines Parameters und welche davon verlierbar sind, wie sie dastehen (ohne Prüfung) — für Werkzeuge,
        /// die jeden Knoten zeigen.
        /// </summary>
        public static (bool Kopf, List<string> Von, List<string> Verlierbar) KopfUndVon(IParameterSymbol p)
        {
            var kopf = false;
            var von = new List<string>();
            var verlierbar = new List<string>();
            foreach (var a in p.GetAttributes())
            {
                var n = a.AttributeClass == null ? "" : Funktionsform.Name(a.AttributeClass);
                if (n == Kopf) kopf = true;
                else if (n == Von)
                {
                    var werte = Werte(a).OfType<string>().ToList();
                    von.AddRange(werte);
                    if (IstVerlierbar(a)) verlierbar.AddRange(werte);
                }
            }
            return (kopf, von, verlierbar);
        }

        /// <summary>
        /// Die Kanten einer Schachtel (Entscheidungen 1–3, 7): je Glied mit <c>[Von]</c> speist jede genannte Quelle die Ports, deren Typ sie ausgibt —
        /// mindestens einen; ein Port ohne Quelle nimmt von außen, wenn das Glied <c>[Kopf]</c> trägt, sonst ist er ein Fehler. Danach die Köpfe der
        /// Schachtel (je Eingangstyp genau einer) und was sie verlässt.
        /// </summary>
        private static void Verdrahte(Schachtel s, INamedTypeSymbol t, Form g)
        {
            var kanten = new List<Kante>();
            foreach (var glied in s.Glieder)
            {
                var ports = Eingaenge(glied);
                if (glied.Knoten is { Funktion.Lausche: not null })
                {
                    if (glied.Kopf || glied.Von.Count > 0)
                        g.Fehler.Add(($"'{glied.Name}' ist eine Quelle (sie lauscht) und trägt {(glied.Kopf ? "[Kopf]" : "[Von]")} — eine Quelle nimmt nichts: "
                            + "sie trägt weder [Kopf] noch [Von], die Knoten hinter ihr nennen sie mit [Von]", glied.Ort));
                    continue;
                }
                if (!glied.Kopf && glied.Von.Count == 0)
                {
                    if (ports.Count > 0)
                        g.Fehler.Add(($"der Knoten '{glied.Name}' trägt weder [Kopf] noch [Von] — ein Knoten nimmt von außen ([Kopf]) oder von Knoten "
                            + "desselben Graphen ([Von(nameof(…))]); nur eine Quelle trägt keins von beiden", glied.Ort));
                    continue;
                }
                var gespeist = new HashSet<Funktionsform.Port>();
                foreach (var name in glied.Von)
                {
                    var quelle = s.Glieder.FirstOrDefault(x => x.Name == name);
                    if (quelle == null) continue;   // kein Knoten (gemeldet) oder einer mit Formfehler
                    var aus = Ausgaenge(quelle);
                    var speist = false;
                    foreach (var (vk, vop, x) in aus)
                        foreach (var (nk, nop, port) in ports.Where(z => z.Port.Typen.Any(y => V(y) == V(x))))
                        {
                            kanten.Add(new Kante { Von = vk, Nach = nk, VonOperation = vop, NachOperation = nop, Port = port, Typ = x, Strom = vop.Stroeme.Contains(V(x)),
                                Verlierbar = glied.Verlierbar.Contains(name) });
                            gespeist.Add(port);
                            speist = true;
                        }
                    if (!speist)
                        g.Fehler.Add(($"'{glied.Name}' nennt in [Von] '{name}', aber {name} gibt nichts aus, was {glied.Name} nimmt ({name}: "
                            + $"{Liste(aus.Select(x => x.Typ))}; {glied.Name}: {Liste(ports.SelectMany(z => z.Port.Typen))}) — eine genannte Quelle speist mindestens "
                            + "einen Port", glied.Ort));
                }
                var ungespeist = ports.Where(z => !gespeist.Contains(z.Port)).ToList();
                if (glied.Kopf)
                {
                    if (ungespeist.Count == 0)
                        g.Fehler.Add(($"'{glied.Name}' trägt [Kopf], aber an ihm kommt nichts von außen an — "
                            + (ports.Count == 0 ? "er nimmt nichts" : "jeden Port speist eine genannte Quelle") + " (ein [Kopf] nimmt, was keine Quelle speist)", glied.Ort));
                    s.Offen.AddRange(ungespeist);
                }
                else
                    foreach (var (k, _, port) in ungespeist)
                        g.Fehler.Add(($"'{port.Name}' in {k.Name} ({Liste(port.Typen)}) bekommt von keiner genannten Quelle ({string.Join(", ", glied.Von)}) etwas, und "
                            + $"'{glied.Name}' ist kein [Kopf] — nenne den Erzeuger in [Von] oder setze [Kopf], wenn er von außen kommt", glied.Ort));
            }
            g.Kanten.AddRange(kanten);

            // GR-KOPF: je Eingangstyp genau ein Kopf.
            foreach (var typ in s.Offen.SelectMany(o => o.Port.Typen).GroupBy(V))
            {
                var nehmen = s.Offen.Where(o => o.Port.Typen.Any(y => V(y) == typ.Key)).Select(o => o.Knoten).Distinct().ToList();
                if (nehmen.Count > 1)
                    g.Fehler.Add(($"'{typ.First().Name}' nehmen {nehmen.Count} Köpfe des Graphen {t.Name} ({string.Join(", ", nehmen.Select(k => k.Name))}) — je "
                        + "Eingangstyp genau ein [Kopf]; was mehrere brauchen, nimmt einer und die übrigen nennen ihn in [Von]", nehmen[1].Ort));
            }

            // Was die Schachtel verlässt: die Ausgaben ihrer Glieder, die keine Kante hier nimmt.
            foreach (var glied in s.Glieder)
                foreach (var (k, op, x) in Ausgaenge(glied))
                    if (!kanten.Any(e => e.VonOperation == op && V(e.Typ) == V(x))) s.Aus.Add((k, op, x));
        }

        /// <summary>Die Ports, an denen ein Glied nimmt: die Ports der Operationen eines Knotens bzw. die Köpfe eines inneren Graphen.</summary>
        private static List<(Knoten Knoten, Operation Op, Funktionsform.Port Port)> Eingaenge(Glied glied) =>
            glied.Innen != null ? glied.Innen.Offen
                : glied.Knoten!.Operationen.SelectMany(o => o.Auf.Ports.Select(p => (glied.Knoten, o, p))).ToList();

        /// <summary>Was ein Glied an Commands ohne Ziel ausgibt (je Erzeuger): die eines Knotens bzw. die Ausgänge eines inneren Graphen.</summary>
        private static List<(Knoten Knoten, Operation Op, INamedTypeSymbol Typ)> Ausgaenge(Glied glied) =>
            glied.Innen != null ? glied.Innen.Aus
                : glied.Knoten!.Operationen.SelectMany(o => o.Ausgaben.Where(Funktionsform.IstCommandOhneZiel).Select(x => (glied.Knoten, o, x))).ToList();

        private static string Liste(IEnumerable<ITypeSymbol> typen)
        {
            var namen = typen.Select(x => x.Name).Distinct().ToList();
            return namen.Count == 0 ? "nichts" : string.Join(", ", namen);
        }

        /// <summary>Ein Funktions-Knoten mit seinen Operationen: je Auf mit Command-Eingang eine.</summary>
        private static Knoten LiesKnoten(INamedTypeSymbol f, string name, List<string> bereich, Location ort)
        {
            var form = Funktionsform.Lies(f);
            var k = new Knoten { Name = name, Typ = f, Funktion = form, Ort = ort, Bereich = bereich };
            var antworten = form.Aufs.Where(form.IstAntwort).ToList();
            for (var i = 0; i < form.Aufs.Count; i++)
            {
                var a = form.Aufs[i];
                if (form.IstAntwort(a)) continue;
                var op = new Operation { Knoten = k, Auf = a, Nummer = i };
                void Nimm(Funktionsform.Auf von)
                {
                    foreach (var x in von.Ausgaben)
                    {
                        if (!op.Ausgaben.Any(y => V(y) == V(x))) op.Ausgaben.Add(x);
                        if (von.Strom) op.Stroeme.Add(V(x));
                        if (von.Wahl) op.Wahlen.Add(V(x));
                    }
                }
                Nimm(a);
                // Gibt sie Commands an Aggregate, kann jede Antwort kommen (ein Ziel entscheidet sie darauf hin) — und mit ihr deren Ausgaben.
                if (a.Ausgaben.Any(x => Funktionsform.Hat(x, A + "IAggregatCommand")))
                    foreach (var an in antworten) Nimm(an);
                k.Operationen.Add(op);
            }
            if (form.Lausche is { } l)
            {
                // Die Quelle: eine Operation ohne Ports; jedes Element startet einen Lauf (kein Unterlauf — es ist der Kopf).
                var op = new Operation { Knoten = k, Auf = l, Nummer = form.Aufs.Count };
                foreach (var x in l.Ausgaben)
                {
                    op.Ausgaben.Add(x);
                    if (l.Wahl) op.Wahlen.Add(V(x));
                }
                k.Operationen.Add(op);
            }
            foreach (var x in form.Aufs.Concat(form.Lausche != null ? new[] { form.Lausche } : new Funktionsform.Auf[0]).SelectMany(a => a.Ausgaben))
                if (!k.Ausgaben.Any(y => V(y) == V(x))) k.Ausgaben.Add(x);
            return k;
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
