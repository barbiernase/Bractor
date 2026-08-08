namespace Infrastructure.PubSub.Startup;

/// <summary>
/// Namen von Subscribern (wie in GeneratedSubscribers.GetSubscriberSpawnInfos), die
/// NICHT über den Push-Pfad gespawnt werden, weil sie auf den Pull-Pfad (Signal → Adapter)
/// migriert wurden. Die Liste füllt der Pull-Pfad-Generator (AddGeneratedPullPaths) aus den
/// mit IPullSubscriber markierten Projektionen — ohne diesen Ausschluss liefen Push
/// und Pull parallel.
/// </summary>
public sealed class PushSubscriberExclusions
{
    private readonly HashSet<string> _names;

    public PushSubscriberExclusions(IEnumerable<string> names)
        => _names = new HashSet<string>(names, StringComparer.Ordinal);

    public bool IsExcluded(string subscriberName) => _names.Contains(subscriberName);
}
