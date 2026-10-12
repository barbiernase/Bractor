using System.Text.Json.Nodes;

namespace GraphExtractor;

/// <summary>
/// Die Konfig-Werte der Quellen je Host: Abschnitt <c>Quellen</c> aus der <c>appsettings.json</c> jedes Host-Projekts (Hosts =
/// Code-Fakt der <see cref="Projektlage"/>). Übergang bis P2 — dann liest der Extractor die Umgebungs-Dateien (docs/konzept-werkbank.md §4).
/// </summary>
public static class QuellenKonfig
{
    public static JsonObject Lies(Projektlage lage)
    {
        var aus = new JsonObject();
        foreach (var (projekt, _) in lage.Hosts)
        {
            var datei = projekt.FilePath is { } p ? Path.Combine(Path.GetDirectoryName(p)!, "appsettings.json") : null;
            if (datei == null || !File.Exists(datei)) continue;
            try
            {
                if (JsonNode.Parse(File.ReadAllText(datei), documentOptions: new() { CommentHandling = System.Text.Json.JsonCommentHandling.Skip })?["Quellen"] is JsonObject q)
                    aus[projekt.Name] = q.DeepClone();
            }
            catch (System.Text.Json.JsonException) { /* kaputte appsettings: dann eben keine Werte */ }
        }
        return aus;
    }
}
