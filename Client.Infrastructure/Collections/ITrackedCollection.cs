namespace Client.Infrastructure.Collections;

/// <summary>
/// Interface für Collections die Deltas akkumulieren.
///
/// StoreBase registriert alle TrackedMaps über dieses Interface
/// und ruft FlushDeltas() im Flush-Zyklus auf.
///
/// Rückgabetyp ist IReadOnlyList&lt;CollectionDelta&gt; — kein object-Boxing,
/// kein Reflection. Die View kann direkt per Pattern-Matching auf
/// Added&lt;TKey&gt;, Replaced&lt;TKey&gt; etc. reagieren.
/// </summary>
public interface ITrackedCollection
{
    /// <summary>
    /// Gibt alle seit dem letzten Flush akkumulierten Deltas zurück
    /// und leert die interne Delta-Liste.
    /// Leere Liste wenn keine Mutationen stattgefunden haben.
    /// </summary>
    IReadOnlyList<CollectionDelta> FlushDeltas();
}
