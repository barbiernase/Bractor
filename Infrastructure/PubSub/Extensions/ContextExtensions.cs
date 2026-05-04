namespace Infrastructure.PubSub.Extensions;

using Proto;
using Proto.Cluster;

public static class ContextExtensions
{
    /// <summary>
    /// Erstellt eine BrokerSubscription für den aktuellen Actor
    /// </summary>
    public static BrokerSubscription CreateBrokerSubscription(
        this IContext context, 
        string subscriberId)
    {
        var cluster = context.System.Cluster();
        return new BrokerSubscription(cluster, subscriberId, context.Self);
    }

    /// <summary>
    /// Erstellt einen BrokerPublisher
    /// </summary>
    public static BrokerPublisher CreateBrokerPublisher(this IContext context)
    {
        var cluster = context.System.Cluster();
        return new BrokerPublisher(cluster);
    }
}