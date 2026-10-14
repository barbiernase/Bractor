using System.Text.Json.Nodes;

namespace GraphExtractor;

/// <summary>
/// Die Umgebungen der Solution (docs/konzept-werkbank.md §4.1): <c>umgebungen/umgebung.&lt;name&gt;.json</c> neben der <c>.sln</c>. Der
/// Extractor liest sie roh (Struktur ist Code, Werte sind Konfig) und legt sie dem Board bei — die Quellkarte zeigt die Werte je
/// Umgebung, der Host-Rahmen (P3) bearbeitet sie. Platzhalter (<c>${env:…}</c>, <c>${branch}</c>) bleiben unaufgelöst stehen.
/// </summary>
public static class Umgebungen
{
    public static JsonObject Lies(string solutionDir)
    {
        var aus = new JsonObject();
        var verz = Path.Combine(solutionDir, Abstractions.Projektstruktur.UmgebungsOrdner);
        if (!Directory.Exists(verz)) return aus;
        foreach (var datei in Directory.GetFiles(verz, "umgebung.*.json").OrderBy(d => d, StringComparer.Ordinal))
        {
            var name = Path.GetFileNameWithoutExtension(datei)["umgebung.".Length..];
            try
            {
                if (JsonNode.Parse(File.ReadAllText(datei), documentOptions: new() { CommentHandling = System.Text.Json.JsonCommentHandling.Skip }) is JsonObject o)
                    aus[name] = o;
            }
            catch (System.Text.Json.JsonException e) { aus[name] = new JsonObject { ["fehler"] = e.Message }; }
        }
        return aus;
    }
}
