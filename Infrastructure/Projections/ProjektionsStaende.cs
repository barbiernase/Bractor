using Abstractions;
using Marten;
using Microsoft.Extensions.Logging;
using Npgsql;

namespace Infrastructure.Projections;

/// <summary>
/// Der Stand einer Projektion je Stream (Plan Pub/Sub-Neustrukturierung §2.7): wie weit sie den Stream verarbeitet hat. Zwei Quellen:
/// <list type="number">
///   <item>der Guard je (Projektion, Stream), den der Batch-Leser in DERSELBEN Transaktion schreibt wie ihre Wirkungen
///     (<see cref="ProjectionCheckpoint"/>) — die Version des letzten Events, das sie angewandt hat;</item>
///   <item>das Log: folgen danach nur Events, die sie nicht hört, gilt ihr Inhalt bis zum nächsten gehörten Event (bzw. bis zum Kopf des Streams)
///     — Stand = nächstes gehörtes Event − 1, ohne ein solches der Kopf.</item>
/// </list>
/// So ist der Stand nie vor den Daten sichtbar, sinkt nie, beginnt mit einem Neuaufbau wieder bei null — und bleibt nicht hinter Events
/// stehen, die die Projektion gar nicht hört (eine Frist, eine Ablehnung): als Version eines Commands taugt er (OCC), und „mindestens S auf v“
/// wartet nur auf Gehörtes. Eine Antwort trägt ihn je gezeigter Id; vor dem Reader wartet der Query-Dienst begrenzt auf ihn (Read-your-writes).
/// </summary>
public sealed class ProjektionsStaende : IReadModelDepsReader
{
    /// <summary>Wie oft das Warten auf „mindestens S auf v“ nachsieht.</summary>
    public static readonly TimeSpan Takt = TimeSpan.FromMilliseconds(15);

    private readonly IDocumentStore _store;
    private readonly IReadOnlyDictionary<string, string[]?> _typen;
    private readonly ILogger<ProjektionsStaende>? _logger;

    public ProjektionsStaende(IDocumentStore store, IEnumerable<BatchRegistrierung> registrierungen, ILogger<ProjektionsStaende>? logger = null)
    {
        _store = store;
        // Je Projektion die Speicher-Diskriminatoren ihrer Typen samt Upcasting-Vorgängern (null = ohne Typfilter: sie hört alles).
        _typen = registrierungen.GroupBy(r => r.ProjektionsId, StringComparer.Ordinal)
            .ToDictionary(g => g.Key, g => BatchLeser.TypenAus(g.First().EventTypes)?.ToArray(), StringComparer.Ordinal);
        _logger = logger;
    }

    public async Task<IReadOnlyList<AggregateMeta>?> ReadAsync(string subscriberId, IReadOnlyList<string> readModelIds)
    {
        var ids = readModelIds.Select(r => Guid.TryParse(r, out var g) ? g : Guid.Empty).Where(g => g != Guid.Empty).Distinct().ToArray();
        if (ids.Length == 0) return null;
        try
        {
            var staende = await StaendeAsync(subscriberId, ids, CancellationToken.None);
            return staende.Select(s => new AggregateMeta(s.Key, "", s.Value)).ToList();
        }
        catch (Exception ex)
        {
            // Nicht autoritativ: ohne Stand gilt die Antwort als frisch.
            _logger?.LogWarning(ex, "[Abfrage] Stand der Projektion {Projektion} nicht lesbar", subscriberId);
            return null;
        }
    }

    public async Task<bool> WarteAsync(string subscriberId, IReadOnlyList<StreamStand> mindestens, TimeSpan frist, CancellationToken ct = default)
    {
        var soll = mindestens.Where(m => m.Stream != Guid.Empty && m.Version > 0).GroupBy(m => m.Stream)
            .ToDictionary(g => g.Key, g => g.Max(m => m.Version));
        if (soll.Count == 0) return true;
        var bis = DateTime.UtcNow + frist;
        while (true)
        {
            var ist = await StaendeAsync(subscriberId, soll.Keys.ToArray(), ct);
            foreach (var (s, v) in ist)
                if (v >= soll[s]) soll.Remove(s);
            if (soll.Count == 0) return true;
            if (DateTime.UtcNow >= bis) return false;
            await Task.Delay(Takt, ct);
        }
    }

    /// <summary>Der Stand je Stream: Guard der Projektion, dann das nächste gehörte Event bzw. der Kopf aus dem Log.</summary>
    private async Task<Dictionary<Guid, int>> StaendeAsync(string subscriberId, Guid[] streams, CancellationToken ct)
    {
        var guards = new Dictionary<Guid, int>();
        await using (var s = _store.QuerySession())
        {
            var cps = await s.LoadManyAsync<ProjectionCheckpoint>(ct, streams.Select(g => ProjectionCheckpoint.MakeId(subscriberId, g)).ToArray());
            foreach (var cp in cps) guards[cp.StreamId] = cp.Version;
        }

        var typen = _typen.TryGetValue(subscriberId, out var t) ? t : null;
        var schema = _store.Options.Events.DatabaseSchemaName;
        // Je Stream ein Index-Zugriff (stream_id, version) und einer auf mt_streams — kein Aggregat, keine laterale Verknüpfung (feste Pläne).
        var sql = $"""
            select i.id, i.g,
                   (select e.version from "{schema}".mt_events e
                     where e.stream_id = i.id and e.version > i.g {(typen is null ? "" : "and e.type = any(@typen)")}
                     order by e.version limit 1) as naechste,
                   (select st.version from "{schema}".mt_streams st where st.id = i.id) as kopf
            from unnest(@ids::uuid[], @guards::bigint[]) as i(id, g)
            """;
        var ergebnis = new Dictionary<Guid, int>();
        await using var conn = _store.Storage.Database.CreateConnection();
        await conn.OpenAsync(ct);
        await using var cmd = new NpgsqlCommand(sql, (NpgsqlConnection)conn);
        cmd.Parameters.AddWithValue("ids", streams);
        cmd.Parameters.AddWithValue("guards", streams.Select(g => (long)(guards.TryGetValue(g, out var v) ? v : 0)).ToArray());
        if (typen is not null) cmd.Parameters.AddWithValue("typen", typen);
        await using var r = await cmd.ExecuteReaderAsync(ct);
        while (await r.ReadAsync(ct))
        {
            var guard = r.GetInt64(1);
            long stand = !r.IsDBNull(2) ? r.GetInt64(2) - 1 : !r.IsDBNull(3) ? r.GetInt64(3) : guard;
            ergebnis[r.GetGuid(0)] = (int)Math.Max(stand, guard);
        }
        return ergebnis;
    }
}
