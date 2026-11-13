using System.Text.Json.Nodes;

namespace GraphExtractor;

/// <summary>
/// Die Umgebungen der Solution (docs/06 §6.5): je Umgebung ein Ordner <c>umgebungen/&lt;name&gt;/</c> mit <c>umgebung.json</c> neben der <c>.sln</c>. Der
/// Extractor liest sie roh (Struktur ist Code, Werte sind Konfig) und legt sie dem Board bei — die Eingänge der Clients (Quellen) und die Instanzen
/// der Graphen mit ihren Werten je Umgebung; der Host-Rahmen (P3) bearbeitet sie. Platzhalter (<c>${env:…}</c>, <c>${branch}</c>) bleiben unaufgelöst stehen.
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
                    aus[name] = o;
            }
            catch (System.Text.Json.JsonException e) { aus[name] = new JsonObject { ["fehler"] = e.Message }; }
        }
        return aus;
    }
}
