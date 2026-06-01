// ── Host.Blazor/CircuitHandlerBase.cs ──
// ── TODO: nach Client.Ui.Razor verschieben wenn das Projekt existiert ──

using Client.Infrastructure;
using Client.Infrastructure.Abstractions;
using Microsoft.AspNetCore.Components.Server.Circuits;

namespace Host.Blazor;

/// <summary>
/// Generischer Blazor Circuit Lifecycle-Handler.
///
/// Kennt keine Domäne — liest ClientWiringConfig aus DI und übergibt
/// die Delegates an ClientStartupService.
///
/// Ersetzt den domain-spezifischen ImagePairCircuitHandler.
///
/// Pro Browser-Tab:
///   OnCircuitOpenedAsync → SubscribeAll, RegisterQueries, Connect
///   OnCircuitClosedAsync → UnsubscribeAll, Disconnect
/// </summary>
public class CircuitHandlerBase : CircuitHandler
{
    private readonly ClientStartupService _startup;
    private readonly ClientWiringConfig _wiring;
    private readonly string _serverAddress;

    public CircuitHandlerBase(
        ClientStartupService startup,
        ClientWiringConfig wiring,
        GrpcServerConfig grpcConfig)
    {
        _startup = startup;
        _wiring = wiring;
        _serverAddress = grpcConfig.Address;
    }

    public override async Task OnCircuitOpenedAsync(Circuit circuit, CancellationToken ct)
    {
        Console.WriteLine($"[Circuit] Opened: {circuit.Id}");

        try
        {
            await _startup.StartAsync(
                subscribeAll:          _wiring.SubscribeAll,
                registerQueries:       _wiring.RegisterQueries,
                serverEventTypes:      _wiring.ServerEventTypes,
                commandTypes:          _wiring.CommandTypes,
                serverAddress:         _serverAddress,
                commandAggregateTypes: _wiring.CommandAggregateTypes,
                unsubscribeAll:        _wiring.UnsubscribeAll,
                ct:                    ct);

            Console.WriteLine($"[Circuit] Connected: {circuit.Id}");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[Circuit] Startup failed: {ex.Message}");
        }
    }

    public override async Task OnCircuitClosedAsync(Circuit circuit, CancellationToken ct)
    {
        Console.WriteLine($"[Circuit] Closed: {circuit.Id}");
        await _startup.StopAsync();
    }
}