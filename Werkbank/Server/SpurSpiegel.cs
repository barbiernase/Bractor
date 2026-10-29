using System.Text.Json;
using System.Text.Json.Nodes;
using Abstractions;
using Eintrag = Abstractions.SpurWeg.Eintrag;

namespace Werkbank.Server;

/// <summary>
/// Der Spur-Spiegel der Werkbank (docs/04 §4.9, Werkbank P6): folgt dem laufenden Host über die Einblick-API — SSE
/// <c>/einblick/neu</c> meldet nur „neu bis seq“, nachgeholt wird ab dem eigenen Cursor (<c>/einblick/spur?ab=</c>). Ein verlorenes Signal
/// oder ein Neustart schadet nicht (dasselbe Muster wie Wecker + Lesen ab Position im Kern). Daraus entsteht der <b>Live-Puls</b> (Zähler und „zuletzt“
/// je Karte, Menge je Kante) und die Zuordnung Vorgang → Streams für den Filter. Vorgänge und Schritte holt er auf Anfrage beim Host und
/// liefert sie fertig aus (Frontend-Bauregel: das JS leitet nichts ab, es schlägt nur Karten-Ids nach).
/// </summary>
public sealed class SpurSpiegel
{
    private static readonly HttpClient Http = new() { Timeout = TimeSpan.FromSeconds(10) };
    private static readonly HttpClient Strom = new() { Timeout = Timeout.InfiniteTimeSpan };

    private readonly WerkbankServer _werkbank;
    private readonly Lock _sperre = new();
    private string? _lauf;        // Umgebung + DB + Start des Hosts — ein neuer Lauf spiegelt neu
    private long _bis;
    private DateTimeOffset? _erste;
    private readonly Dictionary<string, (long N, DateTimeOffset Zuletzt)> _karten = new();
    private readonly Dictionary<(string A, string B), long> _kanten = new();
    private readonly Dictionary<string, Eintrag> _eintrag = new();                        // "stream:version" → Typname, Wirker, Absender
    private readonly Dictionary<string, HashSet<Guid>> _streams = new();                  // Korrelation → Streams
    private long _gemeldet = -1;
    private DateTimeOffset _gemeldetUm;

    public SpurSpiegel(WerkbankServer werkbank) => _werkbank = werkbank;

    /// <summary>Der Einblick-Port des laufenden Hosts der Werkbank und ein Schlüssel für diesen Lauf — null, wenn kein Host läuft.</summary>
    private (int Port, string Lauf)? Ziel()
    {
        if (!_werkbank.Host.Laeuft) return null;
        try
        {
            var u = _werkbank.Umgebungen.Aufgeloest(_werkbank.Aktiv);
            if (u.Transport.EinblickPort <= 0) return null;
            return (u.Transport.EinblickPort, $"{u.Umgebung.Name}|{u.EventStore.Datenbank}|{_werkbank.Host.Seit:O}");
        }
        catch { return null; }
    }

    private string Basis(int port) => $"http://localhost:{port}/einblick";

    /// <summary>Folgt dem jeweils laufenden Host, bis die Werkbank endet.</summary>
    public async Task LaufeAsync(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            var ziel = Ziel();
            if (ziel is null) { await Pause(2, ct); continue; }
            try
            {
                await NachholenAsync(ziel.Value, ct);
                using var anfrage = new HttpRequestMessage(HttpMethod.Get, Basis(ziel.Value.Port) + "/neu");
                using var antwort = await Strom.SendAsync(anfrage, HttpCompletionOption.ResponseHeadersRead, ct);
                antwort.EnsureSuccessStatusCode();
                using var leser = new StreamReader(await antwort.Content.ReadAsStreamAsync(ct));
                // Der Host meldet „neu bis seq“ (oder alle 5 s ein Lebenszeichen) — beides ist nur ein Weckruf, der Cursor entscheidet.
                while (!ct.IsCancellationRequested && await leser.ReadLineAsync(ct) is { } zeile)
                {
                    if (zeile.Length == 0) continue;
                    if (Ziel()?.Lauf != ziel.Value.Lauf) break;   // anderer Lauf (⟳, Umgebung gewechselt)
                    await NachholenAsync(ziel.Value, ct);
                }
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested) { return; }
            catch (Exception) { await Pause(2, ct); }
        }
    }

    private static async Task Pause(int s, CancellationToken ct)
    {
        try { await Task.Delay(TimeSpan.FromSeconds(s), ct); } catch (OperationCanceledException) { }
    }

    /// <summary>
    /// Holt ab dem Cursor nach (seitenweise). Ein neuer Lauf beginnt bei 0 — auch nach jeder Sim-DB-Aktion (zurücksetzen, Stand laden),
    /// denn die startet den Host neu.
    /// </summary>
    private async Task NachholenAsync((int Port, string Lauf) ziel, CancellationToken ct)
    {
        lock (_sperre)
            if (_lauf != ziel.Lauf) Zuruecksetzen(ziel.Lauf);
        for (var seite = 0; seite < 500; seite++)
        {
            long ab;
            lock (_sperre) ab = _bis;
            var j = await HoleAsync(ziel.Port, $"/spur?ab={ab}&n=2000", ct);
            var eintraege = j?["eintraege"] as JsonArray ?? [];
            lock (_sperre)
            {
                if (_lauf != ziel.Lauf) return;
                foreach (var e in eintraege.OfType<JsonObject>()) Falte(e);
            }
            if (eintraege.Count < 2000) break;
        }
        Melde();
    }

    private static async Task<JsonObject?> HoleAsync(int port, string pfad, CancellationToken ct)
    {
        using var antwort = await Http.GetAsync($"http://localhost:{port}/einblick{pfad}", ct);
        if (!antwort.IsSuccessStatusCode) return null;
        return JsonNode.Parse(await antwort.Content.ReadAsStringAsync(ct)) as JsonObject;
    }

    private void Zuruecksetzen(string lauf)
    {
        _lauf = lauf;
        _bis = 0;
        _erste = null;
        _karten.Clear();
        _kanten.Clear();
        _eintrag.Clear();
        _streams.Clear();
        _gemeldet = -1;
    }

    // Ein Eintrag fließt in den Puls: seine Karten zählen, seine Kanten werden dicker.
    private void Falte(JsonObject e)
    {
        var seq = (long?)e["seq"] ?? 0;
        if (seq <= _bis) return;
        _bis = seq;
        var name = (string?)e["name"] ?? (string?)e["typ"] ?? "?";
        var stream = Guid.TryParse((string?)e["stream"], out var s) ? s : Guid.Empty;
        var version = (long?)e["version"] ?? 0;
        var zeit = e["zeit"]?.GetValue<DateTimeOffset>() ?? DateTimeOffset.UtcNow;
        var ueber = (string?)e["ueber"];
        _eintrag[$"{stream:D}:{version}"] = new Eintrag(name, (string?)e["wirker"], ueber);
        if ((string?)e["korrelation"] is { Length: > 0 } k)
        {
            if (!_streams.TryGetValue(k, out var menge)) _streams[k] = menge = [];
            menge.Add(stream);
        }
        var intern = (bool?)e["intern"] ?? false;
        if (intern) return;   // Einträge des Rahmens (Frist gesetzt) sind kein Schritt
        _erste ??= zeit;
        var weg = SpurWeg.Aus(name, (string?)e["wirker"], ueber, (string?)e["ursache"], u => _eintrag.GetValueOrDefault(u));
        foreach (var karte in weg.Karten)
            _karten[karte] = (_karten.TryGetValue(karte, out var z) ? z.N + 1 : 1, zeit);
        foreach (var p in weg.Paare)
            _kanten[(p[0], p[1])] = _kanten.GetValueOrDefault((p[0], p[1])) + 1;
    }

    // Der Editor erfährt über den Fortschritts-Kanal nur „neu bis seq“ — höchstens einmal je Sekunde.
    private void Melde()
    {
        long bis;
        lock (_sperre) bis = _bis;
        if (bis == _gemeldet || DateTimeOffset.UtcNow - _gemeldetUm < TimeSpan.FromSeconds(1)) return;
        _gemeldet = bis;
        _gemeldetUm = DateTimeOffset.UtcNow;
        _werkbank.Fortschritt.Melde("spur", new { bis });
    }

    // ── Für den Editor ──

    /// <summary>
    /// Der Live-Puls (§7.3): je Karte Zähler und „zuletzt“, je gegangener Kante die Menge — über das ganze Log des Laufs; dazu der Stau
    /// (offene Aufträge je Funktion, vom Vermittler — nicht maßgeblich).
    /// </summary>
    public async Task<object> PulsAsync(CancellationToken ct)
    {
        var ziel = Ziel();
        var offen = ziel is null ? [] : await OffenAsync(ziel.Value.Port, ct);
        lock (_sperre)
            return new
            {
                laeuft = ziel != null, bis = _bis, seit = _erste,
                karten = _karten.Select(k => new { id = k.Key, n = k.Value.N, zuletzt = k.Value.Zuletzt }).ToList(),
                kanten = _kanten.Select(k => new { a = k.Key.A, b = k.Key.B, n = k.Value }).ToList(),
                offen = offen.Select(o => new { id = "funktion:" + o.Key, n = o.Value.Count }).Where(o => o.n > 0).ToList(),
            };
    }

    /// <summary>Offene Aufträge je Funktion: Funktion → Korrelationen (<c>/einblick/auftraege</c>).</summary>
    private static async Task<Dictionary<string, List<string>>> OffenAsync(int port, CancellationToken ct)
    {
        var aus = new Dictionary<string, List<string>>();
        try
        {
            var j = await HoleAsync(port, "/auftraege", ct);
            if (j?["auftraege"] is JsonObject je)
                foreach (var (f, liste) in je)
                    aus[f] = (liste as JsonArray ?? []).OfType<JsonObject>().Select(a => (string?)a["korrelation"] ?? "").ToList();
        }
        catch (Exception e) when (e is HttpRequestException or TaskCanceledException or JsonException) { }
        return aus;
    }

    /// <summary>
    /// Die Vorgänge des Hosts, gefiltert nach Quelle (Wurzel-Typ), Akteur, Aggregat-Id (ein Stream des Vorgangs) und Zustand — dazu die
    /// Auswahl für die Filter (Facetten).
    /// </summary>
    public async Task<object> VorgaengeAsync(string? quelle, string? akteur, string? aggregat, string? zustand, CancellationToken ct)
    {
        var ziel = Ziel();
        if (ziel is null) return new { laeuft = false, vorgaenge = Array.Empty<object>() };
        var j = await HoleAsync(ziel.Value.Port, "/vorgaenge?n=500", ct);
        var alle = (j?["vorgaenge"] as JsonArray ?? []).OfType<JsonObject>().ToList();
        string? S(JsonObject v, string k) => (string?)v[k];
        HashSet<Guid> Streams(string k) { lock (_sperre) return _streams.TryGetValue(k, out var m) ? [.. m] : []; }
        var gefiltert = alle.Where(v =>
            (string.IsNullOrEmpty(quelle) || S(v, "wurzelTyp") == quelle || S(v, "wurzelWirker") == quelle)
            && (string.IsNullOrEmpty(akteur) || S(v, "akteur") == akteur)
            && (string.IsNullOrEmpty(zustand) || S(v, "zustand") == zustand)
            && (string.IsNullOrEmpty(aggregat) || S(v, "korrelation")!.Contains(aggregat, StringComparison.OrdinalIgnoreCase)
                || Streams(S(v, "korrelation")!).Any(x => x.ToString("D").Contains(aggregat.Trim(), StringComparison.OrdinalIgnoreCase))))
            .ToList();
        return new
        {
            laeuft = true, gesamt = alle.Count,
            facetten = new
            {
                quellen = alle.Select(v => S(v, "wurzelTyp")).Where(x => x != null).Distinct().Order().ToList(),
                akteure = alle.Select(v => S(v, "akteur")).Where(x => x != null).Distinct().Order().ToList(),
                zustaende = alle.GroupBy(v => S(v, "zustand")).ToDictionary(g => g.Key ?? "?", g => g.Count()),
            },
            vorgaenge = gefiltert.Select(v =>
            {
                var aus = (JsonObject)v.DeepClone();
                aus["streams"] = Streams(S(v, "korrelation")!).Count;
                return aus;
            }).ToList(),
        };
    }

    /// <summary>
    /// Ein Vorgang als nummerierter Weg (§7.3): je Schritt Nummer, Zeit, Karten und Kanten, Payload, Akteur, Stand (älter = grau) und
    /// die Nummern „↤ verursacht von“ / „↦ bewirkte“; dazu Verlierbares (Ablehnungen, transiente Events) aus <c>/einblick/hinweise</c> und
    /// die Bahnen der Zeitleiste (je Baustein, in der Reihenfolge des ersten Auftretens).
    /// </summary>
    public async Task<object> VorgangAsync(string korrelation, CancellationToken ct)
    {
        var ziel = Ziel();
        if (ziel is null) return new { laeuft = false, korrelation };
        var esc = Uri.EscapeDataString(korrelation);
        var j = await HoleAsync(ziel.Value.Port, $"/vorgang/{esc}", ct);
        var eintraege = (j?["eintraege"] as JsonArray ?? []).OfType<JsonObject>().OrderBy(e => (long?)e["seq"]).ToList();
        var h = await HoleAsync(ziel.Value.Port, "/hinweise?ab=0&n=5000", ct);
        var offen = (await OffenAsync(ziel.Value.Port, ct))
            .Where(o => o.Value.Any(k => string.Equals(k, korrelation, StringComparison.OrdinalIgnoreCase))).Select(o => "funktion:" + o.Key).ToList();
        var stand = _werkbank.Stand();

        var nr = new Dictionary<string, int>();      // "stream:version" → Nummer
        var lokal = eintraege.ToDictionary(e => $"{(string?)e["stream"]}:{(long?)e["version"]}",
            e => new Eintrag((string?)e["name"] ?? (string?)e["typ"] ?? "?", (string?)e["wirker"], (string?)e["ueber"]));
        var schritte = new List<JsonObject>();
        foreach (var e in eintraege)
        {
            var name = (string?)e["name"] ?? (string?)e["typ"] ?? "?";
            var intern = (bool?)e["intern"] ?? false;
            if (intern) continue;
            var schluessel = $"{(string?)e["stream"]}:{(long?)e["version"]}";
            var n = schritte.Count + 1;
            nr[schluessel] = n;
            var ursache = (string?)e["ursache"];
            SpurWeg weg;
            lock (_sperre)
                weg = SpurWeg.Aus(name, (string?)e["wirker"], (string?)e["ueber"], ursache,
                    u => lokal.GetValueOrDefault(u) ?? _eintrag.GetValueOrDefault(u));
            var eStand = (string?)e["stand"];
            const string art = "event";
            schritte.Add(new JsonObject
            {
                ["nr"] = n, ["seq"] = (long?)e["seq"], ["zeit"] = e["zeit"]?.DeepClone(), ["name"] = name, ["art"] = art,
                ["wirker"] = (string?)e["wirker"], ["ueber"] = (string?)e["ueber"], ["akteur"] = (string?)e["akteur"],
                ["stream"] = (string?)e["stream"], ["version"] = (long?)e["version"], ["aggregat"] = (string?)e["aggregat"],
                ["stand"] = eStand, ["alt"] = eStand != null && eStand != stand,
                ["ursache"] = ursache, ["vonNr"] = ursache != null && nr.TryGetValue(ursache, out var v) ? v : null,
                ["karten"] = new JsonArray(weg.Karten.Select(x => (JsonNode)x).ToArray()),
                ["paare"] = new JsonArray(weg.Paare.Select(p => (JsonNode)new JsonArray(p[0], p[1])).ToArray()),
                ["daten"] = e["daten"]?.DeepClone(),
            });
        }
        // ↦ bewirkte: die Schritte, deren Ursache dieser ist.
        foreach (var s in schritte)
        {
            var folgen = schritte.Where(x => (int?)x["vonNr"] == (int?)s["nr"]).Select(x => (JsonNode)(int)x["nr"]!).ToArray();
            s["folgen"] = new JsonArray(folgen);
        }
        var hinweise = (h?["hinweise"] as JsonArray ?? []).OfType<JsonObject>()
            .Where(x => (string?)x["korrelation"] == korrelation)
            .Select(x => new JsonObject
            {
                ["zeit"] = x["zeit"]?.DeepClone(), ["art"] = (string?)x["art"], ["wirker"] = (string?)x["wirker"],
                ["ursache"] = (string?)x["ursache"], ["text"] = (string?)x["text"],
                ["vonNr"] = (string?)x["ursache"] is { } u && nr.TryGetValue(u, out var v) ? v : null,
            }).ToList();
        // Zeitleiste: je Baustein (Wirker) eine Bahn, in der Reihenfolge des ersten Auftretens.
        var bahnen = schritte.GroupBy(s => (string?)s["wirker"] ?? "(ohne Wirker)")
            .Select(g => new { wirker = g.Key, schritte = g.Select(s => (int)s["nr"]!).ToList() }).ToList();
        return new
        {
            laeuft = true, korrelation, stand,
            beginn = schritte.FirstOrDefault()?["zeit"]?.DeepClone(), ende = schritte.LastOrDefault()?["zeit"]?.DeepClone(),
            schritte, hinweise, bahnen, offen,
        };
    }
}
