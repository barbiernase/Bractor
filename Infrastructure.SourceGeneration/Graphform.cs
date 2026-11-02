// In Projekt: Infrastructure.SourceGeneration (per Link auch in Domain.SourceGeneration und GraphExtractor.Kern)
// Dateiname: Graphform.cs

using Microsoft.CodeAnalysis;
using System.Collections.Generic;
using System.Linq;

namespace Infrastructure.SourceGeneration
{
    /// <summary>
    /// Die EINE Lesart eines Graphen (docs/04 §4.4) für Generator, Analyzer und Extractor — nur Code-Fakten: der Marker <c>IGraph</c>, die Parameter
    /// des Records (je einer ein Knoten: Name = Konfig-Schlüssel, Typ = eine Funktion oder ein Graph) und die Signaturen der Funktionen
    /// (<see cref="Funktionsform"/>). Kanten gibt es nicht zum Schreiben: gibt ein Knoten einen Command ohne Ziel aus, bekommt ihn jeder Knoten des Graphen,
    /// der ihn nimmt. Ein Graph als Knoten wird aufgefaltet (seine Knoten heißen <c>Aufbereitung.Vorschau</c>) und bleibt nach außen eine Schachtel: von
    /// außen bekommt er nur seine Eingänge (was er nimmt und innen keiner erzeugt), nach außen gibt er nur seine Ausgänge (was er erzeugt und innen keiner
    /// nimmt) — das ist seine SIGNATUR. Ein Eingangstyp, den kein Knoten des Graphen erzeugt, ist ein KOPF — je Command- bzw. Quell-Typ genau einer. Was
    /// kein Knoten nimmt, verlässt den Graphen. Die Reihenfolge der Parameter bedeutet nichts.
    /// <para>Jede Operation (ein <c>Auf</c>) hat eine EBENE: die Kette der Ströme vom Kopf bis zu ihr (ein Strom öffnet je Element einen Unterlauf). Die
    /// Ports eines Auf liegen auf derselben Ebene (<c>IReadOnlyList&lt;T&gt;</c> sammelt eine tiefer), und ein <c>T</c>-Port hinter einer Wahl (vom
    /// nächsten gemeinsamen Vorfahren aus) ist <c>T?</c> oder Teil eines <c>OneOf</c> — die Vielfachheit aus den Typen (Entscheidung 5).</para>
    /// </summary>
    internal static class Graphform
    {
        private const string A = "Abstractions.";
        public const string IGraph = A + "IGraph";

        /// <summary>Ein Knoten (aufgefaltet): sein Pfad (Konfig-Schlüssel, <c>Aufbereitung.Vorschau</c>), seine Funktion und seine Operationen.</summary>
        internal sealed class Knoten
        {
            public string Name = "";
            public INamedTypeSymbol Typ = null!;
            public Funktionsform.Form Funktion = null!;
            public Location Ort = Location.None;
            /// <summary>Die Graphen, in denen der Knoten liegt (Pfade, außen zuerst; leer = im Graphen selbst).</summary>
            public List<string> Bereich = new List<string>();
            /// <summary>Die Operationen (je <c>Auf</c> mit Command-, Quell- oder Hinweis-Eingang eine, in Reihenfolge der Funktion).</summary>
            public List<Operation> Operationen = new List<Operation>();
            /// <summary>Die Commands ohne Ziel, Quell-Nachrichten und Hinweise, die der Knoten nimmt (ohne Antwort-Events).</summary>
            public IEnumerable<INamedTypeSymbol> Eingaenge => Operationen.SelectMany(o => o.Auf.Eingaenge);
            /// <summary>Alles, was der Knoten ausgeben kann (auch aus den Antworten auf seine Commands).</summary>
            public List<INamedTypeSymbol> Ausgaben = new List<INamedTypeSymbol>();
        }

        /// <summary>Eine Operation eines Knotens: sein <c>Auf</c> (Nummer in der Funktion), was sie ausgibt (welche Typen als Strom) und ihre Ebene.</summary>
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
            /// <summary>Die Ebene: die Knoten, deren Ströme bis hierher Unterläufe öffnen (außen zuerst).</summary>
            public List<string> Ebene = new List<string>();
        }

        /// <summary>Eine Kante: von einer Operation über einen Command ohne Ziel zu einem Port eines Knotens desselben Graphen.</summary>
        internal sealed class Kante
        {
            public Knoten Von = null!, Nach = null!;
            public Operation VonOperation = null!, NachOperation = null!;
            public Funktionsform.Port Port = null!;
            public INamedTypeSymbol Typ = null!;
            /// <summary>Kommt der Typ beim Erzeuger als Strom (je Element ein Unterlauf)?</summary>
            public bool Strom;
        }

        /// <summary>Ein gelesener Graph: Knoten (aufgefaltet), Kanten, Köpfe (Eingangstyp → Knoten), Ausgänge, Signatur und Formfehler (Text, Ort).</summary>
        internal sealed class Form
        {
            public INamedTypeSymbol Typ = null!;
            public List<Knoten> Knoten = new List<Knoten>();
            public List<Kante> Kanten = new List<Kante>();
            /// <summary>Die Eingänge des Graphen: Typ → die Knoten, die ihn nehmen (bei Command und Quell-Nachricht genau einer).</summary>
            public List<(INamedTypeSymbol Typ, List<Knoten> Knoten)> Koepfe = new List<(INamedTypeSymbol, List<Knoten>)>();
            /// <summary>Was den Graphen verlässt: je Knoten die Ausgaben, die kein Knoten des Graphen nimmt.</summary>
            public List<(Knoten Von, INamedTypeSymbol Typ)> Ausgaenge = new List<(Knoten, INamedTypeSymbol)>();
            /// <summary>Die Signatur: was der Graph nimmt (seine Köpfe) und was er nach außen gibt (Commands ohne Ziel, die kein innerer Knoten nimmt).</summary>
            public List<INamedTypeSymbol> SignaturEin = new List<INamedTypeSymbol>(), SignaturAus = new List<INamedTypeSymbol>();
            public List<(string Text, Location Ort)> Fehler = new List<(string, Location)>();

            /// <summary>Der Kopf für einen Eingangstyp, oder null.</summary>
            public List<Knoten>? KopfFuer(ITypeSymbol t) => Koepfe.Where(k => Funktionsform.Voll(k.Typ) == Funktionsform.Voll(t)).Select(k => k.Knoten).FirstOrDefault();
        }

        /// <summary>Ist <paramref name="t"/> ein Graph — ein konkreter Record mit <c>IGraph</c>?</summary>
        public static bool IstGraph(INamedTypeSymbol t) =>
            t.TypeKind == TypeKind.Class && !t.IsAbstract && Funktionsform.Name(t) != IGraph && Funktionsform.Hat(t, IGraph);

        private static string V(ITypeSymbol t) => Funktionsform.Voll(t);

        /// <summary>Ein Graph beim Auffalten: seine Funktions-Knoten, seine inneren Graphen und was er erzeugt bzw. nimmt (als Schachtel).</summary>
        private sealed class Schachtel
        {
            public string Pfad = "";
            public List<Knoten> Eigene = new List<Knoten>();
            public List<Schachtel> Innere = new List<Schachtel>();
            public Dictionary<string, INamedTypeSymbol> Ein = new Dictionary<string, INamedTypeSymbol>(), Aus = new Dictionary<string, INamedTypeSymbol>();
            public IEnumerable<Knoten> Alle => Eigene.Concat(Innere.SelectMany(i => i.Alle));
        }

        /// <summary>Liest den Graphen <paramref name="t"/> (<see cref="IstGraph"/> vorausgesetzt).</summary>
        public static Form Lies(INamedTypeSymbol t)
        {
            var g = new Form { Typ = t };
            var wurzel = Falte(t, "", new List<string>(), new HashSet<string>(), g);
            if (wurzel == null) return g;
            g.Knoten = wurzel.Alle.ToList();
            var schachteln = new Dictionary<string, Schachtel>();
            void Merke(Schachtel s) { schachteln[s.Pfad] = s; foreach (var i in s.Innere) Merke(i); }
            Merke(wurzel);

            // Kanten: T fließt von P nach C, wenn T auf dem Weg von P hinauf bis zum gemeinsamen Graphen Ausgang jeder Schachtel ist und hinab bis C
            //   Eingang jeder Schachtel (ein innerer Graph ist eine Schachtel mit Signatur).
            foreach (var von in g.Knoten)
                foreach (var op in von.Operationen)
                    foreach (var x in op.Ausgaben.Where(Funktionsform.IstCommandOhneZiel))
                    {
                        var gefunden = false;
                        foreach (var nach in g.Knoten)
                            foreach (var nop in nach.Operationen)
                                foreach (var port in nop.Auf.Ports.Where(p => p.Typen.Any(y => V(y) == V(x))))
                                {
                                    if (!Sichtbar(von, nach, V(x), schachteln)) continue;
                                    g.Kanten.Add(new Kante { Von = von, Nach = nach, VonOperation = op, NachOperation = nop, Port = port, Typ = x,
                                        Strom = op.Stroeme.Contains(V(x)) });
                                    gefunden = true;
                                }
                        if (!gefunden && !g.Ausgaenge.Any(a => a.Von == von && V(a.Typ) == V(x))) g.Ausgaenge.Add((von, x));
                    }
            foreach (var von in g.Knoten)
                foreach (var x in von.Ausgaben.Where(x => !Funktionsform.IstCommandOhneZiel(x)))
                    if (!g.Ausgaenge.Any(a => a.Von == von && V(a.Typ) == V(x))) g.Ausgaenge.Add((von, x));

            // Köpfe: Eingangstypen der Wurzel-Schachtel und die Knoten, die sie nehmen.
            foreach (var typ in wurzel.Ein.Values)
            {
                var nehmen = g.Knoten.Where(k => k.Eingaenge.Any(e => V(e) == V(typ)) && k.Bereich.All(b => schachteln[b].Ein.ContainsKey(V(typ)))).ToList();
                g.Koepfe.Add((typ, nehmen));
            }
            g.SignaturEin = wurzel.Ein.Values.ToList();
            g.SignaturAus = wurzel.Aus.Values.ToList();
            if (g.Knoten.Count > 0 && g.Koepfe.Count == 0)
                g.Fehler.Add(("der Graph hat keinen Eingang — jeder Knoten nimmt nur, was ein anderer erzeugt (ein Kreis)", OrtVon(t)));

            var kreis = Kreis(g);
            if (kreis != null)
            {
                g.Fehler.Add(($"die Knoten {string.Join(" → ", kreis.Select(k => k.Name))} bilden einen Kreis — ein Graph ist kreisfrei (was über Läufe hinweg "
                    + "gilt, steht im Zustand eines Knotens oder in einem Aggregat; ein neuer Lauf ist ein Command an den eigenen Kopf)", kreis[0].Ort));
                return g;
            }
            if (g.Fehler.Count == 0)
            {
                Ebenen(g);
                if (g.Fehler.Count == 0) Vielfachheit(g);
            }
            return g;
        }

        private static Location OrtVon(ISymbol s) => s.Locations.FirstOrDefault() ?? Location.None;

        /// <summary>
        /// Faltet den Graphen <paramref name="t"/> unter dem Pfad <paramref name="pfad"/> auf: Funktions-Knoten mit ihren Operationen, innere Graphen
        /// rekursiv; dazu die Signatur der Schachtel. Fehler gehen an <paramref name="g"/>.
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
            foreach (var p in ctor.Parameters)
            {
                var pOrt = p.Locations.FirstOrDefault() ?? ort;
                var name = pfad.Length == 0 ? p.Name : pfad + "." + p.Name;
                if (p.Type is INamedTypeSymbol gg && IstGraph(gg))
                {
                    var innen = Falte(gg, name, bereich.Concat(new[] { name }).ToList(), offen, g);
                    if (innen != null) s.Innere.Add(innen);
                    continue;
                }
                if (p.Type is not INamedTypeSymbol f || !Funktionsform.IstFunktion(f) || f.TypeArguments.Any(a => a.TypeKind == TypeKind.TypeParameter))
                {
                    g.Fehler.Add(($"der Knoten '{p.Name}' ist keine Funktion und kein Graph ('{p.Type.ToDisplayString()}') — ein Knoten ist eine Funktion "
                        + "(IFunktion, eine generische mit Typargument) oder ein anderer Graph", pOrt));
                    continue;
                }
                s.Eigene.Add(LiesKnoten(f, name, bereich, pOrt));
            }
            foreach (var doppelt in s.Eigene.GroupBy(k => V(k.Typ)).Where(x => x.Count() > 1))
                g.Fehler.Add(($"die Funktion '{doppelt.First().Typ.ToDisplayString()}' steht mehrfach im Graphen ({string.Join(", ", doppelt.Select(k => k.Name))}) — "
                    + "je Funktion ein Knoten (sie bekäme jeden Eingang doppelt); ein Typargument unterscheidet gleiche Funktionen", doppelt.Last().Ort));

            // Signatur der Schachtel: was ihre Glieder erzeugen bzw. nehmen (ein innerer Graph mit seiner Signatur).
            var erzeugt = new Dictionary<string, INamedTypeSymbol>();
            var nimmt = new Dictionary<string, INamedTypeSymbol>();
            foreach (var k in s.Eigene)
            {
                foreach (var x in k.Ausgaben.Where(Funktionsform.IstCommandOhneZiel)) erzeugt[V(x)] = x;
                foreach (var x in k.Eingaenge) nimmt[V(x)] = x;
            }
            foreach (var i in s.Innere)
            {
                foreach (var x in i.Aus) erzeugt[x.Key] = x.Value;
                foreach (var x in i.Ein) nimmt[x.Key] = x.Value;
            }
            foreach (var x in nimmt.Where(x => !erzeugt.ContainsKey(x.Key))) s.Ein[x.Key] = x.Value;
            foreach (var x in erzeugt.Where(x => !nimmt.ContainsKey(x.Key))) s.Aus[x.Key] = x.Value;

            // Je Eingangstyp (Command, Quell-Nachricht) genau ein Glied, das ihn nimmt (Hinweise dürfen mehrere hören).
            foreach (var e in s.Ein.Values.Where(e => !Funktionsform.Hat(e, A + "ITransientEvent")))
            {
                var glieder = s.Eigene.Where(k => k.Eingaenge.Any(x => V(x) == V(e))).Select(k => (k.Name, k.Ort))
                    .Concat(s.Innere.Where(i => i.Ein.ContainsKey(V(e))).Select(i => (i.Pfad, ort))).ToList();
                if (glieder.Count > 1)
                    g.Fehler.Add(($"'{e.Name}' nehmen {glieder.Count} Knoten als Eingang des Graphen {t.Name} ({string.Join(", ", glieder.Select(x => x.Item1))}) — "
                        + "je Eingangstyp genau ein Kopf; was mehrere brauchen, gibt ein Kopf weiter", glieder[1].Item2));
            }
            offen.Remove(V(t));
            return s;
        }

        /// <summary>Ein Funktions-Knoten mit seinen Operationen: je Auf mit Command-, Quell- oder Hinweis-Eingang eine.</summary>
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
            foreach (var x in form.Aufs.SelectMany(a => a.Ausgaben))
                if (!k.Ausgaben.Any(y => V(y) == V(x))) k.Ausgaben.Add(x);
            return k;
        }

        /// <summary>Darf <paramref name="typ"/> von <paramref name="von"/> zu <paramref name="nach"/> fließen (die Schachteln dazwischen lassen ihn durch)?</summary>
        private static bool Sichtbar(Knoten von, Knoten nach, string typ, Dictionary<string, Schachtel> schachteln)
        {
            var gemeinsam = 0;
            while (gemeinsam < von.Bereich.Count && gemeinsam < nach.Bereich.Count && von.Bereich[gemeinsam] == nach.Bereich[gemeinsam]) gemeinsam++;
            for (var i = gemeinsam; i < von.Bereich.Count; i++)
                if (!schachteln[von.Bereich[i]].Aus.ContainsKey(typ)) return false;
            for (var i = gemeinsam; i < nach.Bereich.Count; i++)
                if (!schachteln[nach.Bereich[i]].Ein.ContainsKey(typ)) return false;
            return true;
        }

        private static string Ebenentext(List<string> e) => e.Count == 0 ? "die Ebene des Kopfs" : "die Unterläufe von " + string.Join(" → ", e);

        /// <summary>
        /// Die Ebene jeder Operation (in der Reihenfolge der Kanten): ein Port ohne Erzeuger ist ein Kopf (Ebene leer); sonst liegt er auf der Ebene seiner
        /// Erzeuger, eine tiefer, wenn sie ihn als Strom ausgeben; <c>IReadOnlyList&lt;T&gt;</c> sammelt eine Ebene hinauf. Alle Ports eines Auf auf einer Ebene.
        /// </summary>
        private static void Ebenen(Form g)
        {
            var fertig = new HashSet<Operation>();
            var ops = g.Knoten.SelectMany(k => k.Operationen).ToList();
            while (fertig.Count < ops.Count)
            {
                var weiter = false;
                foreach (var op in ops.Where(o => !fertig.Contains(o)).ToList())
                {
                    var hinein = g.Kanten.Where(e => e.NachOperation == op).ToList();
                    if (hinein.Any(e => !fertig.Contains(e.VonOperation))) continue;
                    weiter = true;
                    fertig.Add(op);
                    List<string>? ebene = null;
                    Funktionsform.Port? erster = null;
                    foreach (var port in op.Auf.Ports)
                    {
                        List<string>? portEbene = null;
                        foreach (var e in hinein.Where(e => e.Port == port))
                        {
                            var x = e.VonOperation.Ebene.ToList();
                            if (e.Strom) x.Add(e.Von.Name);
                            if (portEbene == null) portEbene = x;
                            else if (!portEbene.SequenceEqual(x))
                                g.Fehler.Add(($"'{port.Name}' in {op.Knoten.Name} bekommt Nachrichten auf verschiedenen Ebenen ({Ebenentext(portEbene)} bzw. "
                                    + $"{Ebenentext(x)}) — was zusammenfließt, kommt aus demselben Lauf", port.Ort));
                        }
                        portEbene ??= new List<string>();
                        if (port.Form == Funktionsform.Portform.Alle)
                        {
                            if (portEbene.Count == 0)
                            {
                                g.Fehler.Add(($"'{port.Name}' in {op.Knoten.Name} sammelt IReadOnlyList<{port.Typen[0].Name}>, aber {port.Typen[0].Name} kommt aus keinem "
                                    + $"Strom — nimm '{port.Typen[0].Name} {port.Name}' (genau einer je Lauf)", port.Ort));
                                continue;
                            }
                            portEbene = portEbene.Take(portEbene.Count - 1).ToList();
                        }
                        if (ebene == null) { ebene = portEbene; erster = port; }
                        else if (!ebene.SequenceEqual(portEbene))
                        {
                            var tiefer = portEbene.Count > ebene.Count ? port : erster!;
                            g.Fehler.Add(($"die Eingänge von {op.Knoten.Name} liegen auf verschiedenen Ebenen ('{erster!.Name}': {Ebenentext(ebene)}, '{port.Name}': "
                                + $"{Ebenentext(portEbene)}) — ein Join paart nur im selben Lauf; was aus einem Strom kommt, sammelt "
                                + $"'IReadOnlyList<{tiefer.Typen[0].Name}> {tiefer.Name}'", port.Ort));
                        }
                    }
                    op.Ebene = ebene ?? new List<string>();
                }
                if (!weiter) break;
            }
        }

        /// <summary>
        /// Die Vielfachheit eines Joins (Entscheidung 5): vom nächsten gemeinsamen Vorfahren der Ports (einer Nachricht, die oberhalb aller liegt) aus
        /// darf auf dem Weg zu einem <c>T</c>-Port keine Wahl liegen (ein Fall eines OneOf: 0 oder 1) — sonst ist er <c>T?</c> oder Teil eines
        /// <c>OneOf</c>. Ports aus verschiedenen Köpfen paaren nie (Läufe verschiedener Köpfe kommen nicht zusammen).
        /// </summary>
        private static void Vielfachheit(Form g)
        {
            var oben = new Dictionary<Kante, HashSet<(Operation, string)>>();
            HashSet<(Operation, string)> Oben(Kante e)
            {
                if (oben.TryGetValue(e, out var o)) return o;
                o = new HashSet<(Operation, string)> { (e.VonOperation, V(e.Typ)) };
                oben[e] = o;
                foreach (var u in g.Kanten.Where(x => x.NachOperation == e.VonOperation)) o.UnionWith(Oben(u));
                return o;
            }
            HashSet<(Operation, string)> ObenPort(Operation op, Funktionsform.Port p)
            {
                var o = new HashSet<(Operation, string)>();
                foreach (var e in g.Kanten.Where(x => x.NachOperation == op && x.Port == p)) o.UnionWith(Oben(e));
                return o;
            }
            bool WahlNach(Kante e, (Operation, string) m)
            {
                if ((e.VonOperation, V(e.Typ)) == m || !Oben(e).Contains(m)) return false;
                if (e.VonOperation.Wahlen.Contains(V(e.Typ))) return true;
                return g.Kanten.Where(x => x.NachOperation == e.VonOperation).Any(u => WahlNach(u, m));
            }
            foreach (var op in g.Knoten.SelectMany(k => k.Operationen).Where(o => o.Auf.Ports.Count > 1))
            {
                var ports = op.Auf.Ports.Where(p => g.Kanten.Any(e => e.NachOperation == op && e.Port == p)).ToList();
                if (ports.Count < op.Auf.Ports.Count)
                {
                    var kopf = op.Auf.Ports.First(p => !ports.Contains(p));
                    g.Fehler.Add(($"'{kopf.Name}' in {op.Knoten.Name} ist ein Eingang des Graphen, die übrigen nicht — ein Join paart nur Nachrichten desselben "
                        + "Laufs; was über Läufe hinweg gilt, steht im Zustand eines Knotens oder in einem Aggregat", kopf.Ort));
                    continue;
                }
                var gemeinsam = new HashSet<(Operation, string)>(ObenPort(op, ports[0]));
                foreach (var p in ports.Skip(1)) gemeinsam.IntersectWith(ObenPort(op, p));
                if (gemeinsam.Count == 0)
                {
                    g.Fehler.Add(($"die Eingänge von {op.Knoten.Name} haben keinen gemeinsamen Vorfahren — sie kämen aus verschiedenen Läufen und paarten nie",
                        op.Auf.Ports[1].Ort));
                    continue;
                }
                // Der nächste gemeinsame Vorfahr: der, unter dem kein anderer gemeinsamer liegt.
                var naechster = gemeinsam.First(m => !gemeinsam.Any(n => n != m && g.Kanten.Any(e => (e.VonOperation, V(e.Typ)) == n && Oben(e).Contains(m))));
                foreach (var p in ports.Where(p => p.Form == Funktionsform.Portform.Eins))
                    if (g.Kanten.Where(e => e.NachOperation == op && e.Port == p).Any(e => WahlNach(e, naechster)))
                        g.Fehler.Add(($"'{p.Typen[0].Name} {p.Name}' in {op.Knoten.Name} kommt hinter einer Wahl (ein Fall eines OneOf nach "
                            + $"{naechster.Item1.Knoten.Name}) nicht in jedem Lauf — nimm '{p.Typen[0].Name}? {p.Name}' (höchstens einer) oder ein OneOf<…> der "
                            + "Fälle (der erste)", p.Ort));
            }
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
