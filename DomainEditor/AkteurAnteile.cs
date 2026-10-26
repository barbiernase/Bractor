namespace DomainEditor;

/// <summary>
/// Von welchem Akteur kommt was? (<c>docs/konzept-akteure.md</c> §2.3/§8) — abgeleitet aus dem <see cref="Nachrichtenfluss"/>, kein
/// weiteres Wort im Code:
/// <list type="bullet">
/// <item><b>direkt</b>: der Akteur darf die Nachricht selbst hineingeben (<c>IDarf&lt;T&gt;</c>, Kante <c>akt:</c> → Nachricht);</item>
/// <item><b>Kette</b>: ein Baustein erzeugt sie aus einer Nachricht, die von dem Akteur stammt — Decide (Command → Events, Frist),
///   Funktion, Projektions-, Reader-Handle (Eingang → Ausgänge desselben Eingangs). Darf A eine Funktion draußen rechnen
///   (<c>IDarf&lt;F&gt;</c>, Kante <c>akt:A</c> mit Handle = Eingang), stammen ihre Ausgaben von A.</item>
/// </list>
/// Fixpunkt über Akteur-MENGEN: ein Command kann von mehreren Akteuren kommen (zur Laufzeit trägt er genau den einen der Kette).
/// Der Editor rechnet nichts davon nach (Werkbank P7.3): er bekommt die Mengen als <c>rahmen.akteurMengen</c>, alles darauf Aufbauende (Akteure
/// je Karte, Akteur-Rahmen, Pfade) als <c>board.sicht</c> (GraphExtractor/Sicht).
/// </summary>
public sealed class AkteurAnteile
{
    /// <summary>Nachricht → Akteure, die sie selbst hineingeben dürfen (IDarf).</summary>
    public IReadOnlyDictionary<string, SortedSet<string>> Direkt { get; }
    /// <summary>Nachricht → Akteure, in deren Kette sie entsteht.</summary>
    public IReadOnlyDictionary<string, SortedSet<string>> Kette { get; }
    /// <summary>Alle Eingänge (Command, Query) mit ihrer Sorte.</summary>
    public IReadOnlyList<(string Nachricht, string Sorte)> Eingaenge { get; }

    private AkteurAnteile(Dictionary<string, SortedSet<string>> direkt, Dictionary<string, SortedSet<string>> kette, List<(string, string)> eingaenge)
    {
        Direkt = direkt; Kette = kette; Eingaenge = eingaenge;
    }

    /// <summary>Direkt ∪ Kette.</summary>
    public SortedSet<string> AkteureVon(string nachricht)
    {
        var s = new SortedSet<string>(StringComparer.Ordinal);
        if (Direkt.TryGetValue(nachricht, out var d)) s.UnionWith(d);
        if (Kette.TryGetValue(nachricht, out var k)) s.UnionWith(k);
        return s;
    }

    public static AkteurAnteile Aus(EditorModell m) => Aus(Nachrichtenfluss.Aus(m));

    public static AkteurAnteile Aus(Nachrichtenfluss fluss)
    {
        var direkt = new Dictionary<string, SortedSet<string>>(StringComparer.Ordinal);
        var kette = new Dictionary<string, SortedSet<string>>(StringComparer.Ordinal);
        SortedSet<string> Menge(Dictionary<string, SortedSet<string>> d, string k) =>
            d.TryGetValue(k, out var s) ? s : d[k] = new SortedSet<string>(StringComparer.Ordinal);
        SortedSet<string> Alle(string msgId)
        {
            var s = new SortedSet<string>(StringComparer.Ordinal);
            if (direkt.TryGetValue(msgId, out var d)) s.UnionWith(d);
            if (kette.TryGetValue(msgId, out var k)) s.UnionWith(k);
            return s;
        }

        // Direkt: Akteur → was er darf. Die Ausgabe einer Funktion, die er draußen rechnet (Kante mit Handle = Eingang), ist Kette von A.
        foreach (var k in fluss.Kanten.Where(k => k.Von.StartsWith("akt:", StringComparison.Ordinal)))
            Menge(k.Handle == null ? direkt : kette, k.Nach).Add(fluss.KnotenVon(k.Von)!.Name);

        // Bausteine: Eingänge und Ausgänge je Handle (null = der ganze Baustein).
        bool IstNachricht(string id) => id.StartsWith("msg:", StringComparison.Ordinal);
        var bausteine = fluss.Knoten.Where(k => k.Art != "nachricht" && k.Id != Nachrichtenfluss.AussenId && !k.Id.StartsWith("akt:", StringComparison.Ordinal))
            .Select(k => k.Id).ToList();
        var aus = bausteine.ToDictionary(b => b, b => fluss.Aus(b).Where(k => IstNachricht(k.Nach)).ToList(), StringComparer.Ordinal);
        var ein = bausteine.ToDictionary(b => b, b => fluss.Ein(b).Where(k => IstNachricht(k.Von)).ToList(), StringComparer.Ordinal);

        for (var geaendert = true; geaendert;)
        {
            geaendert = false;
            foreach (var b in bausteine)
                foreach (var a in aus[b])
                {
                    var quelle = ein[b].Where(e => a.Handle == null || e.Handle == null || e.Handle == a.Handle)
                        .SelectMany(e => Alle(e.Von)).ToList();
                    var ziel = Menge(kette, a.Nach);
                    foreach (var x in quelle) geaendert |= ziel.Add(x);
                }
        }

        string Name(string msgId) => fluss.KnotenVon(msgId)!.Name;
        var eingaenge = fluss.Knoten.Where(k => k.Art == "nachricht" && k.Sorte is Grammatik.Command or Grammatik.Query)
            .Select(k => (k.Name, k.Sorte!)).OrderBy(x => x.Name, StringComparer.Ordinal).ToList();

        // Schlüssel nach außen: Nachrichten-Name (nicht die Knoten-Id), leere Mengen weg.
        Dictionary<string, SortedSet<string>> Namen(Dictionary<string, SortedSet<string>> d) =>
            d.Where(x => x.Value.Count > 0 && IstNachricht(x.Key)).ToDictionary(x => Name(x.Key), x => x.Value, StringComparer.Ordinal);
        var dn = Namen(direkt);
        var kn = Namen(kette);
        return new AkteurAnteile(dn, kn, eingaenge);
    }

    /// <summary>Für das Board (<c>rahmen.akteurMengen</c>): Nachricht → { direkt[], kette[] }.</summary>
    public SortedDictionary<string, object> AlsJson()
    {
        var d = new SortedDictionary<string, object>(StringComparer.Ordinal);
        foreach (var n in Direkt.Keys.Concat(Kette.Keys).Distinct(StringComparer.Ordinal))
            d[n] = new
            {
                direkt = Direkt.GetValueOrDefault(n)?.ToList() ?? [],
                kette = Kette.GetValueOrDefault(n)?.ToList() ?? [],
            };
        return d;
    }
}
