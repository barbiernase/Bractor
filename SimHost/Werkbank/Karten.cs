using System.Text.Json.Nodes;

namespace SimHost.Werkbank;

/// <summary>
/// Ordnet einen Compiler-Befund (Typ + Member aus dem Syntaxbaum, <see cref="Diagnosen"/>) seiner Karte zu — über die stabilen Karten-Ids des
/// gelesenen Boards (warmer Workspace, <see cref="Arbeitsbereich"/>); die Regel steht EINMAL im Extractor-Kern (<see cref="GraphExtractor.KartenIds"/>).
/// Ein Typ, den das Board nicht kennt, bleibt ohne Karte.
/// </summary>
public static class Karten
{
    /// <summary>
    /// Zuerst die INNERSTE Karte, deren Ort (<c>_ort</c>: Datei, Zeile bis <c>bis</c>) die Zeile enthält — so landet ein Befund im Fluss am
    /// Knoten, in einer Prozess-Regel an der Regel, in einem Decide am Decider; sonst Typ + Member (<see cref="GraphExtractor.KartenIds"/>).
    /// </summary>
    public static IReadOnlyList<Diagnose> Zuordnen(IReadOnlyList<Diagnose> diagnosen, JsonObject? board)
    {
        if (diagnosen.Count == 0 || board == null) return diagnosen;
        var orte = new List<(string Id, string Datei, int Von, int Bis)>();
        void Sammle(JsonNode? n)
        {
            switch (n)
            {
                case JsonObject o:
                    if ((string?)o["_id"] is { } id && o["_ort"] is JsonObject ort && (string?)ort["datei"] is { } datei && (int?)ort["bis"] is { } bis)
                        orte.Add((id, datei, (int?)ort["zeile"] ?? 0, bis));
                    foreach (var (k, v) in o) if (k != "sicht") Sammle(v);
                    break;
                case JsonArray a: foreach (var x in a) Sammle(x); break;
            }
        }
        Sammle(board);
        string? Innerste(Diagnose d) => orte.Where(o => o.Datei == d.Datei && o.Von <= d.Zeile && d.Zeile <= o.Bis)
            .OrderBy(o => o.Bis - o.Von).Select(o => o.Id).FirstOrDefault();
        return diagnosen.Select(d => d with { Karte = d.Karte ?? Innerste(d) ?? GraphExtractor.KartenIds.Zu(d.Typ, d.Member, board) }).ToList();
    }
}
