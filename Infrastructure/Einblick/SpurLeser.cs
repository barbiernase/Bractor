using System.Text.Json;
using Abstractions;
using Infrastructure.Umgebung;
using Npgsql;

namespace Infrastructure.Einblick;

/// <summary>
/// Ein Log-Eintrag mit seiner Spur (globale Sequenz, Stream-Position, Header) — die Zeile der Einblick-API. <c>Typ</c> ist der Marten-Name
/// (snake_case), <c>Name</c> der Typname im Code (die Karte <c>rec:Name</c>), <c>Ueber</c> der Absender des Commands bzw. Auftrags.
/// </summary>
public sealed record SpurEintrag(
    long Seq, Guid Stream, long Version, string Typ, string Name, DateTimeOffset Zeit, string? Korrelation, string? Kausal,
    string? Akteur, string? Ursache, string? Wirker, string? Ueber, string? Stand, string? Aggregat, bool Intern, JsonElement Daten);

/// <summary>
/// Ein Vorgang (alle Einträge einer <c>CorrelationId</c>) mit Zustand: <c>laeuft</c> · <c>ruht</c>. Eine Ablehnung steht nicht im Log (Plan
/// Pub/Sub-Neustrukturierung Phase 5): sie kommt als verlierbarer Spur-Hinweis; ist sie für den Ablauf wichtig, ist sie ein Fakt im Log.
/// </summary>
public sealed record Vorgang(
    string Korrelation, long ErsteSeq, long LetzteSeq, int Eintraege, DateTimeOffset Beginn, DateTimeOffset Zuletzt,
    string Zustand, string? WurzelTyp, string? WurzelWirker, string? Akteur);

/// <summary>
/// Liest die Spur aus dem Log (<c>docs/konzept-werkbank.md</c> §7, Werkbank P4.4/P4.6) — nur lesend, direkt über die globale Sequenz
/// (<c>mt_events.seq_id</c>) samt Headern. Die „Spur-Projektion“ ist bewusst keine zweite gespeicherte Wahrheit: Knoten sind die Log-Einträge,
/// Kanten ihre <c>ursache</c>; ein Vorgang ist die Menge einer <c>CorrelationId</c>, sein Zustand folgt aus der letzten Aktivität. Ob ein Vorgang FACHLICH fertig ist, sagt sein Aggregat (ein Fakt), nicht der Einblick. So gilt dasselbe in Sim und Prod; die
/// Werkbank holt ab ihrem Cursor nach (§7.2).
/// </summary>
public sealed class SpurLeser(string verbindung, string schema)
{
    public SpurLeser(UmgebungKonfig umgebung) : this(UmgebungLader.Verbindung(umgebung.EventStore), umgebung.EventStore.Schema) { }

    /// <summary>Wie lange ein Vorgang ohne neuen Eintrag als „läuft“ gilt — danach ruht er (fertig, oder er wartet auf eine Funktion, Hand, Frist).</summary>
    public static readonly TimeSpan Ruhe = TimeSpan.FromSeconds(5);

    // Einträge des Rahmens, die kein Baustein der Domäne geschrieben hat (Frist gesetzt) — die Werkbank blendet sie ein/aus.
    private static readonly string[] InternTypen = [typeof(FristGesetzt).FullName!];

    private string Schema => schema;

    /// <summary>Die Korrelation eines Eintrags: sein <c>CorrelationId</c>-Metadatum.</summary>
    private const string Korrelation = "nullif(correlation_id, '')";

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

    /// <summary>Alle Einträge eines Vorgangs (einer <c>CorrelationId</c>), aufsteigend.</summary>
    public async Task<IReadOnlyList<SpurEintrag>> VorgangAsync(string korrelation, CancellationToken ct)
        => await LiesAsync($"{Korrelation} = @k order by seq_id limit 5000", c => c.Parameters.AddWithValue("k", korrelation), ct);

    /// <param name="wo">Bedingung samt <c>order by</c>/<c>limit</c>; ein <c>or</c> auf oberster Ebene klammert der Aufrufer (davor steht ein
    /// <c>and</c>: Tombstones eines gescheiterten Appends sind keine Log-Einträge, G-7).</param>
    private async Task<IReadOnlyList<SpurEintrag>> LiesAsync(string wo, Action<NpgsqlCommand> parameter, CancellationToken ct)
    {
        await using var conn = await OeffneAsync(ct);
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = $"""
            select seq_id, stream_id, version, type, timestamp, correlation_id, causation_id,
                   headers->>'akteur', headers->>'{Abstractions.Spur.UrsacheHeader}', headers->>'{Abstractions.Spur.WirkerHeader}',
                   headers->>'{Abstractions.Spur.StandHeader}', headers->>'aggregate_type', mt_dotnet_type, data::text,
                   headers->>'{Abstractions.Spur.UeberHeader}'
            from "{Schema}".mt_events where type <> 'tombstone' and {wo}
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
                aus.Add(new SpurEintrag(r.GetInt64(0), r.GetGuid(1), r.GetInt64(2), r.GetString(3), KurzName(dotnet, r.GetString(3)), r.GetFieldValue<DateTimeOffset>(4),
                    S(5) is { Length: > 0 } k ? k : null, S(6), S(7), S(8), S(9), S(14), S(10), S(11),
                    InternTypen.Any(t => dotnet.StartsWith(t + ",", StringComparison.Ordinal)),
                    JsonDocument.Parse(r.GetString(13)).RootElement.Clone()));
            }
        }
        catch (PostgresException e) when (e.SqlState == "42P01") { }
        return aus;
    }

    /// <summary>Der Typname im Code aus dem Marten-Typ (<c>Ns.Typ, Assembly</c>; verschachtelt <c>Ns.Aussen+Typ</c>) — ohne: der Marten-Name.</summary>
    public static string KurzName(string dotnet, string ersatz)
    {
        var t = dotnet.Split(',')[0].Trim();
        if (t.Length == 0) return ersatz;
        var i = Math.Max(t.LastIndexOf('.'), t.LastIndexOf('+'));
        return i >= 0 ? t[(i + 1)..] : t;
    }

    /// <summary>
    /// Die jüngsten <paramref name="n"/> Vorgänge. Ein Vorgang ist die Menge einer <c>CorrelationId</c>; seine Wurzel (Typ, Wirker, Akteur) ist sein
    /// erster Eintrag. Zustand: er läuft, solange in den letzten <see cref="Ruhe"/> etwas geschah, und ruht danach.
    /// </summary>
    public async Task<IReadOnlyList<Vorgang>> VorgaengeAsync(int n, CancellationToken ct)
    {
        await using var conn = await OeffneAsync(ct);
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = $"""
            with g as (
              select {Korrelation} as k, min(seq_id) as erste, max(seq_id) as letzte, count(*)::int as n,
                     min(timestamp) as beginn, max(timestamp) as zuletzt
              from "{Schema}".mt_events
              where {Korrelation} is not null
              group by 1)
            select g.k, g.erste, g.letzte, g.n, g.beginn, g.zuletzt,
                   r.type, r.headers->>'{Abstractions.Spur.WirkerHeader}', r.headers->>'akteur', now(), r.mt_dotnet_type
            from g join "{Schema}".mt_events r on r.seq_id = g.erste
            order by g.letzte desc
            limit @n
            """;
        cmd.Parameters.AddWithValue("n", Math.Clamp(n, 1, 2000));
        var aus = new List<Vorgang>();
        try
        {
            await using var r = await cmd.ExecuteReaderAsync(ct);
            while (await r.ReadAsync(ct))
            {
                string? S(int i) => r.IsDBNull(i) ? null : r.GetString(i);
                var zuletzt = r.GetFieldValue<DateTimeOffset>(5);
                var zustand = r.GetFieldValue<DateTimeOffset>(9) - zuletzt < Ruhe ? "laeuft" : "ruht";
                aus.Add(new Vorgang(r.GetString(0), r.GetInt64(1), r.GetInt64(2), r.GetInt32(3), r.GetFieldValue<DateTimeOffset>(4), zuletzt,
                    zustand, S(6) is { } typ ? KurzName(S(10) ?? "", typ) : null, S(7), S(8)));
            }
        }
        catch (PostgresException e) when (e.SqlState == "42P01") { }
        return aus;
    }
}
