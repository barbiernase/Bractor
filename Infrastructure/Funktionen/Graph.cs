using Abstractions;
using Infrastructure.Komposition;
using Infrastructure.Umgebung;

namespace Infrastructure.Funktionen;

// ═══════════════════════════════════════════════════════════════════════════════════════════════════════════════════════
// GRAPHEN UND INSTANZEN (docs/04 §4.4): ein Graph ist eine Blaupause (generiert aus dem Record, GeneratedGraphen) — Knoten, ihre Wegtabelle aus
// den [Von], Köpfe. Er läuft erst als INSTANZ: eine Datei je Instanz in der Umgebung (umgebungen/<name>/graphen/<instanz>.json) mit dem Graphen, bei einem
// Quellkopf dem Akteur und je Knoten seinen Werten. Dieselbe Blaupause läuft beliebig oft; ein Graph mit Command-Kopf je Umgebung genau einmal.
// ═══════════════════════════════════════════════════════════════════════════════════════════════════════════════════════

/// <summary>
/// Ein Weg der WEGTABELLE (generiert aus den <c>[Von]</c>): eine Ausgabe dieses Typs geht an die Operation eines Knotens desselben Graphen —
/// gesichert (der Absender wartet auf „fertig“) oder verlierbar (einmal geschickt, ohne Antwort).
/// </summary>
public sealed record WegPlan(Type Typ, string Knoten, int Operation, bool Verlierbar = false);

/// <summary>
/// Eine Operation eines Knotens (generiert): die Nummer ihres <c>Auf</c> (so ruft die Rechnung es), die Nachricht, die sie nimmt (null: eine Quelle,
/// die lauscht), was sie ausgeben kann (auch aus den Antworten auf ihre Commands) und ihre Wege an Knoten desselben Graphen.
/// </summary>
public sealed record OperationPlan(int Auf, Type? Eingang, Type[] Ausgaben, WegPlan[] Wege);

/// <summary>Ein Knoten eines Graphen (generiert, aufgefaltet): sein Pfad (Konfig-Schlüssel, <c>Aufbereitung.Vorschau</c>), seine Funktion, seine Operationen.</summary>
public sealed record KnotenPlan(string Name, string Funktion, IReadOnlyList<OperationPlan> Operationen)
{
    public OperationPlan? Operation(int auf) => Operationen.FirstOrDefault(o => o.Auf == auf);

    /// <summary>Die Operation, die eine Nachricht dieses Typs nimmt (je Typ höchstens eine, R2).</summary>
    public OperationPlan? Nimmt(Type typ) => Operationen.FirstOrDefault(o => o.Eingang == typ);
}

/// <summary>
/// Eine ÜBERSETZUNG der Domäne (generiert aus einer Klasse mit <c>ITranslate&lt;TSource, TTarget&gt;</c>, <c>GeneratedUebersetzungen</c>): ihr Name, was
/// sie nimmt und erzeugt, die Rechnung (Feld für Feld, ohne Logik). Sie hängt an keinem Graphen — wohin sie gehört, sagt der Typ, den sie nimmt
/// (<see cref="Uebersetzungen"/>, docs/04 §4.4).
/// </summary>
public sealed record UebersetzungPlan(string Name, Type Ein, Type Aus, Func<object, object> Rechne);

/// <summary>
/// Die Übersetzungen der Anwendung nach dem Typ, den sie nehmen (Invariante 3: Routing über Typen). Eine Ausgabe eines Knotens, ein Event als Antwort auf
/// den Command einer Übersetzung und die Nachricht eines Clients am Kopf schlagen hier nach; mehrere Übersetzungen desselben Typs sind eine Gabel.
/// </summary>
public sealed class Uebersetzungen(IReadOnlyList<UebersetzungPlan> alle)
{
    private static readonly UebersetzungPlan[] Keine = [];
    private readonly IReadOnlyDictionary<Type, UebersetzungPlan[]> _nachTyp = alle.GroupBy(u => u.Ein).ToDictionary(g => g.Key, g => g.ToArray());

    public IReadOnlyList<UebersetzungPlan> Alle { get; } = alle;

    /// <summary>Die Übersetzungen, die eine Nachricht dieses Typs nehmen (leer: keine).</summary>
    public UebersetzungPlan[] Fuer(Type typ) => _nachTyp.TryGetValue(typ, out var u) ? u : Keine;

    /// <summary>Ohne Übersetzungen (Prüfstände, Anwendungen ohne Kataloge).</summary>
    public static readonly Uebersetzungen Leer = new([]);
}

/// <summary>
/// Ein Graph (generiert, <c>GeneratedGraphen</c>): Knoten mit ihrer Wegtabelle, Köpfe (Eingangstyp → Knoten, die ihn von außen nehmen) und was ihn
/// verlässt. Zur Laufzeit wird nur nachgeschlagen: wohin geht eine Ausgabe dieses Typs von diesem Knoten? Ein Graph kennt keine Übersetzung.
/// </summary>
/// <param name="Quelle">Der Quellknoten (eine Funktion, die lauscht): er ist der Kopf, jedes seiner Elemente startet eine Kette; null = keiner.</param>
public sealed record GraphPlan(string Name, Type Typ, IReadOnlyList<KnotenPlan> Knoten, IReadOnlyDictionary<Type, string[]> Koepfe,
    IReadOnlyList<Type> Ausgaenge, string? Quelle = null)
{
    private IReadOnlyDictionary<(string, int, Type), WegPlan[]>? _wege;
    private IReadOnlyDictionary<string, IReadOnlySet<Type>>? _imGraphen;

    public KnotenPlan? KnotenNamens(string name) => Knoten.FirstOrDefault(k => k.Name == name);

    /// <summary>Hat der Graph einen Command-Kopf (einen Command ohne Ziel als Eingang)? Dann läuft er je Umgebung genau einmal.</summary>
    public bool MitCommandKopf => Koepfe.Keys.Any(t => typeof(ICommand).IsAssignableFrom(t));

    /// <summary>
    /// Die Wege einer Ausgabe <paramref name="typ"/> der Operation <paramref name="auf"/> von <paramref name="knoten"/> an die Parents anderer Knoten
    /// (leer: sie verlässt den Graphen).
    /// </summary>
    public IReadOnlyList<WegPlan> Wege(string knoten, int auf, Type typ) =>
        (_wege ??= (from k in Knoten from o in k.Operationen from w in o.Wege group w by (k.Name, o.Auf, w.Typ))
            .ToDictionary(g => g.Key, g => g.ToArray())).TryGetValue((knoten, auf, typ), out var wege) ? wege : Array.Empty<WegPlan>();

    /// <summary>Die Typen, die <paramref name="knoten"/> an Funktionen dieses Graphen gibt (über den Parent; alles andere verlässt ihn).</summary>
    public IReadOnlySet<Type> ImGraphen(string knoten) =>
        (_imGraphen ??= Knoten.ToDictionary(k => k.Name, k => (IReadOnlySet<Type>)k.Operationen.SelectMany(o => o.Wege).Select(w => w.Typ).ToHashSet()))
        .TryGetValue(knoten, out var t) ? t : new HashSet<Type>();
}

/// <summary>
/// Eine Instanz: Name (Datei), Graph, der Akteur ihrer Quellköpfe (null: er folgt dem Auslöser), je Knoten seine Konfig und der Rechner, auf dem sie
/// läuft (null: auf jedem Host).
/// </summary>
public sealed record GraphInstanz(string Name, GraphPlan Graph, string? Akteur, IReadOnlyDictionary<string, KnotenKonfig> Knoten, string? Rechner = null)
{
    /// <summary>Die Konfig eines Knotens (ohne Eintrag: die Standards).</summary>
    public KnotenKonfig Konfig(string knoten) => Knoten.TryGetValue(knoten, out var k) ? k : new KnotenKonfig();

    /// <summary>Der Ort eines Knotens dieser Instanz — Schlüssel der Command-Ids und der Spur (<c>instanz/knoten</c>).</summary>
    public string Ort(string knoten) => Name + "/" + knoten;

    /// <summary>Die Knoten, die einen Eingang dieses Typs am Kopf nehmen (leer: keiner).</summary>
    public IReadOnlyList<string> KopfFuer(Type typ) => Graph.Koepfe.TryGetValue(typ, out var k) ? k : Array.Empty<string>();

    /// <summary>So viele Ketten nimmt die Instanz auf diesem Cluster-Knoten zugleich am Kopf an (die Warteschlange ihres Kopfs; voll → „später“).</summary>
    public int Ketten(Type kopf) =>
        KopfFuer(kopf).Select(k => Konfig(k).Warteschlange).Where(w => w > 0).DefaultIfEmpty(FunktionsOrtKonfig.StandardWarteschlange).Min();
}

/// <summary>
/// Die Instanzen dieses Hosts, beim Start gegen die Graphen geprüft (<see cref="Aus"/>): der Graph existiert, jeder Knoten-Schlüssel ist ein Knoten,
/// der Akteur existiert und darf, was den Graphen verlässt, und ein Graph mit Command-Kopf läuft nur einmal.
/// </summary>
public sealed class GraphInstanzen(IReadOnlyList<GraphInstanz> alle, bool ausUmgebung = false)
{
    public static readonly GraphInstanzen Keine = new(Array.Empty<GraphInstanz>());

    public IReadOnlyList<GraphInstanz> Alle { get; } = alle;

    /// <summary>Aus den Dateien einer Umgebung (<see cref="Aus"/>) — dann gilt die Pflicht des Rechners; je Graph eine Instanz (Prüfstände) nicht.</summary>
    public bool AusUmgebung { get; } = ausUmgebung;

    public GraphInstanz? Von(string name) => Alle.FirstOrDefault(i => i.Name == name);

    /// <summary>Eingangstyp → die Instanzen, deren Graph ihn am Kopf nimmt (Commands ohne Ziel, Quell-Nachrichten, Hinweise).</summary>
    public Verkettung Koepfe(Uebersetzungen? uebersetzungen = null) => new(Alle.SelectMany(i => i.Graph.Koepfe.Keys.Select(t => (t, i.Name)))
        .GroupBy(x => x.t).ToDictionary(g => g.Key, g => g.Select(x => x.Name).ToArray()), uebersetzungen);

    /// <summary>
    /// Je Graph eine Instanz unter seinem Namen — für Prüfstände ohne Umgebung (Tests); jeder Knoten mit den Standards bzw. der Konfig, die
    /// <paramref name="jeFunktion"/> für seine Funktion nennt. Ein Graph mit Quelle läuft nur, wenn sie für seine Quelle eine Konfig nennt (eine Quelle
    /// braucht ihre Werte — welches Verzeichnis, welcher Takt).
    /// </summary>
    public static GraphInstanzen JeGraphEine(IReadOnlyList<GraphPlan> graphen, IReadOnlyDictionary<string, KnotenKonfig>? jeFunktion = null) =>
        new(graphen.Where(g => g.Quelle is not { } q || jeFunktion?.ContainsKey(g.KnotenNamens(q)!.Funktion) == true)
            .Select(g => new GraphInstanz(g.Name, g, null, g.Knoten
                .Where(k => jeFunktion?.ContainsKey(k.Funktion) == true)
                .ToDictionary(k => k.Name, k => jeFunktion![k.Funktion]))).ToList());

    /// <summary>
    /// Die Instanzen aus der Umgebung, geprüft; ein Fehler ist ein Boot-Fehler mit der Datei als Karte. Mit <paramref name="akteure"/> wird der Akteur
    /// einer Instanz geprüft: er darf, was sie verlässt — nach der Übersetzung (<paramref name="uebersetzungen"/>): die Commands an Aggregate, die aus
    /// ihren Ausgängen werden.
    /// </summary>
    public static GraphInstanzen Aus(IReadOnlyDictionary<string, GraphInstanzKonfig> konfig, IReadOnlyList<GraphPlan> graphen,
        IReadOnlyDictionary<string, Infrastructure.Akteure.AkteurRechte>? akteure = null, Uebersetzungen? uebersetzungen = null)
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
                var verlassen = g.Ausgaenge.SelectMany(t => (uebersetzungen ?? Uebersetzungen.Leer).Fuer(t) is { Length: > 0 } u ? u.Select(x => x.Aus) : [t]);
                foreach (var aus in verlassen.Distinct().Where(t => typeof(IAggregatCommand).IsAssignableFrom(t) && !rechte.Commands.Contains(t)))
                    throw new BootFehler($"Instanz {name}: der Akteur {akteur} darf {aus.Name} nicht (kein IDarf<{aus.Name}>) — was den Graphen verlässt, gibt "
                        + "er hinein.", Karte());
            }
            if (k.Rechner is { } r && (r.Length == 0 || r.Trim() != r || r.Any(c => c is ',' or '*' or '?' || char.IsWhiteSpace(c))))
                throw new BootFehler($"Instanz {name}: Rechner '{r}' ist kein Name — ein Rechner ist genau ein Name (kein Muster, keine Liste).", Karte());
            alle.Add(new GraphInstanz(name, g, string.IsNullOrEmpty(k.Akteur) ? null : k.Akteur, k.Knoten, k.Rechner));
        }
        foreach (var doppelt in alle.Where(i => i.Graph.MitCommandKopf).GroupBy(i => i.Graph.Name).Where(x => x.Count() > 1))
            throw new BootFehler($"Der Graph {doppelt.Key} hat einen Command-Kopf und läuft je Umgebung genau einmal — Instanzen: "
                + string.Join(", ", doppelt.Select(i => i.Name)) + " (ein Command hat genau einen Empfänger).", "instanz:" + doppelt.Last().Name);
        return new GraphInstanzen(alle, ausUmgebung: true);
    }
}
