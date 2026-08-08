using System;
using System.Threading;
using System.Threading.Tasks;

namespace Abstractions;

/// <summary>
/// Abgeleiteter, NICHT-autoritativer Zustands-Cache eines Aggregats (Snapshot-Konzept, docs/snapshot-konzept.md).
/// Verkürzt die Rehydration von O(n) auf O(Tail): statt den Stream bei jeder Actor-Aktivierung von 0 zu falten,
/// seedet der Actor aus dem Snapshot und liest nur den Rest ab <see cref="Version"/>+1.
///
/// Die Wahrheit bleibt der Log (Invariante 1): fehlt oder veraltet der Snapshot, ist das ein Cache-Miss —
/// Voll-Replay, nie ein Fehler. Ein Dokument pro Aggregat (Id = AggregateId), abgelegt als jsonb pro
/// State-Typ (<c>Snapshot&lt;Konto&gt;</c> → <c>mt_doc_snapshot_konto</c>).
///
/// WICHTIG — zwei nicht-offensichtliche Felder (sonst wäre der Snapshot falsch):
/// - <see cref="ProcessedCommandIds"/>: die Framework-Inbox (verarbeitete CommandIds des idempotenten
///   Reaktions-/Prozess-Pfads). Ohne sie ginge die Dedup vor <see cref="Version"/> verloren → Doppeleffekt.
/// - <see cref="Version"/>: zählt ALLE Events inkl. der internen Marken (<see cref="IProzessIntern"/>),
///   damit der Tail-Read bündig aufsetzt und die OCC-Assertion beim nächsten Append passt.
/// </summary>
public sealed class Snapshot<TState> where TState : class, IState
{
    /// <summary>Identität = die Aggregat-GUID (die ClusterIdentity des Actors).</summary>
    public Guid Id { get; set; }

    /// <summary>Stream-Position, die dieser Snapshot repräsentiert — inkl. der internen Marken.</summary>
    public int Version { get; set; }

    /// <summary>
    /// Struktur-Version des State-Typs (generierter Struktur-Hash). Stimmt sie beim Laden nicht mit der
    /// erwarteten überein, wird der Snapshot ignoriert und voll replayt (Schema-Evolution, Invariante 1).
    /// </summary>
    public string SchemaVersion { get; set; } = "";

    /// <summary>Der gefaltete Domänen-Zustand.</summary>
    public TState State { get; set; } = default!;

    /// <summary>Die Framework-Inbox zum Zeitpunkt des Snapshots (idempotenter Pfad, Dedup per CommandId).</summary>
    public Guid[] ProcessedCommandIds { get; set; } = Array.Empty<Guid>();

    /// <summary>Zeitpunkt des Snapshots (Diagnose/Betrieb).</summary>
    public DateTimeOffset UpdatedAt { get; set; }
}

/// <summary>
/// Actor-Tuning, in DI registriert und in den Aggregat-Actor injiziert.
/// <see cref="Threshold"/> = Events zwischen zwei Snapshots; der Actor schreibt best-effort nach je so vielen
/// Events. 0 schaltet das Schwellwert-Schreiben ab (nur der Stopping-Hook bleibt).
/// <see cref="InboxCap"/> (Audit-Fix #4/H3) = harte Obergrenze der Framework-Inbox (verarbeitete CommandIds
/// des idempotenten Pfads): nur die letzten so vielen Ids bleiben in Speicher UND Snapshot, statt monoton zu
/// wachsen. Praktisch sicher wegen des kurzen Re-Delivery-Fensters (siehe <c>BoundedInbox</c>).
/// Nicht registriert → der Actor nutzt seine Defaults.
/// </summary>
public sealed record SnapshotOptions(int Threshold = 200, int InboxCap = 10_000);

/// <summary>
/// Ablage-Naht für Aggregat-Snapshots. Reines Vertragsinterface — die konkrete Ablage (Marten-jsonb,
/// In-Memory) ist Store-Sache, analog <see cref="IProjectionTracker"/> / <see cref="IPollCursorStore"/>.
/// Generisch, damit die Registrierung reflection-frei generiert werden kann und der Store selbst winzig bleibt.
/// </summary>
public interface ISnapshotStore
{
    /// <summary>Lädt den Snapshot eines Aggregats (O(1) by-id) oder null, wenn keiner existiert.</summary>
    Task<Snapshot<TState>?> TryLoadAsync<TState>(Guid id, CancellationToken ct)
        where TState : class, IState, new();

    /// <summary>Schreibt/überschreibt den Snapshot eines Aggregats (ein aktueller je Aggregat).</summary>
    Task SaveAsync<TState>(Snapshot<TState> snapshot, CancellationToken ct)
        where TState : class, IState, new();

    /// <summary>Löscht den Snapshot eines Aggregats (Replay/Rebuild-Kohärenz).</summary>
    Task DeleteAsync<TState>(Guid id, CancellationToken ct)
        where TState : class, IState, new();
}
