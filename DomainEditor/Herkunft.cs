using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace DomainEditor;

/// <summary>
/// Der HERKUNFTS-STEMPEL: ein Hash über den Modell-Inhalt eines Elements, gesetzt beim Einlesen aus dem Code. „C# schreiben"
/// fasst ein bestehendes Element nur an, wenn sein Inhalt vom Stempel abweicht (= im Editor geändert). So schreibt ein
/// unverändertes Board garantiert NICHTS — auch wo die Textform des Codes von der kanonischen Form des Scaffolders abweicht.
/// Gehasht wird nur, was das Modell besitzt (Signatur/Deklaration); Ort, usings, Rümpfe und der Stempel selbst nicht.
/// </summary>
public static class Herkunft
{
    public static string Von(Record r) => Hash(r with { Datei = null, Usings = [], Herkunft = null, Aggregat = null, Doku = null, Zusatz = null, Attribute = null });
    public static string Von(Enumeration e) => Hash(e with { Datei = null, Herkunft = null, Doku = null });
    public static string Von(Aggregat a) => Hash(a.State.Select(f => f with { ElementTyp = null }).ToList());
    public static string Von(DecideRegel d) => Hash(new { d.Aggregat, d.Command, d.Ergibt });
    public static string Von(Akteur a) => Hash(new { a.Name, a.Darf, a.Art, Vertrag = a.Vertrag.Count == 0 ? null : a.Vertrag, VertragName = a.Vertrag.Count == 0 ? null : a.VertragTyp });
    public static string Von(Client c) => Hash(new { c.Name, c.Traegt, c.Sendet, c.Fragt, c.Kenntnis });
    public static string Von(Funktion f) => Hash(new { f.Name, f.Auftrag, f.Ergebnisse });
    public static string Von(Saga s) => Hash(new { s.TriggerEvent, Schritte = s.Schritte.Select(Wirksam).ToList() });

    /// <summary>
    /// Eine Regel so, wie der Scaffolder sie WIRKLICH schreibt: ein verbatim gelesener Ausdruck hat Vorrang vor Stub-Argumenten
    /// (die der Browser beim Aufbereiten ergänzt) — die zählen dann nicht; leere Texte/Listen = nicht gesetzt.
    /// </summary>
    private static SagaSchritt Wirksam(SagaSchritt t)
    {
        static string? L(string? x) => string.IsNullOrWhiteSpace(x) ? null : x;
        static IReadOnlyList<string>? LL(IReadOnlyList<string>? x) => x is { Count: > 0 } ? x : null;
        var sende = L(t.SendeAusdruck);
        var komp = L(t.KompensationAusdruck);
        var sammel = L(t.SammelAusdruck);
        return t with
        {
            SendeAusdruck = sende, KompensationAusdruck = komp, SammelAusdruck = sammel,
            SendeArgumente = sende != null ? null : LL(t.SendeArgumente),
            SendeJeCollection = sende != null ? null : L(t.SendeJeCollection),
            SendeJeElement = sende != null ? null : L(t.SendeJeElement),
            KompensationArgumente = komp != null ? null : LL(t.KompensationArgumente),
            SammelAnzahl = sammel != null ? null : L(t.SammelAnzahl),
            SammelEvent = L(t.SammelEvent), Kompensation = L(t.Kompensation),
            Rufe = L(t.Rufe), Zeitlimit = L(t.Zeitlimit), Sende = t.Sende ?? "",
        };
    }
    /// <summary>Ein Fluss so, wie der Scaffolder ihn schreibt: ein gelesener Ausdruck hat Vorrang vor Zuordnungs-Argumenten.</summary>
    public static string Von(FlussPipeline f) => Hash(new
    {
        Knoten = f.Knoten.Select(s => s with
        {
            Doku = null,
            Zeitlimit = string.IsNullOrWhiteSpace(s.Zeitlimit) ? null : s.Zeitlimit,
            Liste = string.IsNullOrWhiteSpace(s.Liste) ? null : s.Liste,
            Eingaenge = s.Eingaenge.Select(e => string.IsNullOrWhiteSpace(e.Ausdruck)
                ? e with { Ausdruck = null, Argumente = e.Argumente is { Count: > 0 } ? e.Argumente : null }
                : e with { Argumente = null }).ToList(),
        }).ToList(),
    });
    public static string Von(Faehigkeit f) => Hash(new { f.Name, f.Methode, f.Lesen, f.Rueckgabe, f.Parameter });
    public static string Von(Handle h) => Hash(new { h.Eingang, h.Faehigkeiten, h.Ausgaenge, h.Rueckgabe });
    public static string Von(Konsument k) => Hash(new { k.SubscriberId, k.Pull, k.Append, k.Basen });
    public static string Von(Leser r) => Hash(new { r.Projektion, r.TrackDeps, r.Basen });
    // Name/Namespace/Konfigs gehören dazu: sonst ging eine Änderung daran im Editor still verloren (weder geschrieben noch gemeldet).
    public static string Von(PipelineKarte p) => Hash(new { p.Name, p.Namespace, p.PipelineId, p.Basen, p.Konfigs });

    /// <summary>Alle änderbaren Elemente des (aus dem Code gelesenen) Modells stempeln.</summary>
    public static EditorModell Stempeln(EditorModell m)
    {
        Handle H(Handle h) => h with { Herkunft = Von(h) };
        var l = m.Lesen;
        return m with
        {
            Records = m.Records.Select(r => r with { Herkunft = Von(r) }).ToList(),
            Enums = m.Enums.Select(e => e with { Herkunft = Von(e) }).ToList(),
            Aggregate = m.Aggregate.Select(a => a with { Herkunft = Von(a) }).ToList(),
            Decider = m.Decider.Select(d => d with { Herkunft = Von(d) }).ToList(),
            Sagas = m.Sagas.Select(s => s with { Herkunft = Von(s) }).ToList(),
            Funktionen = m.Funktionen.Select(f => f with { Herkunft = Von(f) }).ToList(),
            Fluesse = m.Fluesse.Select(f => f with { Herkunft = Von(f) }).ToList(),
            Akteure = m.Akteure.Select(a => a with { Herkunft = Von(a) }).ToList(),
            Clients = m.Clients.Select(c => c with { Herkunft = Von(c) }).ToList(),
            Lesen = l == null ? null : l with
            {
                Stores = l.Stores.Select(s => s with { Fns = s.Fns.Select(f => f with { Herkunft = Von(f) }).ToList() }).ToList(),
                Konsumenten = l.Konsumenten.Select(k => k with { Herkunft = Von(k), Handles = k.Handles.Select(H).ToList() }).ToList(),
                Reader = l.Reader.Select(r => r with { Herkunft = Von(r), Handles = r.Handles.Select(H).ToList() }).ToList(),
                Pipelines = l.Pipelines.Select(p => p with { Herkunft = Von(p), Handles = p.Handles.Select(H).ToList() }).ToList(),
            },
        };
    }

    /// <summary>Alle aus dem Code gelesenen Elemente, deren Inhalt vom Stempel abweicht (= im Editor geändert) — lesbar benannt.</summary>
    public static List<string> Geaenderte(EditorModell m)
    {
        var aus = new List<string>();
        void Pruefe(string? stempel, string jetzt, string was) { if (Geaendert(stempel, jetzt)) aus.Add(was); }
        foreach (var r in m.Records) Pruefe(r.Herkunft, Von(r), $"{r.Kind} {r.Namespace}.{r.Name}");
        foreach (var e in m.Enums) Pruefe(e.Herkunft, Von(e), $"enum {e.Namespace}.{e.Name}");
        foreach (var a in m.Aggregate) Pruefe(a.Herkunft, Von(a), $"state {a.Namespace}.{a.Name}");
        foreach (var d in m.Decider) Pruefe(d.Herkunft, Von(d), $"decide {d.Aggregat}.{d.Command}");
        foreach (var s in m.Sagas) Pruefe(s.Herkunft, Von(s), $"prozess {s.Namespace}.{s.Name}");
        foreach (var f in m.Funktionen) Pruefe(f.Herkunft, Von(f), $"funktion {f.Namespace}.{f.Name}");
        foreach (var f in m.Fluesse) Pruefe(f.Herkunft, Von(f), $"pipeline {f.Namespace}.{f.Name}");
        foreach (var a in m.Akteure) Pruefe(a.Herkunft, Von(a), $"akteur {a.Namespace}.{a.Name}");
        foreach (var c in m.Clients) Pruefe(c.Herkunft, Von(c), $"client {c.Namespace}.{c.Name}");
        var l = m.Lesen;
        if (l == null) return aus;
        foreach (var st in l.Stores) foreach (var f in st.Fns) Pruefe(f.Herkunft, Von(f), $"fähigkeit {st.Name}.{f.Methode}");
        foreach (var k in l.Konsumenten)
        {
            Pruefe(k.Herkunft, Von(k), $"konsument {k.Name}");
            foreach (var h in k.Handles) Pruefe(h.Herkunft, Von(h), $"handle {k.Name}.Handle({h.Eingang})");
        }
        foreach (var r in l.Reader)
        {
            Pruefe(r.Herkunft, Von(r), $"reader {r.Name}");
            foreach (var h in r.Handles) Pruefe(h.Herkunft, Von(h), $"handle {r.Name}.Handle({h.Eingang})");
        }
        foreach (var p in l.Pipelines)
        {
            Pruefe(p.Herkunft, Von(p), $"pipeline {p.Name}");
            foreach (var h in p.Handles) Pruefe(h.Herkunft, Von(h), $"handle {p.Name}.Handle({h.Eingang})");
        }
        return aus;
    }

    /// <summary>Aus dem Code gelesen und seitdem im Editor geändert?</summary>
    public static bool Geaendert(string? stempel, string jetzt) => stempel != null && stempel != jetzt;

    private static string Hash(object o) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(o, EditorModell.JsonOptionen))))[..16];
}
