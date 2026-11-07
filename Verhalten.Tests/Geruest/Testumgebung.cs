using System.Net;
using System.Net.Sockets;
using System.Text.Json.Nodes;
using Npgsql;

namespace Verhalten.Tests.Geruest;

/// <summary>Welche Anwendung ein Host fährt: die Beispieldomäne (<c>Host.Grpc</c>, mit Leitung) oder die Prüfdomäne (<c>Host.Pruefdomaene</c>).</summary>
public enum Anwendung { Beispiel, Pruefdomaene }

/// <summary>
/// Eine Umgebung NUR für einen Testlauf (W7: eine Datei, <c>umgebung.&lt;name&gt;.json</c>): eigene Datenbank, eigene Ports, eigenes
/// Quell-Verzeichnis, eigene Tokens. Geschrieben in ein Temp-Verzeichnis, der Host findet sie über <c>CQRS_UMGEBUNGEN</c> — so wie die
/// Werkbank-Abnahme ihre <c>umgebung.abnahme.json</c> schreibt. Art <c>Sim</c>: der Host legt die Datenbank selbst an, und die Uhr lässt
/// sich vorstellen (Fristen).
/// </summary>
public sealed class Testumgebung
{
    public const string Verbindung = "Host=localhost;Port=5432;Username=postgres;Password=postgres";
    public const string Schema = "es";

    // Tokens der Beispieldomäne (Akteur-Mengen wie in umgebung.sim-main.json); die Prüfdomäne läuft ohne Tor.
    public const string TokenArbeitsplatz = "verhalten-arbeitsplatz";
    public const string TokenInspekteur = "verhalten-inspekteur";
    public const string TokenKiOperator = "verhalten-kioperator";
    public const string TokenKlassifizierer = "verhalten-klassifizierer";
    public const string TokenTrainingssystem = "verhalten-trainingssystem";
    public const string TokenDisponent = "verhalten-disponent";
    public const string TokenReaktionsWorker = "verhalten-reaktionsworker";

    /// <summary>Der Stand des ReaktionsWorkers in dieser Umgebung (eine frische Datenbank) — über alle Hosts, auch nach einem Neustart.</summary>
    public global::Client.Infrastructure.Reaktionen.ReaktionsStandImSpeicher Reaktionsstand { get; } = new();

    private Testumgebung(Anwendung anwendung, string name)
    {
        Anwendung = anwendung;
        Name = name;
        Verzeichnis = Path.Combine(Path.GetTempPath(), "cqrs-verhalten", name);
    }

    public Anwendung Anwendung { get; }
    public string Name { get; }
    public string Datenbank => "cqrs_" + Name.Replace('-', '_');
    public string Verzeichnis { get; }
    public string Quellpfad => Path.Combine(Verzeichnis, "quelle");
    public string Zwischenpfad => Path.Combine(Verzeichnis, "zwischen");
    /// <summary>Consul-Cluster (mehrere Knoten) statt Einzelknoten; der Name ist je Lauf neu, damit kein Mitglied eines früheren Laufs mitspielt.</summary>
    public string? ConsulCluster { get; private init; }
    /// <summary>Takt je Partition in Sekunden (Umgebung <c>Konsum.TaktSekunden</c>); null = Standard 30 s. Kurz für Tests, die auf den Takt warten —
    /// nie für einen Test, der zeigt, dass ein Commit weckt, ohne auf den Takt zu warten.</summary>
    public int? TaktSekunden { get; private init; }

    /// <summary>Eine frische Umgebung: die Datenbank des Namens wird gelöscht, das Verzeichnis neu angelegt.</summary>
    public static async Task<Testumgebung> NeuAsync(Anwendung anwendung, string name, bool consul = false, int? taktSekunden = null)
    {
        var u = new Testumgebung(anwendung, name) { ConsulCluster = consul ? $"cqrs-{name}-{Guid.NewGuid():N}"[..40] : null, TaktSekunden = taktSekunden };
        if (Directory.Exists(u.Verzeichnis)) Directory.Delete(u.Verzeichnis, recursive: true);
        Directory.CreateDirectory(u.Quellpfad);
        Directory.CreateDirectory(u.Zwischenpfad);
        await using var conn = new NpgsqlConnection(Verbindung + ";Database=postgres");
        await conn.OpenAsync();
        await using var cmd = new NpgsqlCommand($"drop database if exists \"{u.Datenbank}\" with (force)", conn);
        await cmd.ExecuteNonQueryAsync();
        // Eine Umgebung ist ein Ordner (docs/06 §6.5): umgebung.json und je Instanz eines Graphen eine Datei in graphen/.
        var ordner = Path.Combine(u.Verzeichnis, u.Name);
        Directory.CreateDirectory(Path.Combine(ordner, "graphen"));
        await File.WriteAllTextAsync(Path.Combine(ordner, "umgebung.json"), u.Json().ToJsonString(new() { WriteIndented = true }));
        foreach (var (instanz, inhalt) in u.Instanzen())
            await File.WriteAllTextAsync(Path.Combine(ordner, "graphen", instanz + ".json"), inhalt.ToJsonString(new() { WriteIndented = true }));
        return u;
    }

    /// <summary>Die Verbindung zur Datenbank der Umgebung (Lesen des Logs, Uhr der Sim).</summary>
    public string DbVerbindung => Verbindung + $";Database={Datenbank}";

    private JsonObject Json()
    {
        var json = new JsonObject
        {
            ["Umgebung"] = new JsonObject { ["Name"] = Name, ["Art"] = "Sim" },
            ["EventStore"] = new JsonObject { ["Betrieb"] = "Vorhanden", ["Verbindung"] = Verbindung, ["Datenbank"] = Datenbank, ["Schema"] = Schema },
            ["Cluster"] = ConsulCluster is { } c
                ? new JsonObject { ["Art"] = "Consul", ["Name"] = c, ["Consul"] = "localhost:8500", ["AdvertisedHost"] = "localhost" }
                : new JsonObject { ["Art"] = "Einzelknoten", ["Name"] = "cqrs-" + Name },
            // Die Ports setzt jeder Host-Prozess selbst (Transport__GrpcPort/EinblickPort): zwei Knoten teilen eine Umgebung.
            ["Transport"] = new JsonObject { ["GrpcPort"] = 0, ["EinblickPort"] = 0, ["JsonEingang"] = true },
        };
        if (TaktSekunden is { } takt) json["Konsum"] = new JsonObject { ["TaktSekunden"] = takt };
        if (Anwendung == Anwendung.Pruefdomaene) return json;
        json["Identitaeten"] = new JsonObject
        {
            ["Tokens"] = new JsonObject
            {
                [TokenArbeitsplatz] = "Inspekteur,KIOperator,Modellfreigeber,Produktpruefer",
                [TokenInspekteur] = "Inspekteur",
                [TokenKiOperator] = "KIOperator",
                [TokenKlassifizierer] = "Klassifizierer",
                [TokenTrainingssystem] = "TrainingsSystem",
                [TokenDisponent] = "Disponent",
                [TokenReaktionsWorker] = "Datensatzpfleger,Teilmelder",
            },
        };
        return json;
    }

    /// <summary>Die Instanzen der Graphen (je Graph eine): der Linienimport mit dem Ausgabe-Pfad der Bildaufbereitung; die Arbeit der Akteure kommt ohne
    /// Datei dazu.</summary>
    private IEnumerable<(string Instanz, JsonObject Inhalt)> Instanzen()
    {
        if (Anwendung == Anwendung.Pruefdomaene)
        {
            foreach (var g in new[] { "Abrechnungsablauf", "Auftragsablauf", "Verteilungsablauf", "Importablauf", "Nachschubablauf", "Zubuchung" })
                yield return (g.ToLowerInvariant(), new JsonObject { ["Graph"] = g });
            // Mit Zustand: im Cluster auf ihrem Rechner (der erste Host, Hostlauf.Rechner1).
            yield return ("merkmalbildung", new JsonObject { ["Graph"] = "Merkmalbildung", ["Rechner"] = Hostlauf.Rechner1 });
            yield break;
        }
        JsonObject ImHost(bool mitPfad = false)
        {
            var k = new JsonObject { ["Wiederholungen"] = 1 };
            if (mitPfad) k["AusgabePfad"] = Path.Combine(Verzeichnis, "aufbereitet");
            return k;
        }
        // Der Linienimport: ein Dateiwächter auf dem Quell-Verzeichnis, darunter die Bildaufbereitung (Graph im Graphen).
        yield return ("linienimport", new JsonObject
        {
            ["Graph"] = "Linienimport", ["Akteur"] = "KameraSystem", ["Rechner"] = Hostlauf.Rechner1,
            ["Eingang"] = new JsonObject
            {
                ["Pfad"] = Quellpfad, ["Muster"] = new JsonArray("*.tiff"), ["Intervall"] = "00:00:01", ["Fertig"] = "Stabil", ["Fenster"] = "00:00:01",
                ["Kennung"] = "NameGroesse",
            },
            ["Aufbereitung.Einlesen"] = ImHost(), ["Aufbereitung.Verkleinern"] = ImHost(mitPfad: true), ["Aufbereitung.Kontrast"] = ImHost(mitPfad: true),
        });
    }

    public static int FreierPort()
    {
        var l = new TcpListener(IPAddress.Loopback, 0);
        l.Start();
        var port = ((IPEndPoint)l.LocalEndpoint).Port;
        l.Stop();
        return port;
    }

    /// <summary>Nur Sim: die Uhr der Datenbank vorstellen — Event-Zeitstempel und Fristen laufen mit derselben Uhr (docs/03 §3.3).</summary>
    public async Task StelleUhrVorAsync(TimeSpan versatz)
    {
        await using var conn = new NpgsqlConnection(DbVerbindung);
        await conn.OpenAsync();
        await using var cmd = new NpgsqlCommand($"update \"{Schema}\".uhr set versatz = make_interval(secs => @s) where id = 1", conn);
        cmd.Parameters.AddWithValue("s", versatz.TotalSeconds);
        (await cmd.ExecuteNonQueryAsync()).Should().Be(1, "die Sim-Uhr hat genau eine Zeile");
    }
}
