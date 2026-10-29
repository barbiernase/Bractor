using System.Text.Json.Nodes;

namespace GraphExtractor;

// Die Ableitungen je Karte: Block (Aggregat), Store-Scope, Inseln, Akteure, Domäne und Einklappen.
public sealed partial class Sicht
{
    private static string Eindeutig(IEnumerable<string?> xs)
    {
        var s = xs.Distinct().ToList();
        return s.Count == 1 && !string.IsNullOrEmpty(s[0]) ? s[0]! : "";
    }

    // ══ ZUGEHÖRIGKEIT = BEZIEHUNG, nie Namespace oder Name ══
    /// <summary>Das Aggregat eines Records: der Decider, der ihn entscheidet/erzeugt, bzw. der Applier, der ihn faltet — eindeutig oder keins.</summary>
    private readonly Dictionary<string, string> _recordAgg = new(StringComparer.Ordinal);
    internal string RecordAgg(string? n)
    {
        if (string.IsNullOrEmpty(n)) return "";
        if (_recordAgg.TryGetValue(n, out var gemerkt)) return gemerkt;
        return _recordAgg[n] = RecordAggOhneCache(n);
    }
    private string RecordAggOhneCache(string n)
    {
        var a = _decider.Where(d => S(d, "command") == n || Alle(d["ergibt"]).Any(o => S(o, "event") == n)).Select(d => S(d, "aggregat"))
            .Concat(_applier.Where(x => S(x, "event") == n).Select(x => S(x, "aggregat")))
            // Die Query, die ein Aggregat beantwortet, und ihre Antworten (Frage im Decider, docs/03 §3.2).
            .Concat(_aggregate.Where(g => Alle(g["fragen"]).Any(f => S(f, "query") == n || L(f, "antworten").Contains(n))).Select(g => S(g, "name"))).ToList();
        return a.Count > 0 ? Eindeutig(a) : "";
    }
    private string TypAgg(string t)
    {
        if (t.Length == 0) return "";
        var a = new List<string>();
        foreach (var r in _records) if (Alle(r["felder"]).Any(f => InnerTyp(S(f, "typ")) == t)) a.Add(RecordAgg(S(r, "name")));
        foreach (var g in _aggregate) if (Alle(g["state"]).Any(f => InnerTyp(S(f, "typ")) == t)) a.Add(S(g, "name"));
        return a.Count > 0 ? Eindeutig(a) : "";
    }

    /// <summary>Der abgeleitete Store-Scope eines Konsumenten: die Stores, deren Funktionen seine Handles aufrufen.</summary>
    internal List<string> StoreScope(JsonObject node)
    {
        var s = new List<string>();
        foreach (var hd in Alle(node["handles"]))
            foreach (var id in L(hd, "fns"))
                if (FnById(id) is { } r && !s.Contains(S(r.Store, "name"))) s.Add(S(r.Store, "name"));
        return s;
    }

    private readonly Dictionary<string, string> _block = new(StringComparer.Ordinal);
    /// <summary>Block einer Karte: das Aggregat, zu dem sie gehört (Spalten-Block), <see cref="Geteilt"/> bzw. <see cref="AkteurBlock"/>.</summary>
    public string Block(SichtKarte n) => _block.TryGetValue(n.Id, out var b) ? b : _block[n.Id] = BlockOhneCache(n.Art, n.Ref, n.Besitzer);
    private string BlockVon(string art, JsonObject r) => BlockOhneCache(art, r, null);
    private string BlockOhneCache(string k, JsonObject r, SichtKarte? own)
    {
        var name = S(r, "name");
        string Oder(string x) => x.Length > 0 ? x : Geteilt;
        switch (k)
        {
            case "akteur": return AkteurBlock;
            case "aggregate": return name;
            case "handle": case "fn": return own == null ? Geteilt : Block(own);
            case "state": case "decider": case "applier": return Oder(S(r, "aggregat"));
        }
        switch (k)
        {
            case "command": case "event": case "rejection": return Oder(RecordAgg(name));
            case "valueobject": case "enum": return Oder(TypAgg(name));
            case "projektion":
            {
                var a = Alle(r["handles"]).Select(h => RecordAgg(S(h, "event"))).ToList();
                return a.Count > 0 ? Oder(Eindeutig(a)) : Geteilt;
            }
            case "reader":
                return _projektionen.FirstOrDefault(x => L(r, "projektionen").FirstOrDefault() == S(x, "name")) is { } p ? BlockVon("projektion", p) : Geteilt;
            case "query":
                if (RecordAgg(name) is { Length: > 0 } qa) return qa;   // eine Frage an ein Aggregat (§2.6)
                return _reader.FirstOrDefault(x => Alle(x["handles"]).Any(h => S(h, "query") == name)) is { } rd ? BlockVon("reader", rd) : Geteilt;
            case "queryresponse":
                if (RecordAgg(name) is { Length: > 0 } ra) return ra;
                return _reader.FirstOrDefault(x => Alle(x["handles"]).Any(h => L(h, "responses").Contains(name))) is { } rq ? BlockVon("reader", rq) : Geteilt;
            case "store":
            {
                if (_projektionen.FirstOrDefault(p => StoreScope(p).Contains(name)) is { } up) return BlockVon("projektion", up);
                if (_reader.FirstOrDefault(rd => StoreScope(rd).Contains(name)) is { } ur) return BlockVon("reader", ur);
                return Geteilt;
            }
            case "readmodel":
                return _stores.FirstOrDefault(s => S(s, "name") == S(r, "store")) is { } st ? BlockVon("store", st) : Geteilt;
        }
        return Geteilt;
    }

    // ══ INSELN: Karten in winzigen Komponenten (≤ 2 fachliche Karten) ohne Aggregat ══
    private HashSet<string>? _inseln;
    public IReadOnlySet<string> Inseln => _inseln ??= BerechneInseln();
    private HashSet<string> BerechneInseln()
    {
        var p = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var k in Karten) p[k.Id] = k.Id;
        string Find(string x)
        {
            while (p[x] != x) { p[x] = p[p[x]]; x = p[x]; }
            return x;
        }
        foreach (var (a, b) in Kanten)
            if (p.ContainsKey(a) && p.ContainsKey(b)) { var ra = Find(a); var rb = Find(b); if (ra != rb) p[ra] = rb; }
        var ids = new HashSet<string>(StringComparer.Ordinal);
        foreach (var g in Karten.GroupBy(k => Find(k.Id)))
        {
            var fach = g.Where(n => n.Art is not ("client" or "host")).ToList();
            if (fach.Count <= 2 && !fach.Any(n => n.Art == "aggregate")) foreach (var n in fach) ids.Add(n.Id);
        }
        return ids;
    }

    // ══ AKTEURE: von wem kommt was? (docs/konzept-akteure.md §2) ══
    private static readonly HashSet<string> DatenArten = ["valueobject", "enum", "konfig"];
    /// <summary>Arten, die mit ihrem Träger mitgehen, aber keinen Akteur-Rahmen begründen.</summary>
    private static readonly HashSet<string> NurMit = ["valueobject", "enum", "konfig", "queryresponse", "code"];
    private static readonly Dictionary<string, int> ArtRang = new() { ["Maschine"] = 0, ["Ki"] = 1, ["Mensch"] = 2 };

    private HashSet<string> MengeVon(string? nm)
    {
        var s = new HashSet<string>(StringComparer.Ordinal);
        if (nm != null && _mengen.TryGetValue(nm, out var m)) { s.UnionWith(m.Direkt); s.UnionWith(m.Kette); }
        return s;
    }
    private List<string> MengeListe(string? nm)
    {
        var l = new List<string>();
        if (nm != null && _mengen.TryGetValue(nm, out var m)) { l.AddRange(m.Direkt); l.AddRange(m.Kette.Where(x => !m.Direkt.Contains(x))); }
        return l;
    }

    private readonly Dictionary<string, List<string>> _akteurSet = new(StringComparer.Ordinal);
    /// <summary>Die Akteure einer Karte (Domäne × Akteur-Rahmen): Nachrichten aus der Ableitung, Bausteine über das, was sie tragen.</summary>
    public IReadOnlyList<string> Akteure(SichtKarte n)
    {
        if (_akteurSet.TryGetValue(n.Id, out var vorhanden)) return vorhanden;
        var s = new List<string>();
        _akteurSet[n.Id] = s;
        void Add(IEnumerable<string>? x) { if (x != null) foreach (var v in x) if (!s.Contains(v)) s.Add(v); }
        var r = n.Ref;
        var name = S(r, "name");
        switch (n.Art)
        {
            case "akteur": Add([name]); break;
            case "command": case "query": case "event": case "rejection": case "queryresponse":
            {
                Add(MengeListe(name));
                if (n.Art == "query" && s.Count == 0)
                    foreach (var rd in _reader.Where(rd => Alle(rd["handles"]).Any(h => S(h, "query") == name))) Add(AkteureId("rdr:" + S(rd, "_id")));
                break;
            }
            case "decider": Add(MengeListe(S(r, "command"))); break;
            case "applier": Add(MengeListe(S(r, "event"))); break;
            case "aggregate": foreach (var d in _decider.Where(d => S(d, "aggregat") == name)) Add(MengeListe(S(d, "command"))); break;
            case "state": foreach (var d in _decider.Where(d => S(d, "aggregat") == S(r, "aggregat"))) Add(MengeListe(S(d, "command"))); break;
            case "reader": foreach (var hd in Alle(r["handles"])) Add(MengeListe(S(hd, "query"))); break;
            case "handle":
            {
                var o = n.Besitzer!;
                if (o.Art == "projektion") Add(ProjektionAkteure(o.Ref));
                else Add(MengeListe(S(r, "query")));
                break;
            }
            case "dienst":
                foreach (var f in BausteinDienstNutzer(r)) Add(AkteureId("fb:" + S(f, "_id")));
                break;
            case "bausteinfunktion":
                foreach (var a in _akteure.Where(a => L(a, "darf").Contains(name))) Add([S(a, "name")]);
                foreach (var e in Alle(r["eingaenge"])) Add(MengeListe(S(e, "eingang")));
                break;
            case "projektion": Add(ProjektionAkteure(r)); break;
            case "store": Add(StoreAkteure(r)); break;
            case "fn": { var l = FnLeser(S(r, "_id")); Add(l.Count > 0 ? l : StoreAkteure(n.Besitzer!.Ref)); break; }
            case "readmodel":
                if (_stores.FirstOrDefault(x => S(x, "name") == S(r, "store")) is { } st) Add(StoreAkteure(st));
                break;
            default:
            {
                // VO/Enum/Konfig …: folgen ihren Nachbarn (ohne Daten-Daten-Ketten).
                var nb = Ein(n.Id).Concat(Aus(n.Id)).Select(x => KarteVon.GetValueOrDefault(x)).Where(m => m != null && m.Art != "akteur").ToList();
                foreach (var m in nb.Where(m => !DatenArten.Contains(m!.Art))) Add(Akteure(m!));
                if (s.Count == 0) foreach (var m in nb.Where(m => DatenArten.Contains(m!.Art))) Add(Akteure(m!));
                break;
            }
        }
        // Datentypen ohne eigene Kette (verschachtelte Response, Read Model ohne Store …) gehen mit ihren Nachbarn. Eingänge NIE.
        if (s.Count == 0 && n.Art is "queryresponse" or "readmodel")
            foreach (var m in Ein(n.Id).Concat(Aus(n.Id)).Select(x => KarteVon.GetValueOrDefault(x)).Where(m => m != null && m.Art != "akteur"))
                Add(Akteure(m!));
        return s;
    }
    private IReadOnlyList<string> AkteureId(string id) => KarteVon.TryGetValue(id, out var k) ? Akteure(k) : [];
    private List<string> FnLeser(string fid)
    {
        var s = new List<string>();
        foreach (var rd in _reader) foreach (var hd in Alle(rd["handles"]))
            if (L(hd, "fns").Contains(fid)) foreach (var x in MengeListe(S(hd, "query"))) if (!s.Contains(x)) s.Add(x);
        return s;
    }
    private List<string> StoreAkteure(JsonObject st)
    {
        var s = new List<string>();
        foreach (var f in Alle(st["readFns"])) foreach (var x in FnLeser(S(f, "_id"))) if (!s.Contains(x)) s.Add(x);
        return s;
    }
    private List<string> ProjektionAkteure(JsonObject p)
    {
        var s = new List<string>();
        void Add(IEnumerable<string> xs) { foreach (var x in xs) if (!s.Contains(x)) s.Add(x); }
        foreach (var rd in _reader.Where(rd => L(rd, "projektionen").Contains(S(p, "name")))) foreach (var hd in Alle(rd["handles"])) Add(MengeListe(S(hd, "query")));
        foreach (var nm in StoreScope(p)) if (_stores.FirstOrDefault(x => S(x, "name") == nm) is { } st) Add(StoreAkteure(st));
        return s;
    }

    private readonly Dictionary<string, List<string>> _akteurOrdnung = new(StringComparer.Ordinal);
    /// <summary>
    /// Die Akteur-Rahmen einer Domäne in ihrer Reihenfolge (docs/konzept-akteure.md §8, CLAUDE.md W1–W12): wer das Ding erschafft, dann Art
    /// (Maschine → KI → Mensch), dann Name. Gezählt werden die sichtbaren fachlichen Karten der Domäne (keine Inseln, keine eingeklappten Details).
    /// Eine gewünschte Reihenfolge (Ansicht) legt der Editor darüber.
    /// </summary>
    public IReadOnlyList<string> AkteurOrdnung(string dom)
    {
        if (_akteurOrdnung.TryGetValue(dom, out var o)) return o;
        var da = new List<string>();
        foreach (var n in Karten)
            if (n.Art != "akteur" && !NurMit.Contains(n.Art) && Sichtbar(n.Id) && !Inseln.Contains(n.Id) && Domaene(n) == dom)
                foreach (var x in Akteure(n)) if (!da.Contains(x)) da.Add(x);
        string? Art(string nm) => _akteure.FirstOrDefault(a => S(a, "name") == nm) is { } a && S(a, "art").Length > 0 ? S(a, "art") : null;
        var schoepfer = new HashSet<string>(StringComparer.Ordinal);
        foreach (var r in _records.Where(r => S(r, "kind") == "command" && B(r, "istErzeugung")))
        {
            var d = _decider.FirstOrDefault(x => S(x, "command") == S(r, "name"));
            var ag = d == null ? null : _aggregate.FirstOrDefault(a => S(a, "name") == S(d, "aggregat"));
            if (ag != null && S(ag, "namespace") == dom) schoepfer.UnionWith(MengeVon(S(r, "name")));
        }
        return _akteurOrdnung[dom] = da.OrderBy(a => schoepfer.Contains(a) ? 0 : 1).ThenBy(a => Art(a) is { } x && ArtRang.TryGetValue(x, out var g) ? g : 3)
            .ThenBy(a => a, Comparer<string>.Create(Locale)).ToList();
    }

    /// <summary>Der Akteur-Rahmen einer Karte: der erste Akteur der Domäne, zu dem sie gehört; ohne Akteur bzw. in einer Domäne ohne Akteure <see cref="OhneAkteur"/>.</summary>
    public string AkteurRahmen(SichtKarte n)
    {
        var ord = AkteurOrdnung(Domaene(n));
        if (ord.Count == 0) return OhneAkteur;
        var s = Akteure(n);
        return ord.FirstOrDefault(s.Contains) ?? OhneAkteur;
    }

    private Dictionary<string, List<string>>? _akteurDom;
    private List<string> AkteurDomaenen(string name)
    {
        if (_akteurDom == null)
        {
            _akteurDom = new(StringComparer.Ordinal);
            foreach (var n in Karten)
            {
                if (n.Art == "akteur" || NurMit.Contains(n.Art)) continue;
                var d = Domaene(n);
                foreach (var a in Akteure(n))
                {
                    if (!_akteurDom.TryGetValue(a, out var l)) _akteurDom[a] = l = [];
                    if (!l.Contains(d)) l.Add(d);
                }
            }
        }
        var x = (_akteurDom.GetValueOrDefault(name) ?? []).ToList();
        x.Sort(DomOrd);
        return x;
    }

    // ══ DOMÄNE: aus dem Graphen (das Aggregat, zu dem eine Karte gehört), sonst der Namespace ══
    private static bool IstAussenDom(string d) => d.StartsWith(ClientPraefix, StringComparison.Ordinal) || d == "§host";
    /// <summary>Ohne Domäne (die Ansicht setzt ggf. eine Heimat) — im Board <c>null</c>.</summary>
    public const string OhneDomaene = "§ohne";
    internal static int DomOrd(string a, string b) =>
        IstAussenDom(a) != IstAussenDom(b) ? (IstAussenDom(a) ? 1 : -1) : a == OhneDomaene ? 1 : b == OhneDomaene ? -1 : Locale(a, b);

    private string? AggDom(SichtKarte n)
    {
        var g = Block(n);
        return g != Geteilt && _aggregate.FirstOrDefault(x => S(x, "name") == g) is { } a && S(a, "namespace").Length > 0 ? S(a, "namespace") : null;
    }
    private static readonly HashSet<string> EinheitArten = ["handle"];
    private readonly Dictionary<string, string?> _einheit = new(StringComparer.Ordinal);
    private string? EinheitDom(SichtKarte n)
    {
        if (_einheit.TryGetValue(n.Id, out var v)) return v;
        var seen = new HashSet<string>(StringComparer.Ordinal) { n.Id };
        var seenListe = new List<string> { n.Id };
        var q = new List<string> { n.Id };
        var ds = new HashSet<string>(StringComparer.Ordinal);
        var dc = new HashSet<string>(StringComparer.Ordinal);
        List<string> Nachbarn(string id) => Ein(id).Concat(Aus(id)).ToList();
        while (q.Count > 0)
        {
            var id = q[^1];
            q.RemoveAt(q.Count - 1);
            foreach (var x in Nachbarn(id))
            {
                if (!KarteVon.TryGetValue(x, out var m)) continue;
                if (AggDom(m) is { } z) { ds.Add(z); if (m.Art == "command") dc.Add(z); continue; }
                if (EinheitArten.Contains(m.Art) && seen.Add(x)) { seenListe.Add(x); q.Add(x); }
            }
        }
        var d = ds.Count == 1 ? ds.First() : dc.Count == 1 ? dc.First() : null;
        foreach (var id in seenListe) _einheit[id] = d;
        return d;
    }
    private string? DatenDom(SichtKarte n, HashSet<string> besucht)
    {
        if (!besucht.Add(n.Id)) return null;
        var ds = new HashSet<string>(StringComparer.Ordinal);
        foreach (var x in Ein(n.Id).Concat(Aus(n.Id)))
            if (KarteVon.TryGetValue(x, out var m) && GraphDom(m, besucht) is { } z) ds.Add(z);
        return ds.Count == 1 ? ds.First() : null;
    }
    private string? GraphDom(SichtKarte n, HashSet<string>? besucht = null) =>
        AggDom(n) ?? (EinheitArten.Contains(n.Art) ? EinheitDom(n) : DatenDom(n, besucht ?? new HashSet<string>(StringComparer.Ordinal)));
    private static string? NsVon(SichtKarte n, Sicht s)
    {
        var r = n.Ref;
        switch (n.Art)
        {
            case "handle": case "fn": return n.Besitzer == null ? null : NsVon(n.Besitzer, s);
            case "decider": case "applier": case "state":
                return s._aggregate.FirstOrDefault(x => S(x, "name") == S(r, "aggregat")) is { } a ? Leer(S(a, "namespace")) : null;
        }
        return Leer(S(r, "namespace"));
    }
    private static string? Leer(string s) => s.Length > 0 ? s : null;

    private readonly Dictionary<string, string> _domaene = new(StringComparer.Ordinal);
    /// <summary>
    /// Die Domäne einer Karte (Rahmen): gehört sie laut Graph zu einem Aggregat, dessen Namespace; Handles dort, wo sie wirken; Datentypen
    /// folgen ihren Nachbarn; sonst der eigene Namespace; ohne alles <see cref="OhneDomaene"/>. Clients haben eigene Rahmen außerhalb der
    /// Domänen, ein Akteur steht in der ersten Domäne, in der er wirkt.
    /// </summary>
    public string Domaene(SichtKarte n)
    {
        if (_domaene.TryGetValue(n.Id, out var d)) return d;
        switch (n.Art)
        {
            case "client": return _domaene[n.Id] = ClientPraefix + S(n.Ref, "name");
            case "akteur":
            {
                // Was er hineingibt: seine Befugnisse; bei einer Funktion, die er rechnen darf, deren Ausgaben.
                var eig = L(n.Ref, "darf").SelectMany(nm => BausteinFunktion(nm) is { } fb ? Alle(fb["eingaenge"]).SelectMany(e => L(e, "ausgaben")) : [nm])
                    .Select(nm => KarteVon.GetValueOrDefault("rec:" + nm)).Where(x => x != null).Select(x => Domaene(x!)).Distinct().ToList();
                eig.Sort(DomOrd);
                var ds = eig.Count > 0 ? eig : AkteurDomaenen(n.Name);
                if (ds.Count > 0) return _domaene[n.Id] = ds[0];
                break;
            }
        }
        return _domaene[n.Id] = GraphDom(n) ?? NsVon(n, this) ?? OhneDomaene;
    }

    /// <summary>Die Rolle (Spalte im Block): Handles je Besitzer-Art, sonst die Art.</summary>
    public string Rolle(SichtKarte n) => n.Art == "handle" ? "handle:" + n.Besitzer!.Art : n.Art;

    // ══ EINKLAPPEN: Details (Decider, Applier, State, Ablehnung, VO, Enum, Response) stehen in ihrem Besitzer ══
    private static readonly HashSet<string> DetailArten = ["decider", "applier", "state", "rejection", "valueobject", "enum", "queryresponse"];
    /// <summary>Die direkten Besitzer einer Detail-Karte (aus der Verdrahtung, nie aus Namen).</summary>
    public List<string> DirekteBesitzer(SichtKarte n)
    {
        var r = n.Ref;
        string? Rec(string x) => x.Length > 0 && IstRec(x) ? "rec:" + x : null;
        string? Agg(string x) => x.Length > 0 && _aggregate.Any(a => S(a, "name") == x) ? "agg:" + x : null;
        return n.Art switch
        {
            "decider" => (Rec(S(r, "command")) ?? Agg(S(r, "aggregat"))) is { } a ? [a] : [],
            "applier" => (Rec(S(r, "event")) ?? Agg(S(r, "aggregat"))) is { } b ? [b] : [],
            "state" or "valueobject" or "enum" => Aus(n.Id).ToList(),
            "rejection" => Ein(n.Id).Where(x => x.StartsWith("dec:", StringComparison.Ordinal)).ToList(),
            "queryresponse" => Ein(n.Id).Where(x => x.StartsWith("rdr:", StringComparison.Ordinal)).ToList(),
            _ => [],
        };
    }
    private Dictionary<string, List<string>>? _vertreter;
    /// <summary>Kompakte Ansicht: die sichtbaren Vertreter einer Karte (transitiv durch eingeklappte Besitzer); eine sichtbare Karte vertritt sich selbst.</summary>
    public IReadOnlyList<string> Vertreter(string id)
    {
        _vertreter ??= new(StringComparer.Ordinal);
        if (_vertreter.TryGetValue(id, out var v)) return v;
        return Vert(id, new HashSet<string>(StringComparer.Ordinal));
    }
    private List<string> Vert(string id, HashSet<string> pfad)
    {
        if (_vertreter!.TryGetValue(id, out var memo)) return memo;
        if (!KarteVon.TryGetValue(id, out var n)) return [];
        if (!DetailArten.Contains(n.Art)) return _vertreter[id] = [id];
        if (!pfad.Add(id)) return [];
        var res = DirekteBesitzer(n).Where(x => x.Length > 0).SelectMany(o => Vert(o, pfad)).Distinct().ToList();
        pfad.Remove(id);
        return _vertreter[id] = res.Count > 0 ? res : [id];
    }
    /// <summary>Kompakt sichtbar (vor Ansichts-Filtern): nicht eingeklappt.</summary>
    internal bool Sichtbar(string id) => Vertreter(id) is [var v] && v == id;
    internal bool Eingeklappt(string id, bool details) => !details && !Sichtbar(id);

    /// <summary>Zusammengezogene Kanten der kompakten Ansicht: sichtbar →(eingeklappt)*→ sichtbar (nicht ins Aggregat, nicht doppelt zu einer direkten Kante).</summary>
    public List<(string, string)> Kontrakt()
    {
        var aus = new List<(string, string)>();
        var direkt = new HashSet<(string, string)>();
        foreach (var (a, bs) in _aus) foreach (var b in bs) { direkt.Add((a, b)); direkt.Add((b, a)); }
        foreach (var u in Karten.Select(k => k.Id).Where(Sichtbar))
        {
            var ziele = new List<string>();
            var gesehen = new HashSet<string>(StringComparer.Ordinal);
            var stack = Aus(u).Where(x => Eingeklappt(x, false)).ToList();
            while (stack.Count > 0)
            {
                var x = stack[^1];
                stack.RemoveAt(stack.Count - 1);
                if (!gesehen.Add(x)) continue;
                foreach (var y in Aus(x))
                {
                    if (!Eingeklappt(y, false)) { if (y != u && !ziele.Contains(y)) ziele.Add(y); }
                    else stack.Add(y);
                }
            }
            foreach (var v in ziele)
                if (KarteVon[v].Art != "aggregate" && !direkt.Contains((u, v))) aus.Add((u, v));
        }
        return aus;
    }

}

// Wer über die Kette: der Pfad, auf dem eine Nachricht im Namen eines Akteurs entsteht (für „👤 über die Kette“ im Panel).
public sealed partial class Sicht
{
    /// <summary>
    /// Die Regeln des Nachrichtenflusses: (Eingänge, Ausgänge, Akteur-Wechsel) — dieselben Kanten wie <c>DomainEditor.Nachrichtenfluss</c>. Darf ein
    /// Akteur eine Funktion draußen rechnen, ist er der Wechsel für ihre Ausgaben.
    /// </summary>
    private List<(List<string> Ein, List<string> Aus, string? Wechsel)> FlussRegeln()
    {
        var r = new List<(List<string>, List<string>, string?)>();
        static List<string> N(IEnumerable<string> xs) => xs.Where(x => x.Length > 0).ToList();
        foreach (var d in _decider) if (S(d, "command").Length > 0) r.Add(([S(d, "command")], N(Alle(d["ergibt"]).Select(o => S(o, "event"))), null));
        foreach (var p in _projektionen) foreach (var hd in Alle(p["handles"])) r.Add((N([S(hd, "event")]), [], null));   // eine Projektion gibt nichts aus
        foreach (var rd in _reader) foreach (var hd in Alle(rd["handles"])) r.Add((N([S(hd, "query")]), L(hd, "responses").ToList(), null));
        foreach (var f in _bausteinFunktionen) foreach (var e in Alle(f["eingaenge"])) r.Add((N([S(e, "eingang")]), L(e, "ausgaben").ToList(), null));
        foreach (var a in _akteure)
            foreach (var f in L(a, "darf").Select(BausteinFunktion).OfType<JsonObject>())
                foreach (var e in Alle(f["eingaenge"])) r.Add((N([S(e, "eingang")]), L(e, "ausgaben").ToList(), S(a, "name")));
        return r;
    }

    /// <summary>Ein Pfad, auf dem <paramref name="nm"/> im Namen von <paramref name="akt"/> entsteht (rückwärts bis zu dessen direktem Eingang bzw. Wechsel).</summary>
    private List<(List<string> Ein, List<string> Aus, string? Wechsel)>? _regeln;
    public List<string> KettenPfad(string nm, string akt)
    {
        var regeln = _regeln ??= FlussRegeln();
        var seen = new HashSet<string>(StringComparer.Ordinal);
        List<string>? Zurueck(string x, int tiefe)
        {
            if (tiefe > 10 || !seen.Add(x)) return null;
            if (_mengen.TryGetValue(x, out var m) && m.Direkt.Contains(akt)) return [x];
            foreach (var (ein, aus, w) in regeln)
            {
                if (!aus.Contains(x)) continue;
                if (w == akt) return ["⚙ " + akt, x];
                foreach (var e in ein)
                {
                    if (!MengeVon(e).Contains(akt)) continue;
                    if (Zurueck(e, tiefe + 1) is { } p) return [.. p, x];
                }
            }
            return null;
        }
        return Zurueck(nm, 0) ?? [nm];
    }
}
