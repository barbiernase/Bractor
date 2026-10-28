using System.Text.Json.Nodes;

namespace GraphExtractor;

// Die zwei Seiten einer Nachricht (◀ kommt aus · geht an ▶) — früher im Editor-JS (partnerVon, akteurePartner; Werkbank P7.4.5). Dieselben
// Regeln, dieselbe Reihenfolge. Das ✕ an einer Partner-Zeile bleibt eine Geste im Editor: der Server beschreibt nur, WAS sie löst (`loesen`),
// der Editor führt es an der Karte aus.
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
        string Agg(JsonObject d, string leer) => S(d, "aggregat") is { Length: > 0 } a ? a : leer;

        if (k == "command")
        {
            FunktionsPartner(nm, ein, aus);
            AkteurePartner(ein, nm);
            if (ein.Count == 0) P(ein, null, "Außenwelt (Client)", "abgeleitet: kein interner Erzeuger · kein Akteur");
            foreach (var d in _decider) if (S(d, "command") == nm) P(aus, "dec:" + S(d, "_id"), "Decide @ " + Agg(d, "— kein Aggregat"));
        }
        else if (k is "event" or "rejection")
        {
            foreach (var d in _decider)
                if (Alle(d["ergibt"]).Any(o => S(o, "event") == nm))
                    P(ein, "dec:" + S(d, "_id"), "Decide(" + (S(d, "command") is { Length: > 0 } c ? c : "?") + ") @ " + Agg(d, "—"));
            FunktionsPartner(nm, ein, aus);
            HD("projektion", (o, hd, id) =>
            {
                if (S(hd, "event") == nm) P(aus, id, HText(o, hd));
            });
            foreach (var a in _applier) if (S(a, "event") == nm) P(aus, "app:" + S(a, "_id"), "Apply @ " + Agg(a, "—"));
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

        // Funktionen: ein Auf, das die Nachricht ausgibt (◀ kommt aus), bzw. sie als Eingang nimmt (geht an ▶) — nur aus der Signatur.
        void FunktionsPartner(string n, List<Partner> e, List<Partner> a)
        {
            foreach (var f in _bausteinFunktionen)
                foreach (var ein2 in Alle(f["eingaenge"]))
                {
                    var txt = "ƒ " + S(f, "name") + ".Auf(" + S(ein2, "eingang") + ")";
                    if (L(ein2, "ausgaben").Contains(n)) P(e, "fb:" + S(f, "_id"), txt, B(ein2, "strom") ? "Strom" : "Ausgabe");
                    if (S(ein2, "eingang") == n) P(a, "fb:" + S(f, "_id"), txt, "Auf");
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
}
