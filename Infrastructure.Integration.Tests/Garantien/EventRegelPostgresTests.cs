using Abstractions;
using Domain.ImagePair;
using FluentAssertions;
using Infrastructure.Persistence;
using Infrastructure.Umgebung;
using Marten;
using Microsoft.Extensions.Logging.Abstractions;
using Npgsql;

namespace Infrastructure.Integration.Tests.Garantien;

/// <summary>
/// Die Regel jeder Event-Zeile (<see cref="EventRegel"/>): G-17, die Voraussetzung des Horizonts der Vollständigkeit — eine Sequenz, die eine
/// Transaktion vor ihrer Nummer zog, behält keine Event-Zeile; G-10, eine Uhr für alles Fachliche — den Zeitstempel setzt die Datenbank mit der
/// Uhr, die <see cref="IDbClock"/> liest, nicht der Host.
/// </summary>
[Trait("Kategorie", "Garantie")]
public class EventRegelPostgresTests
{
    private const string Verbindung = "Host=localhost;Port=5432;Database=cqrs_events;Username=postgres;Password=postgres";

    private static DocumentStore Store(string schema, string uhr) => DocumentStore.For(opts =>
    {
        opts.Events.AppendMode = JasperFx.Events.EventAppendMode.Quick;
        opts.Connection(Verbindung);
        opts.DatabaseSchemaName = schema;
        opts.Events.DatabaseSchemaName = schema;
        opts.AutoCreateSchemaObjects = JasperFx.AutoCreate.All;
        EventRegel.Registriere(opts, uhr);
    });

    private static MartenEventStore EventStore(IDocumentStore store)
        => new(store, new NoopFactory(), NullLogger<MartenEventStore>.Instance);

    /// <summary>
    /// G-17: Eine Transaktion zieht eine Sequenz, bevor sie eine Nummer hat (unter Last gemessen bei Martens Insert, mitunter Dutzende
    /// Millisekunden lang); ein Leser holt dazwischen eine Seite — seine Probe sieht den Zählerstand, aber keine laufende Transaktion, und gibt
    /// die Sequenz frei. Schreibt die Transaktion danach ihre Zeile mit genau dieser Sequenz, stünde sie hinter dem Horizont des Lesers, und er
    /// rückte für immer über sie. Die Regel zieht die Sequenz neu, nachdem die Nummer vergeben ist: die Zeile liegt jenseits des Horizonts.
    /// </summary>
    [Fact]
    public async Task Eine_vor_der_Transaktionsnummer_gezogene_Sequenz_ueberholt_der_Horizont_nicht()
    {
        const string schema = "garantie_eventregel_horizont_it";
        using var store = Store(schema, EventRegel.Datenbankzeit);
        var es = EventStore(store);
        await es.AppendEventsAsync(Guid.NewGuid(), 0, new IEvent[] { new ImagePairInspiziert() });   // legt Tabellen und Regel an

        await using var schreiber = new NpgsqlConnection(Verbindung);
        await schreiber.OpenAsync();
        await using var tx = await schreiber.BeginTransactionAsync();
        long gezogen;
        await using (var cmd = new NpgsqlCommand($"select nextval('{schema}.mt_events_sequence')", schreiber, tx))
            gezogen = (long)(await cmd.ExecuteScalarAsync())!;   // noch ohne Transaktionsnummer: ein select vergibt keine

        var seite = await es.ReadPartitionAsync(0, 0, 1, null, Array.Empty<long>(), 100, default);
        seite.Gesetzt.Should().BeGreaterThanOrEqualTo(gezogen, "die Probe sieht den Zählerstand, aber keine laufende Transaktion — so entsteht die Lücke");

        var stream = Guid.NewGuid();
        long gespeichert;
        await using (var cmd = new NpgsqlCommand($"""
            insert into {schema}.mt_streams (id, type, version, tenant_id) values (@stream, 'test', 1, '*DEFAULT*');
            insert into {schema}.mt_events (seq_id, id, stream_id, version, data, type, tenant_id, mt_dotnet_type, is_archived)
                select @seq, gen_random_uuid(), @stream, 1, data, type, tenant_id, mt_dotnet_type, false from {schema}.mt_events limit 1
                returning seq_id;
            """, schreiber, tx))
        {
            cmd.Parameters.AddWithValue("stream", stream);
            cmd.Parameters.AddWithValue("seq", gezogen);
            gespeichert = (long)(await cmd.ExecuteScalarAsync())!;
        }
        await tx.CommitAsync();

        gespeichert.Should().BeGreaterThan(seite.Gesetzt, "die Zeile darf nicht hinter dem Horizont liegen, den ein Leser schon freigegeben hat");
        (await es.ReadPartitionAsync(seite.Gesetzt, 0, 1, null, Array.Empty<long>(), 100, default)).Events
            .Should().Contain(e => e.Event.AggregateId == stream, "der Leser findet sie ab seinem Horizont");
    }

    [Fact]
    public async Task Den_Event_Zeitstempel_setzt_die_Uhr_der_Datenbank_nicht_der_Host()
    {
        // Die Uhr der Datenbank geht drei Stunden anders als die des Hosts — so groß wie kein echter Versatz, damit die Herkunft eindeutig ist.
        using var store = Store("garantie_eventregel_it", "(clock_timestamp() + interval '3 hours')");
        var es = EventStore(store);
        var stream = Guid.NewGuid();
        await es.AppendEventsAsync(stream, 0, new IEvent[] { new ImagePairInspiziert() });

        var zeit = (await es.ReadStreamAsync(stream, 0, default)).Single().CreatedAtUtc;
        var uhr = await new MartenDbClock(store).JetztAsync();

        (uhr - zeit).Duration().Should().BeLessThan(TimeSpan.FromSeconds(5), "Zeitstempel und IDbClock sind dieselbe Uhr");
        (zeit - DateTimeOffset.UtcNow).Should().BeGreaterThan(TimeSpan.FromHours(2.9), "die Uhr des Hosts hat den Zeitstempel nicht gesetzt");
    }

    [Fact]
    public async Task Ein_Zeitsprung_der_Sim_verschiebt_Event_Zeitstempel_und_Uhr_gemeinsam()
    {
        // Die Sim-Uhr: Zeit der Datenbank plus Versatz aus <schema>.uhr (docs/konzept-werkbank.md §4.4). Nach einem Sprung um zwei Stunden
        // muss ein neues Event in der gesprungenen Zeit stehen — sonst wirkt jede neue Aktivierung sofort zwei Stunden alt, und jedes Zeitlimit
        // unter zwei Stunden liefe beim ersten Hinsehen ab.
        const string schema = "garantie_eventregel_sim_it";
        var u = new UmgebungKonfig
        {
            Umgebung = { Art = UmgebungsArt.Sim },
            EventStore = { Schema = schema, Datenbank = "cqrs_events" },
        };
        await using (var conn = new NpgsqlConnection(Verbindung))
        {
            await conn.OpenAsync();
            await using var cmd = new NpgsqlCommand($"""
                create schema if not exists "{schema}";
                create table if not exists "{schema}".uhr (id int primary key default 1 check (id = 1), versatz interval not null default interval '0');
                insert into "{schema}".uhr (id, versatz) values (1, interval '2 hours') on conflict (id) do update set versatz = excluded.versatz;
                """, conn);
            await cmd.ExecuteNonQueryAsync();
        }
        using var store = Store(schema, UmgebungsDbClock.Ausdruck(u));
        var es = EventStore(store);
        var stream = Guid.NewGuid();
        await es.AppendEventsAsync(stream, 0, new IEvent[] { new ImagePairInspiziert() });

        var zeit = (await es.ReadStreamAsync(stream, 0, default)).Single().CreatedAtUtc;
        var uhr = await new UmgebungsDbClock(u).JetztAsync();

        (uhr - zeit).Duration().Should().BeLessThan(TimeSpan.FromSeconds(5), "Event und Uhr springen gemeinsam");
        (zeit - DateTimeOffset.UtcNow).Should().BeGreaterThan(TimeSpan.FromHours(1.9), "der Versatz steckt im Zeitstempel");
    }

    [Fact]
    public async Task Ein_unveraenderter_Stand_fasst_die_Regel_beim_Start_nicht_an()
    {
        // Ein create or replace trigger braucht eine Sperre auf mt_events, die mit laufenden Inserts kollidiert — beim Neustart schreibt der alte
        // Host oft noch. Steht die Regel schon so, wie sie soll, darf ein Start sie nicht neu schreiben (die Katalog-Zeile bleibt dieselbe).
        using var store = Store("garantie_eventregel_start_it", EventRegel.Datenbankzeit);
        var es = EventStore(store);
        await es.AppendEventsAsync(Guid.NewGuid(), 0, new IEvent[] { new ImagePairInspiziert() });   // legt die Regel an
        async Task<string> Fassung()
        {
            await using var conn = new NpgsqlConnection(Verbindung);
            await conn.OpenAsync();
            await using var cmd = new NpgsqlCommand(
                $"select p.xmin::text from pg_proc p join pg_namespace n on n.oid = p.pronamespace where n.nspname = 'garantie_eventregel_start_it' and p.proname = '{EventRegel.Regel}'", conn);
            return (string)(await cmd.ExecuteScalarAsync())!;
        }
        var vorher = await Fassung();
        await EventRegel.AnlegenAsync(store, default);   // wie ein zweiter Start
        (await Fassung()).Should().Be(vorher, "die Regel stand schon so — kein create or replace, keine Sperre auf mt_events");
    }

    private sealed class NoopFactory : IAggregateHandlerFactory
    {
        public IAggregateHandler CreateHandler(IState state) => throw new NotSupportedException("Test liest und schreibt nur Events.");
    }
}
