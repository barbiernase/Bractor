using System.Text.Json.Nodes;

namespace GraphExtractor;

/// <summary>
/// Die Karte zu einem Code-Fakt (Typ + Member), über die stabilen Karten-Ids des gelesenen Boards (Werkbank P4.8): für schreibende Bausteine
/// die Wirker-Id (<c>decide:A/C</c>, <c>funktion:F</c>, <c>projektion:P</c> …), sonst der natürliche Schlüssel (<c>rec:Name</c>, <c>agg:Name</c>,
/// <c>code:Name</c>). Eine Quelle für Compiler-Befunde (Werkbank) und Lese-Befunde (Extractor). Ein Typ ohne Karte bleibt ohne Id.
/// </summary>
public static class KartenIds
{
    public static string? Zu(string? typ, string? member, JsonObject board)
    {
        if (string.IsNullOrEmpty(typ)) return null;
        IEnumerable<JsonObject> Alle(string k) => (board[k] as JsonArray ?? []).OfType<JsonObject>();
        static string S(JsonObject o, string k) => (string?)o[k] ?? "";
        string? Id(string sammlung, Func<JsonObject, bool> passt) => Alle(sammlung).FirstOrDefault(passt) is { } o ? (string?)o["_id"] : null;

        if (Alle("aggregate").Any(a => S(a, "name") == typ))
        {
            // Decide(Cmd)/Apply(Evt) im Aggregat → Decider-/Applier-Karte; sonst das Aggregat selbst.
            var arg = member is { } m && m.IndexOf('(') is var i and >= 0 ? m[(i + 1)..].TrimEnd(')').Split(',')[0].Trim().Split('.').Last() : "";
            if (member?.StartsWith("Decide(", StringComparison.Ordinal) == true
                && Id("decider", d => S(d, "aggregat") == typ && S(d, "command") == arg) is { } dec) return dec;
            if (member?.StartsWith("Apply(", StringComparison.Ordinal) == true
                && Id("applier", a => S(a, "aggregat") == typ && S(a, "event") == arg) is { } app) return app;
            return "agg:" + typ;
        }
        return Id("funktionen", f => S(f, "name") == typ || S(f, "implementierung") == typ)
            ?? Id("projektionen", p => S(p, "name") == typ)
            ?? Id("reaktionen", p => S(p, "name") == typ)
            ?? Id("reader", p => S(p, "name") == typ)
            ?? Id("stores", s => S(s, "name") == typ || (s["impl"] is JsonObject impl && S(impl, "name") == typ))
            ?? Id("readModels", r => S(r, "name") == typ)
            ?? Id("fluesse", f => S(f, "name") == typ)
            ?? Id("akteure", a => S(a, "name") == typ)
            ?? Id("clients", c => S(c, "name") == typ)
            ?? Id("codeKarten", c => S(c, "name") == typ)
            ?? (Alle("sagas").Any(s => S(s, "name") == typ) ? "saga:" + typ : null)
            ?? (Alle("records").Any(r => S(r, "name") == typ) ? "rec:" + typ : null)
            ?? (Alle("enums").Any(e => S(e, "name") == typ) ? "enum:" + typ : null);
    }
}
