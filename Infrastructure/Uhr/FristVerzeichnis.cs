using Abstractions;
using Marten;
using Npgsql;
using Weasel.Postgresql;
using Weasel.Postgresql.Tables;

namespace Infrastructure.Uhr;

/// <summary>Eine fällige Frist: das Frist-Event (Stream, Version), sein Command (Diskriminator, Wire-JSON) und der Akteur der Kette.</summary>
public sealed record FaelligeFrist(Guid Stream, int Version, string Befehl, string Daten, string? Akteur);

/// <summary>Das Fälligkeits-Verzeichnis, wie die Uhr es braucht (Naht; live die Tabelle <see cref="FristVerzeichnis"/>).</summary>
public interface IFaelligkeiten
{
    /// <summary>Die fälligen Fristen (Fälligkeit ≤ Datenbank-Uhr), die frühesten zuerst, höchstens <paramref name="hoechstens"/>.</summary>
    Task<IReadOnlyList<FaelligeFrist>> FaelligeAsync(int hoechstens, CancellationToken ct);

    /// <summary>Die Frist ist entschieden (zugestellt oder abgelehnt) — sie verlässt das Verzeichnis. Folgenlos, wenn sie schon weg ist.</summary>
    Task ErledigtAsync(Guid stream, int version, CancellationToken ct);
}

/// <summary>
/// Das Fälligkeits-Verzeichnis der Uhr: eine Tabelle im Event-Schema, je Frist eine Zeile (Schlüssel = das
/// Frist-Event, Stream und Version), geschrieben als Beilage in DERSELBEN Transaktion wie das <see cref="FristGesetzt"/> — es gibt keine Frist im
/// Log ohne Zeile. Die Fälligkeit rechnet die Datenbank aus der Log-Zeit des Events plus der Dauer (eine Uhr, G-10). Abgeleitet, nicht maßgeblich:
/// die Wahrheit ist das Frist-Event; die Zeile verschwindet erst, wenn der Command entschieden ist.
/// </summary>
public sealed class FristVerzeichnis(IDocumentStore store) : IFaelligkeiten
{
    public const string Tabelle = "cqrs_fristen";

    public static void Registriere(StoreOptions options)
    {
        var t = new Table(new PostgresqlObjectName(options.Events.DatabaseSchemaName, Tabelle));
        t.AddColumn<Guid>("stream_id").AsPrimaryKey();
        t.AddColumn<int>("version").AsPrimaryKey();
        t.AddColumn("faellig", "timestamptz").NotNull();
        t.AddColumn<string>("befehl").NotNull();
        t.AddColumn<string>("daten").NotNull();
        t.AddColumn<string>("akteur");
        t.Indexes.Add(new IndexDefinition($"ix_{Tabelle}_faellig") { Columns = ["faellig"] });
        options.Storage.ExtendedSchemaObjects.Add(t);
    }

    public static string Name(IDocumentStore store) => $"{store.Options.Events.DatabaseSchemaName}.{Tabelle}";

    /// <summary>
    /// Die Fristen eines Appends bzw. Group-Commits in EINE Anweisung derselben Transaktion; Marten führt sie nach den Events aus — die Fälligkeit ist
    /// die Log-Zeit des Frist-Events plus Dauer.
    /// </summary>
    public static void Lege(IDocumentSession session, IDocumentStore store, List<Guid> streams, List<int> versionen, List<TimeSpan> nach,
        List<string> befehle, List<string> daten, List<string?> akteure)
    {
        var schema = store.Options.Events.DatabaseSchemaName;
        session.QueueSqlCommand(
            $"insert into {Name(store)} (stream_id, version, faellig, befehl, daten, akteur) "
            + "select f.s, f.v, e.timestamp + f.n, f.b, f.d, f.a "
            + "from unnest(?::uuid[], ?::int[], ?::interval[], ?::text[], ?::text[], ?::text[]) as f(s, v, n, b, d, a) "
            + $"join {schema}.mt_events e on e.stream_id = f.s and e.version = f.v on conflict do nothing",
            streams.ToArray(), versionen.ToArray(), nach.ToArray(), befehle.ToArray(), daten.ToArray(), akteure.ToArray());
    }

    /// <summary>
    /// Die fälligen Fristen gegen die Uhr des Stores, die frühesten zuerst, höchstens <c>$1</c> — liest über den Index auf <c>faellig</c>. Die Uhr steht
    /// als skalare Unterabfrage: sie ist volatil (<c>clock_timestamp()</c>), als Vergleich je Zeile wäre sie keine Index-Bedingung, und Postgres
    /// liefe den Index in Fälligkeits-Reihenfolge bis zum Ende durch, um <c>$1</c> Treffer zu finden (das ganze Verzeichnis je Takt).
    /// </summary>
    public static string FaelligeAbfrage(IDocumentStore store) =>
        $"select stream_id, version, befehl, daten, akteur from {Name(store)} where faellig <= (select {Persistence.EventZeit.Uhr(store)}) order by faellig limit $1";

    public async Task<IReadOnlyList<FaelligeFrist>> FaelligeAsync(int hoechstens, CancellationToken ct)
    {
        await Persistence.InboxTabelle.SicherAsync(store, ct);   // rohes SQL löst Martens AutoCreate nicht aus (dieselbe Ablage wie die Inbox)
        await using var conn = store.Storage.Database.CreateConnection();
        await conn.OpenAsync(ct);
        await using var cmd = new NpgsqlCommand(FaelligeAbfrage(store), conn);
        cmd.Parameters.Add(new NpgsqlParameter { Value = hoechstens });
        var aus = new List<FaelligeFrist>();
        await using var r = await cmd.ExecuteReaderAsync(ct);
        while (await r.ReadAsync(ct))
            aus.Add(new FaelligeFrist(r.GetGuid(0), r.GetInt32(1), r.GetString(2), r.GetString(3), r.IsDBNull(4) ? null : r.GetString(4)));
        return aus;
    }

    public async Task ErledigtAsync(Guid stream, int version, CancellationToken ct)
    {
        await using var conn = store.Storage.Database.CreateConnection();
        await conn.OpenAsync(ct);
        await using var cmd = new NpgsqlCommand($"delete from {Name(store)} where stream_id = $1 and version = $2", conn);
        cmd.Parameters.Add(new NpgsqlParameter { Value = stream });
        cmd.Parameters.Add(new NpgsqlParameter { Value = version });
        await cmd.ExecuteNonQueryAsync(ct);
    }
}

