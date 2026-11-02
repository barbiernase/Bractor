using Abstractions;
using Infrastructure.Komposition;
using Infrastructure.Umgebung;

namespace Infrastructure.Funktionen;

// ═══════════════════════════════════════════════════════════════════════════════════════════════════════════════════════
// GRAPHEN UND INSTANZEN (docs/04 §4.4): ein Graph ist eine Blaupause (generiert aus dem Record, GeneratedGraphen) — Knoten, Kanten aus den Typen,
// Köpfe. Er läuft erst als INSTANZ: eine Datei je Instanz in der Umgebung (umgebungen/<name>/graphen/<instanz>.json) mit dem Graphen, bei einem
// Quellkopf dem Akteur und je Knoten seinen Werten. Dieselbe Blaupause läuft beliebig oft; ein Graph mit Command-Kopf je Umgebung genau einmal.
// ═══════════════════════════════════════════════════════════════════════════════════════════════════════════════════════

/// <summary>Wie ein Eingangsport seine Nachrichten erwartet (Entscheidung 5): genau einer, höchstens einer, der erste von mehreren, alle eine Ebene tiefer.</summary>
public enum Portform { Eins, Hoechstens, Erster, Alle }

/// <summary>Ein Erzeuger eines Ports: Knoten und Operation (Nummer ihres <c>Auf</c>), der Typ und ob er als Strom kommt (je Element ein Unterlauf).</summary>
public sealed record ErzeugerPlan(string Knoten, int Operation, Type Typ, bool Strom);

/// <summary>Ein Eingangsport (generiert): Parametername, Form, die Typen (bei <c>OneOf</c> mehrere) und seine Erzeuger im Graphen (keiner: ein Kopf).</summary>
public sealed record PortPlan(string Name, Portform Form, Type[] Typen, ErzeugerPlan[] Erzeuger);

/// <summary>
/// Eine Operation eines Knotens (generiert): die Nummer ihres <c>Auf</c> (so ruft die Rechnung es), ihre Ports, was sie ausgeben kann (auch aus den
/// Antworten auf ihre Commands), was davon als Strom kommt, und ihre Ebene (die Knoten, deren Ströme bis hierher Unterläufe öffnen).
/// </summary>
public sealed record OperationPlan(int Auf, IReadOnlyList<PortPlan> Ports, Type[] Ausgaben, Type[] Stroeme, string[] Ebene);

/// <summary>Ein Knoten eines Graphen (generiert, aufgefaltet): sein Pfad (Konfig-Schlüssel, <c>Aufbereitung.Vorschau</c>), seine Funktion, seine Operationen.</summary>
public sealed record KnotenPlan(string Name, string Funktion, IReadOnlyList<OperationPlan> Operationen)
{
    public OperationPlan? Operation(int auf) => Operationen.FirstOrDefault(o => o.Auf == auf);
}

/// <summary>
/// Ein Graph (generiert, <c>GeneratedGraphen</c>): Knoten, Köpfe (Eingangstyp → Knoten, die ihn nehmen) und was ihn verlässt. Die Abnehmer je
/// Ausgabe leitet er einmal aus den Erzeugern der Ports ab (die Kanten stehen im Generat, nicht im Rumpf).
/// </summary>
/// <param name="Quelle">Der Quellknoten (eine Funktion, die lauscht): er ist der Kopf, jedes seiner Elemente startet einen Lauf; null = keiner.</param>
public sealed record GraphPlan(string Name, Type Typ, IReadOnlyList<KnotenPlan> Knoten, IReadOnlyDictionary<Type, string[]> Koepfe,
    IReadOnlyList<Type> Ausgaenge, string? Quelle = null)
{
    private IReadOnlyDictionary<(string, int, Type), (string Knoten, int Operation, int Port)[]>? _abnehmer;
    private IReadOnlyDictionary<string, IReadOnlySet<Type>>? _imGraphen;
    private IReadOnlyDictionary<string, (string Knoten, int Operation, int Port)[]>? _sammler;

    public KnotenPlan? KnotenNamens(string name) => Knoten.FirstOrDefault(k => k.Name == name);

    /// <summary>Hat der Graph einen Command-Kopf (einen Command ohne Ziel als Eingang)? Dann läuft er je Umgebung genau einmal.</summary>
    public bool MitCommandKopf => Koepfe.Keys.Any(t => typeof(ICommand).IsAssignableFrom(t));

    /// <summary>Die Ports, die eine Ausgabe <paramref name="typ"/> der Operation <paramref name="auf"/> von <paramref name="knoten"/> bekommen.</summary>
    public IReadOnlyList<(string Knoten, int Operation, int Port)> Abnehmer(string knoten, int auf, Type typ) =>
        (_abnehmer ??= BaueAbnehmer()).TryGetValue((knoten, auf, typ), out var a) ? a : Array.Empty<(string, int, int)>();

    /// <summary>Die Typen, die <paramref name="knoten"/> an Knoten dieses Graphen gibt (alles andere verlässt ihn).</summary>
    public IReadOnlySet<Type> ImGraphen(string knoten) =>
        (_imGraphen ??= BaueImGraphen()).TryGetValue(knoten, out var t) ? t : new HashSet<Type>();

    /// <summary>Der Knoten, dessen Strom die Unterläufe öffnet, aus denen ein Sammel-Port (<c>IReadOnlyList&lt;T&gt;</c>) sammelt.</summary>
    public string Ursprung(PortPlan sammel)
    {
        var e = sammel.Erzeuger[0];
        return e.Strom ? e.Knoten : KnotenNamens(e.Knoten)!.Operation(e.Operation)!.Ebene[^1];
    }

    /// <summary>Die Sammel-Ports, die aus den Unterläufen von <paramref name="ursprung"/> sammeln (auch mittelbar: aus einem Knoten im Unterlauf).</summary>
    public IReadOnlyList<(string Knoten, int Operation, int Port)> Sammler(string ursprung) =>
        (_sammler ??= (from k in Knoten
                       from o in k.Operationen
                       from p in o.Ports.Select((p, i) => (p, i))
                       where p.p.Form == Portform.Alle && p.p.Erzeuger.Length > 0
                       group (k.Name, o.Auf, p.i) by Ursprung(p.p))
            .ToDictionary(g => g.Key, g => g.ToArray())).TryGetValue(ursprung, out var s) ? s : Array.Empty<(string, int, int)>();

    private Dictionary<(string, int, Type), (string, int, int)[]> BaueAbnehmer() =>
        (from k in Knoten
         from o in k.Operationen
         from p in o.Ports.Select((p, i) => (p, i))
         from e in p.p.Erzeuger
         group (k.Name, o.Auf, p.i) by (e.Knoten, e.Operation, e.Typ))
        .ToDictionary(g => g.Key, g => g.ToArray());

    private Dictionary<string, IReadOnlySet<Type>> BaueImGraphen() =>
        Knoten.SelectMany(k => k.Operationen).SelectMany(o => o.Ports).SelectMany(p => p.Erzeuger)
            .GroupBy(e => e.Knoten).ToDictionary(g => g.Key, g => (IReadOnlySet<Type>)g.Select(e => e.Typ).ToHashSet());
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

    /// <summary>So viele Läufe nimmt die Instanz auf diesem Cluster-Knoten zugleich an (die Warteschlange ihres Kopfs; voll → „später“).</summary>
    public int Laeufe(Type kopf) =>
        KopfFuer(kopf).Select(k => Konfig(k).Warteschlange).Where(w => w > 0).DefaultIfEmpty(FunktionsOrtKonfig.StandardWarteschlange).Min();
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
    /// <paramref name="jeFunktion"/> für seine Funktion nennt. Ein Graph mit Quelle läuft nur, wenn sie für seine Quelle eine Konfig nennt (eine Quelle
    /// braucht ihre Werte — welches Verzeichnis, welcher Takt). Mit <paramref name="akteure"/> dazu je Akteur mit Arbeit seine Instanz
    /// (<see cref="Arbeit"/>).
    /// </summary>
    public static GraphInstanzen JeGraphEine(IReadOnlyList<GraphPlan> graphen, IReadOnlyDictionary<string, KnotenKonfig>? jeFunktion = null,
        IReadOnlyDictionary<string, Infrastructure.Akteure.AkteurRechte>? akteure = null) =>
        new(graphen.Where(g => g.Quelle is not { } q || jeFunktion?.ContainsKey(g.KnotenNamens(q)!.Funktion) == true)
            .Select(g => new GraphInstanz(g.Name, g, null, g.Knoten
                .Where(k => jeFunktion?.ContainsKey(k.Funktion) == true)
                .ToDictionary(k => k.Name, k => jeFunktion![k.Funktion])))
            .Concat(akteure is null ? [] : Arbeit.Instanzen(akteure)).ToList());

    /// <summary>
    /// Die Instanzen aus der Umgebung, geprüft; ein Fehler ist ein Boot-Fehler mit der Datei als Karte. Mit <paramref name="akteure"/> dazu je Akteur
    /// mit Arbeit seine Instanz (<see cref="Arbeit"/>; ein Name, den schon eine Datei trägt, ist ein Boot-Fehler).
    /// </summary>
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
        if (akteure is not null)
            foreach (var arbeit in Arbeit.Instanzen(akteure))
            {
                if (alle.Any(i => i.Name == arbeit.Name))
                    throw new BootFehler($"Instanz {arbeit.Name}: der Name gehört dem Akteur {arbeit.Name}, dessen Arbeit eine eigene Instanz ist.", "instanz:" + arbeit.Name);
                alle.Add(arbeit);
            }
        foreach (var doppelt in alle.Where(i => i.Graph.MitCommandKopf).GroupBy(i => i.Graph.Name).Where(x => x.Count() > 1))
            throw new BootFehler($"Der Graph {doppelt.Key} hat einen Command-Kopf und läuft je Umgebung genau einmal — Instanzen: "
                + string.Join(", ", doppelt.Select(i => i.Name)) + " (ein Command hat genau einen Empfänger).", "instanz:" + doppelt.Last().Name);
        return new GraphInstanzen(alle);
    }
}
