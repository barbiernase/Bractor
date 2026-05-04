namespace Infrastructure.PubSub;

public static class PubSubConfiguration
{
    public const int ShardCount = 3;
    public const string ManagerKind = "BrokerManager";
    public const string ShardKind = "BrokerShard";
}