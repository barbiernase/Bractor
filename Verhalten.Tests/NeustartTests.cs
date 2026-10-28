using System.Collections.Concurrent;
using System.Diagnostics;
using System.Text;
using System.Text.Json.Nodes;
using Npgsql;

namespace Verhalten.Tests;

/// <summary>
/// Hartes Ende unter Last (G-17, docs/13; der Störungslauf der Vermessung im Kleinen): ein Host der Prüfdomäne im Consul-Cluster, acht Sender über den
/// JSON-Eingang (Aufträge mit Reservierung unter dem Mindestbestand, Verteilungen mit vier Zielen), mitten in der Last SIGKILL und Neustart nach 3 s —
/// die Sender wiederholen mit DERSELBEN CommandId, bis der neue Host antwortet. Danach muss jede Wirkung der Funktionen genau einmal im Log stehen:
/// je Reservierung unter dem Mindestbestand genau ein Nachschub, je Verteilung jedes Ziel genau einmal zugebucht und genau ein Abschluss.
/// </summary>
[Trait("Kategorie", "Garantie")]
[Trait("Kategorie", "Last")]
public sealed class NeustartTests
{
    private const int Sender = 8;
    private static readonly TimeSpan Last = TimeSpan.FromSeconds(40);
    private static readonly TimeSpan Abbruch = TimeSpan.FromSeconds(12);
    private static readonly TimeSpan Nachlauf = TimeSpan.FromMinutes(3);

    // DIAG G-17: Debug-Meldungen der Funktionen und Aggregate mit Zeitstempel, eine Zeile je Meldung.
    private static readonly Dictionary<string, string> Diagnose = new()
    {
        ["Logging__LogLevel__Funktion"] = "Debug", ["Logging__LogLevel__Infrastructure.Persistence.MartenEventStore"] = "Debug",
        ["Logging__Console__FormatterName"] = "simple", ["Logging__Console__FormatterOptions__SingleLine"] = "true",
        ["Logging__Console__FormatterOptions__TimestampFormat"] = "HH:mm:ss.fff ",
    };

    [Fact]
    public async Task Nach_dem_harten_Neustart_unter_Last_wirkt_jede_Funktion_genau_einmal()
    {
        var u = await Testumgebung.NeuAsync(Anwendung.Pruefdomaene, "verhalten-neustart", consul: true, taktSekunden: 2);
        var host = await Hostlauf.StarteAsync(u, "host-1", Diagnose);
        var eingang = new JsonEingang(host);
        var alteEingaenge = new List<JsonEingang>();
        try
        {
            var posten = Enumerable.Range(0, 16).Select(_ => Guid.NewGuid()).ToArray();
            var kontingente = Enumerable.Range(0, 16).Select(_ => Guid.NewGuid()).ToArray();
            var ziele = Enumerable.Range(0, 8).Select(_ => Guid.NewGuid()).ToArray();
            var voll = Guid.NewGuid();
            foreach (var p in posten) await eingang.MussAsync("EroeffnePosten", new() { ["AggregateId"] = p.ToString(), ["Bestand"] = 1_000_000, ["Mindestbestand"] = 999_990 });
            foreach (var k in kontingente) await eingang.MussAsync("RichteKontingentEin", new() { ["AggregateId"] = k.ToString(), ["Plaetze"] = 1_000_000_000 });
            foreach (var z in ziele) await eingang.MussAsync("EroeffnePosten", new() { ["AggregateId"] = z.ToString(), ["Bestand"] = 0 });
            await eingang.MussAsync("RichteKontingentEin", new() { ["AggregateId"] = voll.ToString(), ["Plaetze"] = 0 });

            var verteilungen = new ConcurrentBag<Guid>();
            var gescheitert = new ConcurrentBag<string>();
            long n = 0;
            var uhr = Stopwatch.StartNew();

            // Ein Sender schickt so schnell wie angenommen; scheitert der Weg (Host weg, Zeitüberschreitung), wiederholt er mit derselben CommandId.
            async Task SendeAsync()
            {
                while (uhr.Elapsed < Last)
                {
                    var i = Interlocked.Increment(ref n);
                    var id = Guid.NewGuid();
                    string typ;
                    JsonObject daten;
                    if (i % 10 == 9)
                    {
                        typ = "StarteVerteilung";
                        var z = Enumerable.Range(0, 4).Select(j => ziele[(int)((i / 10 * 4 + j) % ziele.Length)]).ToArray();
                        daten = new JsonObject
                        {
                            ["AggregateId"] = id.ToString(), ["QuelleId"] = posten[i % posten.Length].ToString(),
                            ["Ziele"] = new JsonArray(z.Select(x => (JsonNode)x.ToString()).ToArray()), ["MengeJeZiel"] = 1,
                        };
                        verteilungen.Add(id);
                    }
                    else
                    {
                        typ = "StarteAuftrag";
                        daten = new JsonObject
                        {
                            ["AggregateId"] = id.ToString(), ["PostenId"] = posten[i % posten.Length].ToString(),
                            ["KontingentId"] = (i % 10 == 8 ? voll : kontingente[i % kontingente.Length]).ToString(), ["Menge"] = 1 + (int)(i % 3),
                        };
                    }
                    var commandId = Guid.NewGuid();
                    var bis = DateTimeOffset.UtcNow + TimeSpan.FromMinutes(3);
                    while (true)
                    {
                        JsonEingang.Antwort a;
                        try { a = await Volatile.Read(ref eingang).CommandAsync(typ, (JsonObject)daten.DeepClone(), commandId: commandId); }
                        catch (Exception e) when (e is HttpRequestException or TaskCanceledException or ObjectDisposedException) { a = new(false, 0, null, e.Message); }
                        if (a.Status is 200 or 400 or 403) break;
                        if (DateTimeOffset.UtcNow > bis) { gescheitert.Add($"{typ} {id}: {a.Status} {a.Fehler}"); break; }
                        await Task.Delay(300);
                    }
                }
            }

            var sender = Enumerable.Range(0, Sender).Select(_ => Task.Run(SendeAsync)).ToArray();
            await Task.Delay(Abbruch);
            host.Toete();
            var getoetet = await JetztAsync(u);
            await Task.Delay(3000);
            await host.DisposeAsync();
            host = await Hostlauf.StarteAsync(u, "host-2", Diagnose);
            alteEingaenge.Add(eingang);
            Volatile.Write(ref eingang, new JsonEingang(host));
            await Task.WhenAll(sender);
            gescheitert.Should().BeEmpty("jeder Command kommt nach dem Neustart mit derselben CommandId durch");

            // Die Funktionen laufen den Vorgängen nach: warten, bis jede Wirkung da ist (oder die Frist um ist), dann das Soll prüfen.
            var nachlauf = Stopwatch.StartNew();
            Befund befund;
            while (true)
            {
                befund = await BefundAsync(u, verteilungen.ToArray(), getoetet, mitBericht: false);
                if (befund.Vollstaendig || nachlauf.Elapsed > Nachlauf) break;
                await Task.Delay(1000);
            }
            await Warte.NachlaufAsync(6);   // über drei Takt-Perioden: auch ein Nachzügler darf nicht ein zweites Mal wirken
            befund = await BefundAsync(u, verteilungen.ToArray(), getoetet);

            befund.Nachschuebe.Should().BeGreaterThan(1000, "die Last muss nennenswert sein, sonst beweist der Test nichts");
            befund.ZweimalGewirkt.Should().Be(0, "kein Eingang wirkt zweimal");
            befund.Vollstaendig.Should().BeTrue($"jede Wirkung der Funktionen steht nach dem Neustart genau einmal im Log\n{befund.Bericht}");
        }
        finally
        {
            eingang.Dispose();
            foreach (var e in alteEingaenge) e.Dispose();
            await host.DisposeAsync();
        }
    }

    private sealed record Befund(long Ausloeser, long Nachschuebe, long Zubuchungen, long ZubuchungenSoll, long Abgeschlossen, long Verteilungen,
        long ZweimalGewirkt, string Bericht)
    {
        public bool Vollstaendig => Nachschuebe == Ausloeser && Zubuchungen == ZubuchungenSoll && Abgeschlossen == Verteilungen;
    }

    private static async Task<DateTime> JetztAsync(Testumgebung u)
    {
        await using var conn = new NpgsqlConnection(u.DbVerbindung);
        await conn.OpenAsync();
        await using var cmd = new NpgsqlCommand("select clock_timestamp()", conn);
        return (DateTime)(await cmd.ExecuteScalarAsync())!;
    }

    /// <summary>Soll und Ist aus EINER Momentaufnahme; dazu die fehlenden Nachschübe (Stream, Version, Sequenz, Partition, Zeit relativ zum Abbruch).</summary>
    private static async Task<Befund> BefundAsync(Testumgebung u, Guid[] verteilungen, DateTime getoetet, bool mitBericht = true)
    {
        var s = Testumgebung.Schema;
        const string beleg = "(data->'Beleg'->>'Nummer')::uuid";
        await using var conn = new NpgsqlConnection(u.DbVerbindung);
        await conn.OpenAsync();
        await using var tx = await conn.BeginTransactionAsync(System.Data.IsolationLevel.RepeatableRead);
        async Task<long> Zahl(string sql)
        {
            await using var cmd = new NpgsqlCommand(sql, conn, tx) { CommandTimeout = 120 };
            cmd.Parameters.AddWithValue("v", verteilungen);
            return Convert.ToInt64(await cmd.ExecuteScalarAsync());
        }
        var ausloeser = await Zahl($"select count(*) from {s}.mt_events where type = 'posten_reserviert' and (data->>'Fehlmenge')::int > 0");
        var nachschuebe = await Zahl($"select count(distinct headers->>'ursache') from {s}.mt_events where type = 'posten_zugebucht' and headers->>'ueber' = 'funktion:Nachschub'");
        var zweimal = await Zahl($"""
            select count(*) from (select headers->>'ursache' from {s}.mt_events where type = 'posten_zugebucht' and headers->>'ueber' = 'funktion:Nachschub'
              group by 1 having count(*) > 1) x
            """);
        var zubuchungen = await Zahl($"select count(*) from {s}.mt_events where type = 'posten_zugebucht' and {beleg} = any(@v)");
        var abgeschlossen = await Zahl($"select count(*) from {s}.mt_events where type = 'verteilung_abgeschlossen' and stream_id = any(@v)");
        var gestartet = await Zahl($"select count(*) from {s}.mt_events where type = 'verteilung_gestartet' and stream_id = any(@v)");

        var bericht = new StringBuilder();
        bericht.AppendLine($"Nachschub {nachschuebe} von {ausloeser}; Zubuchungen der Verteilung {zubuchungen} von {gestartet * 4}; "
                           + $"Verteilungen abgeschlossen {abgeschlossen} von {gestartet} (gesendet {verteilungen.Length}); Abbruch {getoetet:HH:mm:ss.fff}");
        if (!mitBericht) return new Befund(ausloeser, nachschuebe, zubuchungen, gestartet * 4, abgeschlossen, gestartet, zweimal, bericht.ToString());
        await using (var cmd = new NpgsqlCommand($"""
            with zu as (select headers->>'ursache' u from {s}.mt_events where type = 'posten_zugebucht' and headers->>'ueber' = 'funktion:Nachschub')
            select r.stream_id, r.version, r.seq_id, get_byte(uuid_send(r.stream_id), 15) % 8, extract(epoch from r.timestamp - @t)::numeric(10, 3)
            from {s}.mt_events r left join zu on zu.u = r.stream_id::text || ':' || r.version
            where r.type = 'posten_reserviert' and (r.data->>'Fehlmenge')::int > 0 and zu.u is null
            order by r.seq_id limit 40
            """, conn, tx) { CommandTimeout = 120 })
        {
            cmd.Parameters.AddWithValue("t", getoetet);
            await using var r = await cmd.ExecuteReaderAsync();
            while (await r.ReadAsync())
                bericht.AppendLine($"  ohne Nachschub: {r.GetGuid(0)} v{r.GetInt64(1)} seq {r.GetInt64(2)} Partition {r.GetInt32(3)} {r.GetDecimal(4):+0.000;-0.000} s");
        }
        await using (var cmd = new NpgsqlCommand($"""
            with zu as (select {beleg} b, count(*) n from {s}.mt_events where type = 'posten_zugebucht' and {beleg} = any(@v) group by 1),
                 ab as (select stream_id from {s}.mt_events where type = 'verteilung_abgeschlossen' and stream_id = any(@v))
            select v.stream_id, v.seq_id, extract(epoch from v.timestamp - @t)::numeric(10, 3), coalesce(zu.n, 0)
            from {s}.mt_events v left join zu on zu.b = v.stream_id left join ab on ab.stream_id = v.stream_id
            where v.type = 'verteilung_gestartet' and v.stream_id = any(@v) and ab.stream_id is null
            order by v.seq_id limit 20
            """, conn, tx) { CommandTimeout = 120 })
        {
            cmd.Parameters.AddWithValue("t", getoetet);
            cmd.Parameters.AddWithValue("v", verteilungen);
            await using var r = await cmd.ExecuteReaderAsync();
            while (await r.ReadAsync())
                bericht.AppendLine($"  offene Verteilung: {r.GetGuid(0)} seq {r.GetInt64(1)} {r.GetDecimal(2):+0.000;-0.000} s, {r.GetInt64(3)} von 4 zugebucht");
        }
        return new Befund(ausloeser, nachschuebe, zubuchungen, gestartet * 4, abgeschlossen, gestartet, zweimal, bericht.ToString());
    }
}
