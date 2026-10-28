using System.Text.Json.Nodes;

namespace GraphExtractor;

// Was der Editor bekommt: board.sicht (je Karte), die statische Kette einer Karte (Fokus), die Anschlussleiste der Clients.
public sealed partial class Sicht
{
    /// <summary>
    /// <c>board.sicht</c>: je Karte <c>{art, domaene?, block, rolle, akteure, akteurRahmen, insel?, vertreter?, besitzer?, storeScope?, aggregat?}</c>,
    /// dazu Kanten, zusammengezogene Kanten der kompakten Ansicht, die Akteur-Reihenfolge je Domäne, die Anschlussleisten der Clients und je
    /// Nachricht ihre zwei Seiten (<c>partner</c>: ◀ kommt aus · geht an ▶). <c>domaene</c> fehlt, wenn die Karte keine hat (die Ansicht setzt
    /// dann ggf. ihre Heimat).
    /// </summary>
    public JsonObject AlsJson()
    {
        var karten = new JsonObject();
        foreach (var n in Karten)
        {
            var k = new JsonObject { ["art"] = n.Art };
            var d = Domaene(n);
            if (d != OhneDomaene) k["domaene"] = d;
            k["block"] = Block(n);
            k["rolle"] = Rolle(n);
            k["akteure"] = Arr(Akteure(n).OrderBy(x => x, StringComparer.Ordinal));
            k["akteurRahmen"] = AkteurRahmen(n);
            if (Inseln.Contains(n.Id)) k["insel"] = true;
            var v = Vertreter(n.Id);
            if (!(v.Count == 1 && v[0] == n.Id)) k["vertreter"] = Arr(v);
            if (DetailArten.Contains(n.Art) && DirekteBesitzer(n) is { Count: > 0 } bs) k["besitzer"] = Arr(bs);
            if (n.Art is "projektion" or "reader") k["storeScope"] = Arr(StoreScope(n.Ref));
            if (n.Art is "command" or "event" or "rejection" or "query" or "queryresponse" && RecordAgg(n.Name) is { Length: > 0 } agg) k["aggregat"] = agg;
            karten[n.Id] = k;
        }
        var domaenen = Karten.Select(Domaene).Distinct().ToList();
        var ordnung = new JsonObject();
        foreach (var dom in domaenen.OrderBy(x => x, StringComparer.Ordinal))
            if (AkteurOrdnung(dom) is { Count: > 0 } o) ordnung[dom] = Arr(o);
        var clients = new JsonObject();
        foreach (var c in _clients) clients["cl:" + S(c, "_id")] = Client(c);
        // Je Akteur: die Domänen, in denen er wirkt; je Nachricht und Ketten-Akteur der Pfad, auf dem sie in seinem Namen entsteht.
        var wirkt = new JsonObject();
        foreach (var a in _akteure) wirkt[S(a, "name")] = Arr(AkteurDomaenen(S(a, "name")));
        var pfade = new JsonObject();
        foreach (var (nm, m) in _mengen.OrderBy(x => x.Key, StringComparer.Ordinal))
        {
            var kette = m.Kette.Where(a => !m.Direkt.Contains(a)).OrderBy(a => a, StringComparer.Ordinal).ToList();
            if (kette.Count == 0) continue;
            var je = new JsonObject();
            foreach (var a in kette) je[a] = Arr(KettenPfad(nm, a));
            pfade[nm] = je;
        }
        return new JsonObject
        {
            ["karten"] = karten,
            ["kanten"] = Paare(Kanten),
            ["kontrakt"] = Paare(Kontrakt()),
            ["akteurOrdnung"] = ordnung,
            ["clients"] = clients,
            ["akteurDomaenen"] = wirkt,
            ["pfade"] = pfade,
            ["partner"] = PartnerJson(),
        };
    }
    private static JsonArray Arr(IEnumerable<string> xs) => new(xs.Select(x => (JsonNode?)x).ToArray());
    private static JsonArray Paare(IEnumerable<(string, string)> ps) => new(ps.Select(p => (JsonNode?)Arr([p.Item1, p.Item2])).ToArray());

    // ══ CLIENTS: die Anschlussleiste (= ihr Vertrag) ══
    private List<string> DarfHalter(string nm) => _akteure.Where(a => L(a, "darf").Contains(nm)).Select(a => S(a, "name")).ToList();
    /// <summary>Verkörpert — dieselbe Regel wie der AkteurRechteGenerator (<c>Abstractions.Clientvertragsregeln.Verkoerpert</c>).</summary>
    public List<string> ClientAkteure(JsonObject c) =>
        Abstractions.Clientvertragsregeln.Verkoerpert(L(c, "traegt").Concat(L(c, "sendet")).Concat(L(c, "fragt")).Select(DarfHalter));
    /// <summary>Die Ports der Anschlussleiste: Richtung, Nachricht bzw. getragene Funktion, in wessen Namen (Akteur), Ziel-Karte.</summary>
    public JsonArray ClientPorts(JsonObject c)
    {
        var p = new JsonArray();
        var ver = ClientAkteure(c);
        string? Node(string nm) => IstRec(nm) ? "rec:" + nm : null;
        List<string> Als(string nm) { var hs = DarfHalter(nm); var drin = hs.Where(ver.Contains).ToList(); return drin.Count > 0 ? drin : hs; }
        foreach (var t in L(c, "traegt"))
            p.Add(new JsonObject
            {
                ["ri"] = "traegt", ["msg"] = t, ["akteure"] = Arr(Als(t)),
                ["node"] = BausteinFunktion(t) is { } f ? "fb:" + S(f, "_id") : null,
            });
        foreach (var nm in L(c, "sendet")) p.Add(new JsonObject { ["ri"] = "sendet", ["msg"] = nm, ["akteure"] = Arr(Als(nm)), ["node"] = Node(nm) });
        foreach (var nm in L(c, "fragt")) p.Add(new JsonObject { ["ri"] = "fragt", ["msg"] = nm, ["akteure"] = Arr(Als(nm)), ["node"] = Node(nm) });
        foreach (var nm in L(c, "kenntnis")) p.Add(new JsonObject { ["ri"] = "kenntnis", ["msg"] = nm, ["akteure"] = new JsonArray(), ["node"] = Node(nm) });
        return p;
    }
    private JsonObject Client(JsonObject c) => new() { ["akteure"] = Arr(ClientAkteure(c)), ["ports"] = ClientPorts(c) };

    // ══ DIE STATISCHE KETTE einer Karte (der eine Fokus, P6.4) ══
    private static readonly HashSet<string> Stop = ["akteur", "aggregate", "command", "valueobject", "enum", "konfig"];
    private static readonly HashSet<string> LeseStart = ["projektion", "reader", "query", "store", "readmodel", "queryresponse"];
    private static readonly HashSet<string> KetteHub = ["aggregate", "projektion", "reader", "store"];
    private static readonly HashSet<string> KetteFluss =
        ["command", "event", "rejection", "query", "queryresponse", "handle", "decider", "applier", "bausteinfunktion", "fn", .. KetteHub];
    private static readonly HashSet<string> KetteStart = ["command", "event", "rejection", "handle", "decider", "applier", "bausteinfunktion"];
    private string ArtVon(string id) => KarteVon.TryGetValue(id, out var k) ? k.Art : "";

    /// <summary>
    /// Die statische Kette einer Karte: von einer Nachricht/einem Handle/Decider/Applier aus dem Nachrichtenfluss transitiv folgen
    /// (Besitzer werden gezeigt, nicht durchlaufen); ein Akteur zeigt, was er darf, und was daraus folgt; Aggregat/Leseseite den
    /// gerichteten Schnitt; ein Client seine Anschlussleiste. <paramref name="details"/>: eingeklappte Details sind eigene Karten.
    /// </summary>
    public List<string> KetteFuer(string id, bool details)
    {
        if (!KarteVon.TryGetValue(id, out var n)) return [id];
        if (n.Art == "client")
        {
            var ids = new List<string> { id };
            void Add(string x) { if (!ids.Contains(x)) ids.Add(x); }
            foreach (var p in ClientPorts(n.Ref).OfType<JsonObject>())
            {
                if (S(p, "node") is { Length: > 0 } node) Add(details ? node : Vertreter(node).FirstOrDefault() ?? node);
                if (S(p, "ri") == "traegt" && L(p, "akteure").FirstOrDefault() is { } an && _akteure.FirstOrDefault(x => S(x, "name") == an) is { } a)
                    Add("akt:" + S(a, "_id"));
            }
            return ids;
        }
        return SliceVon(id, details).ToList();
    }
    private LinkedHashSet SliceVon(string start, bool details)
    {
        var res = new LinkedHashSet { start };
        var k0 = ArtVon(start);
        if (k0 == "akteur")
        {
            foreach (var t in Aus(start)) { res.Add(t); foreach (var x in KetteVon(t, true)) res.Add(x); }
            return res;
        }
        if (KetteStart.Contains(k0) && !(k0 == "handle" && KarteVon[start].Besitzer?.Art == "reader")) return KetteVon(start, false);
        var seeds = new List<string> { start };
        if (k0 == "aggregate") foreach (var x in Ein(start)) { res.Add(x); seeds.Add(x); }
        void Zurueck(string id, HashSet<string> seen)
        {
            foreach (var x in Ein(id))
            {
                if (!seen.Add(x)) continue;
                res.Add(x);
                if (Eingeklappt(x, details)) Zurueck(x, seen);
            }
        }
        foreach (var s in seeds) Zurueck(s, new HashSet<string>(StringComparer.Ordinal));
        var q = new Queue<string>(seeds);
        var fertig = new HashSet<string>(StringComparer.Ordinal);
        void Add(string x, bool weiter) { res.Add(x); if (weiter && !fertig.Contains(x)) q.Enqueue(x); }
        while (q.Count > 0)
        {
            var id = q.Dequeue();
            if (!fertig.Add(id)) continue;
            var k = ArtVon(id);
            if (!seeds.Contains(id) && Stop.Contains(k)) continue;
            foreach (var y in Aus(id)) Add(y, true);
            if (k is "projektion" or "store")
                foreach (var y in Ein(id))
                {
                    var ky = ArtVon(y);
                    if (ky is "reader" or "readmodel") Add(y, true);
                    else if (k == "projektion" && ky == "event" && LeseStart.Contains(k0)) Add(y, false);
                }
            if (k == "reader") foreach (var y in Ein(id)) if (ArtVon(y) == "query") Add(y, false);
        }
        return res;
    }
    private LinkedHashSet KetteVon(string start, bool nurVorwaerts)
    {
        var starts = new List<string> { start };
        var res = new LinkedHashSet();
        foreach (var s in starts) res.Add(s);
        void Lauf(Func<string, IReadOnlyList<string>> nach)
        {
            var q = new Queue<string>(starts);
            var seen = new HashSet<string>(StringComparer.Ordinal);
            while (q.Count > 0)
            {
                var id = q.Dequeue();
                if (!seen.Add(id)) continue;
                if (!starts.Contains(id) && KetteHub.Contains(ArtVon(id))) continue;
                foreach (var y in nach(id)) if (KetteFluss.Contains(ArtVon(y))) { res.Add(y); q.Enqueue(y); }
            }
        }
        Lauf(Aus);
        if (!nurVorwaerts) Lauf(Ein);
        foreach (var id in res.ToList())
            if (ArtVon(id) is "handle" or "decider" or "applier" or "fn")
                foreach (var y in Aus(id)) if (KetteHub.Contains(ArtVon(y))) res.Add(y);
        foreach (var id in res.ToList())
            if (ArtVon(id) is "projektion" or "store")
                foreach (var y in Ein(id)) if (ArtVon(y) is "reader" or "readmodel") res.Add(y);
        return res;
    }

    /// <summary>Eine Menge mit Einfüge-Reihenfolge (wie <c>Set</c> in JS).</summary>
    private sealed class LinkedHashSet : IEnumerable<string>
    {
        private readonly List<string> _liste = [];
        private readonly HashSet<string> _menge = new(StringComparer.Ordinal);
        public bool Add(string x) { if (!_menge.Add(x)) return false; _liste.Add(x); return true; }
        public IEnumerator<string> GetEnumerator() => _liste.GetEnumerator();
        System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator() => GetEnumerator();
    }
}
