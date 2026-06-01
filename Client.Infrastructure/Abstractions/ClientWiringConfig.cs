// ── Client.Infrastructure/Abstractions/ClientWiringConfig.cs ──

using Client.Infrastructure.Bus;
using Client.Infrastructure.Connection;

namespace Client.Infrastructure.Abstractions;

/// <summary>
/// Hält alle Wiring-Delegates die der generierte Code bereitstellt.
///
/// Wird vom WiringGenerator in AddClientDomain() als Singleton registriert.
/// CircuitHandlerBase (oder jeder andere Lifecycle-Manager) resolved diesen
/// Record aus DI und übergibt die Delegates an ClientStartupService.
///
/// Kein Domain-Import — alle Typen kommen aus Client.Infrastructure.
/// </summary>
public record ClientWiringConfig(
    Action<IServiceProvider, IBus> SubscribeAll,
    Action<IServiceProvider> UnsubscribeAll,
    Action<QueryBridge, ClientBus> RegisterQueries,
    IReadOnlyList<Type> ServerEventTypes,
    IReadOnlyList<Type> CommandTypes,
    IReadOnlyDictionary<Type, string> CommandAggregateTypes
);
