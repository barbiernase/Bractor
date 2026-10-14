using System.Net.Sockets;
using Abstractions;
using Npgsql;

namespace Werkbank.Server;

/// <summary>
/// Die Infrastruktur einer Umgebung (docs/konzept-werkbank.md §4.5): Postgres immer, Redis bei <c>VersionIndex = Verwaltet</c>,
/// Consul bei <c>Cluster = Consul</c>. Für <c>Verwaltet</c> erzeugt die Werkbank ein Docker-Compose-Projekt <c>cqrs-&lt;umgebung&gt;</c> und
/// startet es — nur wenn der Dienst nicht schon erreichbar ist (ein lokal laufender Postgres auf demselben Port wird mitbenutzt).
/// <c>Vorhanden</c> verwaltet sie nie.
/// </summary>
public sealed class Infra(string slnRoot)
{
    public sealed record Dienst(string Name, string Host, int Port, Betrieb Betrieb, bool Erreichbar);

    public IReadOnlyList<Dienst> Dienste(UmgebungKonfig u)
    {
        var pg = new NpgsqlConnectionStringBuilder(u.EventStore.Verbindung);
        var aus = new List<Dienst> { Pruefe("Postgres", pg.Host ?? "localhost", pg.Port, u.EventStore.Betrieb) };
        if (u.VersionIndex.Betrieb != Betrieb.Aus)
        {
            var (h, p) = HostPort(u.VersionIndex.Endpunkt, 6379);
            aus.Add(Pruefe("Redis", h, p, u.VersionIndex.Betrieb));
        }
        if (u.Cluster.Art == ClusterArt.Consul)
        {
            var (h, p) = HostPort(u.Cluster.Consul.Replace("http://", "").Replace("https://", ""), 8500);
            aus.Add(Pruefe("Consul", h, p, u.EventStore.Betrieb));
        }
        return aus;
    }

    private static (string, int) HostPort(string endpunkt, int standard)
    {
        var teile = endpunkt.Split(':');
        return (teile[0], teile.Length > 1 && int.TryParse(teile[1], out var p) ? p : standard);
    }

    private static Dienst Pruefe(string name, string host, int port, Betrieb betrieb)
    {
        try
        {
            using var c = new TcpClient();
            var ok = c.ConnectAsync(host, port).Wait(TimeSpan.FromMilliseconds(400)) && c.Connected;
            return new Dienst(name, host, port, betrieb, ok);
        }
        catch { return new Dienst(name, host, port, betrieb, false); }
    }

    /// <summary>Das Compose-Projekt der Umgebung (nur die verwalteten, noch nicht erreichbaren Dienste).</summary>
    public string Compose(string name, UmgebungKonfig u, IReadOnlyList<Dienst> fehlend)
    {
        var b = new System.Text.StringBuilder("# Von der Werkbank erzeugt (docs/konzept-werkbank.md §4.5) — nicht von Hand ändern.\nservices:\n");
        foreach (var d in fehlend)
        {
            var bild = d.Name switch { "Postgres" => "postgres:16-alpine", "Redis" => "redis:7-alpine", _ => "hashicorp/consul:1.19" };
            b.AppendLine($"  {d.Name.ToLowerInvariant()}:\n    image: {bild}\n    ports: [\"127.0.0.1:{d.Port}:{(d.Name == "Postgres" ? 5432 : d.Name == "Redis" ? 6379 : 8500)}\"]");
            if (d.Name == "Postgres") b.AppendLine("    environment: { POSTGRES_USER: postgres, POSTGRES_PASSWORD: postgres }");
            if (d.Name == "Consul") b.AppendLine("    command: agent -dev -client 0.0.0.0");
        }
        var verz = Path.Combine(slnRoot, ".werkbank", name);
        Directory.CreateDirectory(verz);
        var datei = Path.Combine(verz, "docker-compose.yml");
        File.WriteAllText(datei, b.ToString());
        return datei;
    }

    /// <summary>Startet die fehlenden verwalteten Dienste und wartet, bis sie erreichbar sind.</summary>
    public async Task<(bool Ok, IReadOnlyList<string> Meldungen)> StarteAsync(string name, UmgebungKonfig u, CancellationToken ct)
    {
        var fehlend = Dienste(u).Where(d => !d.Erreichbar).ToList();
        var nichtVerwaltet = fehlend.Where(d => d.Betrieb != Betrieb.Verwaltet).ToList();
        if (nichtVerwaltet.Count > 0)
            return (false, nichtVerwaltet.Select(d => $"{d.Name} ({d.Host}:{d.Port}) ist nicht erreichbar und nicht verwaltet").ToList());
        if (fehlend.Count == 0) return (true, ["Infrastruktur erreichbar"]);
        var datei = Compose(name, u, fehlend);
        var r = await Prozesse.LaufeAsync("docker", ["compose", "-p", "cqrs-" + name, "-f", datei, "up", "-d"], slnRoot, zeitlimit: TimeSpan.FromMinutes(5), ct: ct);
        if (!r.Ok) return (false, r.Zeilen.TakeLast(10).ToList());
        var bis = DateTime.UtcNow.AddSeconds(60);
        while (DateTime.UtcNow < bis && Dienste(u).Any(d => !d.Erreichbar)) await Task.Delay(1000, ct);
        var noch = Dienste(u).Where(d => !d.Erreichbar).Select(d => d.Name).ToList();
        return (noch.Count == 0, noch.Count == 0 ? [$"gestartet: {string.Join(", ", fehlend.Select(d => d.Name))}"] : [$"nicht bereit: {string.Join(", ", noch)}"]);
    }
}
