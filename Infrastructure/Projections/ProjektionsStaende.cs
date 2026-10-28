using Abstractions;
using Marten;
using Microsoft.Extensions.Logging;
using Npgsql;

namespace Infrastructure.Projections;

/// <summary>
/// Der Stand einer Projektion je Stream (Plan Pub/Sub-Neustrukturierung §2.7): wie weit sie den Stream verarbeitet hat. Zwei Quellen:
/// <list type="number">
///   <item>der Guard je (Projektion, Stream) aus ihrem Fortschritt — beim Store der Klasse 1 in DERSELBEN Transaktion geschrieben wie ihre
///     Wirkungen, bei Klasse 2 und 3 vom Framework NACH ihnen (<see cref="Projektionsnaht.Ablage"/>): die Version des letzten Events, das sie
///     angewandt hat;</item>
///   <item>das Log: folgen danach nur Events, die sie nicht hört, gilt ihr Inhalt bis zum nächsten gehörten Event (bzw. bis zum Kopf des Streams)
///     — Stand = nächstes gehörtes Event − 1, ohne ein solches der Kopf.</item>
/// </list>
/// So ist der Stand nie vor den Daten sichtbar, sinkt nie, beginnt mit einem Neuaufbau wieder bei null — und bleibt nicht hinter Events
/// stehen, die die Projektion gar nicht hört (eine Frist, eine Ablehnung): als Version eines Commands taugt er (OCC), und „mindestens S auf v“
/// wartet nur auf Gehörtes. Hinter einer Frage können mehrere Projektionen stehen (ihre Lese-Fähigkeiten gehören zu mehreren Stores): dann gilt
/// je Stream der kleinste Stand — eine Projektion, die den Stream nicht hört, steht ohnehin am Kopf.
/// </summary>
public sealed class ProjektionsStaende
{
    /// <summary>Wie oft das Warten auf „mindestens S auf v“ nachsieht.</summary>
    public static readonly TimeSpan Takt = TimeSpan.FromMilliseconds(15);

    private readonly IDocumentStore _store;
    private readonly IReadOnlyDictionary<string, (Projektionseintrag Eintrag, string[]? Typen)> _projektionen;
    private readonly IFaehigkeitsFabrik _faehigkeiten;
    private readonly IFortschrittBeimFramework _framework;
    private readonly ILogger<ProjektionsStaende>? _logger;

    public ProjektionsStaende(IDocumentStore store, IEnumerable<Projektionseintrag> projektionen, IFaehigkeitsFabrik faehigkeiten,
        IFortschrittBeimFramework framework, ILogger<ProjektionsStaende>? logger = null)
    {
        _store = store;
        // Je Projektion die Speicher-Diskriminatoren ihrer Typen samt Upcasting-Vorgängern (null = ohne Typfilter: sie hört alles).
        _projektionen = projektionen.GroupBy(p => p.Kennung, StringComparer.Ordinal)
            .ToDictionary(g => g.Key, g => (g.First(), BatchLeser.TypenAus(g.First().Typen)?.ToArray()), StringComparer.Ordinal);
        _faehigkeiten = faehigkeiten;
        _framework = framework;
        _logger = logger;
    }

    /// <summary>Der Stand je Id (die Deps einer Antwort) über die Projektionen hinter der Frage; null ohne gültige Id oder bei Fehler.</summary>
    public async Task<IReadOnlyList<AggregateMeta>?> LiesAsync(IReadOnlyList<string> projektionen, IEnumerable<Guid> ids, CancellationToken ct = default)
    {
        var streams = ids.Where(g => g != Guid.Empty).Distinct().ToArray();
        if (streams.Length == 0 || projektionen.Count == 0) return null;
        try
        {
            var staende = await StaendeAsync(projektionen, streams, ct);
            return staende.Select(s => new AggregateMeta(s.Key, "", s.Value)).ToList();
        }
        catch (Exception ex)
        {
            // Nicht autoritativ: ohne Stand gilt die Antwort als frisch.
            _logger?.LogWarning(ex, "[Abfrage] Stand der Projektion(en) {Projektionen} nicht lesbar", string.Join(", ", projektionen));
            return null;
        }
    }

    /// <summary>Wartet höchstens <paramref name="frist"/>, bis jede der Projektionen jeden Stream mindestens bis zu seiner Version verarbeitet hat.
    /// false = noch nicht so weit.</summary>
    public async Task<bool> WarteAsync(IReadOnlyList<string> projektionen, IReadOnlyList<StreamStand> mindestens, TimeSpan frist, CancellationToken ct = default)
    {
        var soll = mindestens.Where(m => m.Stream != Guid.Empty && m.Version > 0).GroupBy(m => m.Stream)
            .ToDictionary(g => g.Key, g => g.Max(m => m.Version));
        if (soll.Count == 0 || projektionen.Count == 0) return true;
        var bis = DateTime.UtcNow + frist;
        while (true)
        {
            var ist = await StaendeAsync(projektionen, soll.Keys.ToArray(), ct);
            foreach (var (s, v) in ist)
                if (v >= soll[s]) soll.Remove(s);
            if (soll.Count == 0) return true;
            if (DateTime.UtcNow >= bis) return false;
            await Task.Delay(Takt, ct);
        }
    }

    /// <summary>Je Stream der kleinste Stand über die Projektionen.</summary>
    private async Task<Dictionary<Guid, int>> StaendeAsync(IReadOnlyList<string> projektionen, Guid[] streams, CancellationToken ct)
    {
        Dictionary<Guid, int>? ergebnis = null;
        foreach (var kennung in projektionen.Distinct(StringComparer.Ordinal))
        {
            if (!_projektionen.TryGetValue(kennung, out var p))
                throw new InvalidOperationException($"Projektion '{kennung}' ist nicht registriert.");
            var je = await StaendeAsync(p.Eintrag, p.Typen, streams, ct);
            if (ergebnis is null) ergebnis = je;
            else foreach (var (s, v) in je) ergebnis[s] = Math.Min(ergebnis.GetValueOrDefault(s, v), v);
        }
        return ergebnis ?? new Dictionary<Guid, int>();
    }

    /// <summary>Der Stand je Stream einer Projektion: ihr Guard, dann das nächste gehörte Event bzw. der Kopf aus dem Log.</summary>
    private async Task<Dictionary<Guid, int>> StaendeAsync(Projektionseintrag p, string[]? typen, Guid[] streams, CancellationToken ct)
    {
        IReadOnlyDictionary<Guid, int> guards;
        using (var bereich = _faehigkeiten.Oeffne())
            guards = await p.Naht(bereich, _framework).Ablage.LadeGuardsAsync(p.Kennung, streams, ct);

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
        cmd.Parameters.AddWithValue("guards", streams.Select(g => (long)Math.Max(0, guards.TryGetValue(g, out var v) ? v : 0)).ToArray());
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
