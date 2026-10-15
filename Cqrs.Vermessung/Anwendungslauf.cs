namespace Cqrs.Vermessung;

/// <summary>
/// Eine Anwendung im echten Host: optional frische DB (E3) und frischer Redis-Index, Host-Prozess bereit, Lese-Strom des Logs, JSON-Eingang,
/// Uhr-Versatz. CPU und RSS des Hosts (und seiner Worker) schreibt <see cref="Ressourcen"/> mit.
/// </summary>
internal sealed class Anwendungslauf : IAsyncDisposable
{
    public required Datenbank Db { get; init; }
    public required HostLauf Host { get; init; }
    public required LogStrom Log { get; init; }
    public required Eingang Eingang { get; init; }
    public required TimeSpan Uhrversatz { get; init; }
    public required string Rolle { get; init; }
    public required Kontext Kontext { get; init; }
    public List<WorkerLauf> Worker { get; } = new();

    public static async Task<Anwendungslauf> StarteAsync(Kontext k, Umgebungsdatei u, string hostProjekt, string rolle, string logName,
        bool zuruecksetzen, IReadOnlyDictionary<string, string>? variablen = null, params string[] mitDaten)
    {
        var db = new Datenbank(u.Verbindung, u.Datenbank, u.Schema);
        if (zuruecksetzen)
        {
            await db.ZuruecksetzenAsync();
            Werkzeuge.LeereRedis(u.RedisEndpunkt, u.RedisDatenbank);
            if (u.Quellpfad is { } q)
            {
                var basis = Path.GetDirectoryName(q)!;
                if (Directory.Exists(basis)) Directory.Delete(basis, recursive: true);
                Directory.CreateDirectory(q);
                if (u.Ausgabepfad is { } a) Directory.CreateDirectory(a);
            }
        }
        var host = new HostLauf(k.HostDll(hostProjekt), u.Name, u.EinblickPort, k.LogDatei(logName), variablen);
        await host.StarteAsync();
        k.Ressourcen.Beobachte(rolle, () => host.Prozess);
        var log = new LogStrom(db, mitDaten);
        log.Starte();
        return new Anwendungslauf
        {
            Db = db, Host = host, Log = log, Eingang = new Eingang(u.EinblickPort), Uhrversatz = await db.UhrversatzAsync(), Rolle = rolle, Kontext = k,
        };
    }

    /// <summary>Startet einen externen Funktions-Worker nach dem Worker-Block der Umgebung (wie „▶ Worker“ der Werkbank).</summary>
    public async Task StarteWorkerAsync(Umgebungsdatei u, string funktion, string logName)
    {
        var (skript, argumente, token) = u.Worker(Kontext.Wurzel, funktion);
        var w = new WorkerLauf(u.Python, skript, argumente, u.GrpcPort, token, Kontext.LogDatei(logName));
        Worker.Add(w);
        Kontext.Ressourcen.Beobachte("worker-" + funktion, () => w.Prozess);
        await w.BereitAsync(TimeSpan.FromSeconds(60));
    }

    public async ValueTask DisposeAsync()
    {
        await Log.DisposeAsync();
        foreach (var w in Worker) await w.DisposeAsync();
        await Host.DisposeAsync();
        Kontext.Ressourcen.Vergiss(Rolle);
        Eingang.Dispose();
    }
}
