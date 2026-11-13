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
    /// Das Formular einer Instanz des Graphen <paramref name="graph"/>, aus dem Board: ob Zustand einen Rechner verlangt, der Standard der Stränge — und
    /// je Knoten (Pfad, innere Graphen mit Punkt) seine Funktion, die Rahmenfelder, die Felder seiner Konfig-Records und die Technik mit mehr als einer
    /// Umsetzung. Je Umgebung läuft ein Graph genau einmal; den Akteur einer Kette bringt ihr Auslöser mit (keine Quelle, kein Akteur an der Instanz).
    /// </summary>
    public static JsonObject Schema(JsonObject board, string graph)
    {
        var graphen = Liste(board["graphen"]).ToDictionary(g => S(g, "name"), StringComparer.Ordinal);
        var funktionen = Liste(board["bausteinFunktionen"])
            .GroupBy(f => S(f, "name"), StringComparer.Ordinal).ToDictionary(g => g.Key, g => g.First(), StringComparer.Ordinal);
        if (!graphen.TryGetValue(graph, out var wurzel)) throw new ArgumentException($"Kein Graph „{graph}“.");

        var knoten = new JsonArray();
        var mitZustand = false;
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
                var zustand = (string?)f?["zustand"];
                mitZustand |= zustand != null;
                knoten.Add(new JsonObject
                {
                    ["pfad"] = pfad, ["typ"] = typ, ["funktion"] = basis, ["bekannt"] = f != null,
                    ["zustand"] = zustand, ["konfigs"] = konfigs, ["technik"] = technik,
                });
            }
        }
        Knoten(wurzel, "", [graph]);
        return new JsonObject
        {
            ["graph"] = graph,
            ["rechnerPflicht"] = mitZustand,
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
    /// Das Formular eines Eingangs aus der Quellart <paramref name="art"/> (docs/04 §4.7), aus dem Board: was sie meldet, ob sie Zustand hat, der Standard
    /// für <c>Offen</c>, die Felder ihrer Konfig-Records (Typ, Standard, Werte eines Enums) und je Technik mit mehr als einer Umsetzung die Wahl — dieselbe
    /// Ableitung wie am Knoten einer Instanz. Welche Akteure in Frage kommen, steht an der Karte des Clients (<c>sicht.flaeche</c>, W3).
    /// </summary>
    public static JsonObject EingangSchema(JsonObject board, string art)
    {
        var q = Liste(board["quellarten"]).FirstOrDefault(x => S(x, "name") == art) ?? throw new ArgumentException($"Keine Quellart „{art}“.");
        var funktionen = Liste(board["bausteinFunktionen"])
            .GroupBy(f => S(f, "name"), StringComparer.Ordinal).ToDictionary(g => g.Key, g => g.First(), StringComparer.Ordinal);
        var konfigs = new JsonArray();
        var technik = new JsonArray();
        Bedarf(q, konfigs, technik, funktionen, []);
        return new JsonObject
        {
            ["art"] = art, ["framework"] = (bool?)q["framework"] == true, ["zustand"] = (string?)q["zustand"],
            ["meldet"] = (q["meldet"] ?? new JsonArray()).DeepClone(),
            ["offenStandard"] = EingangKonfig.StandardOffen, ["konfigs"] = konfigs, ["technik"] = technik,
        };
    }

    /// <summary>
    /// Ein Eingang eines Clients in <c>umgebung.json</c> setzen (<paramref name="eingang"/>) bzw. entfernen (null): der Text der ganzen Datei danach —
    /// geschrieben wird er als Geste (Verlauf, ↶, Branch-Sperre). Die übrigen Werte der Datei bleiben, wie sie dastehen (unaufgelöst).
    /// </summary>
    public string MitEingang(string umgebung, string client, string name, JsonObject? eingang)
    {
        if (!System.Text.RegularExpressions.Regex.IsMatch(name, "^[A-Za-z0-9_-]+$")) throw new ArgumentException($"Kein gültiger Name für einen Eingang: „{name}“.");
        if (!System.Text.RegularExpressions.Regex.IsMatch(client, "^[A-Za-z_][A-Za-z0-9_]*$")) throw new ArgumentException($"Kein Client: „{client}“.");
        var inhalt = umgebungen.Roh(umgebung);
        if (inhalt[UmgebungsAbschnitte.Clients] is not JsonObject clients) inhalt[UmgebungsAbschnitte.Clients] = clients = new JsonObject();
        if (clients[client] is not JsonObject c) clients[client] = c = new JsonObject();
        if (c[nameof(ClientKonfig.Eingaenge)] is not JsonObject alle) c[nameof(ClientKonfig.Eingaenge)] = alle = new JsonObject();
        if (eingang == null)
        {
            if (!alle.Remove(name)) throw new ArgumentException($"Einen Eingang {client}/{name} gibt es in „{umgebung}“ nicht.");
            if (alle.Count == 0) c.Remove(nameof(ClientKonfig.Eingaenge));
            if (c.Count == 0) clients.Remove(client);
        }
        else alle[name] = eingang.DeepClone();
        return Umgebungen.Text(inhalt);
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

    /// <summary>
    /// Wo welche Umsetzung einer Technik gewählt ist (<c>Technik</c> am Knoten einer Instanz bzw. an einem Eingang eines Clients), über alle Umgebungen:
    /// Schnittstelle → [{umgebung, wo (<c>instanz/Knoten</c> bzw. <c>Client/Eingang</c>), umsetzung}]. Gelesen wie die Dateien dastehen (unaufgelöst); der
    /// Editor zeigt es nur („gewählt in …“, W3).
    /// </summary>
    public JsonObject TechnikWahl()
    {
        var aus = new JsonObject();
        void Dazu(JsonNode? technik, string umgebung, string wo)
        {
            if (technik is not JsonObject t) return;
            foreach (var (schnittstelle, umsetzung) in t)
            {
                if (umsetzung is not JsonValue v || !v.TryGetValue<string>(out var u) || u.Length == 0) continue;
                if (aus[schnittstelle] is not JsonArray l) aus[schnittstelle] = l = new JsonArray();
                l.Add(new JsonObject { ["umgebung"] = umgebung, ["wo"] = wo, ["umsetzung"] = u });
            }
        }
        foreach (var name in umgebungen.Namen())
        {
            try
            {
                foreach (var (instanz, inhalt) in Liste(name))
                    if (inhalt is JsonObject i)
                        foreach (var (knoten, wert) in i)
                            if (wert is JsonObject k) Dazu(k[nameof(KnotenKonfig.Technik)], name, instanz + "/" + knoten);
                if (umgebungen.Roh(name)[UmgebungsAbschnitte.Clients] is JsonObject clients)
                    foreach (var (client, c) in clients)
                        if (c?[nameof(ClientKonfig.Eingaenge)] is JsonObject eingaenge)
                            foreach (var (eingang, e) in eingaenge)
                                Dazu(e?[nameof(EingangKonfig.Technik)], name, client + "/" + eingang);
            }
            catch (Exception ex) when (ex is JsonException or IOException) { /* eine kaputte Datei meldet ⟳; hier zählt sie nicht */ }
        }
        return aus;
    }

    private static IEnumerable<JsonObject> Liste(JsonNode? n) => (n as JsonArray ?? []).OfType<JsonObject>();
    private static string S(JsonObject o, string k) => (string?)o[k] ?? "";

    /// <summary>Der Dateitext einer Instanz (hübsch eingerückt).</summary>
    public static string Text(JsonObject inhalt) => Umgebungen.Text(inhalt);
}
