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
    public static string Von(Akteur a) => Hash(new { a.Name, a.Darf, a.Art });
    public static string Von(Client c) => Hash(new { c.Name, c.Verkoerpert, c.Kenntnis });
    // Ein Dienst: seine Methoden-Signaturen und je Implementierung ihr Konstruktor (Rümpfe gehören dem Code).
    public static string Von(Dienst d) => Hash(new
    {
        d.Name, Methoden = d.Methoden.Select(m => new { m.Name, m.Rueckgabe, m.Parameter }).ToList(),
        Impls = d.Implementierungen.Select(i => new { i.Name, i.Konstruktor }).ToList(),
    });
    // Eine Funktion: ihre Form, je Eingang (Auf) Eingang und Ausgaben, der Konstruktor der Klasse — Rümpfe und Umsetzungen gehören dem Code.
    public static string Von(BausteinFunktion f) => Hash(new
    {
        f.Name, f.Klasse,
        Eingaenge = f.Eingaenge.Select(e => new { e.Eingang, e.Hinweis, e.Herkunft, e.Strom, e.Ausgaben }).ToList(),
        f.Konstruktor,
    });
    public static string Von(Faehigkeit f) => Hash(new { f.Name, f.Methode, f.Lesen, f.Rueckgabe, f.Parameter });
    public static string Von(Handle h) => Hash(new { h.Eingang, h.Faehigkeiten, h.Ausgaenge, h.Rueckgabe });
    public static string Von(Konsument k) => Hash(new { k.MindestensEinmal, k.Basen });
    public static string Von(Leser r) => Hash(new { r.Basen });

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
            Dienste = m.Dienste.Select(d => d with { Herkunft = Von(d) }).ToList(),
            BausteinFunktionen = m.BausteinFunktionen.Select(f => f with { Herkunft = Von(f) }).ToList(),
            Akteure = m.Akteure.Select(a => a with { Herkunft = Von(a) }).ToList(),
            Clients = m.Clients.Select(c => c with { Herkunft = Von(c) }).ToList(),
            Lesen = l == null ? null : l with
            {
                Stores = l.Stores.Select(s => s with { Fns = s.Fns.Select(f => f with { Herkunft = Von(f) }).ToList() }).ToList(),
                Konsumenten = l.Konsumenten.Select(k => k with { Herkunft = Von(k), Handles = k.Handles.Select(H).ToList() }).ToList(),
                Reader = l.Reader.Select(r => r with { Herkunft = Von(r), Handles = r.Handles.Select(H).ToList() }).ToList(),
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
        foreach (var d in m.Dienste) Pruefe(d.Herkunft, Von(d), $"dienst {d.Namespace}.{d.Name}");
        foreach (var f in m.BausteinFunktionen) Pruefe(f.Herkunft, Von(f), $"funktion {f.Namespace}.{f.Name}");
        foreach (var a in m.Akteure) Pruefe(a.Herkunft, Von(a), $"akteur {a.Namespace}.{a.Name}");
        foreach (var c in m.Clients) Pruefe(c.Herkunft, Von(c), $"client {c.Namespace}.{c.Name}");
        var l = m.Lesen;
        if (l == null) return aus;
        foreach (var st in l.Stores) foreach (var f in st.Fns) Pruefe(f.Herkunft, Von(f), $"fähigkeit {st.Name}.{f.Methode}");
        foreach (var k in l.Konsumenten)
        {
            Pruefe(k.Herkunft, Von(k), $"konsument {k.Name}");
            foreach (var h in k.Handles) Pruefe(h.Herkunft, Von(h), $"handle {k.Name}.{Abstractions.Projektionsvertrag.Auf}({h.Eingang})");
        }
        foreach (var r in l.Reader)
        {
            Pruefe(r.Herkunft, Von(r), $"reader {r.Name}");
            foreach (var h in r.Handles) Pruefe(h.Herkunft, Von(h), $"handle {r.Name}.{Abstractions.Projektionsvertrag.Frage}({h.Eingang})");
        }
        return aus;
    }

    /// <summary>Aus dem Code gelesen und seitdem im Editor geändert?</summary>
    public static bool Geaendert(string? stempel, string jetzt) => stempel != null && stempel != jetzt;

    private static string Hash(object o) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(o, EditorModell.JsonOptionen))))[..16];
}
