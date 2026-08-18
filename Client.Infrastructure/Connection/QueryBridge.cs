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
    // Read-Your-Writes: höchstens so oft nachfragen, dann die (evtl. noch leicht stale) Antwort
    // nehmen — gebounded, damit ein dauerhaft zurückliegendes Read-Model nie einen Hänger erzeugt.
    private const int MaxReadAttempts = 4;
    private const int ReadRetryDelayMs = 120;

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

            // ── Read-Your-Writes (gebounded) ──
            // Die Datensatz-/Trainings-Projektionen sind asynchrone Pull-Consumer; ein Refresh-Query
            // direkt nach einem Mutations-Event kann sie überholen und ein STALES Read-Model lesen.
            // Deshalb: die Deps der Antwort gegen das Read-Ziel prüfen (die Domain-Event-Version, die
            // der Client aus dem Event kennt). Ist das Read-Model noch dahinter, kurz warten und
            // erneut fragen — gebounded, also nie ein Hänger. Ist es frisch (Normalfall), kein Delay.
            List<AggregateDep>? deps = null;
            object? data = null;

            for (var attempt = 1; attempt <= MaxReadAttempts; attempt++)
            {
                var response = await _proxy.QueryAsync<TResponse>(query, correlationId);
                deps = response.Deps?.Select(d =>
                    new AggregateDep(d.Id, d.AggregateType, d.Version)).ToList();
                data = response.Data!;

                if (attempt == MaxReadAttempts || !IstReadModelZurueck(deps))
                    break;

                await Task.Delay(ReadRetryDelayMs);   // der Projektion einen Wimpernschlag geben
            }

            // Alles auf dem UI-Thread: erst Deps, dann Response
            _bus!.PostToSyncContext(() =>
            {
                if (deps != null)
                    _versioning.TrackFromDeps(deps);

                _bus.Publish(data!, MessageContext.Local());
            });
        }
        catch (Exception ex)
        {
            _bus?.PostToSyncContext(() =>
                _bus.Publish(new QueryFailed(queryTypeName, ex.Message)));
        }
    }

    /// <summary>
    /// True, wenn das Read-Model für mindestens ein in der Antwort berührtes Aggregat noch HINTER dem
    /// Read-Ziel liegt (die Domain-Event-Version, die der Client bereits aus dem Event-Push kennt) —
    /// dann lohnt ein erneuter Versuch. Ohne Deps oder ohne bekanntes Ziel: nichts zu erwarten → frisch.
    /// </summary>
    private bool IstReadModelZurueck(List<AggregateDep>? deps)
    {
        if (deps == null) return false;

        foreach (var dep in deps)
        {
            var ziel = _versioning.GetReadTarget(dep.Id);
            if (ziel is int z && dep.Version < z)
                return true;
        }
        return false;
    }

    public ValueTask DisposeAsync()
    {
        foreach (var sub in _subscriptions)
            sub.Dispose();
        _subscriptions.Clear();
        return ValueTask.CompletedTask;
    }
}