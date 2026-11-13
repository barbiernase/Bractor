using System.Text.Json.Nodes;
using System.Text.RegularExpressions;

namespace GraphExtractor;

// Der BESITZ je Karte — die Hierarchie Anwendung → Blöcke → Glieder, nur aus den Signaturen (nie aus Namespace oder Name):
//   - Blöcke: das Aggregat, die Funktion (mit Eingängen oder gerufen), die Quellart, die Sicht (Anker: das Store-Bündel); dazu Begriffe und Rand der
//     Anwendung. Eine Meldung gehört der Quellart, die sie meldet.
//   - Ein Command gehört dem Aggregat, auf das er zielt; was eine Methode zurückgibt, dem Typ der Methode; eine Query dem, dessen Frage sie
//     nimmt; ein Typ, der nur als Feld bzw. im Konstruktor vorkommt, seinem einzigen Nutzer (mehrere → Begriffe); was von außen kommt
//     (Befugnis eines Akteurs ohne Erzeuger im Code), dem Rand.
public sealed partial class Sicht
{
    public const string Anwendung = "§anwendung";
    public const string Begriffe = "§begriffe";
    public const string Rand = "§rand";

    private static readonly HashSet<string> BlockArten = ["aggregate", "bausteinfunktion", "quellart", "store", "graph"];

    /// <summary>Die Graphen (Karten-Ids), die einen Knoten vom Typ <paramref name="typ"/> nennen (generische mit ihrem Typargument).</summary>
    private IEnumerable<string?> GraphenMitKnoten(string typ) => _graphen
        .Where(g => Alle(g["knoten"]).Any(k => (S(k, "typ").IndexOf('<') is > 0 and var i ? S(k, "typ")[..i] : S(k, "typ")) == typ))
        .Select(g => (string?)("gr:" + S(g, "_id"))).Distinct();
    private static readonly Regex Bezeichner = new(@"[A-Za-z_]\w*", RegexOptions.Compiled);

    private readonly Dictionary<string, string> _besitz = new(StringComparer.Ordinal);
    private readonly HashSet<string> _besitzInArbeit = new(StringComparer.Ordinal);
    private readonly HashSet<string> _mehrdeutig = new(StringComparer.Ordinal);

    /// <summary>Ist die Karte ein Block (Aggregat, Funktion, Sicht)?</summary>
    public static bool IstBlock(SichtKarte n) => BlockArten.Contains(n.Art);

    /// <summary>Hat die Karte keinen eindeutigen Besitzer (zwei Erzeuger, zwei Antwortende, zwei Stores) und liegt deshalb bei den Begriffen?</summary>
    public bool Mehrdeutig(SichtKarte n) { Besitzer(n); return _mehrdeutig.Contains(n.Id); }

    /// <summary>
    /// Der Besitzer einer Karte: die Id ihres Elternknotens (ein Block, ein Glied-Halter wie Projektion oder Reader) oder
    /// <see cref="Anwendung"/>, <see cref="Begriffe"/>, <see cref="Rand"/>.
    /// </summary>
    public string Besitzer(SichtKarte n)
    {
        if (_besitz.TryGetValue(n.Id, out var b)) return b;
        if (!_besitzInArbeit.Add(n.Id)) return Begriffe;   // ein Kreis über Felder: keiner besitzt ihn allein
        try { return _besitz[n.Id] = BesitzerOhneCache(n); }
        finally { _besitzInArbeit.Remove(n.Id); }
    }

    private string BesitzerOhneCache(SichtKarte n)
    {
        var r = n.Ref;
        var name = n.Name;
        switch (n.Art)
        {
            case "aggregate": case "store": return Anwendung;
            // Eine Funktion bzw. ein Graph, der als Knoten in genau einem Graphen steht, gehört diesem Graphen — so steht der Graph als Block mit
            //   seinen Knoten beisammen. Technik (gerufen) und Knoten mehrerer Graphen bleiben eigene Blöcke der Anwendung.
            case "bausteinfunktion": return IstGerufen(r) ? Anwendung : Einer(n, GraphenMitKnoten(name), leer: Anwendung, mehrdeutig: false);
            case "graph": return Einer(n, GraphenMitKnoten(name).Where(g => g != n.Id), leer: Anwendung, mehrdeutig: false);
            // Eine Quellart steht in keinem Graphen: ein eigener Block der Anwendung (wie Technik).
            case "quellart": return Anwendung;
            // Eine Meldung gehört der Quellart, die sie meldet (Signatur von Lausche) — eine ohne Quellart der Anwendung dem Rand.
            case "meldung": return Einer(n, MeldendeQuellarten(name).Select(QuellartKarte), leer: Rand);
            case "akteur": case "client": return Rand;
            case "state": case "decider": case "applier": return AggId(S(r, "aggregat")) ?? Begriffe;
            case "handle": return n.Besitzer?.Id ?? Begriffe;
            case "fn": return n.Besitzer?.Id ?? Begriffe;
            case "projektion": case "reader": return Einer(n, StoreScope(r).Select(StoreId));
            case "command":
            {
                if ((AggId(S(r, "aggregat")) ?? AggId(RecordAgg(name))) is { } agg) return agg;
                // Ein Command, den nur Knoten einander geben: er gehört der Funktion, die ihn ausgibt.
                static string Basis(string t) => t.IndexOf('<') is > 0 and var i ? t[..i] : t;   // Geglaettet<TFenster> → Geglaettet
                var erzeuger = _bausteinFunktionen.Where(f => Alle(f["eingaenge"]).Any(e => L(e, "ausgaben").Any(a => Basis(a) == name)))
                    .Select(f => (string?)("fb:" + S(f, "_id"))).ToList();
                if (erzeuger.Count > 0) return Einer(n, erzeuger);
                return VonAussen(name) ? Rand : Begriffe;
            }
            case "event": case "rejection":
            {
                var erzeuger = _decider.Where(d => Alle(d["ergibt"]).Any(o => S(o, "event") == name)).Select(d => AggId(S(d, "aggregat")))
                    .Concat(_bausteinFunktionen.Where(f => Alle(f["eingaenge"]).Any(e => L(e, "ausgaben").Contains(name))).Select(f => (string?)("fb:" + S(f, "_id"))))
                    .ToList();
                if (erzeuger.Count == 0) return VonAussen(name) ? Rand : Nutzer(n);
                return Einer(n, erzeuger);
            }
            case "query":
                return Einer(n, _aggregate.Where(g => Alle(g["fragen"]).Any(f => S(f, "query") == name)).Select(g => AggId(S(g, "name")))
                    .Concat(_reader.Where(x => Alle(x["handles"]).Any(h => S(h, "query") == name)).Select(x => (string?)("rdr:" + S(x, "_id")))),
                    leer: VonAussen(name) ? Rand : Begriffe);
            case "queryresponse":
            {
                var erzeuger = _aggregate.Where(g => Alle(g["fragen"]).Any(f => L(f, "antworten").Contains(name))).Select(g => AggId(S(g, "name")))
                    .Concat(_reader.Where(x => Alle(x["handles"]).Any(h => L(h, "responses").Contains(name))).Select(x => (string?)("rdr:" + S(x, "_id"))))
                    .ToList();
                return erzeuger.Count > 0 ? Einer(n, erzeuger) : Nutzer(n);
            }
            // Was nur als Feld, Parameter oder im Konstruktor vorkommt: Wertobjekt, Enum, Konfig, Read-Model, Hilfscode.
            default: return Nutzer(n);
        }
    }

    /// <summary>
    /// Genau ein Kandidat → er; keiner → <paramref name="leer"/>; mehrere → Begriffe — bei Erzeugern und Ankern <paramref name="mehrdeutig"/>
    /// (zwei Erzeuger, zwei Antwortende, zwei Stores sind offen), bei Nutzern nicht (ein gemeinsamer Begriff ist gewollt).
    /// </summary>
    private string Einer(SichtKarte n, IEnumerable<string?> kandidaten, string leer = Begriffe, bool mehrdeutig = true)
    {
        var k = kandidaten.Where(x => !string.IsNullOrEmpty(x)).Distinct().ToList();
        if (k.Count == 1) return k[0]!;
        if (k.Count > 1 && mehrdeutig) _mehrdeutig.Add(n.Id);
        return leer;
    }

    /// <summary>Kommt der Typ von außen: darf ein Akteur ihn hineingeben?</summary>
    private bool VonAussen(string name) => _akteure.Any(a => L(a, "darf").Contains(name));

    private string? AggId(string? aggregat) => string.IsNullOrEmpty(aggregat) ? null : KarteVon.ContainsKey("agg:" + aggregat) ? "agg:" + aggregat : null;
    private string? StoreId(string store) => _stores.FirstOrDefault(s => S(s, "name") == store) is { } s ? "sto:" + S(s, "_id") : null;

    /// <summary>
    /// Der Besitzer eines Typs, der nur genutzt wird (Feld, Parameter, Konstruktor): der Block seines einzigen Nutzers — bzw. der Nutzer selbst,
    /// wenn er ein Block ist; mehrere Nutzer → Begriffe (die gemeinsamen Begriffe der Anwendung).
    /// </summary>
    private string Nutzer(SichtKarte n)
    {
        var t = n.Name;
        bool Nutzt(string? typ) => typ != null && Bezeichner.Matches(typ).Any(m => m.Value == t);
        bool Felder(JsonNode? o, string k) => Alle(o?[k]).Any(f => Nutzt(S(f, "typ")));
        var nutzer = new List<string?>();
        string? BlockDes(string id) => KarteVon.TryGetValue(id, out var k) ? (IstBlock(k) ? k.Id : BlockVon(Besitzer(k))) : null;
        foreach (var r in _records.Where(r => S(r, "name") != t && Felder(r, "felder"))) nutzer.Add(BlockDes("rec:" + S(r, "name")));
        foreach (var rm in _readModels.Where(x => S(x, "name") != t && Felder(x, "felder"))) nutzer.Add(BlockDes("rm:" + S(rm, "_id")));
        foreach (var g in _aggregate.Where(g => Felder(g, "state"))) nutzer.Add("agg:" + S(g, "name"));
        foreach (var f in _bausteinFunktionen.Where(f => Felder(f, "konstruktor") || Alle(f["umsetzungen"]).Any(u => Felder(u, "konstruktor"))
                                                         || Alle(f["methoden"]).Any(m => Nutzt(S(m, "rueckgabe")) || Felder(m, "parameter"))))
            nutzer.Add("fb:" + S(f, "_id"));
        foreach (var q in _quellarten.Where(q => !B(q, "framework") && Felder(q, "konstruktor"))) nutzer.Add("qa:" + S(q, "_id"));
        foreach (var st in _stores.Where(s => Alle(s["writeFns"]).Concat(Alle(s["readFns"])).Any(f => Felder(f, "params") || Nutzt(S(f["sig"], "rueckgabe")))))
            nutzer.Add("sto:" + S(st, "_id"));
        return Einer(n, nutzer, mehrdeutig: false);
    }

    /// <summary>Der Block über einem Besitzer (Projektion/Reader → ihre Sicht, Glied → sein Block); Begriffe/Rand/Anwendung bleiben.</summary>
    private string? BlockVon(string besitzer)
    {
        if (besitzer.StartsWith('§')) return besitzer == Anwendung ? null : besitzer;
        if (!KarteVon.TryGetValue(besitzer, out var k)) return null;
        return IstBlock(k) ? k.Id : BlockVon(Besitzer(k));
    }

    /// <summary><c>board.sicht.besitz</c>: je Karte ihr Besitzer (Eltern-Id), mehrdeutige mit <c>!</c> markiert.</summary>
    private JsonObject BesitzJson()
    {
        var o = new JsonObject();
        foreach (var n in Karten) o[n.Id] = Besitzer(n) + (_mehrdeutig.Contains(n.Id) ? "!" : "");
        return o;
    }
}
