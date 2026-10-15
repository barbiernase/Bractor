using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Abstractions;

namespace Infrastructure.Pruefstand.BatchKonsum;

/// <summary>
/// Lese-Seam des Batch-Konsums, store-frei: ein Log mit globaler Sequenz, in dem jedes Event <b>sichtbar</b> (committet) und <b>gesetzt</b>
/// (älter als die Karenz) sein kann — so lassen sich Nachzügler (eine kleinere Sequenz wird erst später sichtbar) und doppelte oder
/// ungeordnete Seiten nachbilden. Die Semantik von Marten (Partition, Ordnung, Limit, Ausschluss) prüft die Integration.
/// </summary>
public sealed class PartitionsLog : IEventStoreRepository
{
    public sealed class Eintrag(long seq, EventEnvelope evt)
    {
        public long Seq { get; } = seq;
        public EventEnvelope Event { get; } = evt;
        public bool Sichtbar { get; set; } = true;
        public bool Gesetzt { get; set; } = true;
    }

    private readonly List<Eintrag> _log = new();
    private long _seq;

    /// <summary>Jede Seite enthält jedes Event zweimal und in umgekehrter Reihenfolge (doppelte, ungeordnete Zustellung).</summary>
    public bool DoppeltUndUngeordnet { get; set; }

    public int Lesungen { get; private set; }

    public Eintrag Haenge(Guid stream, int version, bool sichtbar = true, bool gesetzt = true)
    {
        lock (_log)
        {
            var e = new Eintrag(++_seq, new EventEnvelope { AggregateId = stream, AggregateVersion = version, Payload = new Testereignis(version) })
                { Sichtbar = sichtbar, Gesetzt = gesetzt };
            _log.Add(e);
            return e;
        }
    }

    public Task<PartitionsSeite> ReadPartitionAsync(
        long nach, int partition, int partitionen, IReadOnlyCollection<string>? typen, IReadOnlyCollection<long> ausser, int limit, CancellationToken ct)
    {
        lock (_log)
        {
            Lesungen++;
            var sichtbar = _log.Where(e => e.Sichtbar && e.Seq > nach).ToList();
            var gesetzt = sichtbar.Where(e => e.Gesetzt).Select(e => e.Seq).DefaultIfEmpty(nach).Max();
            var seite = sichtbar
                .Where(e => Partitionierung.Von(e.Event.AggregateId, partitionen) == partition && !ausser.Contains(e.Seq))
                .OrderBy(e => e.Seq).Take(limit)
                .Select(e => new PartitionsEvent(e.Seq, e.Event)).ToList();
            var voll = seite.Count >= limit;
            if (DoppeltUndUngeordnet) seite = seite.Concat(seite).Reverse().ToList();
            return Task.FromResult(new PartitionsSeite(seite, gesetzt, voll));
        }
    }

    public Task<IReadOnlyList<EventEnvelope>> ReadStreamAsync(Guid streamId, int fromVersion, CancellationToken ct)
    {
        lock (_log)
            return Task.FromResult<IReadOnlyList<EventEnvelope>>(_log
                .Where(e => e.Sichtbar && e.Event.AggregateId == streamId && e.Event.AggregateVersion >= fromVersion)
                .OrderBy(e => e.Seq).Select(e => e.Event).ToList());
    }

    public Task AppendEventsAsync(Guid a, int v, IReadOnlyList<IEvent> e, string? c = null, string? ca = null, string? at = null, string? ak = null, IReadOnlyList<Beilage>? b = null)
        => throw new NotSupportedException();
    public Task<StreamChanges> ReadChangedStreamsAsync(long after, IReadOnlyCollection<string>? typen, int limit, CancellationToken ct) => throw new NotSupportedException();
    public Task<TState?> LoadStateAsync<TState>(Guid id) where TState : class, IState, new() => throw new NotSupportedException();

    /// <summary>Eine Stream-Id, die in Partition <paramref name="partition"/> von <paramref name="partitionen"/> fällt.</summary>
    public static Guid StreamIn(int partition, int partitionen)
    {
        while (true)
        {
            var id = Guid.NewGuid();
            if (Partitionierung.Von(id, partitionen) == partition) return id;
        }
    }
}

public sealed record Testereignis(int Nummer) : IEvent;

/// <summary>
/// Der durable Teil des Speicher-Trackers (eine „Datenbank“ für mehrere Aktivierungen): Partitionen mit Version, Guards und die committeten
/// Effekte. Committet wird atomar: alles oder nichts, mit Versionsprüfung der Partition.
/// </summary>
public sealed class SpeicherKonsum
{
    public readonly Dictionary<string, PartitionsCheckpoint> Partitionen = new();
    public readonly Dictionary<(string, Guid), int> Guards = new();
    /// <summary>Die committeten Effekte in Commit-Reihenfolge: (Stream, Version).</summary>
    public readonly List<(Guid Stream, int Version)> Angewandt = new();
    /// <summary>Prüf-Naht: wirft vor dem Commit (etwa eine SchreibKonfliktException), wenn sie für die Guards des Batches etwas liefert.</summary>
    public Func<IReadOnlyDictionary<Guid, int>, Exception?>? CommitFehler { get; set; }

    public IEnumerable<int> VersionenVon(Guid stream) => Angewandt.Where(a => a.Stream == stream).Select(a => a.Version);
}

/// <summary>Eine Store-Instanz (eine Aktivierung): puffert Effekte, committet sie mit Guards und Partition über <see cref="SpeicherKonsum"/>.</summary>
public sealed class SpeicherTracker(SpeicherKonsum db) : ICoCommitTracker, IRebuildableProjection
{
    private readonly List<(Guid, int)> _puffer = new();
    public Guid? Aktuell { get; private set; }

    /// <summary>Der Effekt eines Events (aus dem Dispatch): gepuffert bis zum Commit.</summary>
    public void Wende(Guid stream, int version)
    {
        if (Aktuell != stream) throw new InvalidOperationException("Effekt außerhalb des Streams des Events");
        _puffer.Add((stream, version));
    }

    public Task<PartitionsCheckpoint> LadePartitionAsync(string projektionsId, int partition, CancellationToken ct)
    {
        lock (db)
            return Task.FromResult(db.Partitionen.TryGetValue(PartitionsCheckpoint.MakeId(projektionsId, partition), out var p)
                ? Kopie(p) : PartitionsCheckpoint.Neu(projektionsId, partition));
    }

    public Task<IReadOnlyDictionary<Guid, int>> LadeGuardsAsync(string projektionsId, IReadOnlyCollection<Guid> streams, CancellationToken ct)
    {
        lock (db)
            return Task.FromResult<IReadOnlyDictionary<Guid, int>>(streams.Distinct().ToDictionary(s => s,
                s => db.Guards.TryGetValue((projektionsId, s), out var v) ? v : -1));
    }

    public void BeginneBatch() { _puffer.Clear(); Aktuell = null; }
    public void Stream(Guid streamId) => Aktuell = streamId;

    public Task<int> CommitAsync(PartitionsCheckpoint stand, IReadOnlyDictionary<Guid, int> guards, CancellationToken ct)
    {
        try
        {
            lock (db)
            {
                var jetzt = db.Partitionen.TryGetValue(stand.Id, out var p) ? p.Version : 0;
                if (jetzt != stand.Version)
                    throw new PartitionsKonfliktException($"{stand.Id}: erwartet v{stand.Version}, gefunden v{jetzt}", new Exception("Versionsprüfung"));
                if (db.CommitFehler?.Invoke(guards) is { } fehler) throw fehler;
                db.Angewandt.AddRange(_puffer);
                foreach (var (s, v) in guards) db.Guards[(stand.ProjektionsId, s)] = v;
                var neu = Kopie(stand);
                neu.Version = jetzt + 1;
                db.Partitionen[stand.Id] = neu;
                return Task.FromResult(neu.Version);
            }
        }
        finally { _puffer.Clear(); Aktuell = null; }
    }

    public Task ResetAllAsync(string projektionsId, CancellationToken ct)
    {
        lock (db)
        {
            foreach (var p in db.Partitionen.Values.Where(p => p.ProjektionsId == projektionsId))
            {
                p.Position = 0;
                p.Geparkt = new();
                p.Version++;
            }
            foreach (var k in db.Guards.Keys.Where(k => k.Item1 == projektionsId).ToList()) db.Guards.Remove(k);
        }
        return Task.CompletedTask;
    }

    public Task ClearAsync(CancellationToken ct)
    {
        lock (db) db.Angewandt.Clear();
        return Task.CompletedTask;
    }

    private static PartitionsCheckpoint Kopie(PartitionsCheckpoint p) => new()
    {
        Id = p.Id, ProjektionsId = p.ProjektionsId, Partition = p.Partition, Position = p.Position, Version = p.Version,
        Geparkt = p.Geparkt.Select(g => new GeparkterStream { StreamId = g.StreamId, Version = g.Version, Grund = g.Grund, Seit = g.Seit }).ToList()
    };
}
