using Abstractions;
using Marten;
using Marten.Exceptions;

namespace Core;

/// <summary>
/// Marten-Implementierung der Co-Commit-Naht (<see cref="ICoCommitTracker"/>) — die technik­spezifische, in ALLEN Projektions-Stores
/// identische Exactly-once-Maschinerie an EINER auditierten Stelle. Write-Effekte werden nur GEPUFFERT; <see cref="CommitAsync"/> spielt die
/// Effekte eines Batches (viele Streams einer Partition), die Guards seiner Streams und den <see cref="PartitionsCheckpoint"/> mit erwarteter
/// Version in EINER Marten-<c>IdentitySession</c> ab und committet mit EINEM <c>SaveChangesAsync</c> → Effekte und Fortschritt werden gemeinsam
/// durabel (exactly-once, auch für nicht-idempotente Append-Effekte), und eine zweite Aktivierung derselben Partition verliert (G-4).
///
/// <b>Schreib-Regel (Single-Writer je Dokument).</b> Die Konsum-Maschine garantiert genau EINEN Schreiber je (Projektion, Partition) und damit je
/// (Projektion, Stream). Das Co-Commit macht einen Batch atomar, isoliert aber nicht die Batches zweier Partitionen gegeneinander. Darum gibt es
/// drei Arten von Effekten, und jede wird beim Puffern gegen den Stream des laufenden Events (<see cref="Stream"/>) geprüft:
/// <list type="bullet">
///   <item><b>Eigenes Dokument</b> (Schlüssel = Stream des Events): <see cref="EnqueueStore{T}"/>,
///     <see cref="EnqueueTransform{T}"/>, <see cref="EnqueueAnlegenOderAendern{T}"/> — sicher per Konstruktion.</item>
///   <item><b>Zeile dieses Streams</b> (zusammengesetzte Id, z. B. „Datensatz × Paar“): <see cref="EnqueueZeile{T}"/>,
///     <see cref="EnqueueZeileEntfernen{T}"/> — zwei Streams berühren nie dieselbe Zeile.</item>
///   <item><b>Geteiltes Dokument</b> (<see cref="IGeteiltesReadModel"/>, aus mehreren Streams beschrieben):
///     <see cref="EnqueueGeteilt{T}(string, Func{T, T})"/> — Lesen → Ändern → Schreiben mit Versionsprüfung. Innerhalb eines Batches sehen
///     sich zwei Streams über die Identity-Map (read-your-writes); ein Konflikt mit einer anderen Partition verwirft den ganzen Batch
///     (<see cref="SchreibKonfliktException"/>), der Leser halbiert und wiederholt.</item>
/// </list>
/// Ein Schreibzugriff auf das Dokument eines FREMDEN Streams über die ersten beiden Wege wirft sofort — statt still
/// ein Update eines parallel laufenden Batches zu überschreiben.
///
/// Jeder Effekt schreibt ein <see cref="IReadModel"/> — die Leseseite ist damit genau die Menge der Read-Model-Typen, die ein
/// Neuaufbau leert (<c>GeneratedProjectionServices.ReadModelTypen</c>, Werkbank §4.6).
///
/// Konkrete Stores erben und schreiben nur noch ihre FACHLICHEN Write-Methoden. Kein Domänenwissen hier, kein
/// Generat — reiner Marten-Adapter. SCOPED registriert: eine Instanz je Fähigkeits-Bereich (je Partitions-Leser → isolierter Puffer).
/// </summary>
public abstract class MartenCoCommitStoreBase : ICoCommitTracker
{
    /// <summary>Der geteilte Document-Store — für die Lese-Methoden (eigene Query-Sessions, committete Daten).</summary>
    protected IDocumentStore Store { get; }

    private readonly List<Func<IDocumentSession, Task>> _pending = new();

    /// <summary>Der Stream des laufenden Events (gesetzt in <see cref="Stream"/>); null = kein Batch offen.</summary>
    private Guid? _stapelStream;

    protected MartenCoCommitStoreBase(IDocumentStore store) => Store = store;

    // ─────────────────────────────────────────────────────────────
    // Eigenes Dokument: Schlüssel = Stream des Stapels
    // ─────────────────────────────────────────────────────────────

    /// <summary>Puffert: das eigene Dokument <paramref name="id"/> anlegen bzw. ersetzen. Gibt <c>Task.CompletedTask</c>
    /// zurück, damit fachliche Write-Methoden expression-bodied bleiben.</summary>
    protected Task EnqueueStore<T>(Guid id, T document) where T : IReadModel
    {
        PruefeEigen(id, typeof(T));
        _pending.Add(s => { s.Store(document); return Task.CompletedTask; });
        return Task.CompletedTask;
    }

    /// <summary>
    /// Puffert ein read-modify-store des eigenen Dokuments: lädt es im Batch (read-your-writes über die Identity-Map),
    /// transformiert und staged es. Fehlt es (Event vor dem erzeugenden Upsert), passiert nichts.
    /// </summary>
    protected Task EnqueueTransform<T>(Guid id, Func<T, T> transform) where T : IReadModel
    {
        PruefeEigen(id, typeof(T));
        _pending.Add(async s =>
        {
            var existing = await s.LoadAsync<T>(id);
            if (existing is null) return;
            s.Store(transform(existing));
        });
        return Task.CompletedTask;
    }

    /// <summary>Puffert ein load-or-create des eigenen Dokuments: <paramref name="aendern"/> bekommt das vorhandene
    /// Dokument oder <c>null</c> und liefert das neue.</summary>
    protected Task EnqueueAnlegenOderAendern<T>(Guid id, Func<T?, T> aendern) where T : class, IReadModel
    {
        PruefeEigen(id, typeof(T));
        _pending.Add(async s =>
        {
            var existing = await s.LoadAsync<T>(id);
            s.Store(aendern(existing));
        });
        return Task.CompletedTask;
    }

    // ─────────────────────────────────────────────────────────────
    // Zeile dieses Streams: zusammengesetzte Id, Besitzer = Stream des Stapels
    // ─────────────────────────────────────────────────────────────

    /// <summary>
    /// Puffert: eine Zeile anlegen bzw. ersetzen, die dem Stream <paramref name="besitzer"/> gehört. Die Id der Zeile
    /// muss den Besitzer enthalten (z. B. <c>{DatensatzId}:{ImagePairId}</c>) — dann berühren zwei Streams nie
    /// dieselbe Zeile, und eine Menge über viele Streams braucht kein geteiltes Dokument.
    /// </summary>
    protected Task EnqueueZeile<T>(Guid besitzer, T zeile) where T : IReadModel
    {
        PruefeEigen(besitzer, typeof(T));
        _pending.Add(s => { s.Store(zeile); return Task.CompletedTask; });
        return Task.CompletedTask;
    }

    /// <summary>Puffert: die Zeile <paramref name="id"/> des Streams <paramref name="besitzer"/> entfernen.</summary>
    protected Task EnqueueZeileEntfernen<T>(Guid besitzer, string id) where T : IReadModel
    {
        PruefeEigen(besitzer, typeof(T));
        _pending.Add(s => { s.Delete<T>(id); return Task.CompletedTask; });
        return Task.CompletedTask;
    }

    // ─────────────────────────────────────────────────────────────
    // Geteiltes Dokument: aus mehreren Streams beschrieben, Versionsprüfung
    // ─────────────────────────────────────────────────────────────

    /// <summary>
    /// Puffert ein read-modify-store eines GETEILTEN Dokuments. <paramref name="aendern"/> bekommt das vorhandene
    /// Dokument oder <c>null</c> und liefert das neue — oder <c>null</c> für „keine Änderung“. Neu angelegt wird per
    /// <c>Insert</c>, geändert per <c>Store</c> auf dem geladenen Dokument; beides prüft Marten beim Commit gegen
    /// die Version (der Generator registriert <see cref="IGeteiltesReadModel"/> mit Optimistic Concurrency). Hat ein
    /// anderer Stream dazwischen geschrieben, fällt der ganze Stapel zurück → <see cref="SchreibKonfliktException"/>.
    /// </summary>
    protected Task EnqueueGeteilt<T>(string id, Func<T?, T?> aendern) where T : class, IGeteiltesReadModel
    {
        _pending.Add(async s => Geteilt(s, await s.LoadAsync<T>(id), aendern));
        return Task.CompletedTask;
    }

    /// <inheritdoc cref="EnqueueGeteilt{T}(string, Func{T, T})"/>
    protected Task EnqueueGeteilt<T>(Guid id, Func<T?, T?> aendern) where T : class, IGeteiltesReadModel
    {
        _pending.Add(async s => Geteilt(s, await s.LoadAsync<T>(id), aendern));
        return Task.CompletedTask;
    }

    private static void Geteilt<T>(IDocumentSession s, T? existing, Func<T?, T?> aendern) where T : class
    {
        var neu = aendern(existing);
        if (neu is null) return;
        if (existing is null) s.Insert(neu);   // gleichzeitig angelegt → DocumentAlreadyExistsException
        else s.Store(neu);                     // gleichzeitig geändert → ConcurrencyException (Version aus dem Load)
    }

    private void PruefeEigen(Guid id, Type dokument)
    {
        if (_stapelStream is not { } stream)
            throw new InvalidOperationException(
                $"{GetType().Name}: Schreib-Effekt auf {dokument.Name} außerhalb eines Batches — Stores schreiben nur aus einer Projektion.");
        if (id != stream)
            throw new InvalidOperationException(
                $"{GetType().Name}: {dokument.Name} {id} gehört nicht dem Stream des Events ({stream}). Ein Dokument, das " +
                "mehrere Streams beschreiben, als Zeilen je Stream ablegen (EnqueueZeile) oder als IGeteiltesReadModel " +
                "über EnqueueGeteilt — sonst überschreiben sich parallele Batches.");
    }

    // ─────────────────────────────────────────────────────────────
    // Batch-Naht (IProjectionTracker)
    // ─────────────────────────────────────────────────────────────

    public async Task<PartitionsCheckpoint> LadePartitionAsync(string projektionsId, int partition, CancellationToken ct)
    {
        await using var s = Store.QuerySession();
        return await s.LoadAsync<PartitionsCheckpoint>(PartitionsCheckpoint.MakeId(projektionsId, partition), ct)
               ?? PartitionsCheckpoint.Neu(projektionsId, partition);
    }

    public async Task<IReadOnlyDictionary<Guid, int>> LadeGuardsAsync(string projektionsId, IReadOnlyCollection<Guid> streams, CancellationToken ct)
    {
        var guards = streams.ToDictionary(s => s, _ => -1);
        if (guards.Count == 0) return guards;
        await using var s = Store.QuerySession();
        foreach (var cp in await s.LoadManyAsync<ProjectionCheckpoint>(ct, streams.Select(id => ProjectionCheckpoint.MakeId(projektionsId, id))))
            guards[cp.StreamId] = cp.Version;
        return guards;
    }

    public void BeginneBatch()
    {
        _pending.Clear();   // neuer Batch → Vorlauf eines verworfenen Batches weg
        _stapelStream = null;
    }

    public void Stream(Guid streamId) => _stapelStream = streamId;

    // ── Commit-Punkt: Effekte + Guards + Partitions-Checkpoint in EINER Session, ein SaveChanges ──
    public async Task<int> CommitAsync(PartitionsCheckpoint stand, IReadOnlyDictionary<Guid, int> guards, CancellationToken ct)
    {
        try
        {
            await using var s = Store.IdentitySession();   // read-your-writes über alle Streams des Batches; Versionen der Loads für die OCC-Prüfung
            foreach (var op in _pending)
                await op(s);

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

            // Der Partitions-Checkpoint mit erwarteter Version (G-4): noch nie geschrieben → Insert (ein zweiter Erster scheitert am Schlüssel),
            //   sonst UpdateRevision auf Version + 1 (eine zweite Aktivierung oder ein Neuaufbau dazwischen → Versionskonflikt).
            var neu = new PartitionsCheckpoint
            {
                Id = stand.Id, ProjektionsId = stand.ProjektionsId, Partition = stand.Partition,
                Position = stand.Position, Geparkt = stand.Geparkt, UpdatedAt = jetzt, Version = stand.Version + 1
            };
            if (stand.Version == 0) s.Insert(neu);
            else s.UpdateRevision(neu, stand.Version + 1);

            await s.SaveChangesAsync(ct);   // EIN Commit → Effekte, Guards und Partition gemeinsam gültig
            return neu.Version;
        }
        catch (Exception ex) when (Konflikt(ex) is { } doku)
        {
            if (IstPartition(doku))
                throw new PartitionsKonfliktException(
                    $"{GetType().Name}: Partition {stand.Id} zwischenzeitlich geschrieben (erwartet v{stand.Version}) — Batch verworfen.", ex);
            throw new SchreibKonfliktException(
                $"{GetType().Name}: geteiltes Dokument {doku} zwischenzeitlich geändert (Partition {stand.Id}) — Batch verworfen.", ex);
        }
        finally
        {
            _pending.Clear();
            _stapelStream = null;
        }
    }

    // Marten 8 meldet die Versionsprüfung als JasperFx.ConcurrencyException (nicht Marten.Exceptions.ConcurrencyException),
    // ein gleichzeitiges Insert als DocumentAlreadyExistsException — beide gegen echtes Postgres gemessen (SchreibRegelPostgresTests), beide mit
    // dem Dokumenttyp (einmal als Name, einmal als Typ). Liefert den Namen des Dokuments im Konflikt (der Partitions-Checkpoint geht vor), sonst null.
    private static string? Konflikt(Exception ex) => ex switch
    {
        JasperFx.ConcurrencyException c => c.DocType ?? "?",
        DocumentAlreadyExistsException d => d.DocType?.FullName ?? "?",
        AggregateException agg when agg.InnerExceptions.Count > 0 && agg.InnerExceptions.All(i => Konflikt(i) != null)
            => agg.InnerExceptions.Select(Konflikt).FirstOrDefault(n => IstPartition(n!)) ?? Konflikt(agg.InnerExceptions[0]),
        _ => null
    };

    private static bool IstPartition(string dokument)
        => dokument == typeof(PartitionsCheckpoint).FullName || dokument == nameof(PartitionsCheckpoint);

    // ── Neuaufbau ──
    public async Task ResetAllAsync(string projektionsId, CancellationToken ct)
    {
        await using var s = Store.LightweightSession();
        await KonsumZuruecksetzen.PartitionenUndGuardsAsync(s, projektionsId, ct);
        await s.SaveChangesAsync(ct);
    }
}
