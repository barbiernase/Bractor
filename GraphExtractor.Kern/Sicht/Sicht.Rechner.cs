using System.Text.Json.Nodes;
using Abstractions;

namespace GraphExtractor;

// RECHNER UND BACKEND der Fläche (docs/06 §6.5, docs/10 §10.9, docs/10 E18): je Umgebung der Pool (Name → Adresse), welche Rechner das
//   Backend bilden, was auf jedem Rechner steht (das Backend, Instanzen der Graphen, App-Instanzen) und die Befunde, die der Start melden würde —
//   dieselbe Regel wie im Host (Abstractions.Rechnerregeln). Gerechnet aus rahmen.umgebungen (roh, Platzhalter bleiben stehen, die Instanzen unter
//   „Graphen“); der Editor wählt nur die aktive Umgebung.
public sealed partial class Sicht
{
    /// <summary>
    /// Pool und Backend einer Umgebung aus der rohen Datei — ohne Abschnitt nur <c>localhost</c> (wie der Lader des Hosts). <c>Standard</c> sagt, ob der
    /// Pool bzw. das Backend so ergänzt ist (dann schreibt die erste Geste den Abschnitt).
    /// </summary>
    private static (Dictionary<string, string> Pool, List<string> Backend, bool PoolStandard, bool BackendStandard) PoolUndBackend(JsonObject inhalt)
    {
        var pool = new Dictionary<string, string>(StringComparer.Ordinal);
        var poolStandard = inhalt[UmgebungsAbschnitte.Rechner] is not JsonObject rechner || rechner.Count == 0;
        if (!poolStandard)
            foreach (var (name, r) in (JsonObject)inhalt[UmgebungsAbschnitte.Rechner]!)
                pool[name] = WertText(r?[nameof(RechnerKonfig.Adresse)]);
        else pool[Rechnerregeln.Standard] = Rechnerregeln.Standard;
        var backendStandard = inhalt[UmgebungsAbschnitte.Backend]?[nameof(BackendKonfig.Rechner)] is not JsonArray;
        var backend = backendStandard
            ? [Rechnerregeln.Standard]
            : ((JsonArray)inhalt[UmgebungsAbschnitte.Backend]![nameof(BackendKonfig.Rechner)]!).Select(x => WertText(x)).ToList();
        return (pool, backend, poolStandard, backendStandard);
    }

    /// <summary>
    /// <c>board.sicht.flaeche.rechner</c>: je Umgebung der Pool (je Rechner Name, Adresse, ob er zum Backend gehört, was dort steht, Befunde), das Backend
    /// (seine Rechner, Einzelknoten oder Consul, Befunde), je Instanz eines Graphen ihr Rechner samt Befunden und die App-Instanzen (<see cref="AppInstanzenJson"/>).
    /// </summary>
    private JsonObject RechnerJson()
    {
        var aus = new JsonObject();
        foreach (var (umgebung, inhalt) in UmgebungenRoh())
        {
            var (pool, backend, poolStandard, backendStandard) = PoolUndBackend(inhalt);
            var graphen = inhalt[UmgebungsAbschnitte.Graphen] as JsonObject ?? new JsonObject();
            var instanzen = Eintraege(graphen).Where(g => g.Value is JsonObject)
                .Select(g => (Name: g.Key, Graph: S(g.Value, nameof(GraphInstanzKonfig.Graph)),
                    Rechner: g.Value![nameof(GraphInstanzKonfig.Rechner)] is JsonValue v && v.TryGetValue<string>(out var r) ? r : null))
                .ToList();
            var apps = AppsRoh(inhalt).Select(a => (a.Name, Rechner: AppRechner(a.Inhalt), Port: AppPort(a.Inhalt), App: S(a.Inhalt, nameof(AppInstanzKonfig.App))))
                .ToList();
            var transport = inhalt[UmgebungsAbschnitte.Transport] as JsonObject;
            int Port(string feld, int standard) => int.TryParse(WertText(transport?[feld]), out var p) ? p : standard;
            var befunde = Rechnerregeln.Befunde(pool, backend, instanzen.Select(i => (i.Name, i.Rechner)), apps.Select(a => (a.Name, a.Rechner, a.Port)),
                [Port(nameof(TransportKonfig.GrpcPort), new TransportKonfig().GrpcPort), Port(nameof(TransportKonfig.EinblickPort), 0)]);
            var (appsJson, karten, kanten) = AppInstanzenJson(inhalt, pool.Keys, backend, befunde);
            // Was auf einem Rechner steht: das Backend, die Instanzen, die ihn nennen, die App-Instanzen, die auf ihm laufen.
            var dort = pool.Keys.ToDictionary(r => r, _ => new JsonArray(), StringComparer.Ordinal);
            foreach (var r in backend.Where(dort.ContainsKey).Distinct(StringComparer.Ordinal)) dort[r].Add(new JsonObject { ["art"] = "backend" });
            foreach (var i in instanzen.Where(i => i.Rechner != null && dort.ContainsKey(i.Rechner)))
                dort[i.Rechner!].Add(new JsonObject { ["art"] = "instanz", ["name"] = i.Name, ["graph"] = i.Graph });
            foreach (var a in apps.Where(a => dort.ContainsKey(a.Rechner)).OrderBy(a => a.Name, StringComparer.Ordinal))
                dort[a.Rechner].Add(new JsonObject { ["art"] = "app", ["name"] = a.Name, ["app"] = a.App, ["port"] = a.Port });
            var imBackend = backend.ToHashSet(StringComparer.Ordinal);
            aus[umgebung] = new JsonObject
            {
                ["pool"] = new JsonArray(pool.Select(p => (JsonNode)new JsonObject
                {
                    ["name"] = p.Key, ["adresse"] = p.Value, ["backend"] = imBackend.Contains(p.Key), ["dort"] = dort[p.Key],
                    ["befunde"] = Arr(befunde.Where(b => b.Karte == Rechnerregeln.Karte(p.Key)).Select(b => b.Text)),
                }).ToArray()),
                ["poolStandard"] = poolStandard,
                ["backend"] = Arr(backend),
                ["backendStandard"] = backendStandard,
                ["einzelknoten"] = backend.Count <= 1,
                ["befunde"] = Arr(befunde.Where(b => b.Karte == Rechnerregeln.BackendKarte).Select(b => b.Text)),
                ["instanzen"] = new JsonObject(instanzen.Select(i => KeyValuePair.Create(i.Name, (JsonNode?)new JsonObject
                {
                    ["graph"] = i.Graph, ["rechner"] = i.Rechner,
                    ["befunde"] = Arr(befunde.Where(b => b.Karte == Rechnerregeln.InstanzKarte(i.Name)).Select(b => b.Text)),
                }))),
                // Die App-Instanzen (Spalte Rechner) und was die Fläche dazu zeichnet: je Rechner eine Karte, Kanten Rechner → Client und Quelle → Akteur.
                ["apps"] = appsJson,
                ["karten"] = karten,
                ["kanten"] = kanten,
            };
        }
        return aus;
    }
}
