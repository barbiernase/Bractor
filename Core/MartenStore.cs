using Abstractions;
using Marten;

namespace Core;

/// <summary>
/// Der Marten-Store der <b>Klasse 1</b> (<see cref="IAtomaresZiel"/>, Plan Pub/Sub-Neustrukturierung §2.4) — die technikspezifische, in ALLEN
/// Marten-Stores identische Exactly-once-Maschinerie an EINER Stelle. Wirkungen werden nur GEPUFFERT; <see cref="SchreibeMitFortschrittAsync"/>
/// spielt die Wirkungen eines Batches (viele Streams einer Partition), die Guards seiner Streams und den Fortschritt der Partition mit erwarteter
/// Revision in EINER Marten-<c>IdentitySession</c> ab und committet mit EINEM <c>SaveChangesAsync</c> → Wirkungen und Fortschritt werden gemeinsam
/// durabel (genau einmal, auch für nicht-idempotente Wirkungen), und eine zweite Aktivierung derselben Partition verliert (G-4).
///
/// <b>Schreib-Regel (Single-Writer je Dokument).</b> Die Konsum-Maschine garantiert genau EINEN Schreiber je (Projektion, Partition) und damit je
/// (Projektion, Stream). Die Transaktion macht einen Batch atomar, isoliert aber nicht die Batches zweier Partitionen gegeneinander. Darum gibt es
/// drei Arten von Wirkungen, und jede wird beim Puffern gegen den Stream des laufenden Events (<see cref="Stream"/>) geprüft:
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
/// <b>Neuaufbau:</b> für Klasse 1 ist eine neue Generation das Leeren der eigenen Dokument-Typen (der Sonderfall aus A4) — Wirkungen einer alten
/// Aktivierung werden ohnehin nur mit ihrem Fortschritt gültig, und der verliert gegen den zurückgesetzten. Welche Typen dem Store gehören, nennt
/// er im Konstruktor (Code-Fakt); schreibt er einen anderen, wirft er sofort.
///
/// Konkrete Stores erben und schreiben nur noch ihre FACHLICHEN Methoden. Kein Domänenwissen hier, kein Generat — reiner Marten-Adapter. SCOPED
/// registriert: eine Instanz je Fähigkeits-Bereich (je Partitions-Leser → isolierter Puffer).
/// </summary>
public abstract class MartenStore : IAtomaresZiel
{
    /// <summary>Der geteilte Document-Store — für die Lese-Methoden (eigene Query-Sessions, committete Daten).</summary>
    protected IDocumentStore Store { get; }

    private readonly List<Func<IDocumentSession, Task>> _pending = new();
    private readonly Type[] _dokumente;

    /// <summary>Der Stream des laufenden Events (gesetzt in <see cref="Stream"/>); null = kein Batch offen.</summary>
    private Guid? _stapelStream;

    /// <param name="dokumente">Die Read-Model-Typen, die dieser Store schreibt — eine neue Generation leert sie.</param>
    protected MartenStore(IDocumentStore store, params Type[] dokumente)
    {
        Store = store;
        _dokumente = dokumente;
    }

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
        PruefeTyp(typeof(T));
        _pending.Add(async s => Geteilt(s, await s.LoadAsync<T>(id), aendern));
        return Task.CompletedTask;
    }

    /// <inheritdoc cref="EnqueueGeteilt{T}(string, Func{T, T})"/>
    protected Task EnqueueGeteilt<T>(Guid id, Func<T?, T?> aendern) where T : class, IGeteiltesReadModel
    {
        PruefeTyp(typeof(T));
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

    /// <summary>Der Store schreibt nur Typen, die er im Konstruktor nennt — sonst ließe eine neue Generation sie stehen.</summary>
    private void PruefeTyp(Type dokument)
    {
        if (Array.IndexOf(_dokumente, dokument) < 0)
            throw new InvalidOperationException(
                $"{GetType().Name} schreibt {dokument.Name}, nennt es aber nicht in seinen Dokument-Typen (Konstruktor) — ein Neuaufbau würde es nicht leeren.");
    }

    private void PruefeEigen(Guid id, Type dokument)
    {
        PruefeTyp(dokument);
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
    // Klasse 1: Fortschritt im Store, Wirkungen und Fortschritt in einer Transaktion
    // ─────────────────────────────────────────────────────────────

    public Task<PartitionsCheckpoint> LadeFortschrittAsync(string projektion, int partition, CancellationToken ct)
        => MartenFortschritt.LadeAsync(Store, projektion, partition, ct);

    public Task<IReadOnlyDictionary<Guid, int>> LadeGuardsAsync(string projektion, IReadOnlyCollection<Guid> streams, CancellationToken ct)
        => MartenFortschritt.LadeGuardsAsync(Store, projektion, streams, ct);

    public Task FortschrittZuruecksetzenAsync(string projektion, int partitionen, string generation, CancellationToken ct)
        => MartenFortschritt.ZuruecksetzenAsync(Store, projektion, partitionen, generation, ct);

    public void Beginne(string generation)
    {
        _pending.Clear();   // neuer Batch → Vorlauf eines verworfenen Batches weg
        _stapelStream = null;
    }

    public void Stream(Guid streamId) => _stapelStream = streamId;

    // ── Commit-Punkt: Wirkungen + Guards + Fortschritt in EINER Session, ein SaveChanges ──
    public async Task<int> SchreibeMitFortschrittAsync(PartitionsCheckpoint fortschritt, IReadOnlyDictionary<Guid, int> guards, CancellationToken ct)
    {
        try
        {
            await using var s = Store.IdentitySession();   // read-your-writes über alle Streams des Batches; Versionen der Loads für die OCC-Prüfung
            foreach (var op in _pending)
                await op(s);
            var neu = MartenFortschritt.Lege(s, fortschritt, guards);
            await s.SaveChangesAsync(ct);   // EIN Commit → Wirkungen, Guards und Partition gemeinsam gültig
            return neu;
        }
        catch (Exception ex) when (MartenFortschritt.Konflikt(ex, fortschritt, GetType().Name) is { } konflikt)
        {
            throw konflikt;
        }
        finally
        {
            _pending.Clear();
            _stapelStream = null;
        }
    }

    /// <summary>Die Wirkungen ohne Fortschritt — der Batch-Leser ruft für Klasse 1 nur <see cref="SchreibeMitFortschrittAsync"/>.</summary>
    public async Task SchreibeAsync(CancellationToken ct)
    {
        try
        {
            await using var s = Store.IdentitySession();
            foreach (var op in _pending)
                await op(s);
            await s.SaveChangesAsync(ct);
        }
        finally
        {
            _pending.Clear();
            _stapelStream = null;
        }
    }

    // ── Neuaufbau: die eigenen Dokument-Typen leeren (Klasse 1, A4) ──
    public async Task NeueGenerationAsync(string generation, CancellationToken ct)
    {
        foreach (var t in _dokumente)
            await Store.Advanced.Clean.DeleteDocumentsByTypeAsync(t, ct);
    }

    public Task UmschaltenAsync(string generation, CancellationToken ct) => Task.CompletedTask;
}
