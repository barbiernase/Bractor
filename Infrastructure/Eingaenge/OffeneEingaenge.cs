using Abstractions;
using Marten;
using Npgsql;
using Weasel.Postgresql;
using Weasel.Postgresql.Tables;

namespace Infrastructure.Eingaenge;

/// <summary>
/// Ein offener Eingang am Kopf einer Instanz, wie der Zusteller ihn liest: Kennung, Instanz (Spalte <c>funktion</c>), die Nachricht (Diskriminator, Wire-JSON), ihre Herkunft
/// (Stream und Version des Auslösers, Log-Zeit des Commits, Akteur, Korrelation) und wie oft er schon zugestellt wurde.
/// </summary>
public sealed record OffenerEingang(Guid Kennung, string Instanz, string Typ, string Daten, Guid Stream, int Version, DateTimeOffset Zeit, string? Akteur,
    string? Korrelation, int Versuche);

/// <summary>Die offenen Eingänge, wie der Zusteller sie braucht (Naht; live die Tabelle <see cref="OffeneEingaenge"/>).</summary>
public interface IOffeneEingaenge
{
    /// <summary>Die fälligen Eingänge (Fälligkeit ≤ Datenbank-Uhr), die frühesten zuerst, höchstens <paramref name="hoechstens"/>.</summary>
    Task<IReadOnlyList<OffenerEingang>> FaelligeAsync(int hoechstens, CancellationToken ct);

    /// <summary>Die Eingänge sind fertig — sie verlassen die Tabelle. Folgenlos für die, die schon weg sind.</summary>
    Task ErledigtAsync(IReadOnlyCollection<Guid> kennungen, CancellationToken ct);

    /// <summary>Die Eingänge sind zugestellt (angenommen oder später): wieder fällig erst nach <paramref name="nach"/>, ein Versuch mehr.</summary>
    Task VerschiebeAsync(IReadOnlyCollection<Guid> kennungen, TimeSpan nach, CancellationToken ct);

    /// <summary>
    /// Der Lauf dieser Köpfe ist gescheitert (docs/04 §4.4, Entscheidung 11): sie bleiben in der Tabelle, werden aber nie mehr fällig — mit dem Grund
    /// (der Einblick zeigt sie; wer den Fehler behoben hat, gibt sie frei).
    /// </summary>
    Task ParkeAsync(IReadOnlyCollection<(Guid Kennung, string Grund)> geparkt, CancellationToken ct);
}

/// <summary>
/// Die offenen Eingänge der Funktionen (docs/04 §4.4): eine Tabelle im Event-Schema, je Eingang eine Zeile (Schlüssel = seine abgeleitete Kennung),
/// geschrieben als Beilage in DERSELBEN Transaktion wie die Entscheidung, aus der er hervorgeht (die Folge eines Commands) — es gibt keine
/// entschiedene Folge ohne Zeile, und ein wiederholter Command legt keine zweite. Die Zeile verschwindet erst, wenn die Funktion den Eingang fertig
/// meldet; bis dahin stellt der Zusteller ihn nach jeder Frist erneut zu (mindestens einmal).
/// </summary>
public sealed class OffeneEingaenge(IDocumentStore store) : IOffeneEingaenge
{
    public const string Tabelle = "cqrs_eingaenge";

    /// <summary>So lange hat der schnelle Weg (direkt nach dem Commit an die Funktion), bevor der Zusteller einen offenen Eingang selbst bringt.</summary>
    public static readonly TimeSpan SchnellerWeg = TimeSpan.FromSeconds(5);

    public static void Registriere(StoreOptions options)
    {
        var t = new Table(new PostgresqlObjectName(options.Events.DatabaseSchemaName, Tabelle));
        t.AddColumn<Guid>("kennung").AsPrimaryKey();
        t.AddColumn<string>("funktion").NotNull();
        t.AddColumn<string>("typ").NotNull();
        t.AddColumn<string>("daten").NotNull();
        t.AddColumn<Guid>("stream_id").NotNull();
        t.AddColumn<int>("version").NotNull();
        t.AddColumn("zeit", "timestamptz").NotNull();
        t.AddColumn<string>("akteur");
        t.AddColumn<string>("korrelation");
        t.AddColumn("faellig", "timestamptz").NotNull();
        t.AddColumn<int>("versuche").NotNull().DefaultValue(0);
        t.AddColumn<string>("geparkt");
        t.Indexes.Add(new IndexDefinition($"ix_{Tabelle}_faellig") { Columns = ["faellig"] });
        options.Storage.ExtendedSchemaObjects.Add(t);
    }

    public static string Name(IDocumentStore store) => $"{store.Options.Events.DatabaseSchemaName}.{Tabelle}";

    /// <summary>
    /// Die offenen Eingänge eines Appends bzw. Group-Commits in EINE Anweisung derselben Transaktion — fällig nach <see cref="SchnellerWeg"/> (bis dahin
    /// hat der schnelle Weg nach dem Commit sie zugestellt), Zeit und Fälligkeit nach der Uhr des Stores. Eine schon bekannte Kennung (ein
    /// wiederholter Command) schreibt nichts.
    /// </summary>
    public static void Lege(IDocumentSession session, IDocumentStore store, List<Guid> kennungen, List<string> funktionen, List<string> typen,
        List<string> daten, List<Guid> streams, List<int> versionen, List<string?> akteure, List<string?> korrelationen)
    {
        var uhr = Persistence.EventZeit.Uhr(store);
        session.QueueSqlCommand(
            $"insert into {Name(store)} (kennung, funktion, typ, daten, stream_id, version, zeit, akteur, korrelation, faellig) "
            + $"select e.k, e.f, e.t, e.d, e.s, e.v, {uhr}, e.a, e.r, {uhr} + interval '{(int)SchnellerWeg.TotalSeconds} seconds' "
            + "from unnest(?::uuid[], ?::text[], ?::text[], ?::text[], ?::uuid[], ?::int[], ?::text[], ?::text[]) as e(k, f, t, d, s, v, a, r) "
            + "on conflict do nothing",
            kennungen.ToArray(), funktionen.ToArray(), typen.ToArray(), daten.ToArray(), streams.ToArray(), versionen.ToArray(), akteure.ToArray(),
            korrelationen.ToArray());
    }

    /// <summary>Die fälligen Eingänge gegen die Uhr des Stores über den Index auf <c>faellig</c> (die Uhr als skalare Unterabfrage, wie bei den Fristen).</summary>
    public static string FaelligeAbfrage(IDocumentStore store) =>
        $"select kennung, funktion, typ, daten, stream_id, version, zeit, akteur, korrelation, versuche from {Name(store)} "
        + $"where faellig <= (select {Persistence.EventZeit.Uhr(store)}) order by faellig limit $1";

    public Task<IReadOnlyList<OffenerEingang>> FaelligeAsync(int hoechstens, CancellationToken ct) => LiesAsync(FaelligeAbfrage(store), hoechstens, ct);

    /// <summary>Einblick: die offenen Eingänge (fällig oder nicht), die frühesten zuerst, höchstens <paramref name="hoechstens"/> — der Stau je Funktion.</summary>
    public Task<IReadOnlyList<OffenerEingang>> OffeneAsync(int hoechstens, CancellationToken ct) => LiesAsync(
        $"select kennung, funktion, typ, daten, stream_id, version, zeit, akteur, korrelation, versuche from {Name(store)} order by faellig limit $1", hoechstens, ct);

    private async Task<IReadOnlyList<OffenerEingang>> LiesAsync(string sql, int hoechstens, CancellationToken ct)
    {
        await Persistence.InboxTabelle.SicherAsync(store, ct);   // rohes SQL löst Martens AutoCreate nicht aus (dieselbe Ablage wie die Inbox)
        await using var conn = store.Storage.Database.CreateConnection();
        await conn.OpenAsync(ct);
        await using var cmd = new NpgsqlCommand(sql, conn);
        cmd.Parameters.Add(new NpgsqlParameter { Value = hoechstens });
        var aus = new List<OffenerEingang>();
        await using var r = await cmd.ExecuteReaderAsync(ct);
        while (await r.ReadAsync(ct))
            aus.Add(new OffenerEingang(r.GetGuid(0), r.GetString(1), r.GetString(2), r.GetString(3), r.GetGuid(4), r.GetInt32(5),
                r.GetFieldValue<DateTimeOffset>(6), r.IsDBNull(7) ? null : r.GetString(7), r.IsDBNull(8) ? null : r.GetString(8), r.GetInt32(9)));
        return aus;
    }

    public async Task ErledigtAsync(IReadOnlyCollection<Guid> kennungen, CancellationToken ct)
    {
        if (kennungen.Count == 0) return;
        await using var conn = store.Storage.Database.CreateConnection();
        await conn.OpenAsync(ct);
        await using var cmd = new NpgsqlCommand($"delete from {Name(store)} where kennung = any($1)", conn);
        cmd.Parameters.Add(new NpgsqlParameter { Value = kennungen.ToArray() });
        await cmd.ExecuteNonQueryAsync(ct);
    }

    public async Task ParkeAsync(IReadOnlyCollection<(Guid Kennung, string Grund)> geparkt, CancellationToken ct)
    {
        if (geparkt.Count == 0) return;
        await using var conn = store.Storage.Database.CreateConnection();
        await conn.OpenAsync(ct);
        await using var cmd = new NpgsqlCommand(
            $"update {Name(store)} e set faellig = 'infinity', geparkt = g.grund from unnest($1::uuid[], $2::text[]) as g(kennung, grund) where e.kennung = g.kennung",
            conn);
        cmd.Parameters.Add(new NpgsqlParameter { Value = geparkt.Select(g => g.Kennung).ToArray() });
        cmd.Parameters.Add(new NpgsqlParameter { Value = geparkt.Select(g => g.Grund).ToArray() });
        await cmd.ExecuteNonQueryAsync(ct);
    }

    public async Task VerschiebeAsync(IReadOnlyCollection<Guid> kennungen, TimeSpan nach, CancellationToken ct)
    {
        if (kennungen.Count == 0) return;
        await using var conn = store.Storage.Database.CreateConnection();
        await conn.OpenAsync(ct);
        await using var cmd = new NpgsqlCommand(
            $"update {Name(store)} set faellig = (select {Persistence.EventZeit.Uhr(store)}) + $2, versuche = versuche + 1 where kennung = any($1)", conn);
        cmd.Parameters.Add(new NpgsqlParameter { Value = kennungen.ToArray() });
        cmd.Parameters.Add(new NpgsqlParameter { Value = nach });
        await cmd.ExecuteNonQueryAsync(ct);
    }
}
