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
/// G-10, eine Uhr für alles Fachliche: den Zeitstempel eines Events setzt die Datenbank mit der Uhr, die <see cref="IDbClock"/> liest — nicht
/// der Host. Zeitlimits, Warte-Knoten und Fristen vergleichen Zeit aus dem Log mit dieser Uhr; ein Uhrenversatz des Hosts (oder zwischen
/// Knoten) und ein Zeitsprung der Sim verschieben beide gleich.
/// </summary>
[Trait("Kategorie", "Garantie")]
public class EventZeitPostgresTests
{
    private const string Verbindung = "Host=localhost;Port=5432;Database=cqrs_events;Username=postgres;Password=postgres";

    private static DocumentStore Store(string schema, string uhr) => DocumentStore.For(opts =>
    {
        opts.Events.AppendMode = JasperFx.Events.EventAppendMode.Quick;
        opts.Connection(Verbindung);
        opts.DatabaseSchemaName = schema;
        opts.Events.DatabaseSchemaName = schema;
        opts.AutoCreateSchemaObjects = JasperFx.AutoCreate.All;
        EventZeit.Registriere(opts, uhr);
    });

    private static MartenEventStore EventStore(IDocumentStore store)
        => new(store, new NoopFactory(), NullLogger<MartenEventStore>.Instance);

    [Fact]
    public async Task Den_Event_Zeitstempel_setzt_die_Uhr_der_Datenbank_nicht_der_Host()
    {
        // Die Uhr der Datenbank geht drei Stunden anders als die des Hosts — so groß wie kein echter Versatz, damit die Herkunft eindeutig ist.
        using var store = Store("garantie_eventzeit_it", "(clock_timestamp() + interval '3 hours')");
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
        const string schema = "garantie_eventzeit_sim_it";
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
        using var store = Store("garantie_eventzeit_start_it", EventZeit.Datenbankzeit);
        var es = EventStore(store);
        await es.AppendEventsAsync(Guid.NewGuid(), 0, new IEvent[] { new ImagePairInspiziert() });   // legt die Regel an
        async Task<string> Fassung()
        {
            await using var conn = new NpgsqlConnection(Verbindung);
            await conn.OpenAsync();
            await using var cmd = new NpgsqlCommand(
                $"select p.xmin::text from pg_proc p join pg_namespace n on n.oid = p.pronamespace where n.nspname = 'garantie_eventzeit_start_it' and p.proname = '{EventZeit.Regel}'", conn);
            return (string)(await cmd.ExecuteScalarAsync())!;
        }
        var vorher = await Fassung();
        await EventZeit.AnlegenAsync(store, default);   // wie ein zweiter Start
        (await Fassung()).Should().Be(vorher, "die Regel stand schon so — kein create or replace, keine Sperre auf mt_events");
    }

    private sealed class NoopFactory : IAggregateHandlerFactory
    {
        public IAggregateHandler CreateHandler(IState state) => throw new NotSupportedException("Test liest und schreibt nur Events.");
    }
}
