using System.Collections.Concurrent;
using System.Text.Json.Nodes;

namespace Cqrs.Vermessung;

/// <summary>
/// S3 Sammelvorgang (Beispieldomäne). Ein Vorgang ist ein Teil: als Disponent <c>StarteTeilauftrag</c> und <c>SchließeTeilauftragAb</c>;
/// der <c>TeilFertigProzess</c> meldet das Teil am Sammelvorgang (ein langlebiger, heißer Stream), nach N Teilen ist die Barriere erreicht.
/// Es gibt keine Query auf den Sammelvorgang — sichtbar ist ein Teil, wenn sein <c>TeilVerbucht</c> im Log steht (Log-Zeit, mit dem
/// Uhr-Versatz auf die Treiber-Uhr umgerechnet). Den Sammelvorgang legt die Last selbst an (<c>StarteSammelvorgang(N)</c>, bevor sein erstes
/// Teil startet); in der Messung bekommt jeder Block einen eigenen, so groß, dass die Barriere nicht erreicht wird.
/// </summary>
internal sealed class Sammelvorgang : ILast
{
    public const string TokenDisponent = "vermessung-disponent";
    private readonly Eingang _eingang;
    private readonly TimeSpan _uhrversatz;
    private readonly ConcurrentDictionary<Guid, Guid> _korrelationZuTeil = new();   // Vorgang des TeilFertigProzess → Teilauftrag
    private readonly ConcurrentDictionary<Guid, Vorgang> _offen = new();             // Teilauftrag → Vorgang
    private readonly ConcurrentQueue<(string Korrelation, DateTimeOffset Zeit)> _verbuchtOhneTeil = new();
    private readonly object _sperre = new();
    private Lauf? _aktuell;
    private int _groesse = 200;

    /// <summary>Ein Sammelvorgang: seine Größe N, die Teile, die ihm schon zugeteilt sind, und das Anlegen (das erste Teil wartet darauf).</summary>
    public sealed class Lauf
    {
        public required Guid Id { get; init; }
        public required int N { get; init; }
        public required Task Angelegt { get; init; }
        public List<Guid> Teile { get; } = new();
    }

    public Sammelvorgang(Eingang eingang, LogStrom log, TimeSpan uhrversatz)
    {
        _eingang = eingang;
        _uhrversatz = uhrversatz;
        log.Beobachte(Sieh);
    }

    public List<Lauf> Laeufe { get; } = new();

    /// <summary>Die Größe der folgenden Sammelvorgänge; der nächste Vorgang beginnt einen neuen.</summary>
    public void NeueGroesse(int n)
    {
        lock (_sperre) { _groesse = n; _aktuell = null; }
    }

    public async Task StarteAsync(Vorgang v, CancellationToken ct)
    {
        Lauf lauf;
        var teil = Guid.NewGuid();
        lock (_sperre)
        {
            if (_aktuell == null || _aktuell.Teile.Count >= _aktuell.N)
            {
                var id = Guid.NewGuid();
                _aktuell = new Lauf { Id = id, N = _groesse, Angelegt = AnlegenAsync(id, _groesse) };
                Laeufe.Add(_aktuell);
            }
            lauf = _aktuell;
            lauf.Teile.Add(teil);
        }
        v.Start = DateTimeOffset.UtcNow;
        _offen[teil] = v;
        await lauf.Angelegt;
        Pruefe(await _eingang.CommandAsync("StarteTeilauftrag",
            new JsonObject { ["AggregateId"] = teil.ToString(), ["SammelvorgangId"] = lauf.Id.ToString() }, TokenDisponent, ct: ct));
        Pruefe(await _eingang.CommandAsync("SchließeTeilauftragAb", new JsonObject { ["AggregateId"] = teil.ToString() }, TokenDisponent, ct: ct));
    }

    private async Task AnlegenAsync(Guid id, int n)
        => Pruefe(await _eingang.CommandAsync("StarteSammelvorgang", new JsonObject { ["AggregateId"] = id.ToString(), ["Anzahl"] = n }, TokenDisponent));

    private void Sieh(LogZeile z)
    {
        if (z.Typ == "prozess_gestartet" && z.Daten != null)
        {
            var d = JsonNode.Parse(z.Daten)!;
            if ((string?)d["ProzessName"] == "TeilFertigProzess" && Guid.TryParse((string?)d["AuslöserStream"], out var teil))
                _korrelationZuTeil[z.Stream] = teil;
            Zuordnen();
        }
        else if (z.Typ == "teil_verbucht" && z.Korrelation != null)
        {
            _verbuchtOhneTeil.Enqueue((z.Korrelation, z.Zeit + _uhrversatz));
            Zuordnen();
        }
    }

    /// <summary>Ordnet TeilVerbucht über die Korrelation (= Vorgang des TeilFertigProzess) seinem Teilauftrag zu.</summary>
    private void Zuordnen()
    {
        var n = _verbuchtOhneTeil.Count;
        for (var i = 0; i < n && _verbuchtOhneTeil.TryDequeue(out var e); i++)
        {
            if (Guid.TryParse(e.Korrelation, out var k) && _korrelationZuTeil.TryGetValue(k, out var teil))
            {
                if (_offen.TryRemove(teil, out var v)) v.Sichtbar = e.Zeit;
            }
            else _verbuchtOhneTeil.Enqueue(e);   // prozess_gestartet noch nicht gesehen
        }
    }

    private static void Pruefe(Eingang.Antwort a)
    {
        if (!a.Ok) throw new InvalidOperationException($"S3: Command abgewiesen ({a.Status}): {a.Fehler}");
    }

    /// <summary>
    /// Exactly-once (§0.2) je Sammelvorgang: Fertig zählt genau die verbuchten Teile (keines doppelt, keines übersprungen), kein Teil zweimal
    /// verbucht oder beendet; ist alles abgebaut, sind es genau die gestarteten Teile, und die Barriere fällt nur bei vollen Sammelvorgängen.
    /// </summary>
    public async Task<List<Pruefung>> PruefeAsync(Datenbank db, IEnumerable<Lauf> laeufe, string titel, bool vollstaendig)
    {
        var s = db.Schema;
        var liste = laeufe.ToList();
        var ids = liste.Select(l => l.Id).ToArray();
        var teile = liste.SelectMany(l => l.Teile).ToArray();
        var verbucht = await db.LiesAsync($"""
            select stream_id, count(*), coalesce(max((data->>'Fertig')::int), 0) from {s}.mt_events
            where stream_id = any(@s) and type = 'teil_verbucht' group by 1
            """, r => (Id: r.GetGuid(0), Anzahl: r.GetInt64(1), Fertig: r.GetInt32(2)), ("s", ids));
        var jeLauf = verbucht.ToDictionary(x => x.Id);
        var anzahl = verbucht.Sum(x => x.Anzahl);
        return new List<Pruefung>
        {
            Pruefung.Gleich($"{titel}: Fertig = Zahl der TeilVerbucht (Sammelvorgänge mit Abweichung)", 0, verbucht.Count(x => x.Anzahl != x.Fertig)),
            Pruefung.Erwartet($"{titel}: TeilVerbucht = gestartete Teile", teile.Length, anzahl, vollstaendig),
            Pruefung.Gleich($"{titel}: kein Sammelvorgang mit mehr TeilVerbucht als Teilen", 0,
                liste.Count(l => jeLauf.TryGetValue(l.Id, out var x) && x.Anzahl > l.Teile.Count)),
            Pruefung.Gleich($"{titel}: Barriere nur bei vollen Sammelvorgängen", 0, await db.ZahlAsync(
                $"select count(*) from {s}.mt_events where stream_id = any(@s) and type = 'sammelvorgang_abgeschlossen'",
                ("s", liste.Where(l => !jeLauf.TryGetValue(l.Id, out var x) || x.Anzahl < l.N).Select(l => l.Id).ToArray()))),
            Pruefung.Erwartet($"{titel}: je Teil genau ein TeilauftragAbgeschlossen", teile.Length,
                await db.ZahlAsync($"select count(*) from {s}.mt_events where stream_id = any(@t) and type = 'teilauftrag_abgeschlossen'", ("t", teile)), vollstaendig),
            Pruefung.Gleich($"{titel}: kein Teil zweimal abgeschlossen", 0, await db.ZahlAsync(
                $"select count(*) from (select stream_id from {s}.mt_events where stream_id = any(@t) and type = 'teilauftrag_abgeschlossen' group by 1 having count(*) > 1) x",
                ("t", teile))),
            Pruefung.Erwartet($"{titel}: je Teil ein erfolgreich beendeter Vorgang", teile.Length, await db.ZahlAsync($"""
                select count(*) from {s}.mt_events b join {s}.mt_events g on g.stream_id = b.stream_id and g.type = 'prozess_gestartet'
                where b.type = 'prozess_beendet' and (b.data->>'Erfolg')::boolean and (g.data->>'AuslöserStream')::uuid = any(@t)
                """, ("t", teile)), vollstaendig),
            Pruefung.Gleich($"{titel}: kein Vorgang zweimal beendet", 0, await db.ZahlAsync(
                $"select count(*) from (select stream_id from {s}.mt_events where type = 'prozess_beendet' group by 1 having count(*) > 1) x")),
        };
    }
}
