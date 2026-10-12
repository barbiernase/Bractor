using System.Text.Json;
using Abstractions;
using Infrastructure.Umgebung;
using Npgsql;

namespace Infrastructure.Einblick;

/// <summary>Ein Log-Eintrag mit seiner Spur (globale Sequenz, Stream-Position, Header) — die Zeile der Einblick-API.</summary>
public sealed record SpurEintrag(
    long Seq, Guid Stream, long Version, string Typ, DateTimeOffset Zeit, string? Korrelation, string? Kausal,
    string? Akteur, string? Ursache, string? Wirker, string? Stand, string? Aggregat, bool Intern, JsonElement Daten);

/// <summary>Ein Vorgang (alle Einträge einer <c>CorrelationId</c>) mit Zustand: <c>laeuft</c> · <c>wartet</c> · <c>fertig</c> · <c>gescheitert</c>.</summary>
public sealed record Vorgang(
    string Korrelation, long ErsteSeq, long LetzteSeq, int Eintraege, DateTimeOffset Beginn, DateTimeOffset Zuletzt,
    string Zustand, string? Prozess, string? WurzelTyp, string? WurzelWirker, string? Akteur, string? Grund);

/// <summary>
/// Liest die Spur aus dem Log (<c>docs/konzept-werkbank.md</c> §7, Werkbank P4.4/P4.6) — nur lesend, direkt über die globale Sequenz
/// (<c>mt_events.seq_id</c>) samt Headern. Die „Spur-Projektion“ ist bewusst keine zweite gespeicherte Wahrheit: Knoten sind die Log-Einträge,
/// Kanten ihre <c>ursache</c>; ein Vorgang ist die Menge einer <c>CorrelationId</c>, sein Zustand folgt aus den Manager-Einträgen
/// (<c>ProzessBeendet</c>) und der letzten Aktivität. So gilt dasselbe in Sim und Prod; die Werkbank holt ab ihrem Cursor nach (§7.2).
/// </summary>
public sealed class SpurLeser(string verbindung, string schema)
{
    public SpurLeser(UmgebungKonfig umgebung) : this(UmgebungLader.Verbindung(umgebung.EventStore), umgebung.EventStore.Schema) { }

    /// <summary>Wie lange ein offener Prozess ohne neuen Eintrag als „läuft“ gilt — danach „wartet“ er (auf Funktion, Hand, Frist).</summary>
    public static readonly TimeSpan Ruhe = TimeSpan.FromSeconds(5);

    // Framework-Einträge, die kein Baustein der Domäne geschrieben hat (Inbox-Marken, Manager-Log) — die Werkbank blendet sie ein/aus.
    private static readonly string[] InternTypen =
    [
        typeof(Aggregate.KommandoVerarbeitet).FullName!, typeof(Aggregate.KommandoAbgelehnt).FullName!,
        typeof(Prozess.ProzessGestartet).FullName!, typeof(Prozess.ProzessBeendet).FullName!,
        typeof(Prozess.SchrittGescheitert).FullName!, typeof(Prozess.SchrittUmgeleitet).FullName!,
    ];

    private string Schema => schema;

    /// <summary>
    /// Die Korrelation eines Eintrags: sein <c>CorrelationId</c>-Metadatum; der Prozess-Manager schreibt in den Stream seiner Korrelation
    /// ohne dieses Metadatum (sein Stream IST die Korrelation).
    /// </summary>
    private const string Korrelation = "coalesce(nullif(correlation_id, ''), case when headers->>'aggregate_type' = 'ProzessManager' then stream_id::text end)";

    private async Task<NpgsqlConnection> OeffneAsync(CancellationToken ct)
    {
        var conn = new NpgsqlConnection(verbindung);
        await conn.OpenAsync(ct);
        return conn;
    }

    /// <summary>Die höchste globale Sequenz (0: leeres Log).</summary>
    public async Task<long> BisAsync(CancellationToken ct)
    {
        await using var conn = await OeffneAsync(ct);
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = $"select coalesce(max(seq_id), 0) from \"{Schema}\".mt_events";
        try { return Convert.ToInt64(await cmd.ExecuteScalarAsync(ct)); }
        catch (PostgresException e) when (e.SqlState == "42P01") { return 0; }   // noch kein Event-Schema
    }

    /// <summary>Bis zu <paramref name="n"/> Einträge mit <c>seq &gt; ab</c>, aufsteigend — inklusive Header (P4.4 <c>LeseAbSeqAsync</c>).</summary>
    public async Task<IReadOnlyList<SpurEintrag>> LeseAbSeqAsync(long ab, int n, CancellationToken ct)
        => await LiesAsync("seq_id > @ab order by seq_id limit @n", c => { c.Parameters.AddWithValue("ab", ab); c.Parameters.AddWithValue("n", Math.Clamp(n, 1, 5000)); }, ct);

    /// <summary>Alle Einträge eines Vorgangs (einer <c>CorrelationId</c>) samt Auslöser seines Prozesses (die Wurzel), aufsteigend.</summary>
    public async Task<IReadOnlyList<SpurEintrag>> VorgangAsync(string korrelation, CancellationToken ct)
        => await LiesAsync($"""
            {Korrelation} = @k or seq_id in (
              select a.seq_id from "{Schema}".mt_events g join "{Schema}".mt_events a
                on a.stream_id::text = g.data->>'AuslöserStream' and a.version::text = g.data->>'AuslöserVersion'
              where g.stream_id::text = @k and starts_with(g.mt_dotnet_type, @g))
            order by seq_id limit 5000
            """, c => { c.Parameters.AddWithValue("k", korrelation); c.Parameters.AddWithValue("g", typeof(Prozess.ProzessGestartet).FullName + ","); }, ct);

    private async Task<IReadOnlyList<SpurEintrag>> LiesAsync(string wo, Action<NpgsqlCommand> parameter, CancellationToken ct)
    {
        await using var conn = await OeffneAsync(ct);
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = $"""
            select seq_id, stream_id, version, type, timestamp, correlation_id, causation_id,
                   headers->>'akteur', headers->>'{Abstractions.Spur.UrsacheHeader}', headers->>'{Abstractions.Spur.WirkerHeader}',
                   headers->>'{Abstractions.Spur.StandHeader}', headers->>'aggregate_type', mt_dotnet_type, data::text
            from "{Schema}".mt_events where {wo}
            """;
        parameter(cmd);
        var aus = new List<SpurEintrag>();
        try
        {
            await using var r = await cmd.ExecuteReaderAsync(ct);
            while (await r.ReadAsync(ct))
            {
                string? S(int i) => r.IsDBNull(i) ? null : r.GetString(i);
                var dotnet = S(12) ?? "";
                aus.Add(new SpurEintrag(r.GetInt64(0), r.GetGuid(1), r.GetInt64(2), r.GetString(3), r.GetFieldValue<DateTimeOffset>(4),
                    S(5) is { Length: > 0 } k ? k : S(11) == "ProzessManager" ? r.GetGuid(1).ToString() : null, S(6), S(7), S(8), S(9), S(10), S(11),
                    InternTypen.Any(t => dotnet.StartsWith(t + ",", StringComparison.Ordinal)),
                    JsonDocument.Parse(r.GetString(13)).RootElement.Clone()));
            }
        }
        catch (PostgresException e) when (e.SqlState == "42P01") { }
        return aus;
    }

    /// <summary>
    /// Die jüngsten <paramref name="n"/> Vorgänge. Ein Vorgang ist die Menge einer <c>CorrelationId</c>; startet ein Prozess (Fluss) aus einer
    /// Quell-Nachricht oder einem Event, ist dessen Auslöser die WURZEL des Vorgangs (Typ, Wirker, Akteur kommen von dort), und die Korrelation
    /// der Quell-Nachricht selbst erscheint nicht noch einmal. Zustand: ein Manager-<c>ProzessBeendet</c> entscheidet fertig/gescheitert; ein
    /// offener Prozess läuft, solange in den letzten <see cref="Ruhe"/> etwas geschah, sonst wartet er; ohne Prozess (ein Command von außen) fertig.
    /// </summary>
    public async Task<IReadOnlyList<Vorgang>> VorgaengeAsync(int n, CancellationToken ct)
    {
        await using var conn = await OeffneAsync(ct);
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = $"""
            with g as (
              select {Korrelation} as k, min(seq_id) as erste, max(seq_id) as letzte, count(*)::int as n,
                     min(timestamp) as beginn, max(timestamp) as zuletzt,
                     bool_or(starts_with(mt_dotnet_type, @b) and (data->>'Erfolg')::boolean) as fertig,
                     bool_or(starts_with(mt_dotnet_type, @b) and not (data->>'Erfolg')::boolean) as gescheitert,
                     max(case when starts_with(mt_dotnet_type, @g) then data->>'ProzessName' end) as prozess,
                     max(case when starts_with(mt_dotnet_type, @g) then data->>'AuslöserStream' end) as ausl_stream,
                     max(case when starts_with(mt_dotnet_type, @g) then data->>'AuslöserVersion' end) as ausl_version,
                     max(case when starts_with(mt_dotnet_type, @b) then data->>'Grund' end) as grund
              from "{Schema}".mt_events
              where {Korrelation} is not null
              group by 1),
            w as (
              select g.*, coalesce(a.seq_id, g.erste) as wurzel
              from g left join "{Schema}".mt_events a on a.stream_id::text = g.ausl_stream and a.version::text = g.ausl_version)
            select w.k, least(w.erste, w.wurzel), w.letzte, w.n, w.beginn, w.zuletzt, w.fertig, w.gescheitert, w.prozess,
                   r.type, r.headers->>'{Abstractions.Spur.WirkerHeader}', r.headers->>'akteur', w.grund, now()
            from w join "{Schema}".mt_events r on r.seq_id = w.wurzel
            where not exists (select 1 from g p where p.ausl_stream = w.k)
            order by w.letzte desc
            limit @n
            """;
        cmd.Parameters.AddWithValue("b", typeof(Prozess.ProzessBeendet).FullName + ",");
        cmd.Parameters.AddWithValue("g", typeof(Prozess.ProzessGestartet).FullName + ",");
        cmd.Parameters.AddWithValue("n", Math.Clamp(n, 1, 2000));
        var aus = new List<Vorgang>();
        try
        {
            await using var r = await cmd.ExecuteReaderAsync(ct);
            while (await r.ReadAsync(ct))
            {
                string? S(int i) => r.IsDBNull(i) ? null : r.GetString(i);
                bool B(int i) => !r.IsDBNull(i) && r.GetBoolean(i);
                var zuletzt = r.GetFieldValue<DateTimeOffset>(5);
                var prozess = S(8);
                var zustand = B(6) ? "fertig" : B(7) ? "gescheitert" : prozess == null ? "fertig"
                    : r.GetFieldValue<DateTimeOffset>(13) - zuletzt < Ruhe ? "laeuft" : "wartet";
                aus.Add(new Vorgang(r.GetString(0), r.GetInt64(1), r.GetInt64(2), r.GetInt32(3), r.GetFieldValue<DateTimeOffset>(4), zuletzt,
                    zustand, prozess, S(9), S(10), S(11), S(12) is { Length: > 0 } g ? g : null));
            }
        }
        catch (PostgresException e) when (e.SqlState == "42P01") { }
        return aus;
    }
}
