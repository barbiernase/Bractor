namespace Cqrs.Vermessung;

/// <summary>
/// Eine Anwendung im echten Host: optional frische DB (E3) und frischer Redis-Index, Host-Prozess bereit, Lese-Strom des Logs, JSON-Eingang,
/// Uhr-Versatz. CPU und RSS des Hosts schreibt <see cref="Ressourcen"/> mit.
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

    /// <summary>Höchste Zahl aktiver Actors an einem Fensterende dieses Laufs (Maßstab der Passivierung, PF-5).</summary>
    public double HoechstActors { get; private set; } = double.NaN;

    /// <summary>Aktive Actors jetzt; führt den Höchststand nach.</summary>
    public async Task<double> AktiveActorsAsync()
    {
        var n = await Eingang.AktiveActorsAsync();
        if (!double.IsNaN(n) && (double.IsNaN(HoechstActors) || n > HoechstActors)) HoechstActors = n;
        return n;
    }

    public static async Task<Anwendungslauf> StarteAsync(Kontext k, Umgebungsdatei u, string hostProjekt, string rolle, string logName,
        bool zuruecksetzen, params string[] mitDaten)
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
        var host = new HostLauf(k.HostDll(hostProjekt), u.Name, u.EinblickPort, k.LogDatei(logName));
        await host.StarteAsync();
        await Task.Delay(k.Optionen.Warmlauf);   // erst der warme Host wird gemessen
        k.Ressourcen.Beobachte(rolle, () => host.Prozess);
        var log = new LogStrom(db, mitDaten);
        log.Starte();
        return new Anwendungslauf
        {
            Db = db, Host = host, Log = log, Eingang = new Eingang(u.EinblickPort), Uhrversatz = await db.UhrversatzAsync(), Rolle = rolle, Kontext = k,
        };
    }

    private bool _beendet;

    /// <summary>Beendet Host und Lese-Strom; ein zweiter Aufruf tut nichts.</summary>
    public async ValueTask DisposeAsync()
    {
        if (_beendet) return;
        _beendet = true;
        await Log.DisposeAsync();
        await Host.DisposeAsync();
        Kontext.Ressourcen.Vergiss(Rolle);
        Eingang.Dispose();
    }
}
