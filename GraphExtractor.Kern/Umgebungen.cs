using System.Text.Json.Nodes;

namespace GraphExtractor;

/// <summary>
/// Die Umgebungen der Solution (docs/06 §6.5): je Umgebung ein Ordner <c>umgebungen/&lt;name&gt;/</c> mit <c>umgebung.json</c> neben der <c>.sln</c>. Der
/// Extractor liest sie roh (Struktur ist Code, Werte sind Konfig) und legt sie dem Board bei — Rechner, Backend und die Instanzen der Graphen und der
/// Apps mit ihren Werten je Umgebung; der Host-Rahmen (P3) bearbeitet sie. Platzhalter (<c>${env:…}</c>, <c>${branch}</c>) bleiben unaufgelöst stehen.
/// </summary>
public static class Umgebungen
{
    public static JsonObject Lies(string solutionDir)
    {
        var aus = new JsonObject();
        var verz = Path.Combine(solutionDir, Abstractions.Projektstruktur.UmgebungsOrdner);
        if (!Directory.Exists(verz)) return aus;
        foreach (var datei in Directory.GetDirectories(verz).Select(d => Path.Combine(d, Abstractions.Projektstruktur.UmgebungsDatei)).Where(File.Exists)
                     .OrderBy(d => d, StringComparer.Ordinal))
        {
            var name = Path.GetFileName(Path.GetDirectoryName(datei)!);
            try
            {
                if (JsonNode.Parse(File.ReadAllText(datei), documentOptions: new() { CommentHandling = System.Text.Json.JsonCommentHandling.Skip }) is JsonObject o)
                {
                    // Die Instanzen der Graphen (graphen/<instanz>.json) unter „Graphen“, die der Apps (apps/<instanz>.json) unter „Apps“ — dieselben
                    //   Schlüssel, unter die der Lader des Hosts sie hängt.
                    o[Abstractions.UmgebungsAbschnitte.Graphen] = Ordner(Path.Combine(Path.GetDirectoryName(datei)!, Abstractions.Projektstruktur.GraphenOrdner));
                    o[Abstractions.UmgebungsAbschnitte.Apps] = Ordner(Path.Combine(Path.GetDirectoryName(datei)!, Abstractions.Projektstruktur.AppsOrdner));
                    aus[name] = o;
                }
            }
            catch (System.Text.Json.JsonException e) { aus[name] = new JsonObject { ["fehler"] = e.Message }; }
        }
        return aus;
    }

    /// <summary>Die Instanz-Dateien eines Ordners: Name (Dateiname) → Inhalt; eine kaputte Datei als <c>{ fehler }</c>.</summary>
    private static JsonObject Ordner(string ordner)
    {
        var aus = new JsonObject();
        if (!Directory.Exists(ordner)) return aus;
        foreach (var g in Directory.GetFiles(ordner, "*.json").OrderBy(d => d, StringComparer.Ordinal))
            try
            {
                aus[Path.GetFileNameWithoutExtension(g)] = JsonNode.Parse(File.ReadAllText(g),
                    documentOptions: new() { CommentHandling = System.Text.Json.JsonCommentHandling.Skip }) ?? new JsonObject();
            }
            catch (System.Text.Json.JsonException e) { aus[Path.GetFileNameWithoutExtension(g)] = new JsonObject { ["fehler"] = e.Message }; }
        return aus;
    }
}
