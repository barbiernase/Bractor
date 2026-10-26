namespace Infrastructure.PubSub;

using Infrastructure.Lebenszyklus;
using Infrastructure.PubSub.Messages;
using Proto;
using Proto.Cluster;

/// <summary>
/// Die Abonnements eines Subscribers beim Broker, über den <see cref="Bote"/>: Anmelden ist eine <c>Frage</c> (die Quittung zählt, begrenzt
/// wiederholt), Abmelden beim Stoppen eine <c>Meldung</c> (verlierbar — ein vergessenes Abo zeigt auf eine tote PID, der Broker stellt
/// dorthin ins Leere).
/// </summary>
public class BrokerSubscription
{
    private readonly Bote _bote;
    private readonly string _subscriberId;
    private readonly PID _subscriberPid;
    private readonly List<Type> _subscribedTypes = new();

    public BrokerSubscription(Cluster cluster, string subscriberId, PID subscriberPid)
    {
        ArgumentNullException.ThrowIfNull(cluster);
        _bote = new Bote(cluster);
        _subscriberId = subscriberId ?? throw new ArgumentNullException(nameof(subscriberId));
        _subscriberPid = subscriberPid ?? throw new ArgumentNullException(nameof(subscriberPid));
    }

    /// <summary>Abonniert einen Message-Typ; scheitert laut, wenn der Shard in der Wiederholungsfrist nicht quittiert.</summary>
    public async Task SubscribeAsync(Type messageType, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(messageType);
        await Extensions.ClusterExtensions.FrageMitWiederholungAsync<Ack>(_bote, BrokerIdentity.ShardFor(messageType, _subscriberId),
            new Subscribe(_subscriberId, _subscriberPid), $"{_subscriberId} abonniert {messageType.Name}", ct);
        if (!_subscribedTypes.Contains(messageType))
            _subscribedTypes.Add(messageType);
    }

    /// <summary>Abonniert mehrere Message-Typen.</summary>
    public async Task SubscribeAsync(IEnumerable<Type> messageTypes, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(messageTypes);
        foreach (var type in messageTypes)
            await SubscribeAsync(type, ct);
    }

    /// <summary>
    /// Beendet ein Abonnement und wartet (begrenzt) auf die Quittung — für eine Sitzung, die gleich wieder abonnieren kann: das Abmelden darf
    /// ein späteres Anmelden nicht überholen. True = der Shard hat quittiert.
    /// </summary>
    public async Task<bool> UnsubscribeAsync(Type messageType, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(messageType);
        _subscribedTypes.Remove(messageType);
        return await _bote.FrageAsync<Ack>(BrokerIdentity.ShardFor(messageType, _subscriberId), new Unsubscribe(_subscriberId),
            Extensions.ClusterExtensions.VersuchsFrist, ct) is not null;
    }

    /// <summary>Meldet alle Abonnements ab, ohne zu warten (beim Stoppen eines Actors).</summary>
    public void AlleAbmelden()
    {
        foreach (var type in _subscribedTypes)
            _bote.Melde(BrokerIdentity.ShardFor(type, _subscriberId), new Unsubscribe(_subscriberId));
        _subscribedTypes.Clear();
    }

    /// <summary>Liste der abonnierten Typen</summary>
    public IReadOnlyList<Type> SubscribedTypes => _subscribedTypes;
}
