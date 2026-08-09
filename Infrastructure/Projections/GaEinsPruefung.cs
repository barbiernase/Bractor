using Abstractions;

namespace Infrastructure.Projections;

/// <summary>
/// Der GA-1-Check (P4, Boot-/DI-Zeit): erzwingt, dass eine <see cref="IAppendProjektion"/> einen
/// Co-Commit-<see cref="IProjectionTracker"/> mitbringt. Wird von der generierten Adapter-Kind-Factory
/// beim ersten Spawn aufgerufen — genau dort, wo die Stores des Konsumenten schon aufgelöst sind.
///
/// Trennung der Achsen (bewusst): der GA-1-Check betrifft NUR replaybare Append-Projektionen. Ein
/// EMITTIERENDER Konsument (Reaktion/Pipeline-Event) trägt den Marker nie — seine Wirkung ist das
/// idempotente Command am Empfänger, nicht ein co-committetes Read-Model.
/// </summary>
public static class GaEinsPruefung
{
    /// <summary>
    /// Bricht mit klarer Meldung, wenn <paramref name="projektion"/> append-artig ist, aber kein
    /// Co-Commit-Tracker aufgelöst wurde. Sonst folgenlos.
    /// </summary>
    public static void PrüfeCoCommit(object projektion, IProjectionTracker? tracker, string projektionsId)
    {
        if (projektion is IAppendProjektion && tracker is null)
            throw new InvalidOperationException(
                $"GA-1 verletzt: '{projektionsId}' ist als IAppendProjektion markiert (append-artig, " +
                "Doppelverarbeitung korrumpiert), hat aber keinen Co-Commit-IProjectionTracker-Store. " +
                "Ohne Co-Commit läuft sie at-least-once → doppelte Appends. Behebung: der Store der " +
                "Projektion muss IProjectionTracker implementieren (Effekt + Marke in EINER Transaktion).");
    }
}
