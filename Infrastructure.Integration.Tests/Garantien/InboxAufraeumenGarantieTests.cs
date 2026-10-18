using System.Globalization;
using System.Text.Json;
using FluentAssertions;
using Infrastructure.Persistence;
using Marten;
using Npgsql;
using Xunit;

namespace Infrastructure.Integration.Tests.Garantien;

/// <summary>
/// Das Aufräumen der Inbox kostet, was es löscht — nicht, wie groß die Tabelle ist (PF-27; docs/12 §12.5). Gegen echtes Postgres mit einer
/// Million Quittungen über 60 Tage: nach dem ersten Durchlauf (die Hälfte gelöscht, die toten Zeilen noch nicht weggeräumt) und nach VACUUM und
/// Nachfüllen (neue Quittungen in den frei gewordenen Seiten) liest die nächste Seite des Aufräumers nur eine Handvoll Blöcke. Gemessen über
/// <c>EXPLAIN (ANALYZE, BUFFERS)</c> der echten Anweisung (<see cref="InboxAufraeumer.Abfrage"/>) in einer zurückgerollten Transaktion, mit
/// zugeschnittenem und allgemeinem Plan.
/// </summary>
[Trait("Kategorie", "Garantie")]
public class InboxAufraeumenGarantieTests : IClassFixture<InboxAufraeumenGarantieTests.Fixture>
{
    /// <summary>Eine Seite von 100 Quittungen löschen kostet einige hundert Blöcke (Index und Zeilen); ein Durchsuchen der Tabelle läse Tausende.</summary>
    private const long HoechstBloecke = 2_000;
    private const int MessSeite = 100;

    private readonly Fixture _fx;
    public InboxAufraeumenGarantieTests(Fixture fx) => _fx = fx;

    [Fact]
    public async Task Die_naechste_Seite_liest_weder_tote_noch_wiederverwendete_Seiten()
    {
        var aufraeumer = new InboxAufraeumer(_fx.Store, Fixture.Horizont);
        (await aufraeumer.RaeumeAsync(default)).Should().BeGreaterThan(Fixture.Zeilen / 3, "der erste Durchlauf löscht die ältere Hälfte");

        // Die nächste Seite (die Quittungen, die seither den Horizont überschritten haben), die toten Zeilen stehen noch in Tabelle und Index.
        foreach (var allgemein in new[] { false, true })
            (await _fx.BloeckeAsync(aufraeumer, allgemein)).Should().BeLessThan(HoechstBloecke,
                $"nach dem Löschen, vor VACUUM, {(allgemein ? "allgemeiner" : "zugeschnittener")} Plan");

        // VACUUM gibt die Seiten frei, neue Quittungen füllen sie: alte und neue Zeiten liegen jetzt in denselben Seiten.
        await _fx.SqlAsync($"vacuum {Fixture.Tabelle}");
        await _fx.SqlAsync($"""
            insert into {Fixture.Tabelle} (command_id, stream_id, abgelehnt, zeit)
              select md5('n' || g)::uuid, md5('s' || (g % 1000))::uuid, false, now() - interval '1 hour' + g * interval '1 millisecond'
              from generate_series(1, {Fixture.Zeilen / 2}) g;
            analyze {Fixture.Tabelle};
            """);
        foreach (var allgemein in new[] { false, true })
            (await _fx.BloeckeAsync(aufraeumer, allgemein)).Should().BeLessThan(HoechstBloecke,
                $"nach VACUUM und Nachfüllen, {(allgemein ? "allgemeiner" : "zugeschnittener")} Plan");
    }

    public sealed class Fixture : IAsyncLifetime
    {
        public const string Schema = "inbox_it";
        public const string Tabelle = Schema + "." + InboxTabelle.Tabelle;
        public const int Zeilen = 1_000_000;
        public static readonly TimeSpan Horizont = TimeSpan.FromDays(30);
        /// <summary>Die nächste Seite: die Grenze, als wären seit dem ersten Durchlauf 14 Stunden vergangen (≈ 10 000 Quittungen).</summary>
        private static readonly TimeSpan Spaeter = TimeSpan.FromHours(14);

        public IDocumentStore Store { get; } = DocumentStore.For(opts =>
        {
            opts.Connection("Host=localhost;Port=5432;Database=cqrs_events;Username=postgres;Password=postgres");
            opts.DatabaseSchemaName = Schema;
            opts.Events.DatabaseSchemaName = Schema;
            opts.AutoCreateSchemaObjects = JasperFx.AutoCreate.All;
            InboxTabelle.Registriere(opts);
        });

        public async Task InitializeAsync()
        {
            await SqlAsync($"drop schema if exists {Schema} cascade");   // die Inbox ist ein Schema-Objekt, das Martens Aufräumen nicht kennt
            await Store.Advanced.Clean.CompletelyRemoveAllAsync();
            await Store.Storage.ApplyAllConfiguredChangesToDatabaseAsync();
            await InboxTabelle.SicherAsync(Store, default);
            // Eine Million Quittungen in Zeitordnung über 60 Tage bis gestern (wie im Betrieb: die Zeit wächst mit dem Einfügen); kein Autovacuum.
            await SqlAsync($"""
                alter table {Tabelle} set (autovacuum_enabled = false);
                insert into {Tabelle} (command_id, stream_id, abgelehnt, zeit)
                  select md5('c' || g)::uuid, md5('s' || (g % 1000))::uuid, false,
                         now() - interval '60 days' + g * (interval '59 days' / {Zeilen})
                  from generate_series(1, {Zeilen}) g;
                analyze {Tabelle};
                """);
        }

        public Task DisposeAsync() { Store.Dispose(); return Task.CompletedTask; }

        /// <summary>Die Blöcke der nächsten Seite des Aufräumers (echte Anweisung, zurückgerollt), mit zugeschnittenem oder allgemeinem Plan.</summary>
        public async Task<long> BloeckeAsync(InboxAufraeumer aufraeumer, bool allgemein)
        {
            await using var c = await VerbindungAsync();
            async Task Sql(string t) { await using var x = new NpgsqlCommand(t, c); await x.ExecuteNonQueryAsync(); }
            DateTimeOffset jetzt;
            await using (var n = new NpgsqlCommand("select now()", c))
            await using (var r = await n.ExecuteReaderAsync())
            {
                await r.ReadAsync();
                jetzt = r.GetFieldValue<DateTimeOffset>(0);
            }
            var werte = aufraeumer.Werte(jetzt - Horizont + Spaeter, MessSeite);
            await Sql("begin");
            await Sql($"set local plan_cache_mode = {(allgemein ? "force_generic_plan" : "force_custom_plan")}");
            await Sql($"prepare p as {InboxAufraeumer.Abfrage(Tabelle)}");
            await using var cmd = new NpgsqlCommand(
                $"explain (analyze, buffers, format json) execute p({string.Join(", ", werte.Select(Literal))})", c);
            var json = (string)(await cmd.ExecuteScalarAsync())!;
            await Sql("rollback");
            var plan = JsonDocument.Parse(json).RootElement[0].GetProperty("Plan");
            return plan.GetProperty("Shared Hit Blocks").GetInt64() + plan.GetProperty("Shared Read Blocks").GetInt64();
        }

        private static string Literal(object w) => w switch
        {
            long l => l.ToString(CultureInfo.InvariantCulture),
            int i => i.ToString(CultureInfo.InvariantCulture),
            DateTimeOffset d => $"'{d.UtcDateTime:O}'::timestamptz",
            DBNull => "null::timestamptz",
            _ => throw new NotSupportedException($"Parameter vom Typ {w.GetType().Name}"),
        };

        private static async Task<NpgsqlConnection> VerbindungAsync()
        {
            var c = new NpgsqlConnection("Host=localhost;Port=5432;Database=cqrs_events;Username=postgres;Password=postgres;Pooling=false");
            await c.OpenAsync();
            return c;
        }

        public async Task SqlAsync(string sql)
        {
            await using var c = await VerbindungAsync();
            await using var cmd = new NpgsqlCommand(sql, c) { CommandTimeout = 600 };
            await cmd.ExecuteNonQueryAsync();
        }
    }
}
