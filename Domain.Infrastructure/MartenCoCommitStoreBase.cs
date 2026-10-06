using Abstractions;
using Marten;
using Marten.Exceptions;

namespace Domain.Infrastructure;

/// <summary>
/// Marten-Implementierung der Co-Commit-Naht (<see cref="ICoCommitTracker"/>) — die
/// technik­spezifische, in ALLEN Projektions-Stores identische Exactly-once-Maschinerie an EINER
/// auditierten Stelle. Write-Effekte werden nur GEPUFFERT; <see cref="MarkProcessedAsync"/> spielt
/// sie + den <see cref="ProjectionCheckpoint"/> in EINER Marten-<c>IdentitySession</c> ab und
/// committet mit EINEM <c>SaveChangesAsync</c> → Effekt und Marke werden gemeinsam durabel
/// (exactly-once, auch für nicht-idempotente Append-Effekte).
///
/// <b>Schreib-Regel (Single-Writer je Dokument).</b> Die Konsum-Maschine garantiert genau EINEN Schreiber je
/// (Projektion, Stream). Das Co-Commit macht einen Stapel atomar, isoliert aber nicht zwei Stapel verschiedener
/// Streams gegeneinander. Darum gibt es drei Arten von Effekten, und jede wird beim Puffern geprüft:
/// <list type="bullet">
///   <item><b>Eigenes Dokument</b> (Schlüssel = Stream des Stapels): <see cref="EnqueueStore{T}"/>,
///     <see cref="EnqueueTransform{T}"/>, <see cref="EnqueueAnlegenOderAendern{T}"/> — sicher per Konstruktion.</item>
///   <item><b>Zeile dieses Streams</b> (zusammengesetzte Id, z. B. „Datensatz × Paar“): <see cref="EnqueueZeile{T}"/>,
///     <see cref="EnqueueZeileEntfernen{T}"/> — zwei Streams berühren nie dieselbe Zeile.</item>
///   <item><b>Geteiltes Dokument</b> (<see cref="IGeteiltesReadModel"/>, aus mehreren Streams beschrieben):
///     <see cref="EnqueueGeteilt{T}(string, Func{T, T})"/> — Lesen → Ändern → Schreiben mit Versionsprüfung.
///     Ein Konflikt verwirft den ganzen Stapel samt Marke (<see cref="SchreibKonfliktException"/>), der Adapter
///     wiederholt ihn.</item>
/// </list>
/// Ein Schreibzugriff auf das Dokument eines FREMDEN Streams über die ersten beiden Wege wirft sofort — statt still
/// ein Update eines parallel laufenden Stapels zu überschreiben.
///
/// Konkrete Stores erben und schreiben nur noch ihre FACHLICHEN Write-Methoden. Kein Domänenwissen hier, kein
/// Generat — reiner Marten-Adapter. SCOPED registriert: eine Instanz je Fähigkeits-Bereich (je Stream-Actor →
/// isolierter Puffer).
/// </summary>
public abstract class MartenCoCommitStoreBase : ICoCommitTracker
{
    /// <summary>Der geteilte Document-Store — für die Lese-Methoden (eigene Query-Sessions, committete Daten).</summary>
    protected IDocumentStore Store { get; }

    private readonly List<Func<IDocumentSession, Task>> _pending = new();

    /// <summary>Der Stream des offenen Stapels (gesetzt in <see cref="LastProcessedVersionAsync"/>); null = kein Stapel.</summary>
    private Guid? _stapelStream;

    protected MartenCoCommitStoreBase(IDocumentStore store) => Store = store;

    // ─────────────────────────────────────────────────────────────
    // Eigenes Dokument: Schlüssel = Stream des Stapels
    // ─────────────────────────────────────────────────────────────

    /// <summary>Puffert: das eigene Dokument <paramref name="id"/> anlegen bzw. ersetzen. Gibt <c>Task.CompletedTask</c>
    /// zurück, damit fachliche Write-Methoden expression-bodied bleiben.</summary>
    protected Task EnqueueStore<T>(Guid id, T document) where T : notnull
    {
        PruefeEigen(id, typeof(T));
        _pending.Add(s => { s.Store(document); return Task.CompletedTask; });
        return Task.CompletedTask;
    }

    /// <summary>
    /// Puffert ein read-modify-store des eigenen Dokuments: lädt es im Batch (read-your-writes über die Identity-Map),
    /// transformiert und staged es. Fehlt es (Event vor dem erzeugenden Upsert), passiert nichts.
    /// </summary>
    protected Task EnqueueTransform<T>(Guid id, Func<T, T> transform) where T : notnull
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
    protected Task EnqueueAnlegenOderAendern<T>(Guid id, Func<T?, T> aendern) where T : class
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
    protected Task EnqueueZeile<T>(Guid besitzer, T zeile) where T : notnull
    {
        PruefeEigen(besitzer, typeof(T));
        _pending.Add(s => { s.Store(zeile); return Task.CompletedTask; });
        return Task.CompletedTask;
    }

    /// <summary>Puffert: die Zeile <paramref name="id"/> des Streams <paramref name="besitzer"/> entfernen.</summary>
    protected Task EnqueueZeileEntfernen<T>(Guid besitzer, string id) where T : notnull
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
                $"{GetType().Name}: Schreib-Effekt auf {dokument.Name} außerhalb eines Stapels — Stores schreiben nur aus einer Projektion.");
        if (id != stream)
            throw new InvalidOperationException(
                $"{GetType().Name}: {dokument.Name} {id} gehört nicht dem Stream des Stapels ({stream}). Ein Dokument, das " +
                "mehrere Streams beschreiben, als Zeilen je Stream ablegen (EnqueueZeile) oder als IGeteiltesReadModel " +
                "über EnqueueGeteilt — sonst überschreiben sich parallele Stapel.");
    }

    // ── Marke lesen: reiner Read, klammert den Batch-Beginn ──
    public async Task<int> LastProcessedVersionAsync(string projectionId, Guid streamId, CancellationToken ct)
    {
        _pending.Clear();   // neuer Batch → alten Vorlauf verwerfen
        _stapelStream = streamId;
        await using var s = Store.QuerySession();
        var doc = await s.LoadAsync<ProjectionCheckpoint>(
            ProjectionCheckpoint.MakeId(projectionId, streamId), ct);
        return doc?.Version ?? -1;
    }

    // ── Commit-Punkt: Effekt(e) + Marke in EINER Session, ein SaveChanges ──
    public async Task MarkProcessedAsync(string projectionId, Guid streamId, int version, CancellationToken ct)
    {
        await using var s = Store.IdentitySession();   // read-your-writes im Batch; Versionen der Loads für die OCC-Prüfung
        foreach (var op in _pending)
            await op(s);

        s.Store(new ProjectionCheckpoint
        {
            Id = ProjectionCheckpoint.MakeId(projectionId, streamId),
            ProjectionId = projectionId,
            StreamId = streamId,
            Version = version,
            UpdatedAt = DateTimeOffset.UtcNow
        });

        try
        {
            await s.SaveChangesAsync(ct);   // EIN Commit → Effekt(e) + Marke gemeinsam gültig
        }
        catch (Exception ex) when (IstKonflikt(ex))
        {
            _pending.Clear();
            throw new SchreibKonfliktException(
                $"{GetType().Name}: geteiltes Dokument zwischenzeitlich geändert (Stream {streamId}, bis v{version}) — Stapel verworfen.", ex);
        }
        _pending.Clear();
    }

    // Marten 8 meldet die Versionsprüfung als JasperFx.ConcurrencyException (nicht Marten.Exceptions.ConcurrencyException),
    // ein gleichzeitiges Insert als DocumentAlreadyExistsException — beide gegen echtes Postgres gemessen (SchreibRegelPostgresTests).
    private static bool IstKonflikt(Exception ex) => ex switch
    {
        JasperFx.ConcurrencyException or DocumentAlreadyExistsException => true,
        AggregateException agg => agg.InnerExceptions.Count > 0 && agg.InnerExceptions.All(IstKonflikt),
        _ => false
    };

    // ── Replay / Rebuild ──
    public async Task ResetAsync(string projectionId, Guid streamId, CancellationToken ct)
    {
        await using var s = Store.LightweightSession();
        s.Delete<ProjectionCheckpoint>(ProjectionCheckpoint.MakeId(projectionId, streamId));
        await s.SaveChangesAsync(ct);
    }

    public async Task ResetAllAsync(string projectionId, CancellationToken ct)
    {
        await using var s = Store.LightweightSession();
        s.DeleteWhere<ProjectionCheckpoint>(c => c.ProjectionId == projectionId);
        await s.SaveChangesAsync(ct);
    }
}
