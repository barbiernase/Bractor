namespace Infrastructure.PubSub.Actors;

using Infrastructure.Lebenszyklus;
using Infrastructure.PubSub.Messages;
using Microsoft.Extensions.Logging;
using Proto;
using Proto.Cluster;

/// <summary>
/// Aktiviert die Shards eines Message-Typs beim Start (Identity: "{MessageTypeName}"): je Shard eine <c>Frage</c> mit Frist — sie platziert
/// den virtuellen Shard-Actor. Ein Shard, der später mit seinem Knoten stirbt, wird bei der nächsten Anfrage (Publish, Subscribe) wieder
/// aktiviert; sein Abo-Verzeichnis war ohnehin im Speicher (die Sitzungen melden sich neu an, Projektionen heilt der Poll).
/// </summary>
public class BrokerManagerActor : RahmenActor
{
    /// <summary>Frist je Shard-Aktivierung.</summary>
    private static readonly TimeSpan AktivierFrist = TimeSpan.FromSeconds(10);

    private readonly ILogger<BrokerManagerActor>? _logger;
    private string _typeName = string.Empty;
    private Bote? _bote;
    private bool _activated;

    public BrokerManagerActor(ILogger<BrokerManagerActor>? logger = null) : base(logger)
    {
        _logger = logger;
    }

    protected override Task BeginneAsync(IContext context)
    {
        _typeName = context.ClusterIdentity()?.Identity ?? "unknown";
        _bote = new Bote(context.Cluster(), _logger);
        _logger?.LogInformation("[Manager {Type}] Started", _typeName);
        return Task.CompletedTask;
    }

    protected override async Task BehandleAsync(IContext context)
    {
        if (context.Message is not Activate) return;
        if (!_activated)
        {
            _logger?.LogInformation("[Manager {Type}] Activating {Count} shards...", _typeName, PubSubConfiguration.ShardCount);
            for (var i = 0; i < PubSubConfiguration.ShardCount; i++)
            {
                var shard = ClusterIdentity.Create($"{_typeName}_{i}", PubSubConfiguration.ShardKind);
                if (await _bote!.FrageAsync<SubscriberCountResponse>(shard, new GetSubscriberCount(), AktivierFrist, Stopp) is null)
                    _logger?.LogError("[Manager {Type}] Shard {Index} nicht in {Frist} s aktiviert", _typeName, i, AktivierFrist.TotalSeconds);
            }
            _activated = true;
            _logger?.LogInformation("[Manager {Type}] All shards activated", _typeName);
        }
        context.Respond(new Ack());
    }
}
