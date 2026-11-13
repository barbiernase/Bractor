using System.Text.Json.Nodes;
using Abstractions;

namespace Werkbank.Server;

/// <summary>
/// Rechner und Backend einer Umgebung als Gesten (docs/06 §6.5, docs/10 §10.9; Karten Rechner und Backend der Fläche): einen Rechner in den Pool setzen
/// bzw. seine Adresse ändern, ihn entfernen (nur, wenn nichts ihn nennt) und die Rechner des Backends setzen. Jede Methode gibt den Text der ganzen
/// <c>umgebung.json</c> danach — geschrieben wird er als Geste (Verlauf, ↶, Branch-Sperre). Ohne Abschnitt gilt der Pool bzw. das Backend
/// <c>localhost</c> (wie im Lader); die erste Geste schreibt ihn aus.
/// </summary>
public sealed class Rechnerpool(Umgebungen umgebungen, Instanzen instanzen)
{
    /// <summary>Setzt den Rechner <paramref name="name"/> mit seiner Adresse (neu oder geändert).</summary>
    public string MitRechner(string umgebung, string name, string adresse)
    {
        if (!Rechnerregeln.IstName(name)) throw new ArgumentException($"„{name}“ ist kein Name für einen Rechner (Buchstaben, Ziffern, . - _).");
        adresse = adresse.Trim();
        if (adresse.Length == 0 || adresse.Contains("://", StringComparison.Ordinal) || adresse.Any(char.IsWhiteSpace))
            throw new ArgumentException($"„{adresse}“ ist keine Adresse — IP oder Hostname, ohne Schema und Port.");
        var inhalt = umgebungen.Roh(umgebung);
        Pool(inhalt)[name] = new JsonObject { [nameof(RechnerKonfig.Adresse)] = adresse };
        return Umgebungen.Text(inhalt);
    }

    /// <summary>Entfernt den Rechner <paramref name="name"/> — nur, wenn weder das Backend noch eine Instanz eines Graphen oder einer App ihn nennt.</summary>
    public string OhneRechner(string umgebung, string name)
    {
        var inhalt = umgebungen.Roh(umgebung);
        var pool = Pool(inhalt);
        if (!pool.ContainsKey(name)) throw new ArgumentException($"Einen Rechner {name} gibt es in „{umgebung}“ nicht.");
        var nutzer = Nutzer(umgebung, inhalt, name);
        if (nutzer.Count > 0) throw new ArgumentException($"{name} wird noch genannt: {string.Join(", ", nutzer)}.");
        if (pool.Count == 1) throw new ArgumentException("Der Pool braucht mindestens einen Rechner.");
        pool.Remove(name);
        return Umgebungen.Text(inhalt);
    }

    /// <summary>Setzt die Rechner des Backends (mindestens einer, jeder aus dem Pool, jeder einmal).</summary>
    public string MitBackend(string umgebung, IReadOnlyList<string> rechner)
    {
        var inhalt = umgebungen.Roh(umgebung);
        var pool = Pool(inhalt);
        if (rechner.Count == 0) throw new ArgumentException("Das Backend braucht mindestens einen Rechner.");
        if (rechner.FirstOrDefault(r => !pool.ContainsKey(r)) is { } fremd) throw new ArgumentException($"Einen Rechner {fremd} gibt es im Pool nicht.");
        if (rechner.Count != rechner.Distinct(StringComparer.Ordinal).Count()) throw new ArgumentException("Das Backend nennt einen Rechner doppelt.");
        // Eine Instanz, die einen Rechner nennt, der nicht mehr zum Backend gehört, liefe nirgends.
        foreach (var (instanz, i) in instanzen.Liste(umgebung))
            if (i?[nameof(GraphInstanzKonfig.Rechner)] is JsonValue v && v.TryGetValue<string>(out var r) && !rechner.Contains(r))
                throw new ArgumentException($"Die Instanz {instanz} läuft auf {r} — erst ihren Rechner ändern.");
        Setze(inhalt, UmgebungsAbschnitte.Backend, new JsonObject
        {
            [nameof(BackendKonfig.Rechner)] = new JsonArray(rechner.Select(r => (JsonNode)r).ToArray()),
        });
        return Umgebungen.Text(inhalt);
    }

    /// <summary>Der Pool als Abschnitt der Datei — fehlt er, mit dem Standard (<c>localhost</c>) ausgeschrieben.</summary>
    private static JsonObject Pool(JsonObject inhalt)
    {
        if (inhalt[UmgebungsAbschnitte.Rechner] is JsonObject pool && pool.Count > 0) return pool;
        pool = new JsonObject { [Rechnerregeln.Standard] = new JsonObject { [nameof(RechnerKonfig.Adresse)] = Rechnerregeln.Standard } };
        Setze(inhalt, UmgebungsAbschnitte.Rechner, pool);
        return pool;
    }

    /// <summary>
    /// Setzt einen Abschnitt; fehlt er, steht er danach an seinem Platz in der Reihenfolge der Abschnitte (<see cref="UmgebungsAbschnitte.Alle"/>), damit
    /// die Datei lesbar bleibt.
    /// </summary>
    private static void Setze(JsonObject inhalt, string abschnitt, JsonNode wert)
    {
        if (inhalt.ContainsKey(abschnitt)) { inhalt[abschnitt] = wert; return; }
        var rang = UmgebungsAbschnitte.Alle.ToList().IndexOf(abschnitt);
        var alle = inhalt.ToList();
        inhalt.Clear();
        var gesetzt = false;
        foreach (var (k, v) in alle)
        {
            var r = UmgebungsAbschnitte.Alle.ToList().IndexOf(k);
            if (!gesetzt && r > rang) { inhalt[abschnitt] = wert; gesetzt = true; }
            inhalt[k] = v;
        }
        if (!gesetzt) inhalt[abschnitt] = wert;
    }

    /// <summary>Wer den Rechner nennt: das Backend, Instanzen der Graphen, App-Instanzen.</summary>
    private List<string> Nutzer(string umgebung, JsonObject inhalt, string name)
    {
        var aus = new List<string>();
        var backend = inhalt[UmgebungsAbschnitte.Backend]?[nameof(BackendKonfig.Rechner)] is JsonArray b
            ? b.Select(x => (string?)x ?? "").ToList()
            : [Rechnerregeln.Standard];
        if (backend.Contains(name)) aus.Add("das Backend");
        foreach (var (instanz, i) in instanzen.Liste(umgebung))
            if ((string?)i?[nameof(GraphInstanzKonfig.Rechner)] == name) aus.Add("Instanz " + instanz);
        foreach (var (app, a) in instanzen.Liste(umgebung, Projektstruktur.AppsOrdner))
            if (((string?)a?[nameof(AppInstanzKonfig.Rechner)] ?? Rechnerregeln.Standard) == name) aus.Add("App-Instanz " + app);
        return aus;
    }
}
