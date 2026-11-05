using P = Abstractions.Projektstruktur;

namespace DomainEditor;

/// <summary>
/// Wohin eine NEUE Karte gehört (<c>docs/07-projektstruktur.md</c>, Tabelle <see cref="P"/>): das Projekt folgt aus der Rolle ihrer Primitive,
/// der Bereich aus dem Namespace, den die Karte mitbringt, der Dateiname aus der Tabelle. Ein neuer Konfig-Record in <c>Domain.ImagePair</c> landet
/// so in <c>Domain.Pipeline/ImagePair/Konfigs.cs</c> mit dem Namespace <c>Domain.Pipeline.ImagePair</c> — der Server entscheidet (W3), nicht
/// der Editor. Bestehendes (mit Datei) bleibt, wo es ist; Namespaces außerhalb der Rollen-Projekte der Wurzel bleiben unverändert — auch die
/// eines Katalogs: was dort angelegt wird, bleibt im Katalog (die Lage-Prüfung meldet, was dort nicht hingehört).
/// </summary>
public static class Strukturort
{
    /// <summary>Setzt den Namespace jeder neuen Karte auf das Projekt ihrer Rolle (gleicher Bereich). Idempotent.</summary>
    public static EditorModell Normalisiere(EditorModell m, string wurzel = P.StandardWurzel)
    {
        var ergebnisse = Funktionsergebnisse(m);
        var anAggregat = Aggregatfragen(m);
        string Ns(string ns, P.Rolle? rolle) => rolle is { } r ? Ziel(ns, r, wurzel) ?? ns : ns;
        // Im Aggregat angelegt (noch ohne Erzeuger): bei seiner Schreibseite — so muss nichts umziehen, wenn später ein Decide bzw. eine Frage ihn nimmt.
        string RecordNs(Record r) => r.AmAggregat is { } ag && m.Aggregate.FirstOrDefault(a => a.Name == ag) is { } agg
            ? Ns(agg.Namespace, P.Rolle.Schreibseite)
            : Ns(r.Namespace, Rolle(Primitive(r, ergebnisse, anAggregat)));
        var lesen = m.Lesen;
        return m with
        {
            Records = m.Records.Select(r => r.Datei != null ? r : r with { Namespace = RecordNs(r) }).ToList(),
            Aggregate = m.Aggregate.Select(a => a.Datei != null ? a : a with { Namespace = Ns(a.Namespace, P.Rolle.Schreibseite) }).ToList(),
            BausteinFunktionen = m.BausteinFunktionen.Select(f => f.Datei != null ? f : f with { Namespace = Ns(f.Namespace, P.Rolle.Ablaeufe) }).ToList(),
            Akteure = m.Akteure.Select(a => a.Datei != null ? a : a with { Namespace = Ns(a.Namespace, P.Rolle.Akteure) }).ToList(),
            Clients = m.Clients.Select(c => c.Datei != null ? c : c with { Namespace = Ns(c.Namespace, P.Rolle.Akteure) }).ToList(),
            Graphen = m.Graphen.Select(g => g.Datei != null ? g : g with { Namespace = Ns(g.Namespace, P.Rolle.Ablaeufe) }).ToList(),
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

    /// <summary>Liegt der Namespace in einem Rollen-Projekt der Wurzel oder in einem Katalog (dann gelten die Dateiregeln der Tabelle)?</summary>
    public static bool InStruktur(string ns, string wurzel = P.StandardWurzel) =>
        P.KatalogVon(ns) != null || P.Projekte(wurzel).Any(p => ns == p || ns.StartsWith(p + ".", StringComparison.Ordinal));

    /// <summary>
    /// Die Commands ohne Ziel, die kein Decide abgibt (Signatur-Fakt je <c>Decide</c>) — nur Knoten geben sie einander, sie liegen bei den Abläufen
    /// (dieselbe Regel wie die Lage-Prüfung, <c>Domain.SourceGeneration/Lageart.cs</c>).
    /// </summary>
    public static ISet<string> Funktionsergebnisse(EditorModell m)
    {
        var amAggregat = m.Decider.SelectMany(d => d.Ergibt).Select(a => a.Event).Concat(m.Decider.Select(d => d.Command)).ToHashSet(StringComparer.Ordinal);
        return m.Records.Where(r => r.Kind == RecordArt.Command && string.IsNullOrEmpty(r.Aggregat) && !amAggregat.Contains(r.Name))
            .Select(r => r.Name).ToHashSet(StringComparer.Ordinal);
    }

    /// <summary>
    /// Die Queries an Aggregate und ihre Antworten (Signatur-Fakt der <c>Frage</c> im Decider, docs/03 §3.2) — sie liegen bei
    /// der Schreibseite (dieselbe Regel wie die Lage-Prüfung, <c>Domain.SourceGeneration/Lageart.cs</c>).
    /// </summary>
    public static ISet<string> Aggregatfragen(EditorModell m) =>
        m.Aggregate.SelectMany(a => a.Fragen).SelectMany(f => f.Antworten.Prepend(f.Query)).ToHashSet(StringComparer.Ordinal);

    /// <summary>Die Primitive eines Records im Editor-Modell (Art + Code-Fakt Ergebnis einer Funktion, Frage an ein Aggregat bzw. Treiber);
    /// <c>null</c> = nicht in der Tabelle.</summary>
    public static P.Primitive? Primitive(Record r, ISet<string> funktionsErgebnisse, ISet<string> aggregatfragen) => r.Kind switch
    {
        RecordArt.Command when funktionsErgebnisse.Contains(r.Name) => P.Primitive.KnotenCommand,
        RecordArt.Command => P.Primitive.Command,
        RecordArt.Event => P.Primitive.Event,
        RecordArt.Rejection => P.Primitive.Hinweis,
        RecordArt.ValueObject => P.Primitive.Wertobjekt,
        RecordArt.Query when aggregatfragen.Contains(r.Name) => P.Primitive.AggregatQuery,
        RecordArt.Antwort when aggregatfragen.Contains(r.Name) => P.Primitive.AggregatAntwort,
        RecordArt.Query => P.Primitive.Query,
        RecordArt.Antwort => P.Primitive.Antwort,
        RecordArt.ReadModel => P.Primitive.ReadModel,
        RecordArt.Konfig => P.Primitive.Konfig,
        _ => null,
    };

    private static P.Rolle? Rolle(P.Primitive? p) => p is { } x ? P.RollenVon(x)[0] : null;
}
