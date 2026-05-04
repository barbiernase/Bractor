namespace Infrastructure.PubSub.Actors;

using Infrastructure.PubSub.Messages;
using Microsoft.Extensions.Logging;
using Proto;
using Proto.Cluster;

/// <summary>
/// Verwaltet den Lifecycle der Shards für einen Message-Typ.
/// Identity: "{MessageTypeName}"
/// </summary>
public class BrokerManagerActor : IActor
{
    private readonly ILogger<BrokerManagerActor>? _logger;
    private readonly PID?[] _shardPids = new PID?[PubSubConfiguration.ShardCount];
    
    private string _typeName = string.Empty;
    private Cluster? _cluster;
    private bool _activated;

    public BrokerManagerActor(ILogger<BrokerManagerActor>? logger = null)
    {
        _logger = logger;
    }

    public async Task ReceiveAsync(IContext context)
    {
        switch (context.Message)
        {
            case Started:
                OnStarted(context);
                break;
                
            case Activate:
                await OnActivateAsync(context);
                break;
                
            case Terminated msg:
                await OnTerminatedAsync(context, msg.Who);
                break;
                
            case GetStatus:
                await OnGetStatusAsync(context);
                break;
        }
    }

    private void OnStarted(IContext context)
    {
        _typeName = context.ClusterIdentity()?.Identity ?? "unknown";
        _cluster = context.Cluster();
        
        _logger?.LogInformation("[Manager {Type}] Started", _typeName);
    }

    private async Task OnActivateAsync(IContext context)
    {
        if (_activated)
        {
            context.Respond(new Ack());
            return;
        }

        _logger?.LogInformation(
            "[Manager {Type}] Activating {Count} shards...", 
            _typeName, PubSubConfiguration.ShardCount);

        for (int i = 0; i < PubSubConfiguration.ShardCount; i++)
        {
            await ActivateShardAsync(context, i);
        }

        _activated = true;
        
        _logger?.LogInformation("[Manager {Type}] All shards activated", _typeName);
        
        context.Respond(new Ack());
    }

    private async Task ActivateShardAsync(IContext context, int index)
    {
        var identity = ClusterIdentity.Create(
            $"{_typeName}_{index}", 
            PubSubConfiguration.ShardKind
        );

        try
        {
            var pid = await _cluster!.GetAsync(identity, CancellationToken.None);
            
            if (pid != null)
            {
                _shardPids[index] = pid;
                context.Watch(pid);
                
                _logger?.LogDebug(
                    "[Manager {Type}] Shard {Index} activated", 
                    _typeName, index);
            }
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, 
                "[Manager {Type}] Failed to activate shard {Index}", 
                _typeName, index);
        }
    }

    private async Task OnTerminatedAsync(IContext context, PID who)
    {
        var index = Array.FindIndex(_shardPids, p => p?.Equals(who) == true);
        
        if (index < 0)
            return;

        _logger?.LogWarning(
            "[Manager {Type}] Shard {Index} terminated, restarting...", 
            _typeName, index);

        _shardPids[index] = null;
        
        await Task.Delay(100);
        await ActivateShardAsync(context, index);
    }

    private async Task OnGetStatusAsync(IContext context)
    {
        var shards = new List<ShardInfo>();

        for (int i = 0; i < PubSubConfiguration.ShardCount; i++)
        {
            var isAlive = _shardPids[i] != null;
            var count = 0;

            if (isAlive)
            {
                try
                {
                    var identity = ClusterIdentity.Create(
                        $"{_typeName}_{i}", 
                        PubSubConfiguration.ShardKind
                    );
                    
                    var response = await _cluster!.RequestAsync<SubscriberCountResponse>(
                        identity, 
                        new GetSubscriberCount(), 
                        CancellationToken.None
                    );
                    
                    count = response?.Count ?? 0;
                }
                catch
                {
                    // Ignore
                }
            }

            shards.Add(new ShardInfo(i, isAlive, count));
        }

        context.Respond(new BrokerStatus(typeof(object), PubSubConfiguration.ShardCount, shards));
    }
}