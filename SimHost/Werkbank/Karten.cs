using System.Text.Json.Nodes;

namespace SimHost.Werkbank;

/// <summary>
/// Ordnet einen Compiler-Befund (Typ + Member aus dem Syntaxbaum, <see cref="Diagnosen"/>) seiner Karte zu — über die stabilen Karten-Ids des
/// gelesenen Boards (warmer Workspace, <see cref="Arbeitsbereich"/>); die Regel steht EINMAL im Extractor-Kern (<see cref="GraphExtractor.KartenIds"/>).
/// Ein Typ, den das Board nicht kennt, bleibt ohne Karte.
/// </summary>
public static class Karten
{
    public static IReadOnlyList<Diagnose> Zuordnen(IReadOnlyList<Diagnose> diagnosen, JsonObject? board) =>
        diagnosen.Count == 0 || board == null ? diagnosen
            : diagnosen.Select(d => d with { Karte = d.Karte ?? GraphExtractor.KartenIds.Zu(d.Typ, d.Member, board) }).ToList();
}
