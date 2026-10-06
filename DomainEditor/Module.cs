namespace DomainEditor;

/// <summary>
/// Ein Port eines Moduls: <see cref="Richtung"/> <c>ein</c>/<c>aus</c>, die Sorte, die über ihn fließt, die Nachricht (Typname) und
/// die Partner auf der anderen Seite (Namespaces; <c>Außenwelt</c> = Client/Ingress). <see cref="Offen"/> = noch nicht geschlossen:
/// ein Eingang ohne Konsument bzw. ein Ausgang ohne Erzeuger (Entwurf von oben nach unten).
/// </summary>
public sealed record ModulPort(string Richtung, string Sorte, string Nachricht, bool Offen, IReadOnlyList<string> Partner);

/// <summary>
/// Ein Modul = ein Namespace (Code-Fakt; Verschachtelung = Namespace-Hierarchie). Seine Schnittstelle ist ABGELEITET, nicht deklariert:
/// Eingänge = die Nachrichten, die von außen hineinlaufen, Ausgänge = die hinauslaufen (siehe <see cref="Module"/>).
/// </summary>
public sealed record Modul
{
    public required string Namespace { get; init; }
    public required string Name { get; init; }
    /// <summary>Das übergeordnete Modul; null = oberste Ebene.</summary>
    public string? Eltern { get; init; }
    public IReadOnlyList<string> Kinder { get; init; } = [];
    /// <summary>Was DIREKT in diesem Namespace liegt: Art (Baustein bzw. Sorte der Nachricht) → Anzahl.</summary>
    public IReadOnlyDictionary<string, int> Inhalt { get; init; } = new Dictionary<string, int>();
    public IReadOnlyList<ModulPort> Eingaenge { get; init; } = [];
    public IReadOnlyList<ModulPort> Ausgaenge { get; init; } = [];
}

/// <summary>
/// KAPSELUNG (<c>docs/konzept-editor-komposition.md</c> §5): jeder Namespace ist ein Modul, eingeklappt ein Baustein mit abgeleiteten
/// Ports. Regel (auf dem <see cref="Fluss"/>): eine Kante Erzeuger → Nachricht bzw. Nachricht → Konsument, deren Enden auf verschiedenen
/// Seiten der Modulgrenze liegen, ist ein Port — Eingang, wenn ihr Ziel drinnen liegt, Ausgang, wenn ihre Quelle drinnen liegt. Die
/// Außenwelt (Client, Ingress) liegt außerhalb jedes Moduls. Dazu kommen die OFFENEN Ports: eine Nachricht im Modul, die einen
/// Konsumenten braucht und keinen hat (Command, Query, Trigger, Selbst), bzw. einen Erzeuger braucht und keinen hat (Event,
/// Ablehnung, Response). Dieselbe Ableitung rechnet der Editor live auf dem Board (Parität: <c>/api/editor/module</c>).
/// </summary>
public static class Module
{
    public const string Aussenwelt = "Außenwelt";
    /// <summary>Partner-Präfix eines Akteurs (ein benannter Teil der Außenwelt): „Akteur Inspektor".</summary>
    public const string AkteurPraefix = "Akteur ";
    /// <summary>Sorten, deren Nachricht einen Konsumenten braucht (sonst offener Eingang).</summary>
    public static readonly IReadOnlySet<string> BrauchtKonsument = new HashSet<string> { Grammatik.Command, Grammatik.Query, Grammatik.Trigger, Grammatik.Selbst, Grammatik.Auftrag };
    /// <summary>Sorten, deren Nachricht einen Erzeuger braucht (sonst offener Ausgang).</summary>
    public static readonly IReadOnlySet<string> BrauchtErzeuger = new HashSet<string> { Grammatik.Event, Grammatik.Transient, Grammatik.Response };

    public static IReadOnlyList<Modul> Ableiten(EditorModell m) => Ableiten(m, Fluss.Aus(m));

    public static IReadOnlyList<Modul> Ableiten(EditorModell m, Fluss fluss)
    {
        // (1) Alle Namespaces, in denen etwas liegt (auch Value Objects/Konfigs/Enums/ReadModels), plus ihre Präfixe.
        var direkt = new Dictionary<string, Dictionary<string, int>>(StringComparer.Ordinal);
        void Zaehle(string? ns, string art)
        {
            if (string.IsNullOrEmpty(ns)) return;
            if (!direkt.TryGetValue(ns, out var d)) direkt[ns] = d = new Dictionary<string, int>(StringComparer.Ordinal);
            d[art] = d.GetValueOrDefault(art) + 1;
        }
        // Akteure liegen wie die Außenwelt AUSSERHALB jedes Moduls (sie sind die Außenwelt — mit Namen).
        foreach (var k in fluss.Knoten.Where(k => k.Id != Fluss.AussenId && k.Art != Grammatik.Akteur)) Zaehle(k.Namespace, k.Sorte ?? k.Art);
        foreach (var r in m.Records.Where(r => Grammatik.SorteVonRecordArt(r.Kind) == null)) Zaehle(r.Namespace, r.Kind);
        foreach (var e in m.Enums) Zaehle(e.Namespace, "enum");
        var alle = new SortedSet<string>(StringComparer.Ordinal);
        foreach (var ns in direkt.Keys)
            for (var p = ns; p != null; p = Eltern(p)) alle.Add(p);

        // (2) Ports je Modul: Kanten über die Grenze + offene Nachrichten.
        var ports = alle.ToDictionary(ns => ns, _ => new Dictionary<(string, string, string), (bool Offen, SortedSet<string> Partner)>(), StringComparer.Ordinal);
        void Port(string modul, string richtung, string sorte, string nachricht, bool offen, string? partner)
        {
            var d = ports[modul];
            var key = (richtung, sorte, nachricht);
            if (!d.TryGetValue(key, out var p)) d[key] = p = (offen, new SortedSet<string>(StringComparer.Ordinal));
            else if (offen && !p.Offen) d[key] = p = (true, p.Partner);
            if (partner != null) p.Partner.Add(partner);
        }
        string? NsVon(string id) => id == Fluss.AussenId || IstAkteur(id) ? null : fluss.KnotenVon(id)?.Namespace;
        bool IstAkteur(string id) => fluss.KnotenVon(id)?.Art == Grammatik.Akteur;
        string Aussen(string id) => IstAkteur(id) ? $"{AkteurPraefix}{fluss.KnotenVon(id)!.Name}" : Aussenwelt;
        foreach (var k in fluss.Kanten)
        {
            var von = NsVon(k.Von);
            var nach = NsVon(k.Nach);
            foreach (var modul in alle)
            {
                bool drinVon = Drin(modul, von), drinNach = Drin(modul, nach);
                if (drinNach && !drinVon) Port(modul, "ein", k.Sorte, k.Nachricht, false, von ?? Aussen(k.Von));
                else if (drinVon && !drinNach) Port(modul, "aus", k.Sorte, k.Nachricht, false, nach ?? Aussen(k.Nach));
            }
        }
        foreach (var n in fluss.Knoten.Where(n => n.Art == "nachricht" && n.Namespace != null))
        {
            var offenEin = BrauchtKonsument.Contains(n.Sorte!) && !fluss.Aus(n.Id).Any();
            var offenAus = BrauchtErzeuger.Contains(n.Sorte!) && !fluss.Ein(n.Id).Any();
            if (!offenEin && !offenAus) continue;
            foreach (var modul in alle.Where(mo => Drin(mo, n.Namespace)))
                Port(modul, offenEin ? "ein" : "aus", n.Sorte!, n.Name, true, null);
        }

        return alle.Select(ns => new Modul
        {
            Namespace = ns, Name = ns[(ns.LastIndexOf('.') + 1)..], Eltern = Eltern(ns),
            Kinder = alle.Where(x => Eltern(x) == ns).ToList(),
            Inhalt = direkt.GetValueOrDefault(ns) ?? new Dictionary<string, int>(),
            Eingaenge = Liste(ports[ns], "ein"), Ausgaenge = Liste(ports[ns], "aus"),
        }).ToList();
    }

    private static List<ModulPort> Liste(Dictionary<(string R, string S, string N), (bool Offen, SortedSet<string> Partner)> d, string richtung) =>
        d.Where(kv => kv.Key.R == richtung)
            .Select(kv => new ModulPort(richtung, kv.Key.S, kv.Key.N, kv.Value.Offen, kv.Value.Partner.ToList()))
            .OrderBy(p => p.Sorte, StringComparer.Ordinal).ThenBy(p => p.Nachricht, StringComparer.Ordinal).ToList();

    /// <summary>Liegt der Namespace im Modul (gleich oder darunter)? Die Außenwelt (null) liegt in keinem.</summary>
    public static bool Drin(string modul, string? ns) =>
        ns != null && (ns == modul || ns.StartsWith(modul + ".", StringComparison.Ordinal));

    public static string? Eltern(string ns)
    {
        var i = ns.LastIndexOf('.');
        return i > 0 ? ns[..i] : null;
    }

    /// <summary>Die Modul-Ports als flache, sortierte Zeilen (Parität Editor ⇄ C#, Tests): „Modul|ein|Sorte|Nachricht|offen".</summary>
    public static IReadOnlyList<string> AlsZeilen(IEnumerable<Modul> module) =>
        module.SelectMany(mo => mo.Eingaenge.Concat(mo.Ausgaenge)
                .Select(p => $"{mo.Namespace}|{p.Richtung}|{p.Sorte}|{p.Nachricht}|{(p.Offen ? "offen" : "")}"))
            .OrderBy(x => x, StringComparer.Ordinal).ToList();
}
