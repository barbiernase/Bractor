using System.Collections.Concurrent;
using System.Diagnostics;
using System.Net.Http.Json;
using System.Net.Sockets;
using System.Text;
using System.Text.Json.Nodes;
using Npgsql;

namespace Werkbank.Abnahme.Tests;

/// <summary>
/// Eine echte Werkbank auf einer eigenen Kopie des Arbeitsbaums (docs/12 §12.1, docs/13 WB-2):
/// <list type="bullet">
/// <item>Kopie aller Quell-Dateien (<c>git ls-files -co --exclude-standard</c>: verfolgt + neu, ohne Ignoriertes) in ein Temp-Verzeichnis,
///   dort <c>git init -b abnahme-&lt;id&gt;</c> — der Branch benennt Sim-DB, Redis-Präfix und Cluster; Gesten sind erlaubt (nicht <c>main</c>).</item>
/// <item>Umgebung <c>abnahme</c> = Kopie von <c>sim-main</c> mit freien Ports, Pfaden im Temp-Verzeichnis und <c>IBildVerkleinerung</c> auf
///   Laufort <c>Extern</c> (nur in der Kopie).</item>
/// <item>Die gebaute Werkbank als Prozess (<c>dotnet Werkbank.dll</c>, Arbeitsverzeichnis = Kopie, <c>PORT</c> frei); der Test spricht nur HTTP.</item>
/// </list>
/// Der Arbeitsbaum des Entwicklers, seine <c>.werkbank/</c> und seine Sim-DB bleiben unberührt. Aufräumen: SIGTERM an die Werkbank (sie hält
/// Host und Worker an), eigene DB und Redis-Schlüssel löschen, Kopie löschen.
/// </summary>
public sealed class Werkbanklauf : IAsyncDisposable
{
    public const string Umgebung = "abnahme";
    public string Repo { get; }
    public string Kopie { get; }
    public string Daten { get; }
    public string Branch { get; }
    public int Port { get; } = FreierPort();
    public int GrpcPort { get; } = FreierPort();
    public int EinblickPort { get; } = FreierPort();
    public string Datenbank => "cqrs_sim_" + Branch.Replace('-', '_');
    public HttpClient Http { get; }
    /// <summary>Die kopierten Quell-Dateien (relativ) — für „byte-gleich“-Vergleiche.</summary>
    public IReadOnlyList<string> Dateien { get; private set; } = [];

    private readonly string _wurzel;
    private readonly ConcurrentQueue<string> _log = new();
    private Process? _werkbank;
    private string _verbindung = "";
    private string _redis = "";
    private int _redisDb;
    private string _praefix = "";

    private Werkbanklauf()
    {
        Repo = RepoWurzel();
        var id = Guid.NewGuid().ToString("N")[..6];
        Branch = "abnahme-" + id;
        _wurzel = Directory.CreateTempSubdirectory("werkbank-abnahme-").FullName;
        Kopie = Path.Combine(_wurzel, "repo");
        Daten = Path.Combine(_wurzel, "daten");
        Http = new HttpClient { BaseAddress = new Uri($"http://localhost:{Port}"), Timeout = TimeSpan.FromMinutes(15) };
    }

    /// <summary>Die letzten Zeilen der Werkbank-Ausgabe — für Fehlermeldungen.</summary>
    public string Log(int n = 60) => string.Join("\n", _log.Reverse().Take(n).Reverse());

    public static async Task<Werkbanklauf> StarteAsync()
    {
        var w = new Werkbanklauf();
        try
        {
            await w.KopiereAsync();
            await w.SchreibeUmgebungAsync();
            await Pruefe(await LaufeAsync("dotnet", ["restore", "CqrsSolution.sln", "-nologo", "-v", "q"], w.Kopie, TimeSpan.FromMinutes(5)), "dotnet restore der Kopie");
            await w.StarteWerkbankAsync();
            return w;
        }
        catch
        {
            await w.DisposeAsync();
            throw;
        }
    }

    private async Task KopiereAsync()
    {
        var liste = await LaufeAsync("git", ["-c", "core.quotepath=off", "ls-files", "-co", "--exclude-standard"], Repo, TimeSpan.FromMinutes(1));
        await Pruefe(liste, "git ls-files");
        Directory.CreateDirectory(Kopie);
        var kopiert = new List<string>();
        foreach (var datei in liste.Zeilen.Where(z => z.Length > 0).Distinct(StringComparer.Ordinal))
        {
            var quelle = Path.Combine(Repo, datei);
            if (!File.Exists(quelle)) continue;   // verfolgt, aber im Arbeitsbaum gelöscht
            var ziel = Path.Combine(Kopie, datei);
            Directory.CreateDirectory(Path.GetDirectoryName(ziel)!);
            File.Copy(quelle, ziel);
            kopiert.Add(datei);
        }
        Dateien = kopiert;
        await Pruefe(await LaufeAsync("git", ["init", "-q", "-b", Branch], Kopie, TimeSpan.FromMinutes(1)), "git init");
    }

    /// <summary>
    /// <c>umgebung.abnahme.json</c> aus <c>sim-main</c>: eigene Ports, Daten-Pfade im Temp-Verzeichnis, die Verkleinerung extern (Python-Worker).
    /// DB, Redis-Präfix und Cluster folgen über <c>${branch}</c> dem Branch der Kopie.
    /// </summary>
    private async Task SchreibeUmgebungAsync()
    {
        var verz = Path.Combine(Kopie, "umgebungen");
        var text = (await File.ReadAllTextAsync(Path.Combine(verz, "umgebung.sim-main.json"))).Replace("${env:HOME}/cqrs-sim/${branch}", Daten);
        var u = JsonNode.Parse(text)!.AsObject();
        u["Umgebung"]!["Name"] = Umgebung;
        u["Transport"]!["GrpcPort"] = GrpcPort;
        u["Transport"]!["EinblickPort"] = EinblickPort;
        u["Funktionen"]!["IBildVerkleinerung"]!["Laufort"] = "Extern";
        await File.WriteAllTextAsync(Path.Combine(verz, $"umgebung.{Umgebung}.json"), u.ToJsonString(new() { WriteIndented = true }));

        var schluessel = Branch.Replace('-', '_');
        _verbindung = (string)u["EventStore"]!["Verbindung"]!;
        _redis = (string)u["VersionIndex"]!["Endpunkt"]!;
        _redisDb = (int?)u["VersionIndex"]!["Datenbank"] ?? 0;
        _praefix = ((string)u["VersionIndex"]!["Praefix"]!).Replace("${branch}", schluessel);
    }

    private async Task StarteWerkbankAsync()
    {
        // Der Build-Ausgang der Werkbank, gefragt beim Build-System (wie die Werkbank den Host findet) — nicht geraten.
        var ziel = await LaufeAsync("dotnet", ["msbuild", Path.Combine("Werkbank", "Werkbank.csproj"), "-getProperty:TargetPath", "-nologo"], Repo, TimeSpan.FromMinutes(1));
        var dll = ziel.Zeilen.Select(z => z.Trim()).LastOrDefault(z => z.EndsWith(".dll", StringComparison.Ordinal));
        if (dll is null || !File.Exists(dll)) throw new InvalidOperationException($"Werkbank nicht gebaut ({dll}) — erst dotnet build.");

        var psi = new ProcessStartInfo("dotnet") { WorkingDirectory = Kopie, RedirectStandardOutput = true, RedirectStandardError = true, UseShellExecute = false };
        psi.ArgumentList.Add(dll);
        psi.Environment["PORT"] = Port.ToString();
        var p = new Process { StartInfo = psi };
        p.OutputDataReceived += (_, e) => Nimm(e.Data);
        p.ErrorDataReceived += (_, e) => Nimm(e.Data);
        p.Start();
        p.BeginOutputReadLine();
        p.BeginErrorReadLine();
        _werkbank = p;

        await Warte(async () =>
        {
            if (p.HasExited) throw new InvalidOperationException($"Werkbank beendet (Exit {p.ExitCode}):\n{Log()}");
            try { return (await Http.GetAsync("/api/werkbank/zustand")).IsSuccessStatusCode; }
            catch (HttpRequestException) { return false; }
        }, TimeSpan.FromSeconds(90), "Die Werkbank antwortet nicht.");
    }

    private void Nimm(string? z)
    {
        if (z == null) return;
        _log.Enqueue(z);
        while (_log.Count > 2000) _log.TryDequeue(out _);
    }

    /// <summary>Inhalt aller kopierten Quell-Dateien jetzt (Pfad → Bytes).</summary>
    public Dictionary<string, byte[]> Abbild() => Dateien.Where(d => File.Exists(Path.Combine(Kopie, d)))
        .ToDictionary(d => d, d => File.ReadAllBytes(Path.Combine(Kopie, d)), StringComparer.Ordinal);

    // ── HTTP ──

    public async Task<JsonObject> HoleAsync(string pfad)
    {
        using var r = await Http.GetAsync(pfad);
        return JsonNode.Parse(await r.Content.ReadAsStringAsync())!.AsObject();
    }

    public async Task<(int Status, JsonObject Antwort)> SendeAsync(string pfad, object? inhalt = null, string? token = null)
    {
        using var req = new HttpRequestMessage(HttpMethod.Post, pfad);
        if (inhalt is HttpContent c) req.Content = c;
        else if (inhalt != null) req.Content = JsonContent.Create(inhalt);
        if (token != null) req.Headers.Add("akteur-token", token);
        using var r = await Http.SendAsync(req);
        var text = await r.Content.ReadAsStringAsync();
        return ((int)r.StatusCode, JsonNode.Parse(text) as JsonObject ?? new JsonObject { ["roh"] = text });
    }

    // ── Aufräumen ──

    public async ValueTask DisposeAsync()
    {
        var pids = new List<int>();
        if (_werkbank is { HasExited: false } p)
        {
            // Wer noch läuft (Host, Worker), damit nichts verwaist, falls die Werkbank nicht geordnet herunterfährt.
            try
            {
                if ((int?)(await HoleAsync("/api/werkbank/zustand"))["host"]?["pid"] is { } host) pids.Add(host);
                foreach (var w in (await HoleAsync("/api/werkbank/worker"))["worker"] as JsonArray ?? [])
                    if ((int?)w?["pid"] is { } wp) pids.Add(wp);
            }
            catch (Exception) { /* Werkbank antwortet nicht mehr */ }
            // SIGTERM: die Werkbank hält Host und Worker über ApplicationStopping an.
            try { Process.Start("kill", ["-TERM", p.Id.ToString()])?.WaitForExit(2000); } catch (Exception) { }
            if (!p.WaitForExit(TimeSpan.FromSeconds(40))) try { p.Kill(entireProcessTree: true); } catch (Exception) { }
        }
        foreach (var pid in pids)
            try { using var rest = Process.GetProcessById(pid); if (!rest.HasExited) rest.Kill(entireProcessTree: true); } catch (ArgumentException) { }
        if (_verbindung.Length > 0)
            try
            {
                await using var c = new NpgsqlConnection(new NpgsqlConnectionStringBuilder(_verbindung) { Database = "postgres" }.ConnectionString);
                await c.OpenAsync();
                await using var cmd = new NpgsqlCommand($"drop database if exists \"{Datenbank}\" with (force)", c);
                await cmd.ExecuteNonQueryAsync();
            }
            catch (NpgsqlException) { }
        if (_redis.Length > 0)
            try
            {
                await using var r = await StackExchange.Redis.ConnectionMultiplexer.ConnectAsync(_redis + ",allowAdmin=true,connectTimeout=2000");
                var db = r.GetDatabase(_redisDb);
                foreach (var ep in r.GetEndPoints())
                    foreach (var k in r.GetServer(ep).Keys(_redisDb, _praefix + "*"))
                        await db.KeyDeleteAsync(k);
            }
            catch (StackExchange.Redis.RedisException) { }
        Http.Dispose();
        try { Directory.Delete(_wurzel, recursive: true); } catch (IOException) { } catch (UnauthorizedAccessException) { }
    }

    // ── Hilfen ──

    public static async Task Warte(Func<Task<bool>> bedingung, TimeSpan zeit, string meldung)
    {
        var bis = DateTime.UtcNow + zeit;
        while (DateTime.UtcNow < bis)
        {
            if (await bedingung()) return;
            await Task.Delay(500);
        }
        throw new TimeoutException(meldung);
    }

    public sealed record Ergebnis(int Code, IReadOnlyList<string> Zeilen);

    public static async Task<Ergebnis> LaufeAsync(string datei, IEnumerable<string> argumente, string verz, TimeSpan zeit)
    {
        var psi = new ProcessStartInfo(datei) { WorkingDirectory = verz, RedirectStandardOutput = true, RedirectStandardError = true, UseShellExecute = false };
        foreach (var a in argumente) psi.ArgumentList.Add(a);
        // Wiederverwendete MSBuild-Knoten erben die Ausgabe-Pipe und hielten sie über das Prozessende hinaus offen.
        psi.Environment["MSBUILDDISABLENODEREUSE"] = "1";
        var zeilen = new ConcurrentQueue<string>();
        using var p = new Process { StartInfo = psi };
        p.OutputDataReceived += (_, e) => { if (e.Data != null) zeilen.Enqueue(e.Data); };
        p.ErrorDataReceived += (_, e) => { if (e.Data != null) zeilen.Enqueue(e.Data); };
        p.Start();
        p.BeginOutputReadLine();
        p.BeginErrorReadLine();
        using var cts = new CancellationTokenSource(zeit);
        try { await p.WaitForExitAsync(cts.Token); }
        catch (OperationCanceledException) { p.Kill(entireProcessTree: true); throw new TimeoutException($"{datei} {string.Join(' ', argumente)}: Zeitlimit {zeit}"); }
        p.WaitForExit(TimeSpan.FromSeconds(5));   // Ausgabe-Puffer leeren, ohne auf fremde Erben der Pipe zu warten
        return new Ergebnis(p.ExitCode, zeilen.ToList());
    }

    private static Task Pruefe(Ergebnis e, string was) => e.Code == 0 ? Task.CompletedTask
        : throw new InvalidOperationException($"{was} gescheitert (Exit {e.Code}):\n{string.Join("\n", e.Zeilen.TakeLast(20))}");

    private static int FreierPort()
    {
        var l = new TcpListener(System.Net.IPAddress.Loopback, 0);
        l.Start();
        var p = ((System.Net.IPEndPoint)l.LocalEndpoint).Port;
        l.Stop();
        return p;
    }

    private static string RepoWurzel()
    {
        var d = new DirectoryInfo(AppContext.BaseDirectory);
        while (d != null && d.GetFiles("*.sln").Length == 0) d = d.Parent;
        return d?.FullName ?? throw new InvalidOperationException("Keine .sln über " + AppContext.BaseDirectory);
    }

    /// <summary>
    /// Ein kleines Graustufen-TIFF (unkomprimiert, ein Streifen) — genug, damit OpenCV es liest (Python-Verkleinerung, C#-Ausgleich).
    /// </summary>
    public static byte[] Tiff(int breite, int hoehe)
    {
        const int eintraege = 9;
        var ifd = 8;
        var daten = ifd + 2 + eintraege * 12 + 4;
        using var m = new MemoryStream();
        using var w = new BinaryWriter(m, Encoding.ASCII);
        w.Write((byte)'I'); w.Write((byte)'I'); w.Write((ushort)42); w.Write((uint)ifd);
        w.Write((ushort)eintraege);
        void Eintrag(ushort tag, ushort typ, uint wert) { w.Write(tag); w.Write(typ); w.Write(1u); if (typ == 3) { w.Write((ushort)wert); w.Write((ushort)0); } else w.Write(wert); }
        Eintrag(256, 4, (uint)breite);            // ImageWidth
        Eintrag(257, 4, (uint)hoehe);             // ImageLength
        Eintrag(258, 3, 8);                       // BitsPerSample
        Eintrag(259, 3, 1);                       // Compression: keine
        Eintrag(262, 3, 1);                       // Photometric: BlackIsZero
        Eintrag(273, 4, (uint)daten);             // StripOffsets
        Eintrag(277, 3, 1);                       // SamplesPerPixel
        Eintrag(278, 4, (uint)hoehe);             // RowsPerStrip
        Eintrag(279, 4, (uint)(breite * hoehe));  // StripByteCounts
        w.Write(0u);                              // kein weiteres IFD
        for (var y = 0; y < hoehe; y++)
            for (var x = 0; x < breite; x++) w.Write((byte)((x * 255 / Math.Max(1, breite - 1) + y) & 0xFF));
        w.Flush();
        return m.ToArray();
    }
}
