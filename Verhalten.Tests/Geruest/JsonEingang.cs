using System.Net.Http.Headers;
using System.Text;
using System.Text.Json.Nodes;

namespace Verhalten.Tests.Geruest;

/// <summary>
/// Der JSON-Eingang des Hosts (<c>/eingang/command</c>, <c>/eingang/query</c>, Token im Header <c>akteur-token</c>) — derselbe Weg wie die
/// Werkbank ins echte System: Akteur-Tor, Befugnis, Aggregat bzw. Reader. Der Command-Weg wartet auf den Ausgang des Aggregats.
/// </summary>
public sealed class JsonEingang : IDisposable
{
    private readonly HttpClient _http;

    public JsonEingang(Hostlauf host) =>
        _http = new HttpClient { BaseAddress = new Uri($"http://localhost:{host.EinblickPort}"), Timeout = TimeSpan.FromSeconds(60) };

    public sealed record Antwort(bool Ok, int Status, JsonNode? Inhalt, string? Fehler);

    /// <summary>Schickt einen Command; ohne <paramref name="commandId"/> vergibt der Host eine neue. Mit <paramref name="erwarteteVersion"/> wie ein
    /// Client mit Versionsprüfung (OCC), sonst wie ein Emitter.</summary>
    public Task<Antwort> CommandAsync(string typ, JsonObject daten, string? token = null, Guid? commandId = null, int? erwarteteVersion = null)
    {
        var body = new JsonObject { ["typ"] = typ, ["daten"] = daten };
        if (commandId is { } id) body["commandId"] = id.ToString();
        if (erwarteteVersion is { } v) body["erwarteteVersion"] = v;
        return SendeAsync("/eingang/command", body, token);
    }

    /// <summary>Ein Command, der wirken muss — sonst scheitert der Test mit dem Grund.</summary>
    public async Task MussAsync(string typ, JsonObject daten, string? token = null, Guid? commandId = null)
    {
        var a = await CommandAsync(typ, daten, token, commandId);
        if (!a.Ok) throw new InvalidOperationException($"{typ} nicht angenommen ({a.Status}): {a.Fehler}");
    }

    /// <summary>Stellt eine Query; liefert die Antwort (<c>{typ, daten}</c>).</summary>
    public async Task<JsonNode> QueryAsync(string typ, JsonObject daten, string? token = null)
    {
        var a = await SendeAsync("/eingang/query", new JsonObject { ["typ"] = typ, ["daten"] = daten }, token);
        return a.Ok && a.Inhalt?["antwort"] is { } antwort ? antwort : throw new InvalidOperationException($"Query {typ}: {a.Status} {a.Fehler}");
    }

    /// <summary>Stellt eine Query; liefert die Antwort (<c>{typ, daten}</c>) und den Stand je gezeigter Id (<c>[{id, aggregat, version}]</c>).</summary>
    public async Task<(JsonNode Antwort, JsonArray Staende)> QueryMitStandAsync(string typ, JsonObject daten, string? token = null)
    {
        var a = await SendeAsync("/eingang/query", new JsonObject { ["typ"] = typ, ["daten"] = daten }, token);
        return a.Ok && a.Inhalt?["antwort"] is { } antwort && a.Inhalt["staende"] is JsonArray staende
            ? (antwort, staende)
            : throw new InvalidOperationException($"Query {typ}: {a.Status} {a.Fehler}");
    }

    private async Task<Antwort> SendeAsync(string pfad, JsonObject body, string? token)
    {
        using var req = new HttpRequestMessage(HttpMethod.Post, pfad)
        {
            Content = new StringContent(body.ToJsonString(), Encoding.UTF8, new MediaTypeHeaderValue("application/json")),
        };
        if (token != null) req.Headers.Add("akteur-token", token);
        using var res = await _http.SendAsync(req);
        var text = await res.Content.ReadAsStringAsync();
        JsonNode? inhalt = null;
        try { inhalt = JsonNode.Parse(text); } catch (System.Text.Json.JsonException) { }
        var ok = res.IsSuccessStatusCode && inhalt?["ok"]?.GetValue<bool>() == true;
        return new Antwort(ok, (int)res.StatusCode, inhalt, ok ? null : text);
    }

    public void Dispose() => _http.Dispose();
}
