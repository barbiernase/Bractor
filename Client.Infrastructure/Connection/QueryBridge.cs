using Abstractions;
using Client.Infrastructure.Abstractions;
using Client.Infrastructure.Bus;
using Client.Infrastructure.Versioning;

namespace Client.Infrastructure.Connection;

/// <summary>
/// Query-Fehler wenn der Server nicht antwortet oder einen Fehler schickt.
/// </summary>
public record QueryFailed(string QueryType, string ErrorMessage) : IClientEvent;

/// <summary>
/// Brücke zwischen Bus-Queries und gRPC-Server.
///
/// Kein Reflection — Typ-Mapping passiert über Register&lt;TQuery, TResponse&gt;(),
/// das vom generierten Wiring-Code aufgerufen wird. Die konkreten Typen fließen
/// in Closures, die zur Dispatch-Zeit nur noch aufgerufen werden.
///
/// Aus ViewModel-Sicht:
///   bus.Publish(new GetAlleTodos())  → QueryBridge → gRPC → Server
///   Server → TodoListe → bus.Publish(TodoListe) → Store.Handle(TodoListe)
/// </summary>
public class QueryBridge : IAsyncDisposable
{
    private readonly IGrpcProxy _proxy;
    private readonly IVersioningModule _versioning;
    private readonly List<IDisposable> _subscriptions = new();

    private ClientBus? _bus;

    public QueryBridge(IGrpcProxy proxy, IVersioningModule versioning)
    {
        _proxy = proxy;
        _versioning = versioning;
    }

    /// <summary>
    /// Registriert ein Query→Response Mapping und subscribt auf dem Bus.
    ///
    /// Wird vom generierten Wiring-Code aufgerufen:
    ///   queryBridge.Register&lt;GetAlleTodos, TodoListe&gt;(bus);
    ///
    /// Die generischen Typ-Parameter fließen in die Closure —
    /// IGrpcProxy.QueryAsync&lt;TResponse&gt;() ist ein normaler generischer Aufruf,
    /// kein Reflection.
    /// </summary>
    public void Register<TQuery, TResponse>(ClientBus bus)
        where TQuery : IQuery
        where TResponse : IQueryResponse
    {
        _bus = bus;

        var sub = bus.SubscribeAsync(typeof(TQuery), async (obj, ctx) =>
        {
            if (obj is IQuery query)
                await HandleQueryAsync<TResponse>(query, typeof(TQuery).Name);
        });

        _subscriptions.Add(sub);
    }

    private async Task HandleQueryAsync<TResponse>(IQuery query, string queryTypeName)
        where TResponse : IQueryResponse
    {
        if (!_proxy.IsConnected)
        {
            _bus?.PostToSyncContext(() =>
                _bus.Publish(new QueryFailed(queryTypeName, "Not connected")));
            return;
        }

        try
        {
            var correlationId = Guid.NewGuid().ToString();

            // Normaler generischer Aufruf — TResponse ist via Closure bekannt
            var response = await _proxy.QueryAsync<TResponse>(query, correlationId);

            // Alles auf dem UI-Thread: erst Deps, dann Response
            var deps = response.Deps?.Select(d =>
                new AggregateDep(d.Id, d.AggregateType, d.Version)).ToList();
            var data = (object)response.Data!;

            _bus!.PostToSyncContext(() =>
            {
                if (deps != null)
                    _versioning.TrackFromDeps(deps);

                _bus.Publish(data, MessageContext.Local());
            });
        }
        catch (Exception ex)
        {
            _bus?.PostToSyncContext(() =>
                _bus.Publish(new QueryFailed(queryTypeName, ex.Message)));
        }
    }

    public ValueTask DisposeAsync()
    {
        foreach (var sub in _subscriptions)
            sub.Dispose();
        _subscriptions.Clear();
        return ValueTask.CompletedTask;
    }
}