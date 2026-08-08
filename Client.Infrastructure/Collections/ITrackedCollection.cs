// ── Client.Infrastructure/Collections/ITrackedCollection.cs ──

namespace Client.Infrastructure.Collections;

/// <summary>
/// Interface für Collections die Mutations-Tracking unterstützen.
///
/// StoreBase registriert alle TrackedMaps über dieses Interface
/// und fragt im Flush-Zyklus ab, ob Mutationen stattgefunden haben.
///
/// Kein Delta-Tracking mehr — Blazor macht ohnehin einen
/// vollständigen Component-Diff. Ein boolean reicht.
/// </summary>
public interface ITrackedCollection
{
    /// <summary>
    /// Gibt true zurück wenn seit dem letzten Aufruf
    /// Schreiboperationen stattfanden. Setzt das Flag zurück.
    ///
    /// Wird von StoreBase.Flush() nach DispatchCompleted aufgerufen.
    /// </summary>
    bool ConsumeHasChanges();
}
