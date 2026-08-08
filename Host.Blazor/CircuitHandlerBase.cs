using Client.Infrastructure;
using Client.Infrastructure.Abstractions;
using Microsoft.AspNetCore.Components.Server.Circuits;

namespace Host.Blazor;

public class CircuitHandlerBase : CircuitHandler
{
    private readonly ClientStartupService _startup;
    private readonly ClientWiringConfig _config;
    private readonly string _serverAddress;

    public CircuitHandlerBase(
        ClientStartupService startup, ClientWiringConfig config, GrpcServerConfig grpc)
    {
        _startup = startup;
        _config = config;
        _serverAddress = grpc.Address;
    }

    public override async Task OnCircuitOpenedAsync(Circuit circuit, CancellationToken ct)
    {
        await _startup.StartAsync(
            _config.SubscribeAll, _config.RegisterQueries,
            _config.ServerEventTypes, _config.CommandTypes,
            _serverAddress, _config.HydrationStores,
            _config.CommandAggregateTypes,
            _config.UnsubscribeAll, ct);
    }

    public override async Task OnCircuitClosedAsync(Circuit circuit, CancellationToken ct)
        => await _startup.StopAsync();
}

public record GrpcServerConfig(string Address);
