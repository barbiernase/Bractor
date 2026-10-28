using System.Globalization;
using System.Text.Json;
using Abstractions;
using Core;
using FluentAssertions;
using Infrastructure.Persistence;
using Marten;
using Marten.Linq;
using Npgsql;
using Xunit;

namespace Infrastructure.Integration.Tests.Garantien;

/// <summary>
/// Die häufigen Abfragen bleiben billig, egal wie groß das Log ist (PF-17, PF-20; docs/12 §12.5): gegen echtes Postgres mit 500 000 Events
/// und 200 000 Fristen liest jede über einen Index nur eine Handvoll Blöcke — mit dem auf die Werte zugeschnittenen Plan
/// UND mit dem allgemeinen, den Postgres einer vorbereiteten Anweisung nach einigen Aufrufen gibt (Npgsql bereitet sie seit PF-20 vor), mit
/// veralteter Statistik (die Tabelle wuchs nach dem letzten ANALYZE, wie unter Last) UND mit frischer. Gemessen werden gelesene Blöcke aus
/// <c>EXPLAIN (ANALYZE, BUFFERS)</c>, nicht Zeit. Die SQL-Texte kommen aus derselben Quelle wie im Store (<see cref="EventStoreAbfragen"/>,
/// <see cref="Infrastructure.Uhr.FristVerzeichnis.FaelligeAbfrage"/>).
/// </summary>
[Trait("Kategorie", "Garantie")]
public class AbfragePlanGarantieTests : IClassFixture<AbfragePlanGarantieTests.Fixture>
{
    /// <summary>Höchstens so viele Blöcke darf eine Abfrage lesen — ein Durchsuchen der Tabelle läse Tausende.</summary>
    private const long HoechstBloecke = 200;

    private readonly Fixture _fx;
    public AbfragePlanGarantieTests(Fixture fx) => _fx = fx;

    private string S => Fixture.Schema;

    [Theory, InlineData(false), InlineData(true)]
    public async Task Die_Abfragen_des_Event_Stores_lesen_ueber_den_Index(bool frischeStatistik)
    {
        await _fx.StatistikAsync(frischeStatistik);
        var kopf = await _fx.KopfAsync();
        var nah = kopf - 50;
        var stream = Fixture.StreamId(7);
        var faelle = new (string Name, string Sql, object[] Werte)[]
        {
            ("Horizont: steht dahinter etwas", $"select {EventStoreAbfragen.Dahinter(S)}", [nah]),
            ("Seite einer Partition", EventStoreAbfragen.Events(S, MartenEventStore.MetadatenSpalten(_fx.Store),
                EventStoreAbfragen.PartitionsBedingung(false), EventStoreAbfragen.PartitionsOrdnung), [nah, 8, 3, Array.Empty<long>(), 500]),
            ("Seite einer Partition, Typen", EventStoreAbfragen.Events(S, MartenEventStore.MetadatenSpalten(_fx.Store),
                EventStoreAbfragen.PartitionsBedingung(true), EventStoreAbfragen.PartitionsOrdnung),
                [nah, 8, 3, Array.Empty<long>(), 500, new[] { "posten_reserviert" }]),
            ("Stream ab Version", EventStoreAbfragen.Events(S, MartenEventStore.MetadatenSpalten(_fx.Store), "stream_id = $1 and version >= $2",
                "order by version"), [stream, 5L]),
            // Rückstand in /health, eine Partition noch ohne Checkpoint (Position 0): gezählt wird nur über die ersten Events dahinter.
            ("Rückstand ab Position 0", Infrastructure.Projections.KonsumRueckstand.Abfrage(S, true),
                [0L, new long[8], 8, Infrastructure.Projections.KonsumRueckstand.ZaehlGrenze, new[] { "posten_reserviert" }]),
        };
        foreach (var (name, sql, werte) in faelle)
            foreach (var allgemein in new[] { false, true })
                (await _fx.BloeckeAsync(sql, werte, allgemein)).Should().BeLessThan(HoechstBloecke,
                    $"„{name}“ mit {(allgemein ? "allgemeinem" : "zugeschnittenem")} Plan, {(frischeStatistik ? "frischer" : "veralteter")} Statistik");
    }

    [Theory, InlineData(false), InlineData(true)]
    public async Task Die_Faelligkeits_Abfrage_der_Uhr_liest_ueber_den_Index(bool frischeStatistik)
    {
        await _fx.StatistikAsync(frischeStatistik);
        foreach (var allgemein in new[] { false, true })
            (await _fx.BloeckeAsync(Infrastructure.Uhr.FristVerzeichnis.FaelligeAbfrage(_fx.Store), [100], allgemein)).Should().BeLessThan(HoechstBloecke,
                $"„Uhr: fällige Fristen“ mit {(allgemein ? "allgemeinem" : "zugeschnittenem")} Plan, {(frischeStatistik ? "frischer" : "veralteter")} Statistik");
    }

    public sealed class Fixture : IAsyncLifetime
    {
        public const string Schema = "plan_it";
        public const int Events = 500_000, Streams = 50_000, Fristen = 200_000, Faellige = 20;

        public IDocumentStore Store { get; } = DocumentStore.For(opts =>
        {
            opts.Connection("Host=localhost;Port=5432;Database=cqrs_events;Username=postgres;Password=postgres");
            opts.Events.AppendMode = JasperFx.Events.EventAppendMode.Quick;
            opts.DatabaseSchemaName = Schema;
            opts.Events.DatabaseSchemaName = Schema;
            opts.AutoCreateSchemaObjects = JasperFx.AutoCreate.All;
            opts.Events.MetadataConfig.CorrelationIdEnabled = true;
            opts.Events.MetadataConfig.CausationIdEnabled = true;
            opts.Events.MetadataConfig.HeadersEnabled = true;
            KonsumSchema.Registriere(opts);
            Domain.Infrastructure.Generated.GeneratedProjectionServices.RegistriereSchema(opts);
            InboxTabelle.Registriere(opts);
            Infrastructure.Uhr.FristVerzeichnis.Registriere(opts);
        });

        /// <summary>Dieselbe Id wie <c>md5('s' || n)::uuid</c> in Postgres (die Hex-Zeichen in Textreihenfolge).</summary>
        public static Guid StreamId(int n) => StreamIdAus("s" + n);

        /// <summary>Dieselbe Id wie <c>md5(text)::uuid</c> in Postgres.</summary>
        public static Guid StreamIdAus(string text)
            => Guid.Parse(Convert.ToHexString(System.Security.Cryptography.MD5.HashData(System.Text.Encoding.UTF8.GetBytes(text))));

        public async Task InitializeAsync()
        {
            await Store.Advanced.Clean.CompletelyRemoveAllAsync();
            // Das Fälligkeits-Verzeichnis ist ein zusätzliches Schema-Objekt — das räumt Marten nicht mit ab (sonst stünden die Zeilen des letzten Laufs noch da).
            await SqlAsync($"drop table if exists {Schema}.{Infrastructure.Uhr.FristVerzeichnis.Tabelle}");
            await Store.Storage.ApplyAllConfiguredChangesToDatabaseAsync();
            await Store.Storage.Database.EnsureStorageExistsAsync(typeof(JasperFx.Events.IEvent));
            var fristen = $"{Schema}.{Infrastructure.Uhr.FristVerzeichnis.Tabelle}";
            // Veraltete Statistik wie unter Last: ANALYZE, solange die Tabellen fast leer sind, danach füllen; kein Autovacuum dazwischen.
            await SqlAsync($"""
                alter table {Schema}.mt_events set (autovacuum_enabled = false);
                alter table {fristen} set (autovacuum_enabled = false);
                insert into {Schema}.mt_streams (id, type, version)
                  select md5('s' || g)::uuid, 'posten', {Events / Streams} from generate_series(0, {Streams - 1}) g;
                insert into {Schema}.mt_events (seq_id, id, stream_id, version, data, type, timestamp, is_archived)
                  select nextval('{Schema}.mt_events_sequence'), md5('e' || g)::uuid, md5('s' || (g % {Streams}))::uuid, g / {Streams} + 1, jsonb_build_object(),
                         case when g % 3 = 0 then 'posten_reserviert' else 'posten_eroeffnet' end, now(), false
                  from generate_series(0, 999) g;
                analyze {Schema}.mt_events; analyze {fristen};
                insert into {Schema}.mt_events (seq_id, id, stream_id, version, data, type, timestamp, is_archived)
                  select nextval('{Schema}.mt_events_sequence'), md5('e' || g)::uuid, md5('s' || (g % {Streams}))::uuid, g / {Streams} + 1, jsonb_build_object(),
                         case when g % 3 = 0 then 'posten_reserviert' else 'posten_eroeffnet' end, now(), false
                  from generate_series(1000, {Events - 1}) g;
                insert into {fristen} (stream_id, version, faellig, befehl, daten)
                  select md5('f' || g)::uuid, 1,
                         case when g < {Faellige} then now() - g * interval '1 second' else now() + (1 + g % 100) * interval '1 hour' end, 'X', '-'
                  from generate_series(0, {Fristen - 1}) g;
                """);
        }

        public Task DisposeAsync() { Store.Dispose(); return Task.CompletedTask; }

        private bool _analysiert;

        /// <summary>Frische Statistik: einmal ANALYZE über die gefüllten Tabellen; veraltete: der Stand von vor dem Füllen.</summary>
        public async Task StatistikAsync(bool frisch)
        {
            if (frisch && !_analysiert)
            {
                await SqlAsync($"analyze {Schema}.mt_events; analyze {Schema}.{Infrastructure.Uhr.FristVerzeichnis.Tabelle};");
                _analysiert = true;
            }
        }

        public async Task<long> KopfAsync()
        {
            await using var c = await VerbindungAsync();
            await using var cmd = new NpgsqlCommand($"select max(seq_id) from {Schema}.mt_events", c);
            return (long)(await cmd.ExecuteScalarAsync())!;
        }

        /// <summary>
        /// Die von einer Abfrage gelesenen Blöcke (Shared Hit + Read der Wurzel aus <c>EXPLAIN (ANALYZE, BUFFERS)</c>), als vorbereitete Anweisung
        /// mit zugeschnittenem oder erzwungen allgemeinem Plan — so, wie sie nach PF-20 im Betrieb läuft.
        /// </summary>
        public async Task<long> BloeckeAsync(string sql, object[] werte, bool allgemein)
        {
            await using var c = await VerbindungAsync();
            async Task Sql(string t) { await using var x = new NpgsqlCommand(t, c); await x.ExecuteNonQueryAsync(); }
            await Sql($"set plan_cache_mode = {(allgemein ? "force_generic_plan" : "force_custom_plan")}");
            await Sql($"prepare p as {sql}");
            await using var cmd = new NpgsqlCommand(
                $"explain (analyze, buffers, format json) execute p({string.Join(", ", werte.Select(Literal))})", c);
            var json = (string)(await cmd.ExecuteScalarAsync())!;
            await Sql("deallocate p");
            var plan = JsonDocument.Parse(json).RootElement[0].GetProperty("Plan");
            return plan.GetProperty("Shared Hit Blocks").GetInt64() + plan.GetProperty("Shared Read Blocks").GetInt64();
        }

        private static string Literal(object w) => w switch
        {
            long l => l.ToString(CultureInfo.InvariantCulture),
            int i => i.ToString(CultureInfo.InvariantCulture),
            Guid g => $"'{g}'::uuid",
            DateTimeOffset d => $"'{d.UtcDateTime:O}'::timestamptz",
            DateTime d => $"'{d:O}'::timestamptz",
            string s => $"'{s.Replace("'", "''")}'",
            long[] a => $"array[{string.Join(",", a)}]::bigint[]",
            Guid[] a => $"array[{string.Join(",", a.Select(x => $"'{x}'"))}]::uuid[]",
            string[] a => $"array[{string.Join(",", a.Select(x => $"'{x}'"))}]::text[]",
            _ => throw new NotSupportedException($"Parameter vom Typ {w.GetType().Name}"),
        };

        private async Task<NpgsqlConnection> VerbindungAsync()
        {
            var c = new NpgsqlConnection("Host=localhost;Port=5432;Database=cqrs_events;Username=postgres;Password=postgres;Pooling=false");
            await c.OpenAsync();
            return c;
        }

        private async Task SqlAsync(string sql)
        {
            await using var c = await VerbindungAsync();
            await using var cmd = new NpgsqlCommand(sql, c) { CommandTimeout = 300 };
            await cmd.ExecuteNonQueryAsync();
        }
    }
}
