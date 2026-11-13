namespace Cqrs.Vermessung;

/// <summary>
/// Eine Anwendung im echten Host: optional frische DB (E3), Host-Prozess bereit, daneben der Rechnerdienst (Host.Rechner), wenn die Umgebung
/// App-Instanzen hat (er trägt ihre Quellen, etwa den Dateiwächter des Bildeingangs), Lese-Strom des Logs, JSON-Eingang, Uhr-Versatz. CPU und RSS des Hosts (und seiner Worker) schreibt <see cref="Ressourcen"/> mit.
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
    /// <summary>Der Rechnerdienst neben dem Host (null ohne App-Instanzen).</summary>
    public HostLauf? Rechnerdienst { get; init; }

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
        bool zuruecksetzen, IReadOnlyDictionary<string, string>? variablen = null, params string[] mitDaten)
        => await StarteAsync(k, u, hostProjekt, rolle, logName, zuruecksetzen, warmlauf: true, variablen, mitDaten);

    /// <param name="warmlauf">Nach dem Start ruhen (<see cref="Optionen.Warmlauf"/>); ohne: die Diagnose sieht den Leerlauf ab dem Start.</param>
    public static async Task<Anwendungslauf> StarteAsync(Kontext k, Umgebungsdatei u, string hostProjekt, string rolle, string logName,
        bool zuruecksetzen, bool warmlauf, IReadOnlyDictionary<string, string>? variablen = null, params string[] mitDaten)
    {
        var db = new Datenbank(u.Verbindung, u.Datenbank, u.Schema);
        if (zuruecksetzen)
        {
            await db.ZuruecksetzenAsync();
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
        // Die Quellen der App-Instanzen trägt der Rechnerdienst (docs/10 E18) — er gibt über die Leitung dieses Hosts hinein.
        HostLauf? dienst = null;
        if (u.Apps.Count > 0)
        {
            dienst = new HostLauf(k.HostDll("Host.Rechner"), u.Name, einblickPort: 0, k.LogDatei(logName + "-rechnerdienst"), variablen);
            await dienst.StarteAsync();
        }
        if (warmlauf) await Task.Delay(k.Optionen.Warmlauf);   // erst der warme Host wird gemessen
        k.Ressourcen.Beobachte(rolle, () => host.Prozess);
        var log = new LogStrom(db, mitDaten);
        log.Starte();
        return new Anwendungslauf
        {
            Db = db, Host = host, Rechnerdienst = dienst, Log = log, Eingang = new Eingang(u.EinblickPort), Uhrversatz = await db.UhrversatzAsync(), Rolle = rolle, Kontext = k,
        };
    }

    private bool _beendet;

    /// <summary>Beendet Host und Lese-Strom; ein zweiter Aufruf tut nichts (die Diagnose startet den Host danach neu).</summary>
    public async ValueTask DisposeAsync()
    {
        if (_beendet) return;
        _beendet = true;
        await Log.DisposeAsync();
        if (Rechnerdienst is not null) await Rechnerdienst.DisposeAsync();
        await Host.DisposeAsync();
        Kontext.Ressourcen.Vergiss(Rolle);
        Eingang.Dispose();
    }
}
