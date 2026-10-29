using System;
using System.Threading;
using System.Threading.Tasks;

namespace Abstractions;

/// <summary>
/// Abgeleiteter, NICHT-autoritativer Zustands-Cache eines Aggregats (docs/04-konsum-und-prozess-maschine.md §4.8).
/// Verkürzt die Rehydration von O(n) auf O(Tail): statt den Stream bei jeder Actor-Aktivierung von 0 zu falten,
/// seedet der Actor aus dem Snapshot und liest nur den Rest ab <see cref="Version"/>+1.
///
/// Die Wahrheit bleibt der Log (Invariante 1): fehlt oder veraltet der Snapshot, ist das ein Cache-Miss —
/// Voll-Replay, nie ein Fehler. Ein Dokument pro Aggregat (Id = AggregateId), abgelegt als jsonb pro
/// State-Typ (<c>Snapshot&lt;Konto&gt;</c> → <c>mt_doc_snapshot_konto</c>).
///
/// WICHTIG: <see cref="Version"/> zählt ALLE Events inkl. der Events des Rahmens (<see cref="IRahmenEvent"/>, etwa <c>FristGesetzt</c>), damit der Tail-Read bündig
/// aufsetzt und die OCC-Assertion beim nächsten Append passt. Die Inbox reist nicht mit — sie ist eine eigene Tabelle des Stores (docs/04 §4.6).
/// </summary>
public sealed class Snapshot<TState> where TState : class, IState
{
    /// <summary>Identität = die Aggregat-GUID (die ClusterIdentity des Actors).</summary>
    public Guid Id { get; set; }

    /// <summary>Stream-Position, die dieser Snapshot repräsentiert — inkl. der Events des Rahmens.</summary>
    public int Version { get; set; }

    /// <summary>
    /// Struktur-Version des State-Typs (generierter Struktur-Hash). Stimmt sie beim Laden nicht mit der
    /// erwarteten überein, wird der Snapshot ignoriert und voll replayt (Schema-Evolution, Invariante 1).
    /// </summary>
    public string SchemaVersion { get; set; } = "";

    /// <summary>Der gefaltete Domänen-Zustand.</summary>
    public TState State { get; set; } = default!;

    /// <summary>Zeitpunkt des Snapshots (Diagnose/Betrieb).</summary>
    public DateTimeOffset UpdatedAt { get; set; }
}

/// <summary>
/// Actor-Tuning, in DI registriert und in den Aggregat-Actor injiziert.
/// <see cref="Threshold"/> = Events zwischen zwei Snapshots; der Actor schreibt best-effort nach je so vielen
/// Events. 0 schaltet das Schwellwert-Schreiben ab (nur der Stopping-Hook bleibt).
/// <see cref="InboxCache"/> = so viele CommandIds je Ausgang merkt sich ein aktiver Actor (Cache vor der Inbox-Tabelle, nie Semantik:
/// was er vergisst, fängt der eindeutige Schlüssel der Tabelle). <see cref="BuendelGroesse"/> = so viele wartende Commands entscheidet der Actor
/// höchstens gemeinsam und hängt sie in einem Append an (docs/03 §3.1); 1 = jeder einzeln. Nicht registriert → der Actor nutzt seine Defaults.
/// </summary>
public sealed record SnapshotOptions(
    int Threshold = 200, int InboxCache = SnapshotOptions.StandardInboxCache, int BuendelGroesse = SnapshotOptions.StandardBuendelGroesse)
{
    public const int StandardInboxCache = 1_000;
    public const int StandardBuendelGroesse = 64;
}

/// <summary>
/// Ablage-Naht für Aggregat-Snapshots. Reines Vertragsinterface — die konkrete Ablage (Marten-jsonb,
/// In-Memory) ist Store-Sache, analog <see cref="IFortschrittsAblage"/> / <see cref="IPollCursorStore"/>.
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
