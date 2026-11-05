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

    /// <summary>Die Komposition eines Graphen (<c>board.sicht.komposition[gr:…]</c>).</summary>
    public JsonObject Komposition(JsonObject g)
    {
        var gid = "gr:" + S(g, "_id");
        var knoten = Alle(g["knoten"]).ToList();
        // Eine Übersetzung nimmt TEin und gibt TAus (aus ihrer Zuordnung, die der Server rechnet); sonst die Ports der Funktion bzw. des Graphen.
        var uebersetzungen = Alle(g["uebersetzungen"]).ToDictionary(u => S(u, "knoten"), u => u, StringComparer.Ordinal);
        var ports = knoten.ToDictionary(k => S(k, "name"), k => uebersetzungen.TryGetValue(S(k, "name"), out var u)
            ? (Ein: new List<string> { S(u, "ein") }, Aus: new List<string> { S(u, "aus") }, Art: "uebersetzung", Karte: (string?)null)
            : KnotenPorts(S(k, "typ")), StringComparer.Ordinal);
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
            if (uebersetzungen.TryGetValue(n, out var ue))
            {
                o["uebersetzung"] = ue.DeepClone();
                o["felder"] = (kn["felder"] as JsonArray ?? new JsonArray()).DeepClone();
            }
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
        // Die Kataloge der Anwendung: die Projekt-Wurzeln (Verzeichnisse), die ein Katalog sind — auch ein leerer, eben angelegter.
        var kataloge = Arr(Eintraege(_board["rahmen"]?["verzeichnisse"] as JsonObject).Select(e => e.Key)
            .Where(ns => Abstractions.Projektstruktur.KatalogVon(ns) == ns).OrderBy(x => x, StringComparer.Ordinal));
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
