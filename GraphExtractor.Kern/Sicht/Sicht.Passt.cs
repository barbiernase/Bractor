using System.Text.Json.Nodes;
using System.Text.RegularExpressions;

namespace GraphExtractor;

// ⊕ „Was passt an diesen Port?“ — Typregel des Anschlusses und Grammatik (DomainEditor.Grammatik, als rahmen.grammatik im Board).
public sealed partial class Sicht
{
    /// <summary>Ein Port (Anschluss im Panel bzw. auf einer Karte): Schlüssel + seine Beschreibung (Typ, Richtung, Bezüge).</summary>
    public sealed record Port(string Key, JsonObject Info);

    /// <summary>Das Urteil je Kandidat: <c>passt</c> (verbindbar), <c>gesperrt</c> (typgleich, aber von der Grammatik verboten — mit Regel) oder nichts.</summary>
    public sealed record PasstUrteil(IReadOnlyList<string> Passt, IReadOnlyList<(string Key, string Regel, string Text)> Gesperrt);

    /// <summary>
    /// ⊕: welche Kandidaten-Ports zum Port <paramref name="q"/> passen. Typgleich (Anschluss-Regel) UND von der Grammatik erlaubt → passt;
    /// typgleich, aber verboten → gesperrt (mit der Regel). <paramref name="verbunden"/>: Kandidaten, die schon mit q verbunden sind — sie
    /// bleiben lösbar (keine Grammatik-Prüfung).
    /// </summary>
    public PasstUrteil Passt(Port q, IEnumerable<Port> kandidaten, IReadOnlySet<string> verbunden)
    {
        var passt = new List<string>();
        var gesperrt = new List<(string, string, string)>();
        foreach (var x in kandidaten)
        {
            if (!Kompatibel(q.Info, x.Info)) continue;
            var (o, i) = S(q.Info, "dir") == "out" ? (q.Info, x.Info) : (x.Info, q.Info);
            var v = verbunden.Contains(x.Key) ? null : GrPruefe(o, i);
            if (v is { } g) gesperrt.Add((x.Key, g.Regel, g.Text));
            else passt.Add(x.Key);
        }
        return new(passt, gesperrt);
    }

    // ── Anschluss-Regel (früher compatible() im Editor) ──
    private JsonObject? FlKnotenById(string id)
    {
        foreach (var f in _fluesse) foreach (var k in Alle(f["knoten"])) if (S(k, "_id") == id) return k;
        return null;
    }
    private JsonObject? FlFluss(JsonObject k) => _fluesse.FirstOrDefault(f => Alle(f["knoten"]).Any(x => ReferenceEquals(x, k)));
    private static bool FlErreicht(JsonObject f, string ziel, string von)
    {
        var seen = new HashSet<string>(StringComparer.Ordinal);
        var q = new Stack<string>([von]);
        while (q.Count > 0)
        {
            var x = q.Pop();
            if (x == ziel) return true;
            if (!seen.Add(x)) continue;
            if (FlKnoten(f, x) is { } k) foreach (var v in FlVor(k)) q.Push(v);
        }
        return false;
    }
    private bool IstColl(string ty) => ElementVonTyp(ty).Length > 0;
    private static bool IstGanzzahl(string ty) => Regex.IsMatch(ty.Trim(), @"^(int|long)\??$");
    private static bool Da(JsonObject o, string k) => o[k] is { } n && !(n is JsonValue v && v.TryGetValue<string>(out var s) && s.Length == 0);

    internal bool Kompatibel(JsonObject a, JsonObject b)
    {
        if (B(a, "anker") || B(b, "anker")) return false;
        if (S(a, "dir") == S(b, "dir")) return false;
        string T(JsonObject x) => S(x, "type");
        if (T(a) == "kette" || T(b) == "kette")
        {
            var o = T(a) == "kette" ? a : b;
            var i = ReferenceEquals(o, a) ? b : a;
            return S(o, "dir") == "out" && S(i, "dir") == "in" && (T(i) == "rufe" || (T(i) == "sagaCmd" && Wahr(i["rec"])));
        }
        if (T(a) != T(b)) return false;
        if (T(a) is "draht" or "jeEl" or "jeSa")
        {
            var o = S(a, "dir") == "out" ? a : b;
            var i = ReferenceEquals(o, a) ? b : a;
            var v = FlKnotenById(S(o, "flk"));
            var z = FlKnotenById(S(i, "flk"));
            var f = v == null ? null : FlFluss(v);
            if (v == null || z == null || f == null || !ReferenceEquals(FlFluss(z), f) || ReferenceEquals(v, z) || FlQuelleArt(z)
                || FlErreicht(f, S(z, "name"), S(v, "name"))) return false;
            if (T(a) == "draht")
                return S(z, "art") != "je" || !Alle(z["eingaenge"]).Any()
                       || Alle(z["eingaenge"]).Any(e => Alle(e["draehte"]).Any(d => S(d, "von") == S(v, "name")));
            return S(v, "art") == "je" && S(z, "art") != "je";
        }
        if (T(a) == "darf")
        {
            var o = S(a, "dir") == "out" ? a : b;
            var i = ReferenceEquals(o, a) ? b : a;
            if (Wahr(o["nurFunktion"]) != Wahr(i["funktion"])) return false;
        }
        if ((S(a, "kind") == "arg") != (S(b, "kind") == "arg")) return false;
        if (S(a, "kind") == "arg" && S(a, "trans") != S(b, "trans")) return false;
        if (T(a) == "field")
        {
            var src = S(a, "dir") == "out" ? a : b;
            var snk = S(a, "dir") == "out" ? b : a;
            if (S(snk, "want") == "count") return IstColl(S(src, "ftyp")) || IstGanzzahl(S(src, "ftyp"));
            if (S(snk, "want") == "collection") return IstColl(S(src, "ftyp"));
            return true;
        }
        return true;
    }

    // ── Grammatik (früher grPruefe() im Editor; dieselbe Tabelle wie DomainEditor.Grammatik) ──
    private JsonObject? Gr => _board["rahmen"]?["grammatik"] as JsonObject;
    private JsonObject GrRegel(string id) => Alle(Gr?["regeln"]).FirstOrDefault(r => S(r, "id") == id)
                                             ?? new JsonObject { ["id"] = id, ["name"] = id, ["build"] = "?" };
    private string GrText(string id) { var r = GrRegel(id); return "Regel »" + S(r, "name") + "« (" + S(r, "id") + "; Build: " + S(r, "build") + ")"; }
    private string GrName(string liste, string id) => Alle(Gr?[liste]).FirstOrDefault(y => S(y, "id") == id) is { } x ? S(x, "name") : id;
    private JsonObject? GrKonsum(string s, string b) => Alle(Gr?["konsume"]).FirstOrDefault(k => S(k, "sorte") == s && S(k, "baustein") == b);
    private JsonObject? GrErzeugung(string b, string s) => Alle(Gr?["erzeugungen"]).FirstOrDefault(e => S(e, "baustein") == b && S(e, "sorte") == s);
    private string GrRegelSorte(string s) => Alle(Gr?["konsume"]).FirstOrDefault(k => S(k, "sorte") == s) is { } k && S(k, "regel").Length > 0 ? S(k, "regel") : "GR-AUSGANG-GESCHLOSSEN";
    private string GrRegelBaustein(string b) => Alle(Gr?["erzeugungen"]).FirstOrDefault(e => S(e, "baustein") == b) is { } e && S(e, "regel").Length > 0 ? S(e, "regel") : "GR-AUSGANG-GESCHLOSSEN";
    private string? GrBaustein(JsonObject i)
    {
        foreach (var paar in (Gr?["portBaustein"] as JsonArray ?? []).OfType<JsonArray>())
            if (paar.Count == 2 && (string?)paar[0] is { } k && Da(i, k) && i[k] is not null) return (string?)paar[1];
        return null;
    }
    private string? GrNachrichtSorte(string name)
    {
        var g = Gr;
        if (RecByName(name) is not { } r) return null;
        var qb = S(g, "quellBasis");
        if (S(r, "kind") == "event" && (Wahr(r["treiber"]) || (qb.Length > 0 && L(r, "basen").Contains(qb)))) return "quelle";
        return S(g?["recordSorte"], S(r, "kind")) is { Length: > 0 } s ? s : null;
    }
    private List<(string? Dec, string? App, string? Reader, string Name)> GrKonsumenten(string name, string sorte, string baustein)
    {
        if (baustein == "aggregat" && sorte == "command")
            return _decider.Where(d => S(d, "command") == name).Select(d => ((string?)S(d, "_id"), (string?)null, (string?)null, "Decider (" + (S(d, "aggregat") is { Length: > 0 } a ? a : "?") + ")")).ToList();
        if (baustein == "aggregat" && sorte == "event")
            return _applier.Where(x => S(x, "event") == name).Select(x => ((string?)null, (string?)S(x, "_id"), (string?)null, "Applier (" + (S(x, "aggregat") is { Length: > 0 } a ? a : "?") + ")")).ToList();
        if (baustein == "reader")
            return _reader.Where(r => Alle(r["handles"]).Any(h => S(h, "query") == name)).Select(r => ((string?)null, (string?)null, (string?)S(r, "_id"), "Reader " + S(r, "name"))).ToList();
        return [];
    }
    private (string Regel, string Text) Verstoss(string regel, string text) => (regel, text + " — " + GrText(regel));

    /// <summary>Eine Verbindung Ausgang O → Eingang I gegen die Grammatik prüfen. null = erlaubt.</summary>
    internal (string Regel, string Text)? GrPruefe(JsonObject o, JsonObject i)
    {
        var g = Gr;
        if (g == null) return null;
        var orec = S(o, "rec");
        var irec = S(i, "rec");
        // (a) Nachricht → Baustein-Eingang: Sorte × Eingang, Kardinalität je Nachricht.
        if (orec.Length > 0 && irec.Length == 0)
        {
            var s = GrNachrichtSorte(orec);
            var b = GrBaustein(i);
            if (s == null || b == null) return null;
            var k = GrKonsum(s, b);
            if (k == null) return Verstoss(GrRegelSorte(s), GrName("sorten", s) + " '" + orec + "' passt nicht in einen " + GrName("bausteine", b) + "-Eingang");
            if (S(k, "kardinalitaet") == "eins")
            {
                var andere = GrKonsumenten(orec, s, b).Where(x => !((x.Dec != null && x.Dec == S(i, "dec")) || (x.App != null && x.App == S(i, "app"))
                                                                  || (x.Reader != null && x.Reader == S(i, "reader")))).ToList();
                if (andere.Count > 0)
                    return Verstoss(S(k, "regel"), GrName("sorten", s) + " '" + orec + "' hat schon einen Konsumenten: " + string.Join(", ", andere.Select(x => x.Name)));
            }
            return null;
        }
        // (b) Baustein-Ausgang → Nachricht: wer darf welche Sorte erzeugen.
        if (irec.Length > 0 && orec.Length == 0)
        {
            var b = GrBaustein(o);
            var s = GrNachrichtSorte(irec);
            if (b == null || s == null) return null;
            foreach (var (k, v) in Eintraege(g["ausgangSorte"] as JsonObject))
                if (Da(o, k) && o[k] is not null && (string?)v is { } vs) s = vs;
            if (GrErzeugung(b, s) == null)
                return Verstoss(GrRegelBaustein(b), GrName("bausteine", b) + " erzeugt kein " + GrName("sorten", s) + " ('" + irec + "')");
            return null;
        }
        // (c) Baustein → Baustein (Port-Sorte aus der Grammatik).
        if (S(g["portSorte"], S(o, "type")) is not { Length: > 0 } ps) return null;
        if (Strings(g["portGebrauch"]).Contains(S(o, "type")))
        {
            var bg = GrBaustein(o);
            if (bg == null || GrKonsum(ps, bg) != null) return null;
            return Verstoss(GrRegelSorte(ps), GrName("bausteine", bg) + " benutzt keine " + GrName("sorten", ps));
        }
        var bo = GrBaustein(o);
        var bi = GrBaustein(i);
        if (bo != null && GrErzeugung(bo, ps) == null) return Verstoss(GrRegelBaustein(bo), GrName("bausteine", bo) + " erzeugt kein " + GrName("sorten", ps));
        if (bi == null) return null;
        if (GrKonsum(ps, bi) == null) return Verstoss(GrRegelSorte(ps), GrName("sorten", ps) + " passt nicht in einen " + GrName("bausteine", bi) + "-Eingang");
        return null;
    }
}
