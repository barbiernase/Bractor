using System.Text.Json;
using System.Text.Json.Nodes;
using Abstractions;

namespace Werkbank.Server;

/// <summary>
/// Die Instanzen der Graphen einer Umgebung (<c>umgebungen/&lt;name&gt;/graphen/&lt;instanz&gt;.json</c>, docs/04 §4.4, docs/10 §10.3.5): lesen (unaufgelöst,
/// wie sie dastehen) und — als Geste — schreiben bzw. entfernen. Dazu das Formular einer Instanz, abgeleitet aus dem Board (W3): je Knoten (Pfad durch
/// innere Graphen) die Rahmenfelder, die Felder seiner Konfig-Records und die Wahl seiner Technik.
/// </summary>
public sealed class Instanzen(string slnRoot, Umgebungen umgebungen)
{
    public string Ordner(string umgebung) => Path.Combine(umgebungen.Verzeichnis, umgebung, Projektstruktur.GraphenOrdner);

    /// <summary>Die Datei einer Instanz relativ zur Solution (Pfad im Patch einer Geste).</summary>
    public string RelativeDatei(string umgebung, string instanz)
    {
        if (instanz.Length == 0 || instanz.IndexOfAny(['/', '\\', '.']) >= 0 || umgebung.IndexOfAny(['/', '\\']) >= 0 || umgebung.StartsWith('.'))
            throw new ArgumentException($"Kein gültiger Name: „{umgebung}/{instanz}“.");
        return Path.GetRelativePath(slnRoot, Path.Combine(Ordner(umgebung), instanz + ".json")).Replace('\\', '/');
    }

    /// <summary>Alle Instanzen einer Umgebung: Name → Inhalt (Platzhalter bleiben stehen).</summary>
    public JsonObject Liste(string umgebung)
    {
        var aus = new JsonObject();
        if (!Directory.Exists(Ordner(umgebung))) return aus;
        foreach (var datei in Directory.GetFiles(Ordner(umgebung), "*.json").OrderBy(d => d, StringComparer.Ordinal))
            aus[Path.GetFileNameWithoutExtension(datei)] = JsonNode.Parse(File.ReadAllText(datei),
                documentOptions: new() { CommentHandling = JsonCommentHandling.Skip }) ?? new JsonObject();
        return aus;
    }

    /// <summary>
    /// Die Projektstruktur, wie der Editor neue Elemente ablegt (docs/07, eine Quelle): die Wurzel, je Rolle ihr Projekt und ob sie Bereiche hat, je
    /// Primitive die Rollen, in denen sie stehen darf.
    /// </summary>
    public static JsonObject Struktur()
    {
        var wurzel = Projektstruktur.StandardWurzel;
        return new JsonObject
        {
            ["wurzel"] = wurzel,
            ["rollen"] = new JsonArray(Projektstruktur.Rollen.Where(e => !e.NurGeneriert).Select(e => (JsonNode)new JsonObject
            {
                ["rolle"] = e.Rolle.ToString(), ["projekt"] = Projektstruktur.Projekt(wurzel, e.Rolle), ["mitBereich"] = e.MitBereich,
            }).ToArray()),
            ["primitive"] = new JsonObject(Enum.GetValues<Projektstruktur.Primitive>().Select(p => KeyValuePair.Create(p.ToString(),
                (JsonNode?)new JsonArray(Projektstruktur.RollenVon(p).Select(r => (JsonNode)r.ToString()).ToArray())))),
        };
    }

    /// <summary>
    /// Das Formular einer Instanz des Graphen <paramref name="graph"/>, aus dem Board: ob er eine Quelle hat (dann nennt die Instanz ihren Akteur),
    /// ob er Commands am Kopf nimmt (dann je Umgebung genau eine Instanz), ob Zustand oder Quelle einen Rechner verlangen, der Standard der Stränge — und je Knoten (Pfad, innere
    /// Graphen mit Punkt) seine Funktion, die Rahmenfelder, die Felder seiner Konfig-Records und die Technik mit mehr als einer Umsetzung.
    /// </summary>
    public static JsonObject Schema(JsonObject board, string graph)
    {
        var graphen = Liste(board["graphen"]).ToDictionary(g => S(g, "name"), StringComparer.Ordinal);
        var funktionen = Liste(board["bausteinFunktionen"]).Concat(Liste(board["katalog"]))
            .GroupBy(f => S(f, "name"), StringComparer.Ordinal).ToDictionary(g => g.Key, g => g.First(), StringComparer.Ordinal);
        if (!graphen.TryGetValue(graph, out var wurzel)) throw new ArgumentException($"Kein Graph „{graph}“.");

        var knoten = new JsonArray();
        var mitZustand = false;
        var mitQuelle = false;
        void Knoten(JsonObject g, string praefix, HashSet<string> besucht)
        {
            foreach (var k in Liste(g["knoten"]))
            {
                var typ = S(k, "typ");
                var basis = typ.Split('<')[0].Trim();
                var pfad = praefix + S(k, "name");
                if (graphen.TryGetValue(basis, out var innen))
                {
                    if (besucht.Add(basis)) Knoten(innen, pfad + ".", besucht);
                    besucht.Remove(basis);
                    continue;
                }
                var f = funktionen.GetValueOrDefault(basis);
                var konfigs = new JsonArray();
                var technik = new JsonArray();
                if (f != null) Bedarf(f, konfigs, technik, funktionen, []);
                var quelle = f?["lauscht"] is JsonArray { Count: > 0 };
                var zustand = (string?)f?["zustand"];
                mitZustand |= zustand != null;
                mitQuelle |= quelle;
                knoten.Add(new JsonObject
                {
                    ["pfad"] = pfad, ["typ"] = typ, ["funktion"] = basis, ["bekannt"] = f != null, ["katalog"] = f != null && f["datei"] == null,
                    ["quelle"] = quelle, ["zustand"] = zustand, ["konfigs"] = konfigs, ["technik"] = technik,
                });
            }
        }
        Knoten(wurzel, "", [graph]);
        return new JsonObject
        {
            ["graph"] = graph,
            ["quelle"] = mitQuelle,
            ["commandKopf"] = !mitQuelle && (wurzel["koepfe"] as JsonArray ?? []).Count > 0,
            ["rechnerPflicht"] = mitQuelle || mitZustand,
            ["straengeStandard"] = GraphInstanzKonfig.StandardStraenge,
            ["rahmenfelder"] = new JsonArray(KnotenKonfig.Rahmenfelder.Where(r => r != nameof(KnotenKonfig.Technik)).Select(r => (JsonNode)new JsonObject
            {
                ["feld"] = r,
                ["typ"] = r switch { nameof(KnotenKonfig.Vorwaermen) => "bool", nameof(KnotenKonfig.Zeitlimit) => "TimeSpan", _ => "int" },
            }).ToArray()),
            ["knoten"] = knoten,
        };
    }

    /// <summary>
    /// Was der Konstruktor einer Funktion (transitiv über ihre Technik) am Knoten braucht: die Konfig-Records (auch die der Umsetzungen; bei mehreren
    /// Umsetzungen mit dem Namen der Umsetzung) und je Technik mit mehr als einer Umsetzung die Wahl.
    /// </summary>
    private static void Bedarf(JsonObject f, JsonArray konfigs, JsonArray technik, IReadOnlyDictionary<string, JsonObject> funktionen, HashSet<string> besucht,
        string? umsetzung = null)
    {
        foreach (var k in Liste(f["konfigs"])) konfigs.Add(Mit(k, umsetzung));
        foreach (var p in Liste(f["konstruktor"]))
        {
            var typ = S(p, "typ").Split('<')[0];
            if (!funktionen.TryGetValue(typ, out var t) || !besucht.Add(typ)) continue;
            var umsetzungen = Liste(t["umsetzungen"]).ToList();
            if (umsetzungen.Count > 1)
                technik.Add(new JsonObject
                {
                    ["schnittstelle"] = typ, ["umsetzungen"] = new JsonArray(umsetzungen.Select(u => (JsonNode)S(u, "name")).ToArray()),
                });
            // Eine Technik als Klasse trägt ihren Konstruktor selbst; eine Schnittstelle über ihre Umsetzungen.
            if ((bool?)t["klasse"] == true) Bedarf(t, konfigs, technik, funktionen, besucht);
            foreach (var u in umsetzungen)
            {
                foreach (var k in Liste(u["konfigs"])) konfigs.Add(Mit(k, umsetzungen.Count > 1 ? S(u, "name") : null));
                Bedarf(new JsonObject { ["konstruktor"] = u["konstruktor"]?.DeepClone() }, konfigs, technik, funktionen, besucht, umsetzungen.Count > 1 ? S(u, "name") : null);
            }
        }
    }

    private static JsonObject Mit(JsonObject konfig, string? umsetzung)
    {
        var k = (JsonObject)konfig.DeepClone();
        if (umsetzung != null) k["umsetzung"] = umsetzung;
        return k;
    }

    private static IEnumerable<JsonObject> Liste(JsonNode? n) => (n as JsonArray ?? []).OfType<JsonObject>();
    private static string S(JsonObject o, string k) => (string?)o[k] ?? "";

    /// <summary>Der Dateitext einer Instanz (hübsch eingerückt).</summary>
    public static string Text(JsonObject inhalt) => Umgebungen.Text(inhalt);
}
