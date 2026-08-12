using Abstractions;

namespace Infrastructure.Projections;

/// <summary>
/// Der GA-1-Check (P4, Boot-/DI-Zeit): erzwingt, dass eine <see cref="IAppendProjektion"/> einen
/// ECHTEN Co-Commit-Tracker (<see cref="ICoCommitTracker"/>) mitbringt. Wird von der generierten
/// Adapter-Kind-Factory beim ersten Spawn aufgerufen — genau dort, wo die Stores des Konsumenten
/// schon aufgelöst sind.
///
/// Verschärft (2026-08-12): Geprüft wird <see cref="ICoCommitTracker"/>, NICHT bloß „Tracker
/// existiert". Ein bloßer <see cref="IProjectionTracker"/> (z. B. der bewusst dual-writing
/// Fallback, oder ein write-through-Effekt neben einem separaten Tracker) ist gerade NICHT
/// co-committend → er würde still at-least-once laufen (false-green). Nur die Atomaritäts-Zusage
/// <see cref="ICoCommitTracker"/> besteht den Check. Analyse: docs/konzept-exactly-once-naht.md.
///
/// Trennung der Achsen (bewusst): der GA-1-Check betrifft NUR replaybare Append-Projektionen. Ein
/// EMITTIERENDER Konsument (Reaktion/Pipeline-Event) trägt den Marker nie — seine Wirkung ist das
/// idempotente Command am Empfänger, nicht ein co-committetes Read-Model.
/// </summary>
public static class GaEinsPruefung
{
    /// <summary>
    /// Bricht mit klarer Meldung, wenn <paramref name="projektion"/> append-artig ist, aber kein
    /// <see cref="ICoCommitTracker"/> aufgelöst wurde (kein Tracker, oder nur ein
    /// <see cref="IProjectionTracker"/> ohne Atomaritäts-Zusage). Sonst folgenlos.
    /// </summary>
    public static void PrüfeCoCommit(object projektion, IProjectionTracker? tracker, string projektionsId)
    {
        if (projektion is IAppendProjektion && tracker is not ICoCommitTracker)
            throw new InvalidOperationException(
                $"GA-1 verletzt: '{projektionsId}' ist als IAppendProjektion markiert (append-artig, " +
                "Doppelverarbeitung korrumpiert), hat aber keinen ICoCommitTracker-Store. " +
                (tracker is null
                    ? "Es wurde gar kein IProjectionTracker aufgelöst. "
                    : $"Der aufgelöste Tracker ({tracker.GetType().Name}) implementiert nur IProjectionTracker, " +
                      "nicht ICoCommitTracker — er committet Effekt und Marke NICHT gemeinsam (at-least-once). ") +
                "Ohne echten Co-Commit drohen doppelte Appends. Behebung: der Store der Projektion muss " +
                "ICoCommitTracker implementieren (Effekt + Marke in EINER Transaktion, ein SaveChanges).");
    }
}
