using P = Abstractions.Projektstruktur;

namespace DomainEditor;

/// <summary>
/// Wohin eine NEUE Karte gehört (<c>docs/07-projektstruktur.md</c>, Tabelle <see cref="P"/>): das Projekt folgt aus der Rolle ihrer Primitive,
/// der Bereich aus dem Namespace, den die Karte mitbringt, der Dateiname aus der Tabelle. Eine neue Funktion in <c>Domain.ImagePair</c> landet
/// so in <c>Domain.Pipeline/ImagePair/Funktionen.cs</c> mit dem Namespace <c>Domain.Pipeline.ImagePair</c> — der Server entscheidet (W3), nicht
/// der Editor. Bestehendes (mit Datei) bleibt, wo es ist; Namespaces außerhalb der Rollen-Projekte der Wurzel bleiben unverändert (die
/// Lage-Prüfung meldet sie).
/// </summary>
public static class Strukturort
{
    /// <summary>Setzt den Namespace jeder neuen Karte auf das Projekt ihrer Rolle (gleicher Bereich). Idempotent.</summary>
    public static EditorModell Normalisiere(EditorModell m, string wurzel = P.StandardWurzel)
    {
        var ergebnisse = m.Funktionen.SelectMany(f => f.Ergebnisse).ToHashSet(StringComparer.Ordinal);
        string Ns(string ns, P.Rolle? rolle) => rolle is { } r ? Ziel(ns, r, wurzel) ?? ns : ns;
        var lesen = m.Lesen;
        return m with
        {
            Records = m.Records.Select(r => r.Datei != null ? r : r with { Namespace = Ns(r.Namespace, Rolle(Primitive(r, ergebnisse))) }).ToList(),
            Aggregate = m.Aggregate.Select(a => a.Datei != null ? a : a with { Namespace = Ns(a.Namespace, P.Rolle.Schreibseite) }).ToList(),
            Sagas = m.Sagas.Select(s => s.Datei != null ? s : s with { Namespace = Ns(s.Namespace, P.Rolle.Schreibseite) }).ToList(),
            Funktionen = m.Funktionen.Select(f => f.Datei != null ? f : f with { Namespace = Ns(f.Namespace, P.Rolle.Ablaeufe) }).ToList(),
            Dienste = m.Dienste.Select(d => d.Datei != null ? d : d with { Namespace = Ns(d.Namespace, P.Rolle.Ablaeufe) }).ToList(),
            Fluesse = m.Fluesse.Select(f => f.Datei != null ? f : f with { Namespace = Ns(f.Namespace, P.Rolle.Ablaeufe) }).ToList(),
            Akteure = m.Akteure.Select(a => a.Datei != null ? a : a with { Namespace = Ns(a.Namespace, P.Rolle.Akteure) }).ToList(),
            Clients = m.Clients.Select(c => c.Datei != null ? c : c with { Namespace = Ns(c.Namespace, P.Rolle.Akteure) }).ToList(),
            Lesen = lesen == null ? null : lesen with
            {
                Stores = lesen.Stores.Select(s => (s.Datei != null ? s : s with { Namespace = Ns(s.Namespace, P.Rolle.Leseseite) }) with
                {
                    Impl = s.Impl is { Datei: null } i ? i with { Namespace = Ns(i.Namespace, P.Rolle.Speicher) } : s.Impl,
                }).ToList(),
                Konsumenten = lesen.Konsumenten.Select(k => k.Datei != null ? k : k with { Namespace = Ns(k.Namespace, P.Rolle.Leseseite) }).ToList(),
                Reader = lesen.Reader.Select(r => r.Datei != null ? r : r with { Namespace = Ns(r.Namespace, P.Rolle.Leseseite) }).ToList(),
            },
        };
    }

    /// <summary>
    /// Der Namespace einer neuen Primitive der Rolle <paramref name="rolle"/>, wenn <paramref name="ns"/> in einem Rollen-Projekt der Wurzel
    /// liegt (Bereich bleibt); sonst <c>null</c>.
    /// </summary>
    public static string? Ziel(string ns, P.Rolle rolle, string wurzel = P.StandardWurzel)
    {
        foreach (var projekt in P.Projekte(wurzel).OrderByDescending(p => p.Length))
        {
            string? bereich;
            if (ns == projekt) bereich = null;
            else if (ns.StartsWith(projekt + ".", StringComparison.Ordinal)) bereich = ns[(projekt.Length + 1)..];
            else continue;
            var ziel = P.Projekt(wurzel, rolle);
            return P.Von(rolle).MitBereich ? P.Namespace(ziel, bereich) : ziel;
        }
        return null;
    }

    /// <summary>Liegt der Namespace in einem Rollen-Projekt der Wurzel (dann gelten die Dateiregeln der Tabelle)?</summary>
    public static bool InStruktur(string ns, string wurzel = P.StandardWurzel) =>
        P.Projekte(wurzel).Any(p => ns == p || ns.StartsWith(p + ".", StringComparison.Ordinal));

    /// <summary>Die Primitive eines Records im Editor-Modell (Art + Code-Fakt Ergebnis einer Funktion bzw. Treiber); <c>null</c> = nicht in der Tabelle.</summary>
    public static P.Primitive? Primitive(Record r, ISet<string> funktionsErgebnisse) => r.Kind switch
    {
        RecordArt.Command => P.Primitive.Command,
        RecordArt.Event when r.Treiber != null => P.Primitive.Quelle,
        RecordArt.Event when funktionsErgebnisse.Contains(r.Name) => P.Primitive.FunktionsErgebnis,
        RecordArt.Event or RecordArt.Rejection => P.Primitive.Event,
        RecordArt.ValueObject => P.Primitive.Wertobjekt,
        RecordArt.Query => P.Primitive.Query,
        RecordArt.Antwort => P.Primitive.Antwort,
        RecordArt.ReadModel => P.Primitive.ReadModel,
        RecordArt.Konfig => P.Primitive.Konfig,
        RecordArt.Auftrag => P.Primitive.Auftrag,
        _ => null,
    };

    private static P.Rolle? Rolle(P.Primitive? p) => p is { } x ? P.RollenVon(x)[0] : null;
}
