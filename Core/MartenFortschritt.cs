using Abstractions;
using Marten;
using Marten.Exceptions;

namespace Core;

/// <summary>
/// Eine Fortschritts-Ablage, die die Guards als <see cref="ProjectionCheckpoint"/>-Dokumente in <see cref="Log"/> hält (über <see cref="MartenFortschritt"/>):
/// wer Guard und Log zusammen braucht (der Stand je Id), liest beides in einer Abfrage.
/// </summary>
public interface IFortschrittImLog
{
    IDocumentStore Log { get; }
}

/// <summary>
/// Der Fortschritt einer Projektion in Marten (docs/04 §4.3) an EINER Stelle: je Partition ein
/// <see cref="PartitionsCheckpoint"/> mit numerischer Revision (<see cref="KonsumSchema"/>), je (Projektion, Stream) ein
/// <see cref="ProjectionCheckpoint"/> (Guard). Zwei Nutzer: der Marten-Store der Klasse 1 legt ihn in derselben Session ab wie seine Wirkungen
/// (<see cref="MartenStore"/>), das Framework hält ihn für die Klassen 2 und 3 allein (<c>Infrastructure.Projections.FortschrittBeimFramework</c>).
/// </summary>
public static class MartenFortschritt
{
    public static async Task<PartitionsCheckpoint> LadeAsync(IDocumentStore store, string projektion, int partition, CancellationToken ct)
    {
        await using var s = store.QuerySession();
        return await s.LoadAsync<PartitionsCheckpoint>(PartitionsCheckpoint.MakeId(projektion, partition), ct)
               ?? PartitionsCheckpoint.Neu(projektion, partition);
    }

    public static async Task<IReadOnlyDictionary<Guid, int>> LadeGuardsAsync(
        IDocumentStore store, string projektion, IReadOnlyCollection<Guid> streams, CancellationToken ct)
    {
        var guards = streams.Distinct().ToDictionary(s => s, _ => -1);
        if (guards.Count == 0) return guards;
        await using var s = store.QuerySession();
        foreach (var cp in await s.LoadManyAsync<ProjectionCheckpoint>(ct, guards.Keys.Select(id => ProjectionCheckpoint.MakeId(projektion, id))))
            guards[cp.StreamId] = cp.Version;
        return guards;
    }

    /// <summary>
    /// Legt die Guards und den Fortschritt mit erwarteter Revision in die Session (noch nie geschrieben → <c>Insert</c>, ein zweiter Erster scheitert
    /// am Schlüssel; sonst <c>UpdateRevision</c> auf Revision + 1, eine zweite Aktivierung oder ein Neuaufbau dazwischen scheitert an der Revision).
    /// Liefert die neue Revision; gültig wird beides erst mit dem <c>SaveChanges</c> des Aufrufers.
    /// </summary>
    public static int Lege(IDocumentSession s, PartitionsCheckpoint stand, IReadOnlyDictionary<Guid, int> guards)
    {
        var jetzt = DateTimeOffset.UtcNow;
        foreach (var (stream, version) in guards)
            s.Store(new ProjectionCheckpoint
            {
                Id = ProjectionCheckpoint.MakeId(stand.ProjektionsId, stream),
                ProjectionId = stand.ProjektionsId,
                StreamId = stream,
                Version = version,
                UpdatedAt = jetzt
            });
        var neu = new PartitionsCheckpoint
        {
            Id = stand.Id, ProjektionsId = stand.ProjektionsId, Partition = stand.Partition, Position = stand.Position,
            Geparkt = stand.Geparkt, Generation = stand.Generation, UpdatedAt = jetzt, Version = stand.Version + 1
        };
        if (stand.Version == 0) s.Insert(neu);
        else s.UpdateRevision(neu, stand.Version + 1);
        return neu.Version;
    }

    /// <summary>
    /// Neuaufbau: alle Partitionen der Projektion auf Anfang in der neuen Generation (auch die noch nie geschriebenen — sonst schriebe ihr erster
    /// Leser in die erste Generation) mit neuer Revision, alle Guards weg. <c>Store</c> erhöht die Revision bedingungslos: ein noch laufender Leser
    /// verliert seinen nächsten Commit, statt seinen alten Stand wieder anzulegen.
    /// </summary>
    public static async Task ZuruecksetzenAsync(IDocumentStore store, string projektion, int partitionen, string generation, CancellationToken ct)
    {
        await using var s = store.LightweightSession();
        var jetzt = DateTimeOffset.UtcNow;
        for (var p = 0; p < partitionen; p++)
            s.Store(new PartitionsCheckpoint
            {
                Id = PartitionsCheckpoint.MakeId(projektion, p), ProjektionsId = projektion, Partition = p, Position = 0,
                Geparkt = new(), Generation = generation, UpdatedAt = jetzt
            });
        s.DeleteWhere<PartitionsCheckpoint>(c => c.ProjektionsId == projektion && c.Partition >= partitionen);
        s.DeleteWhere<ProjectionCheckpoint>(c => c.ProjectionId == projektion);
        await s.SaveChangesAsync(ct);
    }

    /// <summary>
    /// Übersetzt einen Konflikt beim <c>SaveChanges</c> in die Ausnahmen des Batch-Lesers: am Fortschritt → <see cref="PartitionsKonfliktException"/>,
    /// an einem geteilten Dokument → <see cref="SchreibKonfliktException"/>; sonst null (der Aufrufer wirft weiter).
    /// </summary>
    public static Exception? Konflikt(Exception ex, PartitionsCheckpoint stand, string wer)
    {
        if (Dokument(ex) is not { } doku) return null;
        if (IstPartition(doku))
            return new PartitionsKonfliktException(
                $"{wer}: Partition {stand.Id} zwischenzeitlich geschrieben (erwartet Revision {stand.Version}) — Batch verworfen.", ex);
        return new SchreibKonfliktException($"{wer}: geteiltes Dokument {doku} zwischenzeitlich geändert (Partition {stand.Id}) — Batch verworfen.", ex);
    }

    // Marten 8 meldet die Versionsprüfung als JasperFx.ConcurrencyException (nicht Marten.Exceptions.ConcurrencyException),
    // ein gleichzeitiges Insert als DocumentAlreadyExistsException — beide gegen echtes Postgres gemessen (SchreibRegelPostgresTests), beide mit
    // dem Dokumenttyp (einmal als Name, einmal als Typ). Liefert den Namen des Dokuments im Konflikt (der Partitions-Checkpoint geht vor), sonst null.
    private static string? Dokument(Exception ex) => ex switch
    {
        JasperFx.ConcurrencyException c => c.DocType ?? "?",
        DocumentAlreadyExistsException d => d.DocType?.FullName ?? "?",
        AggregateException agg when agg.InnerExceptions.Count > 0 && agg.InnerExceptions.All(i => Dokument(i) != null)
            => agg.InnerExceptions.Select(Dokument).FirstOrDefault(n => IstPartition(n!)) ?? Dokument(agg.InnerExceptions[0]),
        _ => null
    };

    private static bool IstPartition(string dokument)
        => dokument == typeof(PartitionsCheckpoint).FullName || dokument == nameof(PartitionsCheckpoint);
}
