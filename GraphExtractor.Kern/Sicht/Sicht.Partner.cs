using System.Text.Json.Nodes;

namespace GraphExtractor;

// Die zwei Seiten einer Nachricht (◀ kommt aus · geht an ▶) und die Fluss-Sicht (Ports, Vorgänger, Tiefe, Zuweisung, Slice) — früher im
// Editor-JS (partnerVon, flussPartner, akteurePartner, flPorts, flVor, flHoert, flDrahtTyp, flZuweisung, flTiefen, flussSlice; Werkbank
// P7.4.5). Dieselben Regeln, dieselbe Reihenfolge. Das ✕ an einer Partner-Zeile bleibt eine Geste im Editor: der Server beschreibt nur, WAS
// sie löst (`loesen`), der Editor führt es an der Karte aus.
public sealed partial class Sicht
{
    // ══ PARTNER: je Nachricht die Erzeuger (ein) und Konsumenten (aus) — die umgekehrte Sicht auf die Signaturen der Bausteine ══

    /// <summary>Eine Partner-Zeile: Karte (null = Außenwelt), Text, Marke und optional, was ✕ löst.</summary>
    public sealed record Partner(string? Id, string Text, string Marke, JsonObject? Loesen);

    private static readonly string[] PartnerArten = ["command", "event", "rejection", "query", "queryresponse"];

    /// <summary>Was ✕ löst: <paramref name="wert"/> aus der Liste <paramref name="feld"/> der Karte.</summary>
    private static JsonObject AusListe(string karte, string feld, string wert) => new() { ["karte"] = karte, ["feld"] = feld, ["wert"] = wert };

    public (List<Partner> Ein, List<Partner> Aus) PartnerVon(string nm)
    {
        var ein = new List<Partner>();
        var aus = new List<Partner>();
        void P(List<Partner> l, string? id, string text, string mark = "", JsonObject? los = null) => l.Add(new(id, text, mark, los));
        var r = RecByName(nm);
        var k = r != null ? S(r, "kind") : "";
        if (nm.Length == 0) return (ein, aus);
        void HD(string art, Action<JsonObject, JsonObject, string> f)
        {
            var (_, sammlung, pre) = HandleArten.First(x => x.Art == art);
            foreach (var o in SammlungVon(sammlung))
            {
                var ids = HandleIds(o, pre + ":" + S(o, "_id"));
                foreach (var (hd, hi) in Alle(o["handles"]).Select((h, i) => (h, i))) f(o, hd, ids[hi]);
            }
        }
        static string HText(JsonObject o, JsonObject hd) => S(o, "name") + ".Handle(" + HandleDisc(hd) + ")";
        string Prozess(JsonObject t) => S(t, "prozess") is { Length: > 0 } p ? p : "Prozess";
        string Agg(JsonObject d, string leer) => S(d, "aggregat") is { Length: > 0 } a ? a : leer;

        if (k == "command")
        {
            foreach (var t in _transitions)
                foreach (var d in Alle(t["dann"]))
                {
                    if (S(d, "sende") == nm) P(ein, "tr:" + S(t, "_id"), "Regel · " + Prozess(t));
                    if (S(d, "kompensation") == nm) P(ein, "tr:" + S(t, "_id"), "Regel · " + Prozess(t), "↩ Kompensation");
                }
            HD("reaktion", (o, hd, id) => { if (L(hd, "sends").Contains(nm)) P(ein, id, HText(o, hd), "", AusListe(id, "sends", nm)); });
            FlussPartner(nm, ein, aus);
            AkteurePartner(ein, nm);
            foreach (var (a, z, _, id) in VertragsZusagen())
                if (L(z, "ausgaenge").Contains(nm))
                    P(ein, id, S(a, "name") + " · Zusage auf " + (S(z, "eingang") is { Length: > 0 } e ? e : "?"), "Vertrag", AusListe(id, "ausgaenge", nm));
            if (ein.Count == 0) P(ein, null, "Außenwelt (Client)", "abgeleitet: kein interner Erzeuger · kein Akteur");
            foreach (var d in _decider) if (S(d, "command") == nm) P(aus, "dec:" + S(d, "_id"), "Decide @ " + Agg(d, "— kein Aggregat"));
        }
        else if (k is "event" or "rejection")
        {
            foreach (var d in _decider)
                if (Alle(d["ergibt"]).Any(o => S(o, "event") == nm))
                    P(ein, "dec:" + S(d, "_id"), "Decide(" + (S(d, "command") is { Length: > 0 } c ? c : "?") + ") @ " + Agg(d, "—"));
            foreach (var f in _funktionen)
                if (L(f, "ergebnisse").Contains(nm)) P(ein, "fk:" + S(f, "_id"), "ƒ " + S(f, "name"), "Ergebnis", AusListe("fk:" + S(f, "_id"), "ergebnisse", nm));
            FlussPartner(nm, ein, aus);
            foreach (var art in new[] { "projektion", "reaktion" })
                HD(art, (o, hd, id) =>
                {
                    if (L(hd, "publishes").Contains(nm)) P(ein, id, HText(o, hd), "veröffentlicht", AusListe(id, "publishes", nm));
                    if (S(hd, "event") == nm) P(aus, id, HText(o, hd));
                });
            foreach (var a in _applier) if (S(a, "event") == nm) P(aus, "app:" + S(a, "_id"), "Apply @ " + Agg(a, "—"));
            foreach (var sg in _sagas) if (S(sg, "triggerEvent") == nm) P(aus, "saga:" + S(sg, "name"), "Prozess " + S(sg, "name"), "Auslöser");
            foreach (var t in _transitions)
                if (L(t, "wenn").Contains(nm) || S(t, "sammelEvent") == nm)
                    P(aus, "tr:" + S(t, "_id"), "Regel · " + Prozess(t), S(t, "sammelEvent") == nm ? "Σ sammelt" : "");
            foreach (var (a, z, _, id) in VertragsZusagen())
            {
                if (S(z, "eingang") != nm) continue;
                var ausg = L(z, "ausgaenge").ToList();
                var teil = S(z, "teil") is { Length: > 0 } tl ? tl : VertragsTyp(a);
                var cs = _clients.Where(c => L(c, "traegt").Contains(teil)).Select(ClientAnzeige).ToList();
                P(aus, id, S(a, "name") + " · Zusage",
                    (ausg.Count > 0 ? "Vertrag → " + string.Join(", ", ausg) : "Vertrag · zur Kenntnis") + (cs.Count > 0 ? " · 🔌 " + string.Join(", ", cs) : ""),
                    new JsonObject { ["karte"] = id, ["zusage"] = true });
            }
            foreach (var c in _clients)
                if (L(c, "kenntnis").Contains(nm))
                    P(aus, "cl:" + S(c, "_id"), "Client " + ClientAnzeige(c), "Kenntnis", AusListe("cl:" + S(c, "_id"), "kenntnis", nm));
        }
        else if (k == "query")
        {
            AkteurePartner(ein, nm);
            if (ein.Count == 0) P(ein, null, "Außenwelt (Client)", "abgeleitet · kein Akteur");
            HD("reader", (o, hd, id) => { if (S(hd, "query") == nm) P(aus, id, HText(o, hd)); });
        }
        else if (k == "queryresponse")
        {
            var qs = new List<string>();
            HD("reader", (o, hd, id) => { if (L(hd, "responses").Contains(nm)) { P(ein, id, HText(o, hd)); qs.Add(S(hd, "query")); } });
            // Verschachtelte Antwort (Element einer Listen-Antwort): sie kommt als Teil ihres Trägers heraus.
            if (ein.Count == 0)
                foreach (var (owner, f) in AlleFelder())
                    if (InnerTyp(S(f, "typ")) == nm && IstRec(owner)) P(ein, "rec:" + owner, "Teil von " + owner, "Feld " + S(f, "name"));
            // Die Antwort geht an den, der fragen darf (Akteure der Query) — sonst an die anonyme Außenwelt.
            foreach (var a in _akteure) if (qs.Any(q => L(a, "darf").Contains(q))) P(aus, "akt:" + S(a, "_id"), "Akteur " + S(a, "name"), "fragt");
            if (aus.Count == 0) P(aus, null, "Außenwelt (Client)", "abgeleitet · kein Akteur");
        }
        return (ein, aus);

        void FlussPartner(string n, List<Partner> e, List<Partner> a)
        {
            foreach (var f in _fluesse)
                foreach (var kn in Alle(f["knoten"]))
                {
                    var id = "fs:" + S(kn, "_id");
                    var txt = FlSym.GetValueOrDefault(S(kn, "art"), "") + " " + S(kn, "name") + " · ⛓ " + S(f, "name");
                    if (S(kn, "art") is "command" or "quelle" && S(kn, "typ") == n) P(e, id, txt, S(kn, "art") == "command" ? "sendet" : "meldet");
                    if (FlHoert(kn).Contains(n)) P(a, id, txt, S(kn, "art") == "warte" ? "wartet darauf" : "Auf");
                    foreach (var (ei, ein2) in Alle(kn["eingaenge"]).Select((x, i) => (i, x)))
                    {
                        var ds = Alle(ein2["draehte"]).ToList();
                        foreach (var (di, d) in ds.Select((x, i) => (i, x)))
                        {
                            if ((S(d, "port") is { Length: > 0 } p ? p : "fall") != "fall" || FlDrahtTyp(f, d) != n) continue;
                            P(a, id, txt, "← " + S(d, "von") + (ds.Count > 1 ? " ∧" : ""),
                                new JsonObject { ["karte"] = id, ["eingang"] = ei, ["draht"] = di });
                        }
                    }
                }
        }
        // Akteure, die diesen Typ hineingeben dürfen (◀ kommt aus; ✕ entzieht das Recht), und Clients, deren Vertrag ihn sendet bzw. fragt.
        void AkteurePartner(List<Partner> e, string n)
        {
            foreach (var a in _akteure)
                if (L(a, "darf").Contains(n)) P(e, "akt:" + S(a, "_id"), "Akteur " + S(a, "name"), "darf", AusListe("akt:" + S(a, "_id"), "darf", n));
            foreach (var c in _clients)
            {
                var id = "cl:" + S(c, "_id");
                if (L(c, "sendet").Contains(n)) P(e, id, "Client " + ClientAnzeige(c), "sendet", AusListe(id, "sendet", n));
                if (L(c, "fragt").Contains(n)) P(e, id, "Client " + ClientAnzeige(c), "fragt", AusListe(id, "fragt", n));
            }
        }
    }

    private JsonObject PartnerJson()
    {
        var o = new JsonObject();
        foreach (var r in _records)
        {
            var nm = S(r, "name");
            if (!PartnerArten.Contains(S(r, "kind")) || o.ContainsKey(nm) || RecByName(nm) != r) continue;
            var (ein, aus) = PartnerVon(nm);
            o[nm] = new JsonObject { ["ein"] = Zeilen(ein), ["aus"] = Zeilen(aus) };
        }
        return o;
        static JsonArray Zeilen(List<Partner> l) => new(l.Select(p =>
        {
            var z = new JsonObject { ["id"] = p.Id, ["text"] = p.Text, ["mark"] = p.Marke };
            if (p.Loesen != null) z["loesen"] = p.Loesen;
            return (JsonNode?)z;
        }).ToArray());
    }

    // ══ FLUSS: je Knoten Ports (Fälle + ⏳/✕, mit der Nachricht des Ports), Vorgänger, Tiefe, gehörte Events, Zuweisung; je Fluss der Slice ══

    public sealed record FlussPort(string? Fall, string Port, string Label, string? Typ);

    private List<string> FlDecideEvents(JsonObject k) =>
        _decider.Where(d => S(d, "command") == S(k, "typ")).SelectMany(d => Alle(d["ergibt"]).Select(o => S(o, "event"))).Distinct(StringComparer.Ordinal).ToList();
    private bool IstAblehnung(string e) => RecByName(e) is { } r && S(r, "kind") == "rejection";

    /// <summary>Die Ausgänge eines Knotens: die Fälle (Ergebnisse der Funktion, Events aus dem Decide des Commands, die Quell-Nachricht und ihr Strom).</summary>
    private List<FlussPort> FlAusgaenge(JsonObject k)
    {
        var art = S(k, "art");
        if (FlQuelleArt(k))
            return [new(null, "fall", S(k, "typ") is { Length: > 0 } t ? t : "Nachricht", S(k, "typ")), new(null, "strom", "⇢ Strom (Id, Version)", "QuellStrom")];
        if (art == "warte") return FlWarteAuf(k).Select(e => new FlussPort(e, "fall", e, e)).ToList();
        if (art == "funktion")
            return (_funktionen.FirstOrDefault(x => S(x, "name") == S(k, "typ")) is { } f ? L(f, "ergebnisse") : []).Select(e => new FlussPort(e, "fall", e, e)).ToList();
        // Ablehnungen sind KEIN Fall-Port: das Aggregat schreibt dann die Ablehnungs-Marke — sie kommt am ✕-Port an.
        if (art == "command") return FlDecideEvents(k).Where(e => !IstAblehnung(e)).Select(e => new FlussPort(e, "fall", e, e)).ToList();
        return [];
    }
    public List<FlussPort> FlPorts(JsonObject k)
    {
        var p = FlAusgaenge(k);
        var art = S(k, "art");
        if (art is "funktion" or "command")
        {
            if (Wahr(k["zeitlimit"])) p.Add(new(null, "zeitlimit", "⏳ Zeitlimit abgelaufen", "ZeitlimitAbgelaufen"));
            var abl = art == "command" ? FlDecideEvents(k).Where(IstAblehnung).ToList() : [];
            p.Add(new(null, "abgelehnt", art == "command" ? "✕ abgelehnt" + (abl.Count > 0 ? " (" + string.Join(", ", abl) + ")" : "") : "✕ gescheitert", "SchrittAbgelehnt"));
        }
        if (art == "warte" && Wahr(k["zeitlimit"])) p.Add(new(null, "zeitlimit", "⏳ Zeitlimit abgelaufen (kein Event)", "ZeitlimitAbgelaufen"));
        return p;
    }
    /// <summary>Welche Nachricht ein Draht trägt.</summary>
    internal static string? FlDrahtTyp(JsonObject f, JsonObject d)
    {
        var v = FlKnoten(f, S(d, "von"));
        if (v == null) return null;
        switch (S(d, "port"))
        {
            case "zeitlimit": return "ZeitlimitAbgelaufen";
            case "abgelehnt": return "SchrittAbgelehnt";
            case "strom": return "QuellStrom";
        }
        return FlQuelleArt(v) ? S(v, "typ") : d["fall"] is JsonValue fv && fv.TryGetValue<string>(out var fall) ? fall : null;
    }
    /// <summary>Die Tiefe je Knoten (Spalte im Fluss-Rahmen): in Deklarations-Reihenfolge, 0 ohne Vorgänger, sonst 1 + die tiefste Quelle.</summary>
    private static Dictionary<string, int> FlTiefen(JsonObject f)
    {
        var t = new Dictionary<string, int>(StringComparer.Ordinal);
        foreach (var k in Alle(f["knoten"]))
        {
            var v = FlVor(k);
            t[S(k, "name")] = v.Count > 0 ? 1 + v.Max(x => t.GetValueOrDefault(x, 0)) : 0;
        }
        return t;
    }
    /// <summary>◎ am Fluss-Rahmen: seine Knoten, ihre Zuweisungen und die Events, auf die sie hören.</summary>
    public List<string> FlussSlice(JsonObject f)
    {
        var ids = new List<string>();
        void Dazu(string id) { if (!ids.Contains(id)) ids.Add(id); }
        foreach (var k in Alle(f["knoten"]))
        {
            Dazu("fs:" + S(k, "_id"));
            if (FlZuweisung(k) is { } zw) Dazu(zw);
            foreach (var nm in FlHoert(k)) Dazu("rec:" + nm);
        }
        return ids;
    }

    private JsonObject FlussJson(JsonObject f)
    {
        var tiefe = FlTiefen(f);
        var knoten = new JsonObject();
        foreach (var k in Alle(f["knoten"]))
        {
            var o = new JsonObject
            {
                ["ports"] = new JsonArray(FlPorts(k).Select(p => (JsonNode?)new JsonObject
                {
                    ["fall"] = p.Fall, ["port"] = p.Port, ["label"] = p.Label, ["typ"] = p.Typ,
                }).ToArray()),
                ["vor"] = Arr(FlVor(k)),
                ["tiefe"] = tiefe[S(k, "name")],
                ["hoert"] = Arr(FlHoert(k)),
                ["zuweisung"] = FlZuweisung(k),
                ["draehte"] = new JsonArray(Alle(k["eingaenge"]).Select(e => (JsonNode?)new JsonArray(
                    Alle(e["draehte"]).Select(d => (JsonNode?)FlDrahtTyp(f, d)).ToArray())).ToArray()),
            };
            knoten["fs:" + S(k, "_id")] = o;
        }
        return new JsonObject { ["heim"] = FlussHeim(f), ["slice"] = Arr(FlussSlice(f)), ["knoten"] = knoten };
    }
}
