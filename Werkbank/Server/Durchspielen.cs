using System.Text.Json.Nodes;
using System.Text.RegularExpressions;

namespace Werkbank.Server;

/// <summary>
/// Durchspielen (docs/konzept-werkbank.md §6, Werkbank P5): nur echte Eingänge — die Werkbank ist Client des laufenden Hosts (JSON-Eingang
/// hinter dem Akteur-Tor), Ausführer von Hand-Funktionen, legt Dateien dorthin, wo der echte Treiber sie findet, und stellt die Uhr der
/// Sim-DB vor. Was der Editor dafür braucht (Formular-Schema eines Records, Tokens, Sandbox-Pfad), kommt fertig von hier (Frontend-Bauregel).
/// </summary>
public sealed partial class Durchspielen(WerkbankServer werkbank, Func<string?> modellDatei)
{
    private static readonly HttpClient Http = new() { Timeout = TimeSpan.FromSeconds(30) };

    // ── Als Akteur: Proxy zum JSON-Eingang des laufenden Hosts (/eingang/* auf dem Einblick-Port) ──

    /// <summary>Reicht eine Anfrage an <c>/eingang/{pfad}</c> des laufenden Hosts weiter (samt <c>akteur-token</c>) und gibt die Antwort zurück.</summary>
    public async Task<(int Status, string Inhalt)> EingangAsync(string methode, string pfad, string? token, string? inhalt, CancellationToken ct)
    {
        var u = werkbank.Umgebungen.Aufgeloest(werkbank.Aktiv);
        if (!werkbank.Host.Laeuft) return (409, """{"ok":false,"fehler":"Der Host läuft nicht — ⟳ startet ihn."}""");
        if (!u.Transport.JsonEingang || u.Transport.EinblickPort <= 0)
            return (409, $$"""{"ok":false,"fehler":"In {{u.Umgebung.Name}} ist der JSON-Eingang aus (Transport:JsonEingang)."}""");
        using var req = new HttpRequestMessage(new HttpMethod(methode), $"http://localhost:{u.Transport.EinblickPort}/eingang/{pfad}");
        if (!string.IsNullOrEmpty(token)) req.Headers.Add("akteur-token", token);
        if (inhalt != null) req.Content = new StringContent(inhalt, System.Text.Encoding.UTF8, "application/json");
        try
        {
            using var antwort = await Http.SendAsync(req, ct);
            return ((int)antwort.StatusCode, await antwort.Content.ReadAsStringAsync(ct));
        }
        catch (HttpRequestException e) { return (502, new JsonObject { ["ok"] = false, ["fehler"] = "Host nicht erreichbar: " + e.Message }.ToJsonString()); }
    }

    /// <summary>Die Sim-Tokens der gewählten Umgebung (Abschnitt <c>Identitaeten</c>): Token → Akteur(e).</summary>
    public object Identitaeten()
    {
        var roh = werkbank.Umgebungen.AufgeloestRoh(werkbank.Aktiv);
        var tokens = roh["Identitaeten"]?["Tokens"] as JsonObject;
        return new
        {
            umgebung = werkbank.Aktiv,
            tokens = tokens?.Select(t => new
            {
                token = t.Key,
                akteure = t.Value is JsonArray a
                    ? a.Select(x => x?.GetValue<string>() ?? "").Where(x => x.Length > 0).ToList()
                    : (t.Value?.GetValue<string>() ?? "").Split(',', '+').Select(x => x.Trim()).Where(x => x.Length > 0).ToList(),
            }).ToList() ?? [],
        };
    }

    // ── Datei-Drop auf die Quellkarte ──

    /// <summary>Der Pfad, den der Treiber der Quelle <paramref name="quelle"/> in der gewählten Umgebung beobachtet.</summary>
    public string? QuellPfad(string quelle)
        => werkbank.Umgebungen.AufgeloestRoh(werkbank.Aktiv)["Quellen"]?[quelle]?["Pfad"]?.GetValue<string>();

    /// <summary>
    /// Legt eine Datei in den Pfad der Quelle — fertig geschrieben (erst daneben, dann umbenannt), damit der Treiber nie eine halbe sieht.
    /// Gemeldet wird sie vom ECHTEN Treiber, nicht von hier.
    /// </summary>
    public async Task<object> LegeAbAsync(string quelle, string dateiname, Stream inhalt, CancellationToken ct)
    {
        var pfad = QuellPfad(quelle) ?? throw new InvalidOperationException($"Umgebung {werkbank.Aktiv}: kein Abschnitt Quellen:{quelle}:Pfad.");
        var name = Path.GetFileName(dateiname);
        if (string.IsNullOrWhiteSpace(name)) throw new ArgumentException("Dateiname fehlt.");
        Directory.CreateDirectory(pfad);
        var ablage = Path.Combine(Path.GetDirectoryName(Path.GetFullPath(pfad).TrimEnd(Path.DirectorySeparatorChar)) ?? pfad, ".werkbank-ablage");
        Directory.CreateDirectory(ablage);
        var zwischen = Path.Combine(ablage, Guid.NewGuid().ToString("N"));
        await using (var f = File.Create(zwischen)) await inhalt.CopyToAsync(f, ct);
        var ziel = Path.Combine(pfad, name);
        File.Move(zwischen, ziel, overwrite: true);
        var groesse = new FileInfo(ziel).Length;
        werkbank.Fortschritt.Melde("datei", new { quelle, ziel, groesse });
        return new { ok = true, quelle, ziel, groesse, hostLaeuft = werkbank.Host.Laeuft };
    }

    // ── ⏩ Uhr vorstellen ──

    public async Task<object> UhrVorAsync(TimeSpan um)
    {
        var u = werkbank.Umgebungen.Aufgeloest(werkbank.Aktiv);
        if (u.Umgebung.Art != Abstractions.UmgebungsArt.Sim) throw new InvalidOperationException($"{u.Umgebung.Name} ist {u.Umgebung.Art} — die Uhr steht fest.");
        var neu = (await werkbank.Db.VersatzAsync(u) ?? TimeSpan.Zero) + um;
        await werkbank.Db.SetzeVersatzAsync(u, neu);
        werkbank.Fortschritt.Melde("uhr", new { versatz = neu.ToString() });
        return new { ok = true, versatz = neu.ToString() };
    }

    // ── Formular-Schema eines Records (aus dem gelesenen Code) ──

    /// <summary>
    /// Das Formular für einen Record des Boards (Command, Query, Ergebnis-Event …) — im selben Format wie das Umgebungs-Schema, damit der
    /// EINE generische Formular-Renderer es zeichnet: Klasse → Felder, Enum → Auswahl, Werte-Objekt → Unterformular, Sammlung → Liste.
    /// </summary>
    public JsonObject Formular(string typ)
    {
        var p = modellDatei();
        if (p is null || !File.Exists(p)) throw new FileNotFoundException("domain-model.json fehlt — ⟳ liest den Code.");
        var m = JsonNode.Parse(File.ReadAllText(p))!.AsObject();
        var records = (m["records"] as JsonArray ?? []).OfType<JsonObject>().GroupBy(r => r["name"]!.GetValue<string>()).ToDictionary(g => g.Key, g => g.First());
        var enums = (m["enums"] as JsonArray ?? []).OfType<JsonObject>().GroupBy(e => e["name"]!.GetValue<string>())
            .ToDictionary(g => g.Key, g => (g.First()["werte"] as JsonArray ?? []).Select(w => w!.GetValue<string>()).ToList());
        if (!records.TryGetValue(typ, out var rec)) throw new KeyNotFoundException($"Record {typ} steht nicht im gelesenen Code.");
        var schema = Klasse(rec, records, enums, 0);
        schema["typ"] = typ;
        schema["kind"] = rec["kind"]?.GetValue<string>();
        return schema;
    }

    private static JsonObject Klasse(JsonObject rec, Dictionary<string, JsonObject> records, Dictionary<string, List<string>> enums, int tiefe)
    {
        var felder = new JsonObject();
        foreach (var f in (rec["felder"] as JsonArray ?? []).OfType<JsonObject>())
        {
            var name = f["name"]?.GetValue<string>();
            if (string.IsNullOrEmpty(name) || f["ausdruck"] != null) continue;
            felder[name] = Feld(f["typ"]?.GetValue<string>() ?? "string", f["elementTyp"]?.GetValue<string>(), records, enums, tiefe);
        }
        return new JsonObject { ["art"] = "klasse", ["felder"] = felder };
    }

    [GeneratedRegex(@"^(?:global::)?(?:System\.Collections\.Generic\.)?(?:IReadOnlyList|IReadOnlyCollection|IEnumerable|IList|ICollection|List|HashSet|IReadOnlySet|ISet)<(.+)>$")]
    private static partial Regex Sammlung();

    private static JsonObject Feld(string typ, string? elementTyp, Dictionary<string, JsonObject> records, Dictionary<string, List<string>> enums, int tiefe)
    {
        var nullbar = typ.EndsWith('?');
        var t = typ.TrimEnd('?');
        var kurz = t.Contains('.') && !t.Contains('<') ? t[(t.LastIndexOf('.') + 1)..] : t;
        JsonObject aus;
        string? element = elementTyp ?? (Sammlung().Match(t) is { Success: true } s ? s.Groups[1].Value : t.EndsWith("[]", StringComparison.Ordinal) ? t[..^2] : null);
        if (element != null && t != "string")
        {
            var e = Feld(element, null, records, enums, tiefe + 1);
            aus = e.ContainsKey("art")
                ? new JsonObject { ["art"] = "frei", ["hilfe"] = $"JSON-Liste von {element}", ["liste"] = true }
                : new JsonObject { ["typ"] = "liste", ["element"] = e };
        }
        else aus = kurz switch
        {
            "Guid" => new JsonObject { ["typ"] = "guid" },
            "string" or "char" or "String" => new JsonObject { ["typ"] = "string" },
            "int" or "long" or "short" or "byte" or "uint" or "ulong" or "double" or "float" or "decimal" or "Int32" or "Int64" or "Double" or "Decimal"
                => new JsonObject { ["typ"] = "zahl" },
            "bool" or "Boolean" => new JsonObject { ["typ"] = "bool" },
            "DateTimeOffset" or "DateTime" or "DateOnly" => new JsonObject { ["typ"] = "zeit" },
            "TimeSpan" => new JsonObject { ["typ"] = "dauer" },
            _ when enums.TryGetValue(kurz, out var werte) => new JsonObject { ["typ"] = "enum", ["werte"] = new JsonArray(werte.Select(w => (JsonNode)w).ToArray()) },
            _ when records.TryGetValue(kurz, out var vo) && tiefe < 4 => Klasse(vo, records, enums, tiefe + 1),
            _ => new JsonObject { ["typ"] = "string", ["hilfe"] = $"Typ {typ} — als Text" },
        };
        aus["clr"] = typ;
        if (nullbar) aus["nullbar"] = true;
        return aus;
    }
}
