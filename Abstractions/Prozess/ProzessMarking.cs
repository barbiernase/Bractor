using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Abstractions;

/// <summary>
/// Der abgeleitete, NICHT-autoritative Marking-Cache eines Prozesses (P5b, docs/prozess-marking-cursor-konzept.md).
/// Das prozess-lokale Analogon zum Aggregat-<see cref="Snapshot{TState}"/>: statt bei JEDER Weckung jeden
/// Ziel-Stream ab 0 zu falten (das O(N²) des <c>ProzessManager</c>-Folds), hält er je Ziel-Stream einen
/// <see cref="StreamCursor"/> und faltet nur den <b>Tail</b> nach — jedes Ziel-Event wird genau EINMAL gelesen
/// (O(N) statt O(N²) Stream-Reads).
///
/// Die Wahrheit bleibt der Log (Invariante 1): fehlt oder passt <see cref="RegelHash"/> nicht, ist das ein
/// Cache-Miss → der Manager faltet voll ab 0 (Fallback), das Ergebnis ist identisch. Und weil der Manager
/// ohnehin at-least-once feuert und das Ziel über den deterministischen Vorgang (= CommandId, Framework-Inbox)
/// dedupliziert, kann ein stale Cache höchstens eine Transition ERNEUT feuern (verpufft beim Empfänger) — nie
/// ein falscher Effekt. Der Cache ist ein Beschleuniger, kein Zustand.
///
/// Ein Dokument pro Korrelation (Id = Korrelation), überschreibend abgelegt. Marten-Registrierung nötig
/// (in der StoreOptions-Konfiguration): <c>opts.Schema.For&lt;ProzessMarking&gt;().Identity(x =&gt; x.Id);</c>
/// </summary>
public sealed class ProzessMarking
{
    /// <summary>Identität = die Korrelations-Guid des Prozesses.</summary>
    public Guid Id { get; set; }

    /// <summary>
    /// Struktur-Hash der Prozess-Regeln (deterministisch, compile-zeit-stabil). Ändern sich die Regeln,
    /// passt der Hash nicht mehr → der Cache wird verworfen und voll ab 0 gefaltet (Invalidierung, analog
    /// zur <see cref="Snapshot{TState}.SchemaVersion"/> der Aggregat-Snapshots).
    /// </summary>
    public string RegelHash { get; set; } = "";

    /// <summary>
    /// Stand des Manager-Entscheidungs-Logs (dessen Länge) beim letzten Fortschreiben. Reine Diagnose-/
    /// Kohärenz-Hilfe — die Entscheidungen selbst bleiben OCC im Log, nicht im Cache.
    /// </summary>
    public int LogVersion { get; set; }

    /// <summary>Die verdichtete Faltung: Cursor je Ziel-Stream + die aufgelösten Vorgänge.</summary>
    public MarkingKompakt Marking { get; set; } = new();

    /// <summary>Zeitpunkt des Fortschreibens (Diagnose/Betrieb).</summary>
    public DateTimeOffset UpdatedAt { get; set; }
}

/// <summary>
/// Die verdichtete Marking-Faltung (docs/…-konzept.md §2/§4). NICHT „alle rohen Tokens" (das brächte das O(N²)
/// zurück), sondern je Ziel-Stream ein Cursor plus je aufgelöstem Vorgang eine kompakte Marke mit den DREI
/// Achsen, die der Manager liest (aufgelöst/Wirkung/abgelehnt) und dem von einer Wirkung erzeugten Downstream-
/// Token (kompakt: Stream+Version+Payload), das einen Join scharf schalten kann.
/// </summary>
public sealed class MarkingKompakt
{
    /// <summary>Je Ziel-Stream: die zuletzt gefaltete (höchste gelesene) Version. Der Tail-Read setzt hier +1 auf.</summary>
    public Dictionary<Guid, int> StreamCursor { get; set; } = new();

    /// <summary>
    /// Je Vorgang (Schlüssel = CausationId-String = deterministischer Vorgang): die aufgelösten Achsen. Ein
    /// vorhandener Schlüssel bedeutet „aufgelöst" (irgendein Ziel-Event mit dieser Kausalität lag vor — auch
    /// die interne Noop-Marke). <see cref="VorgangMarke.Wirkung"/>/<see cref="VorgangMarke.Abgelehnt"/> tragen
    /// die beiden anderen Achsen.
    /// </summary>
    public Dictionary<string, VorgangMarke> Vorgänge { get; set; } = new();

    /// <summary>Der Payload des Wurzel-(Auslöse-)Events, einmalig gelesen und zwischengehalten (Token-Wurzel des Folds).</summary>
    public IEvent? AuslöserPayload { get; set; }

    /// <summary>Eine tiefe Kopie (der Voll-Fold darf den Cache nicht mutieren; der inkrementelle baut auf einer Kopie auf).</summary>
    public MarkingKompakt Kopie()
    {
        var k = new MarkingKompakt
        {
            StreamCursor = new Dictionary<Guid, int>(StreamCursor),
            Vorgänge = new Dictionary<string, VorgangMarke>(Vorgänge.Count),
            AuslöserPayload = AuslöserPayload,
        };
        foreach (var (id, m) in Vorgänge) k.Vorgänge[id] = m.Kopie();
        return k;
    }
}

/// <summary>
/// Die kompakte Marke EINES aufgelösten Vorgangs. Erhält die DREI Achsen (Backend-Handoff §4-Falle 2), an denen
/// <c>WakeAsync</c> Vorwärts/Kompensation/Terminal entscheidet:
///   • Aufgelöst (Schlüssel-Präsenz im <see cref="MarkingKompakt.Vorgänge"/>-Dict): irgendein Ziel-Event lag vor.
///   • <see cref="Wirkung"/>: ein DOMÄNEN-Event (kein <see cref="IProzessIntern"/>) — kompensierbar, aktiviert Joins.
///   • <see cref="Abgelehnt"/> (+ <see cref="Grund"/>): eine durable KommandoAbgelehnt-Marke (Treiber-Fold/EM-1).
/// Bei Wirkung trägt sie zusätzlich den erzeugten Downstream-Token (kompakt).
/// </summary>
public sealed class VorgangMarke
{
    /// <summary>Ein Domänen-Event mit dieser Kausalität lag vor (aktiviert Downstream-Joins, ist kompensierbar).</summary>
    public bool Wirkung { get; set; }

    /// <summary>Eine durable KommandoAbgelehnt-Marke mit dieser Kausalität lag vor (fachliche Ablehnung).</summary>
    public bool Abgelehnt { get; set; }

    /// <summary>Der getippte Ablehnungs-Grund (nur bei <see cref="Abgelehnt"/> gesetzt).</summary>
    public string Grund { get; set; } = "";

    /// <summary>Der Ziel-Stream des von der Wirkung erzeugten Downstream-Tokens.</summary>
    public Guid TokenStream { get; set; }

    /// <summary>Die Version des von der Wirkung erzeugten Downstream-Tokens.</summary>
    public int TokenVersion { get; set; }

    /// <summary>Der Payload des von der Wirkung erzeugten Downstream-Tokens (nur bei <see cref="Wirkung"/> gesetzt).</summary>
    public IEvent? TokenPayload { get; set; }

    public VorgangMarke Kopie() => new()
    {
        Wirkung = Wirkung,
        Abgelehnt = Abgelehnt,
        Grund = Grund,
        TokenStream = TokenStream,
        TokenVersion = TokenVersion,
        TokenPayload = TokenPayload,
    };
}

/// <summary>
/// Ablage-Naht für den Prozess-Marking-Cursor. Reines Vertragsinterface (analog <see cref="IEmittentenCursor"/>
/// / <see cref="ISnapshotStore"/>): die konkrete Ablage (Marten-jsonb, In-Memory) ist Store-Sache.
///
/// Best-effort — außerhalb der Entscheidungs-Transaktion. Ein Verlust/Fehler kostet nur die Beschleunigung
/// (Voll-Fold ab 0 heilt), nie Korrektheit; darum werfen Implementierungen ihre I/O-Fehler NICHT nach oben,
/// sondern schlucken sie (der Aufrufer soll durch einen Store-Ausfall nie hängen).
/// </summary>
public interface IProzessMarkingStore
{
    /// <summary>Lädt den Marking-Cache einer Korrelation (O(1) by-id) oder null bei Cache-Miss.</summary>
    Task<ProzessMarking?> LadeAsync(Guid korrelation, CancellationToken ct);

    /// <summary>Schreibt/überschreibt den Marking-Cache einer Korrelation (best-effort, ein aktueller je Korrelation).</summary>
    Task SchreibeAsync(ProzessMarking marking, CancellationToken ct);

    /// <summary>Löscht den Marking-Cache einer Korrelation (z. B. beim Terminal — der Prozess braucht ihn nie wieder).</summary>
    Task LöscheAsync(Guid korrelation, CancellationToken ct);
}
