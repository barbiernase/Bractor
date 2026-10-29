namespace DomainEditor;

/// <summary>Ein Knoten des Nachrichtenflusses: ein Baustein (Art aus <see cref="Grammatik"/>) oder eine Nachricht (Art „nachricht").</summary>
public sealed record FlussKnoten(string Id, string Art, string Name, string? Namespace, string? Sorte = null);

/// <summary>
/// Eine Kante des Nachrichtenflusses: Erzeuger → Nachricht (Ausgang) oder Nachricht → Konsument (Eingang). <see cref="Sorte"/> ist die
/// Sorte, die über DIESE Kante fließt (eine Frist liefert einen Command, fließt aber als Sorte Frist). <see cref="Handle"/> = der
/// Eingang des Handles, an dem die Kante hängt (für Meldungen), sonst null.
/// </summary>
public sealed record FlussKante(string Von, string Nach, string Nachricht, string Sorte, string? Handle = null);

/// <summary>
/// Der NACHRICHTENFLUSS des Modells — die Grundlage, auf der die Modul-Schnittstellen und die Akteur-Anteile abgeleitet werden.
/// Nur Signatur-Fakten: Decide-OneOf (mit Fristen), Apply-Eingang, je <c>Auf</c> einer Funktion Eingang/Ausgaben/Fähigkeiten,
/// Handle-Eingang/-Ausgänge/-Fähigkeiten der Leseseite, Befugnisse der Akteure.
/// Bausteine: <c>agg:</c> Aggregat, <c>fn:</c> Funktion, <c>kon:</c> Projektion, <c>rdr:</c> Reader, <c>sto:</c> Store, <c>akt:</c> Akteur,
/// <c>aussen</c> Außenwelt (unbekannter Client oder Treiber). Nachrichten: <c>msg:</c> Record-Name, <c>faeh:</c> Fähigkeit.
/// </summary>
public sealed class Nachrichtenfluss
{
    public IReadOnlyList<FlussKnoten> Knoten { get; }
    public IReadOnlyList<FlussKante> Kanten { get; }
    private readonly Dictionary<string, FlussKnoten> _knoten;

    private Nachrichtenfluss(Dictionary<string, FlussKnoten> knoten, List<FlussKante> kanten)
    {
        _knoten = knoten;
        Knoten = knoten.Values.ToList();
        Kanten = kanten;
    }

    public FlussKnoten? KnotenVon(string id) => _knoten.GetValueOrDefault(id);
    public IEnumerable<FlussKante> Aus(string id) => Kanten.Where(k => k.Von == id);
    public IEnumerable<FlussKante> Ein(string id) => Kanten.Where(k => k.Nach == id);

    public const string AussenId = "aussen";
    private static readonly string FristName = typeof(Abstractions.Frist<>).Name.Split('`')[0];

    /// <summary>Den Nachrichtenfluss aus dem Modell ableiten.</summary>
    public static Nachrichtenfluss Aus(EditorModell m)
    {
        var knoten = new Dictionary<string, FlussKnoten>(StringComparer.Ordinal);
        var kanten = new List<FlussKante>();
        var recs = m.Records.GroupBy(r => r.Name, StringComparer.Ordinal).ToDictionary(g => g.Key, g => g.First(), StringComparer.Ordinal);
        var aggNs = m.Aggregate.GroupBy(a => a.Name).ToDictionary(g => g.Key, g => g.First().Namespace, StringComparer.Ordinal);

        string B(string praefix, string art, string name, string? ns)
        {
            var id = praefix + ":" + name;
            knoten.TryAdd(id, new FlussKnoten(id, art, name, ns));
            return id;
        }
        // Nachricht: Sorte aus der Record-Art (oder der Hinweis-Sorte, wenn es keinen Record gibt), Namespace des Records bzw. des Bausteins.
        string? N(string name, string? sorteOhneRecord, string? nsOhneRecord)
        {
            if (name.Length == 0) return null;
            var sorte = recs.TryGetValue(name, out var r) ? Grammatik.SorteVon(r) : sorteOhneRecord;
            if (sorte == null) return null;
            var id = "msg:" + name;
            knoten.TryAdd(id, new FlussKnoten(id, "nachricht", name, r?.Namespace ?? nsOhneRecord, sorte));
            return id;
        }
        string SorteVon(string msgId) => knoten[msgId].Sorte!;
        void K(string? von, string? nach, string? handle = null, string? sorte = null)
        {
            if (von == null || nach == null) return;
            var msg = von.StartsWith("msg:", StringComparison.Ordinal) || von.StartsWith("faeh:", StringComparison.Ordinal) ? von : nach;
            var k = new FlussKante(von, nach, knoten[msg].Name, sorte ?? SorteVon(msg), handle);
            if (!kanten.Contains(k)) kanten.Add(k);
        }

        // Jede Nachricht ist ein Knoten — auch unverdrahtet (eine skizzierte Schnittstelle: offener Port).
        foreach (var r in m.Records) N(r.Name, null, null);

        // Aggregat: Command → Decide → Events (eine Frist fließt als Sorte Frist zum Command an dasselbe Aggregat); Event → Apply.
        foreach (var a in m.Aggregate) B("agg", Grammatik.Aggregat, a.Name, a.Namespace);
        foreach (var d in m.Decider.Where(d => d.Aggregat.Length > 0))
        {
            var agg = B("agg", Grammatik.Aggregat, d.Aggregat, aggNs.GetValueOrDefault(d.Aggregat));
            K(N(d.Command, null, null), agg, d.Command);
            foreach (var e in d.Ergibt)
                if (Generisch(e.Event) is ({ } huelle, { } cmd) && huelle == FristName) K(agg, N(cmd, null, null), d.Command, Grammatik.Frist);
                else K(agg, N(e.Event, null, null), d.Command);
        }
        foreach (var ap in m.Applier.Where(a => a.Aggregat.Length > 0))
            K(N(ap.Event, null, null), B("agg", Grammatik.Aggregat, ap.Aggregat, aggNs.GetValueOrDefault(ap.Aggregat)), ap.Event);

        // Funktion: je Auf Eingang → Funktion → Ausgaben (nur die Signatur); der Handle ist der Eingang.
        foreach (var f in m.BausteinFunktionen)
        {
            var fid = B("fn", Grammatik.Funktion, f.Name, f.Namespace);
            foreach (var e in f.Eingaenge)
            {
                K(N(e.Eingang, e.Hinweis ? Grammatik.Transient : Grammatik.Event, f.Namespace), fid, e.Eingang);
                foreach (var a in e.Ausgaben) K(fid, N(a, null, null), e.Eingang);
            }
        }

        var lesen = m.Lesen;
        if (lesen != null)
        {
            // Store → Fähigkeit (der Store stellt sie bereit); Fähigkeit → Handle bzw. Funktion (sie darf sie).
            var faehVon = new Dictionary<string, string>(StringComparer.Ordinal);
            foreach (var st in lesen.Stores)
            {
                var sto = B("sto", Grammatik.Store, st.Name, st.Namespace);
                foreach (var f in st.Fns)
                {
                    var id = "faeh:" + f.Name;
                    knoten.TryAdd(id, new FlussKnoten(id, "nachricht", f.Name, st.Namespace, Grammatik.Faehigkeit));
                    faehVon.TryAdd(f.Name, id);
                    K(sto, id);
                }
            }
            string? F(Parameter p) => faehVon.GetValueOrDefault(Basisname(p.Typ));

            foreach (var k in lesen.Konsumenten)
            {
                var kid = B("kon", Grammatik.Projektion, k.Name, k.Namespace);
                foreach (var h in k.Handles)
                {
                    K(N(h.Eingang, Grammatik.Event, k.Namespace), kid, h.Eingang);
                    foreach (var a in h.Ausgaenge) K(kid, N(a, null, null), h.Eingang);
                    foreach (var p in h.Faehigkeiten) K(F(p), kid, h.Eingang);
                }
            }
            foreach (var r in lesen.Reader)
            {
                var rid = B("rdr", Grammatik.Reader, r.Name, r.Namespace);
                foreach (var h in r.Handles)
                {
                    K(N(h.Eingang, Grammatik.Query, r.Namespace), rid, h.Eingang);
                    foreach (var a in h.Ausgaenge) K(rid, N(a, Grammatik.Response, r.Namespace), h.Eingang);
                    foreach (var p in h.Faehigkeiten) K(F(p), rid, h.Eingang);
                }
            }
            // Funktion mit Lese-Fähigkeit: Fähigkeit → Funktion (sie liest je Eingang).
            foreach (var f in m.BausteinFunktionen)
                foreach (var e in f.Eingaenge)
                    foreach (var p in e.Faehigkeiten) K(F(p), "fn:" + f.Name, e.Eingang);
        }

        // Akteure: Akteur → was er darf (IDarf<T>). Sie ersetzen für diese Nachrichten die anonyme Außenwelt. Darf er eine Funktion rechnen
        //   (IDarf<F>, F eine Schnittstelle), gibt er ihre Ausgaben hinein — im Namen der Funktion, deren Eingang der Handle ist.
        var funktionen = m.BausteinFunktionen.GroupBy(f => f.Name, StringComparer.Ordinal).ToDictionary(g => g.Key, g => g.First(), StringComparer.Ordinal);
        foreach (var a in m.Akteure)
        {
            var aid = B("akt", Grammatik.Akteur, a.Name, a.Namespace);
            foreach (var d in a.Darf)
                if (funktionen.TryGetValue(d, out var f))
                    foreach (var e in f.Eingaenge)
                        foreach (var aus in e.Ausgaben) K(aid, N(aus, null, null), e.Eingang);
                else K(aid, N(d, null, null));
        }

        // Außenwelt: Commands/Queries ohne internen Erzeuger (Client), Quell-Nachrichten (Treiber oder Client).
        foreach (var n in knoten.Values.Where(n => n.Art == "nachricht").ToList())
        {
            var erzeugt = kanten.Any(k => k.Nach == n.Id);
            var aussen = n.Sorte switch
            {
                Grammatik.Command or Grammatik.Query => !erzeugt,
                // Eine Quell-Nachricht kommt immer von außen: vom Treiber oder vom Webhook der Umgebung (Quellen:<Typ>, docs/06 §6.6).
                Grammatik.Quelle => true,
                _ => false,
            };
            if (!aussen) continue;
            knoten.TryAdd(AussenId, new FlussKnoten(AussenId, Grammatik.Aussenwelt, "Außenwelt", null));
            K(AussenId, n.Id);
        }
        return new Nachrichtenfluss(knoten, kanten);
    }

    /// <summary><c>Hülle&lt;Arg&gt;</c> → (Hülle, Arg); kein generischer Typ → (null, null).</summary>
    internal static (string? Huelle, string? Arg) Generisch(string typ)
    {
        var lt = typ.IndexOf('<');
        return lt > 0 && typ.EndsWith('>') ? (Basisname(typ[..lt]), Basisname(typ[(lt + 1)..^1].Trim())) : (null, null);
    }

    internal static string Basisname(string typ)
    {
        var t = typ.Trim().TrimEnd('?');
        var lt = t.IndexOf('<');
        var kopf = lt >= 0 ? t[..lt] : t;
        var punkt = kopf.LastIndexOf('.');
        return punkt >= 0 ? t[(punkt + 1)..] : t;
    }
}
