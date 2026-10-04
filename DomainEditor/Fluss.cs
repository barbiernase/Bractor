using Abstractions;

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
/// Der NACHRICHTENFLUSS des Modells — die Grundlage, auf der die Grammatik geprüft und die Modul-Schnittstellen abgeleitet werden.
/// Nur Signatur-Fakten: Decide-OneOf, Apply-Eingang, Prozess-Regeln, Handle-Eingang/-Ausgänge/-Fähigkeiten, Ingress-Bindungen.
/// Bausteine: <c>agg:</c> Aggregat, <c>saga:</c> Prozess, <c>kon:</c> Projektion/Reaktion, <c>rdr:</c> Reader, <c>pl:</c> Pipeline,
/// <c>sto:</c> Store, <c>akt:</c> Akteur, <c>aussen</c> Außenwelt (unbekannter Client/Ingress). Nachrichten: <c>msg:</c> Record-Name, <c>faeh:</c> Fähigkeit.
/// </summary>
public sealed class Fluss
{
    public IReadOnlyList<FlussKnoten> Knoten { get; }
    public IReadOnlyList<FlussKante> Kanten { get; }
    private readonly Dictionary<string, FlussKnoten> _knoten;

    private Fluss(Dictionary<string, FlussKnoten> knoten, List<FlussKante> kanten)
    {
        _knoten = knoten;
        Knoten = knoten.Values.ToList();
        Kanten = kanten;
    }

    public FlussKnoten? KnotenVon(string id) => _knoten.GetValueOrDefault(id);
    public IEnumerable<FlussKante> Aus(string id) => Kanten.Where(k => k.Von == id);
    public IEnumerable<FlussKante> Ein(string id) => Kanten.Where(k => k.Nach == id);

    public const string AussenId = "aussen";
    private static readonly string SelbstName = typeof(Selbst<>).Name.Split('`')[0];
    private static readonly string FristName = typeof(Frist<>).Name.Split('`')[0];
    private static readonly string StartName = nameof(PipelineGestartet);

    /// <summary>Den Nachrichtenfluss aus dem Modell ableiten.</summary>
    public static Fluss Aus(EditorModell m)
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
            var sorte = recs.TryGetValue(name, out var r) ? Grammatik.SorteVonRecordArt(r.Kind) : sorteOhneRecord;
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

        // Aggregat: Command → Decide → Events; Event → Apply.
        foreach (var a in m.Aggregate) B("agg", Grammatik.Aggregat, a.Name, a.Namespace);
        foreach (var d in m.Decider.Where(d => d.Aggregat.Length > 0))
        {
            var agg = B("agg", Grammatik.Aggregat, d.Aggregat, aggNs.GetValueOrDefault(d.Aggregat));
            K(N(d.Command, null, null), agg, d.Command);
            foreach (var e in d.Ergibt) K(agg, N(e.Event, null, null), d.Command);
        }
        foreach (var ap in m.Applier.Where(a => a.Aggregat.Length > 0))
            K(N(ap.Event, null, null), B("agg", Grammatik.Aggregat, ap.Aggregat, aggNs.GetValueOrDefault(ap.Aggregat)), ap.Event);

        // Prozess: Auslöser + Wenn-Join → Sende/Kompensation.
        foreach (var s in m.Sagas)
        {
            var sg = B("saga", Grammatik.Prozess, s.Name, s.Namespace);
            K(N(s.TriggerEvent, null, null), sg);
            foreach (var st in s.Schritte)
            {
                foreach (var w in st.Wenn) K(N(w, null, null), sg);
                if (st.SammelEvent is { } se) K(N(se, null, null), sg);
                K(sg, N(st.Sende, null, null));
                if (st.Kompensation is { } ko) K(sg, N(ko, null, null));
            }
        }

        var lesen = m.Lesen;
        if (lesen != null)
        {
            // Store → Fähigkeit (der Store stellt sie bereit); Fähigkeit → Handle (der Handle darf sie).
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
                var istReaktion = k.Handles.Any(h => h.Ausgaenge.Any(a => recs.TryGetValue(a, out var r) && r.Kind == RecordArt.Command));
                var kid = B("kon", istReaktion ? Grammatik.Reaktion : Grammatik.Projektion, k.Name, k.Namespace);
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
            foreach (var p in lesen.Pipelines)
            {
                var pid = B("pl", Grammatik.Pipeline, p.Name, p.Namespace);
                foreach (var h in p.Handles)
                {
                    // Eingang: Record-Sorte; ohne Record ist es eine Selbst-Nachricht (Code: nested/privat), der Start ist Framework.
                    if (h.Eingang != StartName) K(N(h.Eingang, Grammatik.Selbst, p.Namespace), pid, h.Eingang);
                    foreach (var a in h.Ausgaenge)
                    {
                        var (huelle, arg) = Generisch(a);
                        if (huelle == SelbstName) K(pid, N(arg!, Grammatik.Selbst, p.Namespace), h.Eingang);
                        else if (huelle == FristName) K(pid, N(arg!, null, null), h.Eingang, Grammatik.Frist);
                        else if (huelle == null) K(pid, N(a, null, null), h.Eingang);
                        // FristStorno<T>: löscht eine Frist — kein Nachrichtenfluss.
                    }
                    foreach (var fp in h.Faehigkeiten) K(F(fp), pid, h.Eingang);
                }
            }
        }

        // Akteure: Akteur → was er darf (IDarf<T>). Sie ersetzen für diese Nachrichten die anonyme Außenwelt.
        foreach (var a in m.Akteure)
        {
            var aid = B("akt", Grammatik.Akteur, a.Name, a.Namespace);
            foreach (var d in a.Darf) K(aid, N(d, null, null));
        }

        // Außenwelt: Commands/Queries ohne internen Erzeuger (Client), Trigger mit Ingress-Bindung oder ohne Erzeuger.
        var ingress = new HashSet<string>((m.Ingress ?? []).Select(i => i.Trigger), StringComparer.Ordinal);
        foreach (var n in knoten.Values.Where(n => n.Art == "nachricht").ToList())
        {
            var erzeugt = kanten.Any(k => k.Nach == n.Id);
            var aussen = n.Sorte switch
            {
                Grammatik.Command or Grammatik.Query => !erzeugt,
                Grammatik.Trigger => !erzeugt || ingress.Contains(n.Name),
                _ => false,
            };
            if (!aussen) continue;
            knoten.TryAdd(AussenId, new FlussKnoten(AussenId, Grammatik.Aussenwelt, "Außenwelt", null));
            K(AussenId, n.Id);
        }
        return new Fluss(knoten, kanten);
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
