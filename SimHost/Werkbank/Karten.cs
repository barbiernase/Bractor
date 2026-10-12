using System.Text.Json.Nodes;

namespace SimHost.Werkbank;

/// <summary>
/// Ordnet einen Compiler-Befund (Typ + Member aus dem Syntaxbaum, <see cref="Diagnosen"/>) seiner Karte zu — über die stabilen Karten-Ids des
/// zuletzt gelesenen Boards (<c>domain-model.json</c>, Werkbank P4.8): für schreibende Bausteine die Wirker-Id (<c>decide:A/C</c>,
/// <c>funktion:F</c>, <c>projektion:P</c> …), sonst der natürliche Schlüssel (<c>rec:Name</c>, <c>agg:Name</c>). Der Editor sucht nur noch
/// nach der Id. Ein Typ, den das Board noch nicht kennt (neu, noch nie gelesen), bleibt ohne Karte.
/// </summary>
public static class Karten
{
    public static IReadOnlyList<Diagnose> Zuordnen(IReadOnlyList<Diagnose> diagnosen, string slnRoot)
    {
        if (diagnosen.Count == 0) return diagnosen;
        JsonObject? board = null;
        try
        {
            var pfad = Path.Combine(slnRoot, "domain-model.json");
            if (File.Exists(pfad)) board = JsonNode.Parse(File.ReadAllText(pfad)) as JsonObject;
        }
        catch (System.Text.Json.JsonException) { }
        return board == null ? diagnosen : diagnosen.Select(d => d with { Karte = Zu(d.Typ, d.Member, board) }).ToList();
    }

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
            ?? (Alle("sagas").Any(s => S(s, "name") == typ) ? "saga:" + typ : null)
            ?? (Alle("records").Any(r => S(r, "name") == typ) ? "rec:" + typ : null)
            ?? (Alle("enums").Any(e => S(e, "name") == typ) ? "enum:" + typ : null);
    }
}
