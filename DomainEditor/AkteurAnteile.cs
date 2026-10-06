namespace DomainEditor;

/// <summary>
/// Von welchem Akteur kommt was? (<c>docs/konzept-akteure.md</c> §8.2/§8.4) — abgeleitet aus dem <see cref="Fluss"/>, kein
/// weiteres Wort im Code:
/// <list type="bullet">
/// <item><b>direkt</b>: der Akteur darf die Nachricht selbst hineingeben (<c>IDarf&lt;T&gt;</c>, Kante <c>akt:</c> → Nachricht);</item>
/// <item><b>Kette</b>: ein Baustein erzeugt sie aus einer Nachricht, die von dem Akteur stammt — Decide (Command → Events), Pipeline-,
///   Reaktions-, Reader-Handle (Eingang → Ausgänge desselben Handles), Prozess (Auslöser/Wenn → Sende), Frist. Ein Handle, der
///   einen Akteur-Dienst (<c>IAkteurDienst&lt;A&gt;</c>) als Parameter nimmt, wechselt den Akteur: seine Ausgaben stammen von A.
///   Ebenso eine Reaktion im Vertrag von A (<c>Auf(Event)</c>, Kante <c>akt:A</c> mit Handle): ihre Ausgaben stammen von A.</item>
/// </list>
/// Fixpunkt über Akteur-MENGEN: ein Command kann von mehreren Akteuren kommen (zur Laufzeit trägt er genau den einen der Kette).
/// Dieselbe Ableitung rechnet der Editor live nach (<c>akteurMengen()</c> in HtmlPresenter) — Parität über <c>rahmen.akteurMengen</c>.
/// </summary>
public sealed class AkteurAnteile
{
    /// <summary>Nachricht → Akteure, die sie selbst hineingeben dürfen (IDarf).</summary>
    public IReadOnlyDictionary<string, SortedSet<string>> Direkt { get; }
    /// <summary>Nachricht → Akteure, in deren Kette sie entsteht.</summary>
    public IReadOnlyDictionary<string, SortedSet<string>> Kette { get; }
    /// <summary>Alle Eingänge (Command, Query, Trigger) mit ihrer Sorte.</summary>
    public IReadOnlyList<(string Nachricht, string Sorte)> Eingaenge { get; }
    /// <summary>Trigger, die ohne Kette entstehen und die mehrere Akteure dürfen (Regel „Ingress eindeutig").</summary>
    public IReadOnlyList<(string Nachricht, IReadOnlyList<string> Kandidaten)> MehrdeutigeIngresse { get; }

    private AkteurAnteile(Dictionary<string, SortedSet<string>> direkt, Dictionary<string, SortedSet<string>> kette,
        List<(string, string)> eingaenge, List<(string, IReadOnlyList<string>)> mehrdeutig)
    {
        Direkt = direkt; Kette = kette; Eingaenge = eingaenge; MehrdeutigeIngresse = mehrdeutig;
    }

    /// <summary>Direkt ∪ Kette.</summary>
    public SortedSet<string> AkteureVon(string nachricht)
    {
        var s = new SortedSet<string>(StringComparer.Ordinal);
        if (Direkt.TryGetValue(nachricht, out var d)) s.UnionWith(d);
        if (Kette.TryGetValue(nachricht, out var k)) s.UnionWith(k);
        return s;
    }

    public static AkteurAnteile Aus(EditorModell m) => Aus(m, Fluss.Aus(m));

    public static AkteurAnteile Aus(EditorModell m, Fluss fluss)
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

        // Direkt: Akteur → was er darf. Eine Vertrags-Reaktion (Kante mit Handle = Event) ist ein Akteur-Wechsel: Kette von A.
        foreach (var k in fluss.Kanten.Where(k => k.Von.StartsWith("akt:", StringComparison.Ordinal)))
            Menge(k.Handle == null ? direkt : kette, k.Nach).Add(fluss.KnotenVon(k.Von)!.Name);

        // Akteur-Wechsel: (Pipeline, Handle-Eingang) → Akteur des Dienst-Parameters.
        var dienstVon = m.Akteure.SelectMany(a => a.Dienste.Select(d => (d, a.Name))).GroupBy(x => x.d, StringComparer.Ordinal)
            .ToDictionary(g => g.Key, g => g.First().Name, StringComparer.Ordinal);
        var wechsel = new Dictionary<(string, string), string>();
        foreach (var p in m.Lesen?.Pipelines ?? [])
            foreach (var h in p.Handles)
                if (h.Faehigkeiten.Select(f => Fluss.Basisname(f.Typ)).FirstOrDefault(dienstVon.ContainsKey) is { } d)
                    wechsel[("pl:" + p.Name, h.Eingang)] = dienstVon[d];

        // Bausteine: Eingänge und Ausgänge je Handle (null = der ganze Baustein, z. B. ein Prozess).
        bool IstNachricht(string id) => id.StartsWith("msg:", StringComparison.Ordinal);
        var bausteine = fluss.Knoten.Where(k => k.Art != "nachricht" && k.Id != Fluss.AussenId && !k.Id.StartsWith("akt:", StringComparison.Ordinal))
            .Select(k => k.Id).ToList();
        var aus = bausteine.ToDictionary(b => b, b => fluss.Aus(b).Where(k => IstNachricht(k.Nach)).ToList(), StringComparer.Ordinal);
        var ein = bausteine.ToDictionary(b => b, b => fluss.Ein(b).Where(k => IstNachricht(k.Von)).ToList(), StringComparer.Ordinal);

        for (var geaendert = true; geaendert;)
        {
            geaendert = false;
            foreach (var b in bausteine)
                foreach (var a in aus[b])
                {
                    IEnumerable<string> quelle = wechsel.TryGetValue((b, a.Handle ?? ""), out var wa)
                        ? [wa]
                        : ein[b].Where(e => a.Handle == null || e.Handle == null || e.Handle == a.Handle)
                            .SelectMany(e => Alle(e.Von)).ToList();
                    var ziel = Menge(kette, a.Nach);
                    foreach (var x in quelle) geaendert |= ziel.Add(x);
                }
        }

        string Name(string msgId) => fluss.KnotenVon(msgId)!.Name;
        var eingaenge = fluss.Knoten.Where(k => k.Art == "nachricht" && k.Sorte is Grammatik.Command or Grammatik.Query or Grammatik.Trigger)
            .Select(k => (k.Name, k.Sorte!)).OrderBy(x => x.Name, StringComparer.Ordinal).ToList();

        var mehrdeutig = new List<(string, IReadOnlyList<string>)>();
        foreach (var k in fluss.Knoten.Where(k => k.Art == "nachricht" && k.Sorte == Grammatik.Trigger))
        {
            var intern = fluss.Ein(k.Id).Any(e => !e.Von.StartsWith("akt:", StringComparison.Ordinal));
            var d = direkt.GetValueOrDefault(k.Id);
            if (intern && (kette.GetValueOrDefault(k.Id)?.Count ?? 0) == 0 && d is { Count: > 1 })
                mehrdeutig.Add((k.Name, d.ToList()));
        }

        // Schlüssel nach außen: Nachrichten-Name (nicht die Fluss-Id), leere Mengen weg.
        Dictionary<string, SortedSet<string>> Namen(Dictionary<string, SortedSet<string>> d) =>
            d.Where(x => x.Value.Count > 0 && IstNachricht(x.Key)).ToDictionary(x => Name(x.Key), x => x.Value, StringComparer.Ordinal);
        var dn = Namen(direkt);
        var kn = Namen(kette);
        return new AkteurAnteile(dn, kn, eingaenge, mehrdeutig);
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
