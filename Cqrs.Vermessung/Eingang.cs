using System.Net.Http.Headers;
using System.Text;
using System.Text.Json.Nodes;

namespace Cqrs.Vermessung;

/// <summary>
/// Der JSON-Eingang des Hosts (<c>/eingang/command</c>, <c>/eingang/query</c>, Token im Header <c>akteur-token</c>) — derselbe Weg wie die
/// Werkbank ins echte System (Akteur-Tor, Befugnis, Aggregat bzw. Reader). Der Command-Weg wartet auf das Ergebnis des Aggregats.
/// </summary>
internal sealed class Eingang : IDisposable
{
    private readonly HttpClient _http;

    public Eingang(int einblickPort)
    {
        var handler = new SocketsHttpHandler { MaxConnectionsPerServer = 512, PooledConnectionLifetime = TimeSpan.FromMinutes(10) };
        _http = new HttpClient(handler) { BaseAddress = new Uri($"http://localhost:{einblickPort}"), Timeout = TimeSpan.FromSeconds(60) };
    }

    public sealed record Antwort(bool Ok, int Status, JsonNode? Inhalt, string? Fehler);

    /// <summary>Schickt einen Command; ohne <paramref name="commandId"/> vergibt der Host eine neue.</summary>
    public async Task<Antwort> CommandAsync(string typ, JsonObject daten, string? token, Guid? commandId = null, CancellationToken ct = default)
    {
        var body = new JsonObject { ["typ"] = typ, ["daten"] = daten };
        if (commandId is { } id) body["commandId"] = id.ToString();
        return await SendeAsync("/eingang/command", body, token, ct);
    }

    /// <summary>Wiederholt einen Command mit DERSELBEN CommandId, bis der Host antwortet (Störungslauf: Host weg, Timeout).</summary>
    public async Task<Antwort> CommandMitWiederholungAsync(string typ, JsonObject daten, string? token, TimeSpan frist, CancellationToken ct = default)
    {
        var id = Guid.NewGuid();
        var ende = DateTimeOffset.UtcNow + frist;
        while (true)
        {
            Antwort a;
            try { a = await CommandAsync(typ, (JsonObject)daten.DeepClone(), token, id, ct); }
            catch (Exception e) when (e is HttpRequestException or TaskCanceledException) { a = new Antwort(false, 0, null, e.Message); }
            if (a.Status is 200 or 403 or 400 || DateTimeOffset.UtcNow > ende) return a;
            await Task.Delay(500, ct);
        }
    }

    /// <summary>Lokal aktive Actors des Hosts (<c>/monitoring/metrics</c>, PF-5); NaN, wenn der Host nicht antwortet.</summary>
    public async Task<double> AktiveActorsAsync()
    {
        try
        {
            var m = JsonNode.Parse(await _http.GetStringAsync("/monitoring/metrics"));
            return m?["aktiveActors"]?.GetValue<int>() ?? double.NaN;
        }
        catch (Exception e) when (e is HttpRequestException or TaskCanceledException or System.Text.Json.JsonException) { return double.NaN; }
    }

    /// <summary>Stellt eine Query; liefert <c>antwort</c> (<c>{typ, daten}</c>) oder null.</summary>
    public async Task<JsonNode?> QueryAsync(string typ, JsonObject daten, string? token, CancellationToken ct = default)
    {
        var a = await SendeAsync("/eingang/query", new JsonObject { ["typ"] = typ, ["daten"] = daten }, token, ct);
        return a.Ok ? a.Inhalt?["antwort"] : throw new InvalidOperationException($"Query {typ}: {a.Status} {a.Fehler}");
    }

    private async Task<Antwort> SendeAsync(string pfad, JsonObject body, string? token, CancellationToken ct)
    {
        using var req = new HttpRequestMessage(HttpMethod.Post, pfad)
        {
            Content = new StringContent(body.ToJsonString(), Encoding.UTF8, new MediaTypeHeaderValue("application/json")),
        };
        if (token != null) req.Headers.Add("akteur-token", token);
        using var res = await _http.SendAsync(req, ct);
        var text = await res.Content.ReadAsStringAsync(ct);
        JsonNode? inhalt = null;
        try { inhalt = JsonNode.Parse(text); } catch { }
        var ok = res.IsSuccessStatusCode && inhalt?["ok"]?.GetValue<bool>() == true;
        return new Antwort(ok, (int)res.StatusCode, inhalt, ok ? null : (inhalt?["fehler"]?.ToString() ?? inhalt?["abgelehnt"]?.ToString() ?? inhalt?["ablehnung"]?.ToJsonString() ?? text));
    }

    public void Dispose() => _http.Dispose();
}
