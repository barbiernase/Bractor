using System.Text.Json.Nodes;
using DomainEditor;

namespace GraphExtractor;

// Die FLÄCHE der Werkbank (docs/10 §10.9): acht Spalten in der Reihenfolge des Ablaufs — die Rechner mit ihren App-Instanzen (je Umgebung, Konfig),
//   die Clients, was Akteure hineingeben, Graphen, Übersetzungen, Aggregate, Projektionen, was zu Akteuren kommt —, die Domänen als Bänder über die
//   mittleren Spalten, Nachrichten als Ports an den Karten und Kanten von Port zu Port mit ihrer Art. Eine Quelle ist kein Knoten eines Graphen: sie ist
//   ein Parameter einer App und läuft mit deren Instanz auf ihrem Rechner (Sicht.Clients.cs); ihre Übersetzung der Akteur-Seite macht aus der Meldung, was
//   ein Akteur des Clients darf.
//   Gerechnet nur aus dem Board (Code-Fakten) und den Umgebungen (Konfig); der Editor ordnet innerhalb der Zellen an.
public sealed partial class Sicht
{
    /// <summary>Die Spalten der Fläche von links nach rechts.</summary>
    public static readonly string[] Spalten = ["rechner", "clients", "ausgang", "graphen", "uebersetzungen", "aggregate", "projektionen", "eingang"];

    /// <summary>
    /// Ein Port: Typ der Nachricht, Richtung (<c>ein</c>/<c>aus</c>), Sorte (<c>command</c>, <c>event</c>, <c>hinweis</c>, <c>query</c>,
    /// <c>antwort</c>, <c>frist</c>, <c>meldung</c>) und ob die Karte den Typ deklariert (<c>eigen</c>: sie ist sein Besitzer) oder nur auf ihn verweist.
    /// </summary>
    public sealed record FlaechenPort(string Typ, string Richtung, string Sorte, bool Eigen);

    /// <summary>
    /// Eine Karte der Fläche: Id (eindeutig auf der Fläche; ein Akteur steht zweimal, <c>…|aus</c> und <c>…|ein</c>), die Karte der Sicht dahinter
    /// (Panel), Art, Name, Spalte, Band (Domäne; Akteure haben keins), Gruppe (der Store einer Projektion bzw. eines Readers) und Ports.
    /// </summary>
    public sealed record FlaechenKarte(string Id, string Karte, string Art, string Name, string Spalte, string? Band, string? Gruppe, List<FlaechenPort> Ports);

    /// <summary>
    /// Eine Kante von einem Ausgangsport (<paramref name="Von"/>, Typ) zu einem Eingangsport (<paramref name="Nach"/>, Typ). Art: <c>schreiben</c>,
    /// <c>lesen</c>, <c>kenntnis</c>, <c>ruecklauf</c>; <c>liest</c> verbindet einen Graphen mit der Gruppe eines Stores
    /// (Typ = die Lese-Fähigkeit, keine Ports), <c>verkoerpert</c> einen Client mit einem Akteur (Typ = der Akteur, keine Ports).
    /// </summary>
    public sealed record FlaechenKante(string Von, string Nach, string Typ, string Art);

    public sealed record FlaechenGruppe(string Id, string Name, string? Band);

    public sealed record FlaechenBild(List<FlaechenKarte> Karten, List<FlaechenKante> Kanten, List<string> Baender, List<FlaechenGruppe> Gruppen);

    private FlaechenBild? _flaeche;
    /// <summary>Die Fläche (einmal gerechnet).</summary>
    public FlaechenBild Flaeche => _flaeche ??= BaueFlaeche();

    // ── Sorte einer Nachricht ──
    internal static string Basis(string t) => t.IndexOf('<') is > 0 and var i ? t[..i] : t;
    internal static bool IstFrist(string t) => t.StartsWith("Frist<", StringComparison.Ordinal);
    /// <summary>Die Sorte eines Nachrichtentyps — aus der Art seines Records (<c>ITransientEvent</c> = Hinweis), eine Frist an ihrem Typ.</summary>
    internal string Sorte(string typ)
    {
        if (IstFrist(typ)) return "frist";
        return S(RecByName(Basis(typ)), "kind") switch
        {
            "event" => "event",
            "rejection" => "hinweis",
            "query" => "query",
            "queryresponse" => "antwort",
            "meldung" => "meldung",
            _ => "command",
        };
    }

    // ── Graphen: Knoten, innere Funktionen ──
    private JsonObject? GraphNamens(string name) => _graphen.FirstOrDefault(g => S(g, "name") == name);
    /// <summary>Die Funktionen eines Graphen, transitiv durch Graphen im Graphen.</summary>
    internal IEnumerable<JsonObject> InnereFunktionen(JsonObject g, HashSet<string>? gesehen = null)
    {
        gesehen ??= new HashSet<string>(StringComparer.Ordinal);
        if (!gesehen.Add(S(g, "name"))) yield break;
        foreach (var kn in Alle(g["knoten"]))
        {
            var basis = Basis(S(kn, "typ"));
            if (BausteinFunktion(basis) is { } f) yield return f;
            else if (GraphNamens(basis) is { } inner) foreach (var x in InnereFunktionen(inner, gesehen)) yield return x;
        }
    }
    /// <summary>
    /// Steht der Graph auf der Fläche? Ein Graph gehört keinem anderen Graphen (sonst steht er in dessen Komposition), und liegt er in einem Katalog,
    /// bindet die Anwendung ihn an (docs/04 §4.4): eine Übersetzung nimmt einen seiner Ausgänge oder gibt an seinen Kopf (auch eine der Akteur-Seite,
    /// die aus der Meldung einer Quellart seinen Kopf-Command macht), oder ein Akteur gibt an seinen Kopf. Ein Graph ohne Anbindung ist eine Blaupause im
    /// Katalog-Modal.
    /// </summary>
    private bool AufFlaeche(JsonObject g)
    {
        if (!KarteVon.TryGetValue("gr:" + S(g, "_id"), out var k) || Besitzer(k).StartsWith("gr:", StringComparison.Ordinal)) return false;
        if (Abstractions.Projektstruktur.KatalogVon(S(g, "namespace")) == null) return true;
        var uebersetzt = _uebersetzungen.Select(u => Basis(S(u, "ein"))).ToHashSet(StringComparer.Ordinal);
        var gegeben = _uebersetzungen.Select(u => Basis(S(u, "aus"))).Concat(_akteure.SelectMany(a => L(a, "darf"))).ToHashSet(StringComparer.Ordinal);
        return L(g, "ausgaenge").Any(t => uebersetzt.Contains(Basis(t))) || L(g, "koepfe").Any(t => gegeben.Contains(Basis(t)));
    }

    // ── Band (Domäne) ──
    private string? AggDomaene(string aggregat) =>
        KarteVon.TryGetValue("agg:" + aggregat, out var a) && Domaene(a) is var d && d != OhneDomaene ? d : null;
    /// <summary>
    /// Das Band eines Graphen: der Katalog, in dem er liegt (domänenneutral). Liegt er falsch in einer Anwendung (die Lage meldet es), die Domäne der
    /// Aggregate, an die er schreibt (seine Ausgänge mit Ziel), sonst derer, deren Decide seine Köpfe speist — eindeutig; sonst die Domäne seiner Karte.
    /// Lese-Fähigkeiten zählen nicht.
    /// </summary>
    private string? GraphBand(JsonObject g)
    {
        // Ein Graph aus einem Katalog ist domänenneutral: er steht im Band seines Katalogs (docs/04 §4.4).
        if (Abstractions.Projektstruktur.KatalogVon(S(g, "namespace")) is { } katalog) return katalog;
        string? Eine(IEnumerable<string> aggs)
        {
            var ds = aggs.Where(a => a.Length > 0).Select(AggDomaene).Where(d => d != null).Distinct().ToList();
            return ds.Count == 1 ? ds[0] : null;
        }
        return Eine(L(g, "ausgaenge").Select(t => RecByName(Basis(t)) is { } r && S(r, "aggregat").Length > 0 ? S(r, "aggregat") : ""))
               ?? Eine(L(g, "koepfe").Select(RecordAgg))
               ?? (KarteVon.TryGetValue("gr:" + S(g, "_id"), out var k) && Domaene(k) is var d && d != OhneDomaene ? d : null);
    }
    private string? BandVon(string kartenId) => KarteVon.TryGetValue(kartenId, out var k) && Domaene(k) is var d && d != OhneDomaene ? d : null;

    private FlaechenBild BaueFlaeche()
    {
        var karten = new List<FlaechenKarte>();
        var kanten = new List<FlaechenKante>();
        // Karte der Sicht → Karte der Fläche (für den Besitz eines Typs: wer ihn deklariert).
        var flaecheVon = new Dictionary<string, string>(StringComparer.Ordinal);
        // Ports, die nicht über den Typ verbunden werden (Antworten an Akteure): ihre Kanten stehen ausdrücklich.
        var ausdruecklich = new HashSet<(string, string)>();

        // ① und ⑥: je Akteur zwei Karten — links mit dem, was er darf, rechts mit dem, was er hört.
        var gruppen = new List<FlaechenGruppe>();
        foreach (var a in _akteure)
        {
            var id = "akt:" + S(a, "_id");
            var darf = L(a, "darf").Where(IstRec).Distinct().ToList();
            karten.Add(new(id + "|aus", id, "akteur", S(a, "name"), "ausgang", null, null,
                darf.Select(t => new FlaechenPort(t, "aus", Sorte(t), false)).ToList()));
            flaecheVon[id] = id + "|aus";
            var ein = L(a, "hoert").Distinct().Select(t => new FlaechenPort(t, "ein", Sorte(t), false)).ToList();
            // Die Antworten auf die Queries, die er stellen darf — von dem Reader bzw. Aggregat, das antwortet.
            foreach (var q in darf.Where(t => Sorte(t) == "query"))
                foreach (var (quelle, antwort) in Antwortende(q))
                {
                    if (!ein.Any(p => p.Typ == antwort && p.Sorte == "antwort")) ein.Add(new(antwort, "ein", "antwort", false));
                    ausdruecklich.Add((id + "|ein", antwort));
                    if (!kanten.Contains(new(quelle, id + "|ein", antwort, "lesen"))) kanten.Add(new(quelle, id + "|ein", antwort, "lesen"));
                }
            karten.Add(new(id + "|ein", id, "akteur", S(a, "name"), "eingang", null, null, ein));
        }
        // ⓪ Je Client eine Karte in der Spalte Clients (ohne Ports) und je Akteur, den er verkörpert, eine Kante zu dessen linker Karte. Was ihn umsetzt,
        //   sind die App-Instanzen auf den Rechnern links (je Umgebung, AppInstanzenJson); seine Reaktionen nennt die Karte.
        foreach (var c in _clients)
        {
            karten.Add(new("cl:" + S(c, "_id"), "cl:" + S(c, "_id"), "client", ClientAnzeige(c), "clients", null, null, []));
            foreach (var a in _akteure.Where(a => L(c, "verkoerpert").Contains(S(a, "name"))))
                kanten.Add(new("cl:" + S(c, "_id"), "akt:" + S(a, "_id") + "|aus", S(a, "name"), "verkoerpert"));
        }

        // ② Graphen (nur die obersten).
        foreach (var g in _graphen.Where(AufFlaeche))
        {
            var gid = "gr:" + S(g, "_id");
            var band = GraphBand(g);
            var ports = new List<FlaechenPort>();
            void Port(string t, string ri, string? sorte = null)
            {
                if (!ports.Any(p => p.Typ == t && p.Richtung == ri)) ports.Add(new(t, ri, sorte ?? Sorte(t), false));
            }
            foreach (var t in L(g, "koepfe")) Port(t, "ein");
            // Ein Event nimmt eine Funktion nur als Antwort auf ihren eigenen Aggregat-Command (CQRS108) — es kommt vom Aggregat zurück.
            foreach (var f in InnereFunktionen(g))
                foreach (var e in Alle(f["eingaenge"]))
                    if (Sorte(S(e, "eingang")) == "event") Port(S(e, "eingang"), "ein");
            foreach (var t in L(g, "ausgaenge")) Port(t, "aus");
            karten.Add(new(gid, gid, "graph", S(g, "name"), "graphen", band, null, ports));
            flaecheVon[gid] = gid;
        }
        // ③ Übersetzungen der Domänen (docs/04 §4.4): je Record eine Karte im Band ihrer Domäne — der des Aggregats, an das sie schreibt
        //   bzw. dessen Antwort sie nimmt, sonst der ihres Namespace. Die Kanten folgen dem Typ: von dem Graphen, der TEin ausgibt (bzw. vom Akteur, der
        //   es gibt, vom Aggregat, das so antwortet), zu dem, der TAus nimmt. Eine Übersetzung der Akteur-Seite steht nicht hier: sie verwirklicht einen
        //   Akteur (aus der Meldung einer Quellart wird, was er darf) und gehört zu ihm — auf der Fläche gibt der Akteur, was er darf.
        foreach (var u in _uebersetzungen.Where(u => !B(u, "akteurSeite")))
        {
            var uid = "ue:" + S(u, "_id");
            var (ein, aus) = (S(u, "ein"), S(u, "aus"));
            var band = AggDomaene(RecordAgg(aus)) ?? (Sorte(ein) == "event" ? AggDomaene(RecordAgg(ein)) : null) ?? BandVon(uid);
            karten.Add(new(uid, uid, "uebersetzung", S(u, "name"), "uebersetzungen", band, null,
                [new FlaechenPort(ein, "ein", Sorte(ein), false), new FlaechenPort(aus, "aus", Sorte(aus), false)]));
            flaecheVon[uid] = uid;
        }

        // ④ Aggregate: links die Commands und Queries, rechts was das Decide ergibt und die Antworten.
        foreach (var a in _aggregate)
        {
            var name = S(a, "name");
            var aid = "agg:" + name;
            var ports = new List<FlaechenPort>();
            void Port(string t, string ri) { if (t.Length > 0 && !ports.Any(p => p.Typ == t && p.Richtung == ri)) ports.Add(new(t, ri, Sorte(t), false)); }
            foreach (var d in _decider.Where(d => S(d, "aggregat") == name)) Port(S(d, "command"), "ein");
            foreach (var f in Alle(a["fragen"])) Port(S(f, "query"), "ein");
            foreach (var d in _decider.Where(d => S(d, "aggregat") == name)) foreach (var o in Alle(d["ergibt"])) Port(S(o, "event"), "aus");
            foreach (var f in Alle(a["fragen"])) foreach (var x in L(f, "antworten")) Port(x, "aus");
            karten.Add(new(aid, aid, "aggregate", name, "aggregate", BandVon(aid), null, ports));
            flaecheVon[aid] = aid;
        }

        // ⑤ Projektionen und Reader, je Store gruppiert.
        string? Gruppe(string kartenId, JsonObject o)
        {
            var b = KarteVon.TryGetValue(kartenId, out var k) ? Besitzer(k) : "";
            // Ohne Fähigkeit (eben im Store angelegt) hält die Ansicht den Store fest (amStore, nur Editor) — sonst stünde sie lose.
            var sto = b.StartsWith("sto:", StringComparison.Ordinal) ? b
                : StoreScope(o).Select(StoreId).FirstOrDefault(x => x != null) ?? (S(o, "amStore") is { Length: > 0 } a ? StoreId(a) : null);
            if (sto == null) return null;
            if (!gruppen.Any(x => x.Id == sto) && KarteVon.TryGetValue(sto, out var st)) gruppen.Add(new(sto, st.Name, BandVon(sto)));
            return sto;
        }
        foreach (var p in _projektionen)
        {
            var pid = "prj:" + S(p, "_id");
            var ports = Alle(p["handles"]).SelectMany(HandleEvents).Where(t => t.Length > 0).Distinct()
                .Select(t => new FlaechenPort(t, "ein", Sorte(t), false)).ToList();
            karten.Add(new(pid, pid, "projektion", S(p, "name"), "projektionen", BandVon(pid), Gruppe(pid, p), ports));
            flaecheVon[pid] = pid;
        }
        foreach (var r in _reader)
        {
            var rid = "rdr:" + S(r, "_id");
            var ports = Alle(r["handles"]).Select(h => S(h, "query")).Where(t => t.Length > 0).Distinct()
                .Select(t => new FlaechenPort(t, "ein", Sorte(t), false))
                .Concat(Alle(r["handles"]).SelectMany(h => L(h, "responses")).Distinct().Select(t => new FlaechenPort(t, "aus", Sorte(t), false))).ToList();
            karten.Add(new(rid, rid, "reader", S(r, "name"), "projektionen", BandVon(rid), Gruppe(rid, r), ports));
            flaecheVon[rid] = rid;
        }
        // Ein Store ohne Projektion und Reader (eben angelegt) steht als eigene Karte in seiner Gruppe — sonst fände man ihn nicht wieder.
        foreach (var st in _stores)
        {
            var sid = "sto:" + S(st, "_id");
            if (gruppen.Any(g => g.Id == sid)) continue;
            gruppen.Add(new(sid, S(st, "name"), BandVon(sid)));
            karten.Add(new(sid, sid, "store", S(st, "name"), "projektionen", BandVon(sid), sid, []));
            flaecheVon[sid] = sid;
        }

        // Besitz: je Typ die Karte der Fläche, die ihn deklariert (sein Besitzer, über Besitz-Ketten bis zu einer Karte der Fläche).
        string? Deklariert(string typ)
        {
            if (!KarteVon.TryGetValue("rec:" + Basis(typ), out var rec)) return null;
            var b = Besitzer(rec);
            if (b == Rand)
                return _akteure.FirstOrDefault(a => L(a, "darf").Contains(Basis(typ))) is { } a ? "akt:" + S(a, "_id") + "|aus" : null;
            for (var i = 0; i < 16 && !b.StartsWith('§'); i++)
            {
                if (flaecheVon.TryGetValue(b, out var f)) return f;
                if (!KarteVon.TryGetValue(b, out var k)) return null;
                b = Besitzer(k);
            }
            return null;
        }
        var deklariert = new Dictionary<string, string?>(StringComparer.Ordinal);
        karten = karten.Select(k => k with
        {
            Ports = k.Ports.Select(p =>
            {
                if (p.Sorte == "frist") return p;
                if (!deklariert.TryGetValue(p.Typ, out var d)) deklariert[p.Typ] = d = Deklariert(p.Typ);
                return p with { Eigen = d == k.Id };
            }).ToList(),
        }).ToList();

        // Kanten über den Typ: je Ausgangsport jeder Eingangsport desselben Typs auf einer anderen Karte.
        var art = karten.ToDictionary(k => k.Id, k => (k.Art, k.Spalte, k.Karte), StringComparer.Ordinal);
        var nehmer = karten.SelectMany(k => k.Ports.Where(p => p.Richtung == "ein").Select(p => (Karte: k, Port: p)))
            .Where(x => !ausdruecklich.Contains((x.Karte.Id, x.Port.Typ)))
            .ToLookup(x => x.Port.Typ, StringComparer.Ordinal);
        foreach (var k in karten)
            foreach (var p in k.Ports.Where(p => p.Richtung == "aus" && !ausdruecklich.Contains((k.Id, p.Typ))))
                foreach (var (nk, np) in nehmer[p.Typ])
                {
                    if (nk.Id == k.Id || nk.Karte == k.Karte) continue;
                    var a = np.Sorte is "query" or "antwort" ? "lesen"
                        : nk.Spalte == "eingang" ? "kenntnis"
                        : k.Spalte == "aggregate" && nk.Spalte is "graphen" or "uebersetzungen" ? "ruecklauf"
                        : "schreiben";
                    kanten.Add(new(k.Id, nk.Id, p.Typ, a));
                }
        // liest: ein Graph, dessen Funktionen eine Lese-Fähigkeit nehmen, hängt am Store dahinter.
        foreach (var g in _graphen.Where(AufFlaeche))
        {
            var gid = "gr:" + S(g, "_id");
            foreach (var f in InnereFunktionen(g).Distinct())
                foreach (var fa in Alle(f["eingaenge"]).SelectMany(e => Alle(e["faehigkeiten"])))
                    if (LeseFnZu(S(fa, "typ")) is { } z && StoreId(S(z.Store, "name")) is { } sto
                        && !kanten.Any(x => x.Von == gid && x.Nach == sto && x.Typ == S(fa, "typ")))
                    {
                        if (!gruppen.Any(x => x.Id == sto) && KarteVon.TryGetValue(sto, out var st)) gruppen.Add(new(sto, st.Name, BandVon(sto)));
                        kanten.Add(new(gid, sto, S(fa, "typ"), "liest"));
                    }
        }

        var baender = karten.Select(k => k.Band).Concat(gruppen.Select(g => g.Band)).OfType<string>().Distinct().ToList();
        baender.Sort(DomOrd);
        return new(karten, kanten, baender, gruppen);
    }

    /// <summary>Wer eine Query beantwortet und womit: je Reader-Handle bzw. Frage eines Aggregats die Karte der Fläche und die Antwort.</summary>
    private IEnumerable<(string Karte, string Antwort)> Antwortende(string query)
    {
        foreach (var r in _reader)
            foreach (var h in Alle(r["handles"]).Where(h => S(h, "query") == query))
                foreach (var x in L(h, "responses")) yield return ("rdr:" + S(r, "_id"), x);
        foreach (var a in _aggregate)
            foreach (var f in Alle(a["fragen"]).Where(f => S(f, "query") == query))
                foreach (var x in L(f, "antworten")) yield return ("agg:" + S(a, "name"), x);
    }

    /// <summary><c>board.sicht.flaeche</c>.</summary>
    private JsonObject FlaecheJson()
    {
        var f = Flaeche;
        JsonObject Karte(FlaechenKarte k)
        {
            var o = new JsonObject { ["id"] = k.Id, ["karte"] = k.Karte, ["art"] = k.Art, ["name"] = k.Name, ["spalte"] = k.Spalte };
            // Ein Client: die Apps, die ihn umsetzen (Code), und die Klassen seiner Reaktionen (er reagiert auf, was seine Akteure hören).
            if (k.Art == "client" && _clients.FirstOrDefault(c => "cl:" + S(c, "_id") == k.Id) is { } cl)
            {
                o["apps"] = new JsonArray(_apps.Where(a => S(a, "client") == S(cl, "name")).Select(a => (JsonNode)new JsonObject
                    { ["name"] = S(a, "name"), ["karte"] = "ap:" + S(a, "_id") }).ToArray());
                o["reagiert"] = Arr(Alle(cl["implementierungen"]).Select(i => S(i, "klasse")).Distinct());
            }
            if (k.Band != null) o["band"] = k.Band;
            if (k.Gruppe != null) o["gruppe"] = k.Gruppe;
            o["ports"] = new JsonArray(k.Ports.Select(p =>
            {
                var po = new JsonObject { ["typ"] = p.Typ, ["richtung"] = p.Richtung, ["sorte"] = p.Sorte, ["eigen"] = p.Eigen };
                if (Loesbar(k, p)) po["loesbar"] = true;
                return (JsonNode)po;
            }).ToArray());
            return o;
        }
        return new JsonObject
        {
            ["spalten"] = Arr(Spalten),
            // Ein Band eines Katalogs (seine Graphen sind domänenneutral) heißt nach dem Katalog: „📚 Bildvorschau“.
            ["baender"] = new JsonArray(f.Baender.Select(b => (JsonNode)new JsonObject { ["id"] = b, ["name"] = Abstractions.Projektstruktur.IstKatalog(b)
                ? "📚 " + b[..b.LastIndexOf('.')] : b[(b.LastIndexOf('.') + 1)..] }).ToArray()),
            ["gruppen"] = new JsonArray(f.Gruppen.Select(g =>
            {
                var o = new JsonObject { ["id"] = g.Id, ["name"] = g.Name };
                if (g.Band != null) o["band"] = g.Band;
                return (JsonNode)o;
            }).ToArray()),
            ["karten"] = new JsonArray(f.Karten.Select(k => (JsonNode)Karte(k)).ToArray()),
            ["kanten"] = new JsonArray(f.Kanten.Select(k => (JsonNode)new JsonObject { ["von"] = k.Von, ["nach"] = k.Nach, ["typ"] = k.Typ, ["art"] = k.Art }).ToArray()),
            ["anschluss"] = Anschluss(f),
            // Je Umgebung der Pool der Rechner, das Backend, die Rechner der Instanzen und die App-Instanzen samt ihren Karten und Kanten (Konfig) — der
            //   Editor nimmt die der aktiven Umgebung.
            ["rechner"] = RechnerJson(),
        };
    }

    /// <summary>
    /// Was ✕ an einem Port löst, steht im Code als Liste: ein Auf einer Projektion. Was ein Akteur darf und hört, ändert man nur im Modal „Akteure
    /// und Clients“ (je Absicht eine Geste).
    /// </summary>
    private static bool Loesbar(FlaechenKarte k, FlaechenPort p) => k.Art == "projektion" && p.Richtung == "ein";

    /// <summary>
    /// Was an einen „＋“ der Fläche passt — je Start (<c>event|Karte</c>, <c>uebersetzen|Typ</c>) die Ports <c>Karte|Richtung|Typ</c>, die die
    /// Grammatik dort erlaubt und die noch nicht verbunden sind: eine Projektion nimmt ein Event vom Aggregat, das es entscheidet; ein Command ohne
    /// Ziel wird in einen Command an einem Aggregat übersetzt (GR-UEBERSETZUNG).
    /// </summary>
    private JsonObject Anschluss(FlaechenBild f)
    {
        var o = new JsonObject();
        string Key(FlaechenKarte k, FlaechenPort p) => k.Id + "|" + p.Richtung + "|" + p.Typ;
        IEnumerable<(FlaechenKarte K, FlaechenPort P)> Ports(string ri) => f.Karten.SelectMany(k => k.Ports.Where(p => p.Richtung == ri).Select(p => (k, p)));
        bool Erzeugt(string baustein, string typ) => GrSorte(Basis(typ)) is { } s && Grammatik.ErzeugungVon(baustein, s) != null;
        bool Nimmt(string typ, string baustein) => GrSorte(Basis(typ)) is { } s && Grammatik.KonsumVon(s, baustein) != null;
        foreach (var p in _projektionen)
        {
            var hat = Alle(p["handles"]).SelectMany(HandleEvents).ToHashSet(StringComparer.Ordinal);
            o["event|prj:" + S(p, "_id")] = Arr(Ports("aus").Where(x => x.K.Art == "aggregate" && Erzeugt(Grammatik.Aggregat, x.P.Typ)
                    && GrSorte(Basis(x.P.Typ)) == Grammatik.Event && Nimmt(x.P.Typ, Grammatik.Projektion) && !hat.Contains(Basis(x.P.Typ)))
                .Select(x => Key(x.K, x.P)).Distinct());
        }
        foreach (var r in _records.Where(r => GrSorte(S(r, "name")) == Grammatik.Command && RecordAgg(S(r, "name")).Length == 0))
        {
            var t = S(r, "name");
            var schon = _uebersetzungen.Where(u => Basis(S(u, "ein")) == t).Select(u => Basis(S(u, "aus"))).ToHashSet(StringComparer.Ordinal);
            o["uebersetzen|" + t] = Arr(Ports("ein").Where(x => x.K.Art == "aggregate" && GrSorte(Basis(x.P.Typ)) == Grammatik.Command
                    && RecordAgg(Basis(x.P.Typ)).Length > 0 && !schon.Contains(Basis(x.P.Typ)))
                .Select(x => Key(x.K, x.P)).Distinct());
        }
        return o;
    }

    /// <summary>
    /// Die Befunde der Fläche für die Paritätsprüfung: jede Kante verbindet zwei vorhandene Ports, und jeder Typ auf der Fläche hat höchstens einen
    /// deklarierenden Port. Ein Typ ohne deklarierenden Port (sein Besitzer steht nicht auf der Fläche, etwa eine Funktion in mehreren Graphen) ist
    /// ein Hinweis.
    /// </summary>
    public IEnumerable<(string Schweregrad, string Meldung)> FlaechenBefunde()
    {
        var f = Flaeche;
        var karte = f.Karten.ToDictionary(k => k.Id, StringComparer.Ordinal);
        bool Hat(string id, string ri, string typ) => karte.TryGetValue(id, out var k) && k.Ports.Any(p => p.Richtung == ri && p.Typ == typ);
        foreach (var k in f.Kanten)
        {
            if (k.Art == "verkoerpert")
            {
                if (!karte.TryGetValue(k.Von, out var c) || c.Art != "client" || !karte.TryGetValue(k.Nach, out var a) || a.Art != "akteur")
                    yield return ("error", $"Fläche: {k.Von} verkörpert {k.Typ} ohne Client- bzw. Akteur-Karte");
                continue;
            }
            if (k.Art == "liest")
            {
                if (!karte.ContainsKey(k.Von) || !f.Gruppen.Any(g => g.Id == k.Nach)) yield return ("error", $"Fläche: Kante liest {k.Von} → {k.Nach} ohne Karte bzw. Gruppe");
                continue;
            }
            if (!Hat(k.Von, "aus", k.Typ)) yield return ("error", $"Fläche: Kante {k.Typ} beginnt an {k.Von} ohne Ausgangsport");
            if (!Hat(k.Nach, "ein", k.Typ)) yield return ("error", $"Fläche: Kante {k.Typ} endet an {k.Nach} ohne Eingangsport");
        }
        foreach (var g in f.Karten.SelectMany(k => k.Ports.Select(p => (Karte: k, Port: p))).Where(x => x.Port.Sorte != "frist" && IstRec(Basis(x.Port.Typ))).GroupBy(x => x.Port.Typ, StringComparer.Ordinal))
        {
            var eigen = g.Where(x => x.Port.Eigen).Select(x => x.Karte.Id).Distinct().ToList();
            if (eigen.Count > 1) yield return ("error", $"Fläche: {g.Key} wird an {eigen.Count} Ports deklariert ({string.Join(", ", eigen)})");
            else if (eigen.Count == 0) yield return ("info", $"Fläche: {g.Key} hat keinen deklarierenden Port auf der Fläche");
        }
        foreach (var k in f.Karten.Where(k => k.Spalte is "graphen" or "uebersetzungen" or "aggregate" or "projektionen" && k.Band == null))
            yield return ("warning", $"Fläche: {k.Name} ({k.Art}) steht in keinem Band");
        foreach (var b in AppKantenBefunde(f)) yield return b;
    }
}
