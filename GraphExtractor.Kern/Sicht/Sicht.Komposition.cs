using System.Text.Json.Nodes;

namespace GraphExtractor;

// Die KOMPOSITION eines Graphen (docs/10 §10.9, von innen): seine Knoten mit Tiefe in der Kette, Ports, Konstruktor
//   (Technik, Konfig, Zustand, Logger), die Kanten je [Von] mit ihrem Typ, Köpfe und Ausgänge, an einer Übersetzung ihre Zuordnung (die ↦-Karte) —
//   dazu der Baukasten der Anwendung, aus dem man komponiert. Alles aus dem Board (Code-Fakten); der Editor zeichnet.
public sealed partial class Sicht
{
    /// <summary>Die Ports eines Knotens: was er nimmt und was er ausgibt (Funktion: Auf/Lausche; Graph: Köpfe/Ausgänge; Katalog: was er meldet).</summary>
    private (List<string> Ein, List<string> Aus, string Art, string? Karte) KnotenPorts(string typ)
    {
        var basis = Basis(typ);
        if (BausteinFunktion(basis) is { } f)
            return (Alle(f["eingaenge"]).Select(e => S(e, "eingang")).Where(x => x.Length > 0).Distinct().ToList(),
                Alle(f["eingaenge"]).SelectMany(e => L(e, "ausgaben")).Concat(L(f, "lauscht")).Distinct().ToList(), "funktion", "fb:" + S(f, "_id"));
        if (GraphNamens(basis) is { } g)
            return (L(g, "koepfe").ToList(), L(g, "ausgaenge").ToList(), "graph", "gr:" + S(g, "_id"));
        if (KatalogNamens(basis) is { } k)
            return (Alle(k["eingaenge"]).Select(e => S(e, "eingang")).ToList(),
                L(k, "lauscht").Concat(Alle(k["eingaenge"]).SelectMany(e => L(e, "ausgaben"))).Distinct().ToList(), "katalog", null);
        return ([], [], "fehlt", null);
    }

    /// <summary>Ein Konstruktor-Parameter einer Funktion als Chip: Zustand, Logger, Konfig (ein Konfig-Record der Funktion) oder Technik.</summary>
    private JsonObject KonstruktorChip(JsonObject f, JsonObject p)
    {
        var typ = S(p, "typ").Trim().TrimEnd('?');
        var konfigs = Alle(f["konfigs"]).Concat(Alle(f["umsetzungen"]).SelectMany(u => Alle(u["konfigs"]))).Select(k => S(k, "record")).ToHashSet(StringComparer.Ordinal);
        var o = new JsonObject { ["typ"] = typ, ["name"] = S(p, "name") };
        if (typ.StartsWith("IZustand<", StringComparison.Ordinal)) o["art"] = "zustand";
        else if (typ.StartsWith("ILogger", StringComparison.Ordinal)) o["art"] = "logger";
        else if (konfigs.Contains(typ)) o["art"] = "konfig";
        else
        {
            o["art"] = "technik";
            if (GerufenZu(typ) is { } t)
            {
                o["karte"] = "fb:" + S(t, "_id");
                o["umsetzungen"] = Arr(Alle(t["umsetzungen"]).Select(u => S(u, "name")));
            }
        }
        return o;
    }

    /// <summary>
    /// Die Komposition eines Graphen (<c>board.sicht.komposition[gr:…]</c>), dazu <c>flach</c>: alle Teilgraphen aufgefaltet auf eine Ebene, damit man
    /// den ganzen Graphen auf einmal sieht (docs/10 §10.9).
    /// </summary>
    public JsonObject Komposition(JsonObject g)
    {
        var k = KompositionEbene(g);
        k["flach"] = Flach(g);
        return k;
    }

    /// <summary>
    /// Die flache Komposition: jeder Teilgraph (ein Knoten, der ein Graph ist) wird durch seine Knoten ersetzt — mit Pfad (<c>Aufbereitung.Kontrast</c>),
    /// ihrem lokalen Namen und dem Graphen, dem sie gehören (eine Geste trifft dessen Record). Kanten an einer Schachtel enden an ihren Köpfen bzw.
    /// beginnen an den Knoten, die ihre Ausgänge geben; <c>rahmen</c> nennt je Teilgraph seinen Pfad, so zeichnet der Editor ihn als Rahmen. Die Tiefe
    /// folgt aus den flachen Kanten.
    /// </summary>
    private JsonObject Flach(JsonObject g)
    {
        var f = FlachEbene(g, "", new HashSet<string>(StringComparer.Ordinal) { S(g, "_id") });
        var knoten = Alle(f["knoten"]).ToList();
        var kanten = Alle(f["kanten"]).ToList();
        var tiefe = new Dictionary<string, int>(StringComparer.Ordinal);
        int Tiefe(string n, int rest)
        {
            if (tiefe.TryGetValue(n, out var t)) return t;
            var von = kanten.Where(e => S(e, "nach") == n).Select(e => S(e, "von")).Where(v => v != n).Distinct().ToList();
            t = von.Count == 0 || rest == 0 ? 0 : von.Max(v => Tiefe(v, rest - 1)) + 1;
            return tiefe[n] = t;
        }
        foreach (var kn in knoten) kn["tiefe"] = Tiefe(S(kn, "name"), knoten.Count);
        f["passt"] = Passt(knoten);
        return f;
    }

    /// <summary>
    /// Was im Graphen aneinander passt (für „＋“ an einem Port): je Paar von Knoten <c>von</c> → <c>nach</c> derselben Ebene, die noch nicht verbunden
    /// sind, der Eingang <c>ein</c> von <c>nach</c> und der Ausgang <c>aus</c> von <c>von</c>, der ihn speist — gleicher Typ oder eine generische Ausgabe
    /// mit dem Typargument des liefernden Knotens. Der Editor schlägt nur nach.
    /// </summary>
    private static JsonArray Passt(List<JsonObject> knoten)
    {
        static string Arg(string x) { var i = x.IndexOf('<'); return i > 0 && x.EndsWith('>') ? x[(i + 1)..^1] : ""; }
        var passt = new JsonArray();
        foreach (var z in knoten)
            foreach (var t in L(z, "ein"))
                foreach (var m in knoten)
                {
                    if (S(m, "name") == S(z, "name") || S(m, "graph") != S(z, "graph")) continue;
                    if (Alle(z["von"]).Any(v => S(v, "knoten") == (S(m, "lokal") is { Length: > 0 } l ? l : S(m, "name")))) continue;
                    var aus = L(m, "aus").FirstOrDefault(a => a == t || (Basis(a) == Basis(t) && Arg(a).Length > 0 && Arg(t) == Arg(S(m, "typ"))));
                    if (aus != null) passt.Add(new JsonObject { ["von"] = S(m, "name"), ["nach"] = S(z, "name"), ["ein"] = t, ["aus"] = aus });
                }
        return passt;
    }

    private JsonObject FlachEbene(JsonObject g, string pfad, HashSet<string> offen)
    {
        var gid = "gr:" + S(g, "_id");
        var k = KompositionEbene(g);
        var knoten = new JsonArray();
        var kanten = new JsonArray();
        var rahmen = new JsonArray();
        var innen = new Dictionary<string, JsonObject>(StringComparer.Ordinal);
        foreach (var kn in Alle(k["knoten"]))
        {
            var n = S(kn, "name");
            if (S(kn, "art") == "graph" && GraphNamens(Basis(S(kn, "typ"))) is { } ig && offen.Add(S(ig, "_id")))
            {
                var f = FlachEbene(ig, pfad + n + ".", offen);
                offen.Remove(S(ig, "_id"));
                innen[n] = f;
                rahmen.Add(new JsonObject { ["pfad"] = pfad + n, ["name"] = n, ["graph"] = S(ig, "name"), ["karte"] = "gr:" + S(ig, "_id"), ["in"] = gid });
                foreach (var x in Alle(f["rahmen"])) rahmen.Add(x.DeepClone());
                foreach (var x in Alle(f["knoten"])) knoten.Add(x.DeepClone());
                foreach (var x in Alle(f["kanten"])) kanten.Add(x.DeepClone());
                continue;
            }
            var o = (JsonObject)kn.DeepClone();
            o["name"] = pfad + n;
            o["lokal"] = n;
            o["graph"] = gid;
            o["rahmen"] = pfad.TrimEnd('.');
            knoten.Add(o);
        }
        // Wo eine Kante an einer Schachtel tatsächlich beginnt bzw. endet: an den Knoten ihrer Ausgänge bzw. Köpfe für diesen Typ.
        // Ein Ausgang der Schachtel kommt von seinen Knoten (sonst von dem inneren Knoten, der ihn ausgibt).
        IEnumerable<string> Quellen(string n, string typ)
        {
            if (!innen.TryGetValue(n, out var f)) return [pfad + n];
            var raus = Alle(f["ausgaenge"]).Where(a => Basis(S(a, "typ")) == Basis(typ)).SelectMany(a => L(a, "knoten")).Distinct().ToList();
            return raus.Count > 0 ? raus : Alle(f["knoten"]).Where(x => L(x, "aus").Any(a => Basis(a) == Basis(typ))).Select(x => S(x, "name")).ToList();
        }
        IEnumerable<string> Ziele(string n, string typ) => innen.TryGetValue(n, out var f)
            ? Alle(f["koepfe"]).Where(a => Basis(S(a, "typ")) == Basis(typ)).SelectMany(a => L(a, "knoten")).Distinct() : [pfad + n];
        foreach (var e in Alle(k["kanten"]))
            foreach (var v in Quellen(S(e, "von"), S(e, "typ")))
                foreach (var z in Ziele(S(e, "nach"), S(e, "typ")))
                    kanten.Add(new JsonObject
                    {
                        ["von"] = v, ["nach"] = z, ["typ"] = S(e, "typ"), ["verlierbar"] = B(e, "verlierbar"),
                        ["graph"] = gid, ["vonLokal"] = S(e, "von"), ["nachLokal"] = S(e, "nach"),
                    });
        var koepfe = new JsonArray(Alle(k["koepfe"]).Select(x => (JsonNode)new JsonObject
        {
            ["typ"] = S(x, "typ"), ["knoten"] = Arr(L(x, "knoten").SelectMany(n => Ziele(n, S(x, "typ"))).Distinct()),
        }).ToArray());
        var ausgaenge = new JsonArray(Alle(k["ausgaenge"]).Select(x => (JsonNode)new JsonObject
        {
            ["typ"] = S(x, "typ"), ["sorte"] = S(x, "sorte"), ["knoten"] = Arr(L(x, "knoten").SelectMany(n => Quellen(n, S(x, "typ"))).Distinct()),
        }).ToArray());
        return new JsonObject { ["knoten"] = knoten, ["kanten"] = kanten, ["koepfe"] = koepfe, ["ausgaenge"] = ausgaenge, ["rahmen"] = rahmen };
    }

    /// <summary>Die Komposition einer Ebene: die Knoten des Graphen, wie sie im Record stehen (ein Teilgraph als ein Knoten).</summary>
    private JsonObject KompositionEbene(JsonObject g)
    {
        var gid = "gr:" + S(g, "_id");
        var knoten = Alle(g["knoten"]).ToList();
        // Die Ports der Funktion bzw. des Graphen (eine Übersetzung ist kein Knoten: sie lebt in der Domäne).
        var ports = knoten.ToDictionary(k => S(k, "name"), k => KnotenPorts(S(k, "typ")), StringComparer.Ordinal);
        // Tiefe in der Kette: 0 für Köpfe und Quellen, sonst eins mehr als die tiefste Quelle in [Von] (kreisfrei, CQRS115; begrenzt zur Sicherheit).
        var tiefe = new Dictionary<string, int>(StringComparer.Ordinal);
        int Tiefe(string n, int rest)
        {
            if (tiefe.TryGetValue(n, out var t)) return t;
            var kn = knoten.FirstOrDefault(k => S(k, "name") == n);
            var von = kn == null ? new List<string>() : L(kn, "von").Where(ports.ContainsKey).ToList();
            t = von.Count == 0 || rest == 0 ? 0 : von.Max(v => Tiefe(v, rest - 1)) + 1;
            return tiefe[n] = t;
        }
        // Die Kanten je [Von]: was die genannte Quelle ausgibt und der Knoten nimmt — gleicher Typ; eine generische Ausgabe (Geglaettet<TFenster>)
        //   mit dem Typargument ihres Knotens (Glaettung<Kurzfenster> → Geglaettet<Kurzfenster>).
        static string Args(string t) => t.IndexOf('<') is > 0 and var i && t.EndsWith('>') ? t[(i + 1)..^1] : "";
        var typVon = knoten.ToDictionary(k => S(k, "name"), k => S(k, "typ"), StringComparer.Ordinal);
        var kanten = new JsonArray();
        var innen = new HashSet<string>(StringComparer.Ordinal);
        foreach (var kn in knoten)
        {
            var n = S(kn, "name");
            foreach (var v in L(kn, "von").Where(ports.ContainsKey))
                foreach (var a in ports[v].Aus)
                    foreach (var t in ports[n].Ein.Where(e => e == a || (Basis(e) == Basis(a) && Args(a).Length > 0 && Args(e) == Args(typVon[v]))))
                    {
                        innen.Add(v + "|" + Basis(t));
                        kanten.Add(new JsonObject { ["von"] = v, ["nach"] = n, ["typ"] = t, ["verlierbar"] = L(kn, "verlierbar").Contains(v) });
                    }
        }
        var koepfe = new JsonArray();
        foreach (var t in L(g, "koepfe"))
            koepfe.Add(new JsonObject
            {
                ["typ"] = t,
                ["knoten"] = Arr(knoten.Where(k => B(k, "kopf") && ports[S(k, "name")].Ein.Contains(t)).Select(k => S(k, "name"))),
            });
        var ausgaenge = new JsonArray();
        foreach (var t in L(g, "ausgaenge"))
            ausgaenge.Add(new JsonObject
            {
                ["typ"] = t, ["sorte"] = Sorte(t),
                ["knoten"] = Arr(knoten.Where(k => ports[S(k, "name")].Aus.Any(a => Basis(a) == Basis(t)) && !innen.Contains(S(k, "name") + "|" + Basis(t)))
                    .Select(k => S(k, "name"))),
            });
        var kj = new JsonArray();
        foreach (var kn in knoten)
        {
            var n = S(kn, "name");
            var (ein, aus, art, karte) = ports[n];
            var o = new JsonObject
            {
                ["name"] = n, ["typ"] = S(kn, "typ"), ["art"] = art, ["kopf"] = B(kn, "kopf"), ["quelle"] = Meldet(S(kn, "typ")).Count > 0,
                ["tiefe"] = Tiefe(n, knoten.Count),
                ["von"] = new JsonArray(L(kn, "von").Select(v => (JsonNode)new JsonObject { ["knoten"] = v, ["verlierbar"] = L(kn, "verlierbar").Contains(v) }).ToArray()),
                ["ein"] = Arr(ein), ["aus"] = Arr(aus),
            };
            if (karte != null) o["karte"] = karte;
            if (BausteinFunktion(Basis(S(kn, "typ"))) is { } f)
            {
                o["konstruktor"] = new JsonArray(BausteinKonstruktor(f).GroupBy(p => S(p, "typ")).Select(x => (JsonNode)KonstruktorChip(f, x.First())).ToArray());
                // Die Lese-Fähigkeiten ihrer Auf (Parameter, nicht Konstruktor): was sie aus einem Store liest.
                if (Alle(f["eingaenge"]).SelectMany(e => Alle(e["faehigkeiten"])).Select(p => S(p, "typ")).Distinct().ToList() is { Count: > 0 } liest)
                    o["liest"] = Arr(liest);
            }
            kj.Add(o);
        }
        return new JsonObject
        {
            ["name"] = S(g, "name"),
            ["eltern"] = Arr(GraphenMitKnoten(S(g, "name")).OfType<string>().Where(x => x != gid)),
            ["knoten"] = kj, ["kanten"] = kanten, ["koepfe"] = koepfe, ["ausgaenge"] = ausgaenge,
            // Offen: ein Command ohne Ziel, den der Graph ausgibt und den keine Übersetzung nimmt — er braucht einen Empfänger (CQRS120).
            ["offen"] = Arr(L(g, "ausgaenge").Where(t => Sorte(t) == "command" && RecordAgg(Basis(t)).Length == 0
                && !_uebersetzungen.Any(u => Basis(S(u, "ein")) == Basis(t)))),
            // Die Quellen aus dem Katalog des Frameworks, deren Meldung ein Kopf des Graphen nimmt.
            ["quellen"] = new JsonArray(Alle(_board["katalog"]).Where(k => L(k, "lauscht").Any(m => L(g, "koepfe").Any(t => Basis(t) == Basis(m))))
                .Select(k => (JsonNode)new JsonObject { ["name"] = S(k, "name"), ["meldet"] = Arr(L(k, "lauscht")) }).ToArray()),
        };
    }

    /// <summary>
    /// Der Baukasten der Anwendung (<c>board.sicht.baukasten</c>): Funktionen (mit Verwendung in Graphen, den Typargumenten generischer und ihrem
    /// Katalog — <c>null</c> = in der Anwendung), die Kataloge, die die Anwendung nutzt, Graphen, der Katalog des Frameworks (Quellen), Technik (mit
    /// Umsetzungen und Nutzern), Konfig-Records und die Nachrichten, die Funktionen ausgeben (Commands ohne Ziel und Hinweise, deren Besitzer eine
    /// Funktion ist).
    /// </summary>
    public JsonObject Baukasten()
    {
        IEnumerable<string> Verwendet(string name) => GraphenMitKnoten(name).OfType<string>();
        IEnumerable<string> Typargumente(string name) => _graphen.SelectMany(g => Alle(g["knoten"])).Select(k => S(k, "typ"))
            .Where(t => Basis(t) == name && t.Length > name.Length).Select(t => t[(name.Length + 1)..^1]).Distinct();
        var funktionen = new JsonArray(_bausteinFunktionen.Where(f => !IstGerufen(f)).Select(f =>
        {
            var o = new JsonObject
            {
                ["karte"] = "fb:" + S(f, "_id"), ["name"] = S(f, "name"), ["quelle"] = L(f, "lauscht").Any(),
                ["verwendet"] = Arr(Verwendet(S(f, "name"))), ["katalog"] = Abstractions.Projektstruktur.KatalogVon(S(f, "namespace")),
            };
            if (Typargumente(S(f, "name")).ToList() is { Count: > 0 } ta) o["typargumente"] = Arr(ta);
            return (JsonNode)o;
        }).ToArray());
        var graphen = new JsonArray(_graphen.Select(g => (JsonNode)new JsonObject
        {
            ["karte"] = "gr:" + S(g, "_id"), ["name"] = S(g, "name"), ["verwendet"] = Arr(Verwendet(S(g, "name")).Where(x => x != "gr:" + S(g, "_id"))),
        }).ToArray());
        var katalog = new JsonArray(Alle(_board["katalog"]).Select(k => (JsonNode)new JsonObject
        {
            ["name"] = S(k, "name"), ["meldet"] = Arr(L(k, "lauscht")), ["konfig"] = Arr(Alle(k["konfigs"]).Select(c => S(c, "record"))),
        }).ToArray());
        var technik = new JsonArray(_bausteinFunktionen.Where(IstGerufen).Select(t => (JsonNode)new JsonObject
        {
            ["karte"] = "fb:" + S(t, "_id"), ["name"] = S(t, "name"),
            ["umsetzungen"] = Arr(Alle(t["umsetzungen"]).Select(u => S(u, "name"))),
            ["genutztVon"] = Arr(GerufenNutzer(t).Select(f => "fb:" + S(f, "_id"))),
        }).ToArray());
        var konfig = new JsonArray(_bausteinFunktionen.SelectMany(f => Alle(f["konfigs"]).Select(c => (Record: S(c, "record"), Felder: c["felder"], F: f)))
            .GroupBy(x => x.Record, StringComparer.Ordinal).Select(x => (JsonNode)new JsonObject
            {
                ["name"] = x.Key, ["felder"] = Arr(Alle(x.First().Felder).Select(fe => S(fe, "feld"))),
                ["genutztVon"] = Arr(x.Select(y => "fb:" + S(y.F, "_id")).Distinct()),
            }).ToArray());
        var nachrichten = new JsonArray();
        foreach (var r in _records.Where(r => S(r, "kind") is "command" or "rejection"))
            if (KarteVon.TryGetValue("rec:" + S(r, "name"), out var k) && Besitzer(k) is var b && b.StartsWith("fb:", StringComparison.Ordinal))
                nachrichten.Add(new JsonObject { ["typ"] = S(r, "name"), ["sorte"] = Sorte(S(r, "name")), ["besitzer"] = b });
        var kataloge = Arr(KatalogNamen());
        return new JsonObject
        {
            ["funktionen"] = funktionen, ["kataloge"] = kataloge, ["graphen"] = graphen, ["katalog"] = katalog, ["technik"] = technik, ["konfig"] = konfig,
            ["nachrichten"] = nachrichten,
        };
    }

    private JsonObject KompositionJson()
    {
        var o = new JsonObject();
        foreach (var g in _graphen) o["gr:" + S(g, "_id")] = Komposition(g);
        return o;
    }
}
