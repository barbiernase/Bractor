using System.Globalization;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;

namespace GraphExtractor;

/// <summary>Eine Karte des Editors: Id (= Knoten-Id im Editor, z. B. <c>rec:X</c>, <c>dec:decide:A/C</c>), Art, Name, die Board-Daten und
/// ggf. ihr Besitzer (Handle → Projektion/Reader/Reaktion, Fn → Store, Fluss-Knoten → Fluss, Zusage → Akteur).</summary>
public sealed record SichtKarte(string Id, string Art, string Name, JsonObject Ref, SichtKarte? Besitzer = null);

/// <summary>
/// Die ABLEITUNGEN des Boards (Werkbank P7.3, <c>docs/konzept-werkbank.md</c> §8.3): alles, was der Editor früher aus dem Board nachrechnete —
/// Karten und Kanten, Domäne, Block (Aggregat), Akteur-Mengen und Akteur-Rahmen, Store-Scope, Inseln, eingeklappte Details, die Kette eines
/// Prozesses, die statische Kette einer Karte, die Anschlussleiste der Clients und „was passt an diesen Port“ (⊕). Gerechnet wird NUR hier;
/// der Editor zeichnet, was ankommt (<c>board.sicht</c>, <c>/api/werkbank/kette</c>, <c>/api/werkbank/passt</c>).
/// <para>Eingabe ist das Board selbst (Code-Stand). Die Regeln sind die des früheren Editor-JS (dieselben Karten-Ids, dieselbe Reihenfolge der
/// Kanten — die Zuordnung von Datentypen folgt ihren Nachbarn in Kanten-Reihenfolge). Ansichts-Wünsche (Heimat neuer Karten, gewünschte
/// Akteur-Reihenfolge, ausgeblendete Arten) bleiben Sache der Ansicht; Code-Blöcke (📝/🤖) gibt es erst im Editor — sie folgen ihrem Besitzer.</para>
/// </summary>
public sealed partial class Sicht
{
    public const string Geteilt = "§geteilt";
    public const string AkteurBlock = "§akteure";
    public const string OhneAkteur = "§ohneAkteur";
    public const string ClientPraefix = "§client:";
    public const string FlussPraefix = "§fluss:";

    private readonly JsonObject _board;
    private readonly List<JsonObject> _records, _enums, _aggregate, _decider, _applier, _sagas, _states, _transitions, _funktionen, _fluesse,
        _akteure, _clients, _readModels, _stores, _projektionen, _reader, _reaktionen, _codeKarten;
    private readonly Dictionary<string, (HashSet<string> Direkt, HashSet<string> Kette)> _mengen = new(StringComparer.Ordinal);

    public IReadOnlyList<SichtKarte> Karten { get; }
    public IReadOnlyDictionary<string, SichtKarte> KarteVon { get; }
    public IReadOnlyList<(string Von, string Nach)> Kanten { get; }
    private readonly Dictionary<string, List<string>> _aus = new(StringComparer.Ordinal), _ein = new(StringComparer.Ordinal);

    private Sicht(JsonObject board)
    {
        _board = board;
        List<JsonObject> Liste(string k) => Alle(board[k]).ToList();
        _records = Liste("records"); _enums = Liste("enums"); _aggregate = Liste("aggregate"); _decider = Liste("decider");
        _applier = Liste("applier"); _sagas = Liste("sagas"); _funktionen = Liste("funktionen"); _fluesse = Liste("fluesse");
        _akteure = Liste("akteure"); _clients = Liste("clients"); _readModels = Liste("readModels"); _stores = Liste("stores");
        _projektionen = Liste("projektionen"); _reader = Liste("reader"); _reaktionen = Liste("reaktionen"); _codeKarten = Liste("codeKarten");
        // Wie der Editor das Board liest: je Prozess-Regel eine Transition (Prozess, Wenn, ein Dann), je Aggregat mit State eine State-Karte.
        _transitions = _sagas.SelectMany(s => Alle(s["schritte"]).Select(st => Transition(s, st))).ToList();
        _states = _aggregate.Where(a => Alle(a["state"]).Any()).Select(a => new JsonObject { ["_id"] = "state:" + S(a, "name"), ["aggregat"] = S(a, "name") }).ToList();
        foreach (var (n, m) in Eintraege(board["rahmen"]?["akteurMengen"] as JsonObject))
            _mengen[n] = (Strings(m?["direkt"]).ToHashSet(StringComparer.Ordinal), Strings(m?["kette"]).ToHashSet(StringComparer.Ordinal));

        Karten = BaueKarten();
        var von = new Dictionary<string, SichtKarte>(StringComparer.Ordinal);
        foreach (var k in Karten) von[k.Id] = k;   // wie im Editor: die letzte Karte einer Id gilt
        KarteVon = von;
        Kanten = BaueKanten();
        foreach (var (a, b) in Kanten)
        {
            if (a == b || !von.ContainsKey(a) || !von.ContainsKey(b)) continue;
            Dazu(_aus, a, b);
            Dazu(_ein, b, a);
        }
        static void Dazu(Dictionary<string, List<string>> m, string k, string v)
        {
            if (!m.TryGetValue(k, out var l)) m[k] = l = [];
            if (!l.Contains(v)) l.Add(v);
        }
    }

    public static Sicht Aus(JsonObject board) => new(board);

    private static JsonObject Transition(JsonObject saga, JsonObject st)
    {
        var dann = new JsonObject
        {
            ["sende"] = S(st, "sende"), ["sendeJe"] = B(st, "sendeJe"), ["kompensation"] = S(st, "kompensation"),
        };
        if (S(st, "rufe").Length > 0) dann["rufe"] = S(st, "rufe");
        if (st["zeitlimit"] is { } z) dann["zeitlimit"] = z.DeepClone();
        var t = new JsonObject
        {
            ["_id"] = S(st, "_id"), ["prozess"] = S(saga, "name"),
            ["wenn"] = new JsonArray(Strings(st["wenn"]).Select(x => (JsonNode?)x).ToArray()),
            ["dann"] = new JsonArray(dann),
        };
        if (S(st, "sammelEvent").Length > 0) t["sammelEvent"] = S(st, "sammelEvent");
        return t;
    }

    // ── JSON-Zugriff (JS-Wahrheitswerte: "" und fehlend sind falsch) ──
    internal static IEnumerable<JsonObject> Alle(JsonNode? n) => (n as JsonArray)?.OfType<JsonObject>() ?? [];
    internal static IEnumerable<KeyValuePair<string, JsonNode?>> Eintraege(JsonObject? o) => o ?? [];
    internal static string S(JsonNode? o, string k) => o?[k] is JsonValue v && v.TryGetValue<string>(out var s) ? s : "";
    internal static bool B(JsonNode? o, string k) => o?[k] is JsonValue v && v.TryGetValue<bool>(out var b) && b;
    internal static IEnumerable<string> Strings(JsonNode? n) =>
        (n as JsonArray)?.Select(x => x is JsonValue v && v.TryGetValue<string>(out var s) ? s : "") ?? [];
    internal static IEnumerable<string> L(JsonNode? o, string k) => Strings(o?[k]);
    /// <summary>Wahrheitswert wie in JS: fehlend, <c>null</c>, <c>false</c>, <c>0</c> und <c>""</c> sind falsch.</summary>
    internal static bool Wahr(JsonNode? n) => n switch
    {
        null => false,
        JsonValue v when v.TryGetValue<bool>(out var b) => b,
        JsonValue v when v.TryGetValue<string>(out var s) => s.Length > 0,
        JsonValue v when v.TryGetValue<double>(out var d) => d != 0,
        _ => true,
    };

    private Dictionary<string, JsonObject>? _recVon;
    private JsonObject? RecByName(string? n)
    {
        if (string.IsNullOrEmpty(n)) return null;
        if (_recVon == null)
        {
            _recVon = new(StringComparer.Ordinal);
            foreach (var r in _records) _recVon.TryAdd(S(r, "name"), r);   // wie find(): der erste Record eines Namens
        }
        return _recVon.GetValueOrDefault(n);
    }
    private bool IstRec(string? n) => RecByName(n) != null;

    // ── Karten (in der Reihenfolge des Editors) ──
    private static readonly Dictionary<string, string> FlSym = new()
    {
        ["quelle"] = "⛲", ["auf"] = "◆", ["funktion"] = "ƒ", ["command"] = "▶", ["je"] = "⧉", ["warte"] = "⧗",
    };
    internal static string ClientAnzeige(JsonObject c)
    {
        var n = S(c, "name");
        return n.Length > 1 && n[0] == 'I' && n[1].ToString() != n[1].ToString().ToLowerInvariant() ? n[1..] : n;
    }
    internal static string AufId(JsonObject a, int i) => "auf:" + S(a, "_id") + ":" + i;
    private IEnumerable<(JsonObject A, JsonObject R, int I, string Id)> VertragsZusagen() =>
        _akteure.SelectMany(a => Alle(a["vertrag"]).Select((r, i) => (a, r, i, AufId(a, i))));
    internal static string HandleDisc(JsonObject hd) =>
        new[] { S(hd, "query"), S(hd, "event"), S(hd, "selfName"), S(hd, "input") }.FirstOrDefault(x => x.Length > 0) ?? "";
    private static readonly (string Art, string Sammlung, string Praefix)[] HandleArten =
        [("projektion", "projektionen", "prj"), ("reader", "reader", "rdr"), ("reaktion", "reaktionen", "rk")];
    internal static List<string> HandleIds(JsonObject o, string oid)
    {
        var seen = new HashSet<string>(StringComparer.Ordinal);
        return Alle(o["handles"]).Select((hd, hi) =>
        {
            var d = HandleDisc(hd);
            var id = "hd:" + oid + ":" + (d.Length > 0 ? d : "#" + hi);
            if (seen.Contains(id)) id += "#" + hi;
            seen.Add(id);
            return id;
        }).ToList();
    }
    private List<JsonObject> SammlungVon(string sammlung) => sammlung switch
    {
        "projektionen" => _projektionen, "reader" => _reader, "reaktionen" => _reaktionen, _ => [],
    };

    private List<SichtKarte> BaueKarten()
    {
        var k = new List<SichtKarte>();
        foreach (var a in _akteure) k.Add(new("akt:" + S(a, "_id"), "akteur", S(a, "name"), a));
        foreach (var c in _clients) k.Add(new("cl:" + S(c, "_id"), "client", ClientAnzeige(c), c));
        foreach (var (a, r, _, id) in VertragsZusagen())
            k.Add(new(id, "auf", S(r, "eingang") is { Length: > 0 } e ? e : "?", r, new SichtKarte("akt:" + S(a, "_id"), "akteur", S(a, "name"), a)));
        foreach (var r in _records) k.Add(new("rec:" + S(r, "name"), S(r, "kind"), S(r, "name"), r));
        foreach (var a in _aggregate) k.Add(new("agg:" + S(a, "name"), "aggregate", S(a, "name"), a));
        foreach (var s in _states) k.Add(new("st:" + S(s, "_id"), "state", S(s, "aggregat") is { Length: > 0 } x ? x : "frei", s));
        foreach (var d in _decider) k.Add(new("dec:" + S(d, "_id"), "decider", S(d, "command"), d));
        foreach (var a in _applier) k.Add(new("app:" + S(a, "_id"), "applier", S(a, "event"), a));
        foreach (var e in _enums) k.Add(new("enum:" + S(e, "name"), "enum", S(e, "name"), e));
        foreach (var s in _sagas) k.Add(new("saga:" + S(s, "name"), "saga", S(s, "name"), s));
        foreach (var t in _transitions)
            k.Add(new("tr:" + S(t, "_id"), "transition",
                string.Join(", ", Alle(t["dann"]).Select(d => S(d, "rufe") is { Length: > 0 } r ? r : S(d, "sende")).Where(x => x.Length > 0)), t));
        foreach (var f in _funktionen) k.Add(new("fk:" + S(f, "_id"), "funktion", S(f, "name"), f));
        foreach (var rm in _readModels) k.Add(new("rm:" + S(rm, "_id"), "readmodel", S(rm, "name"), rm));
        foreach (var s in _stores) k.Add(new("sto:" + S(s, "_id"), "store", S(s, "name"), s));
        foreach (var p in _projektionen) k.Add(new("prj:" + S(p, "_id"), "projektion", S(p, "name"), p));
        foreach (var r in _reader) k.Add(new("rdr:" + S(r, "_id"), "reader", S(r, "name"), r));
        foreach (var r in _reaktionen) k.Add(new("rk:" + S(r, "_id"), "reaktion", S(r, "name"), r));
        foreach (var c in _codeKarten) k.Add(new(S(c, "_id"), "code", S(c, "name"), c));
        foreach (var f in _fluesse)
        {
            var fl = new SichtKarte("fl:" + S(f, "_id"), "fluss", S(f, "name"), f);
            foreach (var kn in Alle(f["knoten"]))
                k.Add(new("fs:" + S(kn, "_id"), "flussknoten", (FlSym.GetValueOrDefault(S(kn, "art")) ?? "") + " " + S(kn, "name"), kn, fl));
        }
        foreach (var (art, sammlung, pre) in HandleArten)
            foreach (var o in SammlungVon(sammlung))
            {
                var oid = pre + ":" + S(o, "_id");
                var ids = HandleIds(o, oid);
                var own = new SichtKarte(oid, art, S(o, "name"), o);
                foreach (var (hd, hi) in Alle(o["handles"]).Select((h, i) => (h, i)))
                    k.Add(new(ids[hi], "handle", HandleDisc(hd) is { Length: > 0 } d ? d : "(neu)", hd, own));
            }
        foreach (var st in _stores)
        {
            var own = new SichtKarte("sto:" + S(st, "_id"), "store", S(st, "name"), st);
            foreach (var f in Alle(st["writeFns"]).Concat(Alle(st["readFns"])))
                k.Add(new("fn:" + S(f, "_id"), "fn", S(f, "name") is { Length: > 0 } n ? n : "?", f, own));
        }
        return k;
    }

    // ── Hilfen über das Board ──
    private Dictionary<string, (JsonObject Store, JsonObject Fn, bool Lesen)>? _fnVon;
    internal (JsonObject Store, JsonObject Fn, bool Lesen)? FnById(string id)
    {
        if (_fnVon == null)
        {
            _fnVon = new(StringComparer.Ordinal);
            foreach (var st in _stores)
            {
                foreach (var w in Alle(st["writeFns"])) _fnVon.TryAdd(S(w, "_id"), (st, w, false));
                foreach (var r in Alle(st["readFns"])) _fnVon.TryAdd(S(r, "_id"), (st, r, true));
            }
        }
        return _fnVon.TryGetValue(id, out var x) ? x : null;
    }
    private string FaehigkeitVorschlag(JsonObject st, JsonObject fn)
    {
        var belegt = new HashSet<string>(StringComparer.Ordinal);
        foreach (var s in _stores)
            foreach (var f in Alle(s["writeFns"]).Concat(Alle(s["readFns"])))
                if (!ReferenceEquals(f, fn) && S(f, "faehigkeit").Length > 0) belegt.Add(S(f, "faehigkeit"));
        var n = S(fn, "name");
        var kern = n.Length > 5 && n.EndsWith("Async", StringComparison.Ordinal) ? n[..^5] : n;
        static string OhneI(string x) => Regex.IsMatch(x, "^I[A-Z]") ? x[1..] : x;
        var v = "I" + kern;
        if (belegt.Contains(v)) v = "I" + kern + OhneI(S(st, "name"));
        return v;
    }
    private (JsonObject Store, JsonObject Fn)? LeseFnZu(string typ)
    {
        foreach (var st in _stores)
            foreach (var fn in Alle(st["readFns"]))
            {
                var f = S(fn, "faehigkeit") is { Length: > 0 } x ? x : fn["sig"] != null ? null : FaehigkeitVorschlag(st, fn);
                if (f == typ) return (st, fn);
            }
        return null;
    }
    private static readonly Regex EntwurfSammlung = new(@"^(?:List|IReadOnlyList|IEnumerable)<(.+)>\??$|^(.+)\[\]\??$");
    private Dictionary<string, string>? _elem;
    internal IEnumerable<(string Owner, JsonObject F)> AlleFelder()
    {
        foreach (var r in _records) foreach (var f in Alle(r["felder"])) yield return (S(r, "name"), f);
        foreach (var a in _aggregate) foreach (var f in Alle(a["state"])) yield return (S(a, "name"), f);
        foreach (var rm in _readModels) foreach (var f in Alle(rm["felder"])) yield return (S(rm, "name"), f);
    }
    private static string ElementVon(JsonObject? f)
    {
        if (f == null) return "";
        if (S(f, "elementTyp").Length > 0) return S(f, "elementTyp");
        var m = EntwurfSammlung.Match(S(f, "typ").Trim());
        return m.Success ? (m.Groups[1].Success ? m.Groups[1].Value : m.Groups[2].Value).Trim() : "";
    }
    internal string ElementVonTyp(string ty)
    {
        if (_elem == null)
        {
            _elem = new(StringComparer.Ordinal);
            foreach (var (_, f) in AlleFelder())
                if (S(f, "elementTyp").Length > 0) _elem.TryAdd(S(f, "typ"), S(f, "elementTyp"));
        }
        return _elem.GetValueOrDefault(ty) ?? ElementVon(new JsonObject { ["typ"] = ty });
    }
    internal string InnerTyp(string? ty)
    {
        var e = ElementVonTyp(ty ?? "");
        var t = (e.Length > 0 ? e : ty ?? "").Trim();
        if (t.EndsWith('?')) t = t[..^1];
        return t.Trim();
    }
    private static List<string> FlWarteAuf(JsonObject k)
    {
        var w = L(k, "warteAuf").ToList();
        return (w.Count > 0 ? w : [S(k, "typ")]).Where(x => x.Length > 0).ToList();
    }
    private static List<string> FlHoert(JsonObject k) => S(k, "art") switch
    {
        "auf" => S(k, "typ").Length > 0 ? [S(k, "typ")] : [],
        "warte" => FlWarteAuf(k),
        _ => [],
    };
    internal static bool FlQuelleArt(JsonObject? k) => k != null && S(k, "art") is "quelle" or "auf";
    internal static List<string> FlVor(JsonObject k)
    {
        var l = new List<string>();
        foreach (var e in Alle(k["eingaenge"]))
        {
            foreach (var d in Alle(e["draehte"])) l.Add(S(d, "von"));
            if (S(e, "je").Length > 0) l.Add(S(e, "je"));
        }
        return l.Distinct(StringComparer.Ordinal).ToList();
    }
    internal static JsonObject? FlKnoten(JsonObject f, string name) => Alle(f["knoten"]).FirstOrDefault(k => S(k, "name") == name);
    private string? FlZuweisung(JsonObject k)
    {
        var art = S(k, "art");
        if (art is "quelle" or "command" && S(k, "typ").Length > 0) return "rec:" + S(k, "typ");
        if (art == "funktion") return _funktionen.FirstOrDefault(x => S(x, "name") == S(k, "typ")) is { } fk ? "fk:" + S(fk, "_id") : null;
        return null;
    }

    // ── Kanten (Knoten-Id → Knoten-Id), in der Reihenfolge des Editors; Code-Blöcke (📝/🤖) gibt es hier nicht ──
    private List<(string, string)> BaueKanten()
    {
        var e = new List<(string, string)>();
        void P(string? a, string? b) { if (!string.IsNullOrEmpty(a) && !string.IsNullOrEmpty(b)) e.Add((a, b)); }
        static string Rec(string n) => "rec:" + n;
        foreach (var a in _akteure)
            foreach (var nm in L(a, "darf"))
            {
                if (IstRec(nm)) P("akt:" + S(a, "_id"), Rec(nm));
                else if (_funktionen.FirstOrDefault(x => S(x, "name") == nm) is { } fk) P("akt:" + S(a, "_id"), "fk:" + S(fk, "_id"));
            }
        foreach (var (a, r, _, id) in VertragsZusagen())
        {
            P(id, "akt:" + S(a, "_id"));
            if (IstRec(S(r, "eingang"))) P(Rec(S(r, "eingang")), id);
            foreach (var c in L(r, "ausgaenge")) if (IstRec(c)) P(id, Rec(c));
        }
        foreach (var d in _decider)
        {
            var did = "dec:" + S(d, "_id");
            if (IstRec(S(d, "command"))) P(Rec(S(d, "command")), did);
            if (S(d, "aggregat").Length > 0) P(did, "agg:" + S(d, "aggregat"));
            foreach (var o in Alle(d["ergibt"])) if (IstRec(S(o, "event"))) P(did, Rec(S(o, "event")));
        }
        foreach (var f in _fluesse)
            foreach (var z in Alle(f["knoten"]))
            {
                var zid = "fs:" + S(z, "_id");
                foreach (var v in FlVor(z)) if (FlKnoten(f, v) is { } q) P("fs:" + S(q, "_id"), zid);
                if (FlZuweisung(z) is { } zw) { if (FlQuelleArt(z)) P(zw, zid); else P(zid, zw); }
                foreach (var ein in Alle(z["eingaenge"]))
                    foreach (var d in Alle(ein["draehte"]))
                        if ((S(d, "port") is { Length: > 0 } p ? p : "fall") == "fall" && S(d, "fall").Length > 0 && IstRec(S(d, "fall"))) P(Rec(S(d, "fall")), zid);
                foreach (var nm in FlHoert(z)) if (IstRec(nm)) P(Rec(nm), zid);
            }
        foreach (var a in _applier)
        {
            var aid = "app:" + S(a, "_id");
            if (IstRec(S(a, "event"))) P(Rec(S(a, "event")), aid);
            if (S(a, "aggregat").Length > 0) P(aid, "agg:" + S(a, "aggregat"));
        }
        foreach (var s in _states) if (S(s, "aggregat").Length > 0) P("st:" + S(s, "_id"), "agg:" + S(s, "aggregat"));
        foreach (var s in _sagas) if (IstRec(S(s, "triggerEvent"))) P(Rec(S(s, "triggerEvent")), "saga:" + S(s, "name"));
        foreach (var t in _transitions)
        {
            var tid = "tr:" + S(t, "_id");
            if (S(t, "prozess").Length > 0) P(tid, "saga:" + S(t, "prozess"));
            foreach (var w in L(t, "wenn")) if (IstRec(w)) P(Rec(w), tid);
            foreach (var d in Alle(t["dann"]))
            {
                if (IstRec(S(d, "sende"))) P(tid, Rec(S(d, "sende")));
                if (IstRec(S(d, "kompensation"))) P(tid, Rec(S(d, "kompensation")));
                if (S(d, "rufe").Length > 0 && _funktionen.FirstOrDefault(f => S(f, "name") == S(d, "rufe")) is { } fk) P(tid, "fk:" + S(fk, "_id"));
            }
        }
        foreach (var f in _funktionen)
        {
            var fid = "fk:" + S(f, "_id");
            if (IstRec(S(f, "auftrag"))) P(Rec(S(f, "auftrag")), fid);
            foreach (var r in L(f, "ergebnisse")) if (IstRec(r)) P(fid, Rec(r));
            foreach (var p in Alle(f["faehigkeiten"])) if (LeseFnZu(S(p, "typ")) is { } z) P(fid, "fn:" + S(z.Fn, "_id"));
        }
        foreach (var rm in _readModels)
            if (_stores.FirstOrDefault(s => S(s, "name") == S(rm, "store")) is { } st) P("rm:" + S(rm, "_id"), "sto:" + S(st, "_id"));
        foreach (var st in _stores)
            foreach (var fn in Alle(st["writeFns"]).Concat(Alle(st["readFns"]))) P("fn:" + S(fn, "_id"), "sto:" + S(st, "_id"));
        // Handles: Hub-Kante Handle → Besitzer; Eingang → Handle → Ausgänge.
        var hid = new Dictionary<JsonObject, string>(ReferenceEqualityComparer.Instance);
        foreach (var (_, sammlung, pre) in HandleArten)
            foreach (var o in SammlungVon(sammlung))
            {
                var ids = HandleIds(o, pre + ":" + S(o, "_id"));
                foreach (var (hd, hi) in Alle(o["handles"]).Select((h, i) => (h, i))) hid[hd] = ids[hi];
            }
        foreach (var p in _projektionen)
            foreach (var hd in Alle(p["handles"]))
            {
                var h = hid[hd];
                P(h, "prj:" + S(p, "_id"));
                if (IstRec(S(hd, "event"))) P(Rec(S(hd, "event")), h);
                foreach (var fid in L(hd, "fns")) if (FnById(fid) != null) P(h, "fn:" + fid);
                foreach (var ev in L(hd, "publishes")) if (IstRec(ev)) P(h, Rec(ev));
            }
        foreach (var r in _reader)
        {
            if (S(r, "projektion").Length > 0 && _projektionen.FirstOrDefault(x => S(x, "name") == S(r, "projektion")) is { } p) P("rdr:" + S(r, "_id"), "prj:" + S(p, "_id"));
            foreach (var hd in Alle(r["handles"]))
            {
                var h = hid[hd];
                P(h, "rdr:" + S(r, "_id"));
                if (IstRec(S(hd, "query"))) P(Rec(S(hd, "query")), h);
                foreach (var fid in L(hd, "fns")) if (FnById(fid) != null) P(h, "fn:" + fid);
                foreach (var resp in L(hd, "responses")) if (IstRec(resp)) P(h, Rec(resp));
            }
        }
        foreach (var r in _reaktionen)
            foreach (var hd in Alle(r["handles"]))
            {
                var h = hid[hd];
                P(h, "rk:" + S(r, "_id"));
                if (IstRec(S(hd, "event"))) P(Rec(S(hd, "event")), h);
                foreach (var c in L(hd, "sends")) if (IstRec(c)) P(h, Rec(c));
                foreach (var ev in L(hd, "publishes")) if (IstRec(ev)) P(h, Rec(ev));
            }
        // Store-API: Records in Fn-Parametern/-Rückgaben gehören an ihre Fn.
        foreach (var st in _stores)
            foreach (var fn in Alle(st["writeFns"]).Concat(Alle(st["readFns"])))
                foreach (var t in new[] { S(fn, "rueckgabe") }.Concat(Alle(fn["params"]).Select(x => S(x, "typ"))))
                    foreach (Match m in Regex.Matches(t, @"[A-Za-z_]\w*"))
                        if (RecByName(m.Value) is { } r && S(r, "kind") != "command") P(Rec(m.Value), "fn:" + S(fn, "_id"));
        // Typ-Komposition: VO/Enum → Feld-Owner (Record/Aggregat/ReadModel).
        var vo = _records.Where(r => S(r, "kind") == "valueobject").Select(r => S(r, "name")).ToHashSet(StringComparer.Ordinal);
        var en = _enums.Select(x => S(x, "name")).ToHashSet(StringComparer.Ordinal);
        string? OwnerId(string o) =>
            _records.Any(r => S(r, "name") == o) ? "rec:" + o
            : _aggregate.Any(a => S(a, "name") == o) ? "agg:" + o
            : _readModels.FirstOrDefault(rm => S(rm, "name") == o) is { } rm ? "rm:" + S(rm, "_id")
            : _states.Any(s => S(s, "_id") == o) ? "st:" + o : null;
        foreach (var (owner, f) in AlleFelder())
        {
            var bt = InnerTyp(S(f, "typ"));
            if (OwnerId(owner) is not { } oid) continue;
            if (vo.Contains(bt)) P("rec:" + bt, oid);
            else if (en.Contains(bt)) P("enum:" + bt, oid);
        }
        return e;
    }

    internal IReadOnlyList<string> Aus(string id) => _aus.GetValueOrDefault(id) ?? (IReadOnlyList<string>)[];
    internal IReadOnlyList<string> Ein(string id) => _ein.GetValueOrDefault(id) ?? (IReadOnlyList<string>)[];

    /// <summary>Sortierung wie <c>localeCompare</c> im Browser (ICU, nicht ordinal).</summary>
    internal static int Locale(string a, string b) => string.Compare(a, b, CultureInfo.InvariantCulture, CompareOptions.None);
}
