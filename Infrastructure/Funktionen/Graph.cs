using Abstractions;
using Infrastructure.Komposition;
using Infrastructure.Umgebung;

namespace Infrastructure.Funktionen;

// ═══════════════════════════════════════════════════════════════════════════════════════════════════════════════════════
// GRAPHEN UND INSTANZEN (docs/04 §4.4): ein Graph ist eine Blaupause (generiert aus dem Record, GeneratedGraphen) — Knoten, Kanten aus den Typen,
// Köpfe. Er läuft erst als INSTANZ: eine Datei je Instanz in der Umgebung (umgebungen/<name>/graphen/<instanz>.json) mit dem Graphen, bei einem
// Quellkopf dem Akteur und je Knoten seinen Werten. Dieselbe Blaupause läuft beliebig oft; ein Graph mit Command-Kopf je Umgebung genau einmal.
// ═══════════════════════════════════════════════════════════════════════════════════════════════════════════════════════

/// <summary>Ein Knoten eines Graphen (generiert): sein Name (Konfig-Schlüssel), seine Funktion und seine Kanten (Command ohne Ziel → Knoten desselben Graphen).</summary>
public sealed record KnotenPlan(string Name, string Funktion, IReadOnlyDictionary<Type, string[]> Kanten);

/// <summary>Ein Graph (generiert, <c>GeneratedGraphen</c>): Knoten, Köpfe (Eingangstyp → Knoten, die ihn nehmen) und was ihn verlässt.</summary>
public sealed record GraphPlan(string Name, Type Typ, IReadOnlyList<KnotenPlan> Knoten, IReadOnlyDictionary<Type, string[]> Koepfe,
    IReadOnlyList<Type> Ausgaenge)
{
    public KnotenPlan? KnotenNamens(string name) => Knoten.FirstOrDefault(k => k.Name == name);

    /// <summary>Hat der Graph einen Command-Kopf (einen Command ohne Ziel als Eingang)? Dann läuft er je Umgebung genau einmal.</summary>
    public bool MitCommandKopf => Koepfe.Keys.Any(t => typeof(ICommand).IsAssignableFrom(t));
}

/// <summary>Eine Instanz: Name (Datei), Graph, der Akteur ihrer Quellköpfe (null: er folgt dem Auslöser) und je Knoten seine Konfig.</summary>
public sealed record GraphInstanz(string Name, GraphPlan Graph, string? Akteur, IReadOnlyDictionary<string, KnotenKonfig> Knoten)
{
    /// <summary>Die Konfig eines Knotens (ohne Eintrag: die Standards).</summary>
    public KnotenKonfig Konfig(string knoten) => Knoten.TryGetValue(knoten, out var k) ? k : new KnotenKonfig();

    /// <summary>Der Ort eines Knotens dieser Instanz — Schlüssel der Command-Ids und der Spur (<c>instanz/knoten</c>).</summary>
    public string Ort(string knoten) => Name + "/" + knoten;

    /// <summary>Die Knoten, die einen Eingang dieses Typs am Kopf nehmen (leer: keiner).</summary>
    public IReadOnlyList<string> KopfFuer(Type typ) => Graph.Koepfe.TryGetValue(typ, out var k) ? k : Array.Empty<string>();
}

/// <summary>
/// Die Instanzen dieses Hosts, beim Start gegen die Graphen geprüft (<see cref="Aus"/>): der Graph existiert, jeder Knoten-Schlüssel ist ein Knoten,
/// der Akteur existiert und darf, was den Graphen verlässt, und ein Graph mit Command-Kopf läuft nur einmal.
/// </summary>
public sealed class GraphInstanzen(IReadOnlyList<GraphInstanz> alle)
{
    public static readonly GraphInstanzen Keine = new(Array.Empty<GraphInstanz>());

    public IReadOnlyList<GraphInstanz> Alle { get; } = alle;

    public GraphInstanz? Von(string name) => Alle.FirstOrDefault(i => i.Name == name);

    /// <summary>Eingangstyp → die Instanzen, deren Graph ihn am Kopf nimmt (Commands ohne Ziel, Quell-Nachrichten, Hinweise).</summary>
    public Verkettung Koepfe() => new(Alle.SelectMany(i => i.Graph.Koepfe.Keys.Select(t => (t, i.Name)))
        .GroupBy(x => x.t).ToDictionary(g => g.Key, g => g.Select(x => x.Name).ToArray()));

    /// <summary>
    /// Je Graph eine Instanz unter seinem Namen — für Prüfstände ohne Umgebung (Tests); jeder Knoten mit den Standards bzw. der Konfig, die
    /// <paramref name="jeFunktion"/> für seine Funktion nennt.
    /// </summary>
    public static GraphInstanzen JeGraphEine(IReadOnlyList<GraphPlan> graphen, IReadOnlyDictionary<string, KnotenKonfig>? jeFunktion = null) =>
        new(graphen.Select(g => new GraphInstanz(g.Name, g, null, g.Knoten
            .Where(k => jeFunktion?.ContainsKey(k.Funktion) == true)
            .ToDictionary(k => k.Name, k => jeFunktion![k.Funktion]))).ToList());

    /// <summary>Die Instanzen aus der Umgebung, geprüft; ein Fehler ist ein Boot-Fehler mit der Datei als Karte.</summary>
    public static GraphInstanzen Aus(IReadOnlyDictionary<string, GraphInstanzKonfig> konfig, IReadOnlyList<GraphPlan> graphen,
        IReadOnlyDictionary<string, Infrastructure.Akteure.AkteurRechte>? akteure = null)
    {
        var alle = new List<GraphInstanz>();
        foreach (var (name, k) in konfig.OrderBy(x => x.Key, StringComparer.Ordinal))
        {
            string Karte() => "instanz:" + name;
            var g = graphen.FirstOrDefault(x => x.Name == k.Graph)
                ?? throw new BootFehler($"Instanz {name}: Graph '{k.Graph}' gibt es nicht (bekannt: {string.Join(", ", graphen.Select(x => x.Name).DefaultIfEmpty("keine"))}).",
                    Karte());
            foreach (var fremd in k.Knoten.Keys.Where(n => g.KnotenNamens(n) is null))
                throw new BootFehler($"Instanz {name}: '{fremd}' ist kein Knoten von {g.Name} (Knoten: {string.Join(", ", g.Knoten.Select(x => x.Name))}).", Karte());
            if (k.Akteur is { Length: > 0 } akteur && akteure is not null)
            {
                if (!akteure.TryGetValue(akteur, out var rechte))
                    throw new BootFehler($"Instanz {name}: Akteur '{akteur}' gibt es nicht.", Karte());
                foreach (var aus in g.Ausgaenge.Where(t => typeof(IAggregatCommand).IsAssignableFrom(t) && !rechte.Commands.Contains(t)))
                    throw new BootFehler($"Instanz {name}: der Akteur {akteur} darf {aus.Name} nicht (kein IDarf<{aus.Name}>) — was den Graphen verlässt, gibt "
                        + "er hinein.", Karte());
            }
            alle.Add(new GraphInstanz(name, g, string.IsNullOrEmpty(k.Akteur) ? null : k.Akteur, k.Knoten));
        }
        foreach (var doppelt in alle.Where(i => i.Graph.MitCommandKopf).GroupBy(i => i.Graph.Name).Where(x => x.Count() > 1))
            throw new BootFehler($"Der Graph {doppelt.Key} hat einen Command-Kopf und läuft je Umgebung genau einmal — Instanzen: "
                + string.Join(", ", doppelt.Select(i => i.Name)) + " (ein Command hat genau einen Empfänger).", "instanz:" + doppelt.Last().Name);
        return new GraphInstanzen(alle);
    }
}
