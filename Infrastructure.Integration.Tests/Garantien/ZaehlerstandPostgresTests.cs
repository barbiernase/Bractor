using System.Collections.Concurrent;
using FluentAssertions;
using Infrastructure.Persistence;
using Npgsql;

namespace Infrastructure.Integration.Tests.Garantien;

/// <summary>
/// G-17, der Zählerstand einer Probe des Horizonts (<see cref="Horizont"/>): er darf nie einer Sequenz vorauseilen, die noch niemand gezogen hat.
/// Ein gewöhnliches <c>select last_value</c> liest die Seite der Sequenz ohne Sperre und sieht mitunter den Wert, den ein <c>nextval</c> gerade
/// vorausloggt (bis zu 32 darüber); eine Probe mit diesem Stand gab Sequenzen frei, deren Transaktionen erst danach ihre Nummer bekamen — unter
/// Last übersprangen Funktionen so Eingänge, ihre Wirkungen fehlten. Nachweis unter Last: acht Verbindungen ziehen ununterbrochen, ein Leser liest
/// den Stand und zieht gleich danach selbst — was er zieht, muss größer sein als der gelesene Stand.
/// </summary>
[Trait("Kategorie", "Garantie")]
public class ZaehlerstandPostgresTests
{
    private const string Verbindung = "Host=localhost;Port=5432;Database=cqrs_events;Username=postgres;Password=postgres";
    private const string Schema = "garantie_zaehlerstand_it";

    [Fact]
    public async Task Der_Zaehlerstand_der_Probe_eilt_der_naechsten_Sequenz_nie_voraus()
    {
        await using (var conn = new NpgsqlConnection(Verbindung))
        {
            await conn.OpenAsync();
            await using var cmd = new NpgsqlCommand($"""
                create schema if not exists {Schema};
                drop sequence if exists {Schema}.mt_events_sequence;
                create sequence {Schema}.mt_events_sequence;
                """, conn);
            await cmd.ExecuteNonQueryAsync();
        }

        using var dauer = new CancellationTokenSource(TimeSpan.FromSeconds(8));
        var schreiber = Enumerable.Range(0, 8).Select(_ => Task.Run(async () =>
        {
            await using var conn = new NpgsqlConnection(Verbindung);
            await conn.OpenAsync();
            await using var cmd = new NpgsqlCommand($"select nextval('{Schema}.mt_events_sequence')", conn);
            while (!dauer.IsCancellationRequested) await cmd.ExecuteScalarAsync();
        })).ToArray();

        var voraus = new ConcurrentQueue<(long Stand, long Gezogen)>();
        var proben = 0;
        await using (var conn = new NpgsqlConnection(Verbindung))
        {
            await conn.OpenAsync();
            await using var stand = new NpgsqlCommand(EventStoreAbfragen.Zaehlerstand(Schema), conn);
            await using var ziehe = new NpgsqlCommand($"select nextval('{Schema}.mt_events_sequence')", conn);
            while (!dauer.IsCancellationRequested)
            {
                var s = Convert.ToInt64(await stand.ExecuteScalarAsync());
                var g = Convert.ToInt64(await ziehe.ExecuteScalarAsync());
                proben++;
                if (g <= s) voraus.Enqueue((s, g));
            }
        }
        await Task.WhenAll(schreiber);

        proben.Should().BeGreaterThan(1_000, "sonst beweist der Test nichts");
        voraus.Should().BeEmpty($"der Stand einer Probe ist eine schon vergebene Sequenz, nie eine künftige ({proben} Proben)");
    }
}
