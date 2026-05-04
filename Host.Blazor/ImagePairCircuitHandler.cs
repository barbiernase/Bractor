using Client.Infrastructure;
using Client.Infrastructure.Abstractions;
using Client.Infrastructure.Bus;
using Client.Infrastructure.Connection;
using Domain.Client.ImagePair;
using Domain.Projections;
using Microsoft.AspNetCore.Components.Server.Circuits;

/// <summary>
/// Blazor Circuit Lifecycle — pro Browser-Tab.
///
/// OnCircuitOpenedAsync: Framework starten via GeneratedWiring.
/// OnCircuitClosedAsync: gRPC trennen.
///
/// GeneratedWiring wird vom WiringGenerator erzeugt und enthält:
///   - SubscribeAll (Stores vor Handlern, korrekte Reihenfolge)
///   - RegisterQueries (inkl. OneOf-Auflösung)
///   - ServerEventTypes, CommandTypes, CommandAggregateTypes
///
/// Einziger manueller Teil: DI-Registrierung in Program.cs
/// nutzt AddScoped statt das generierte AddClientDomain (AddSingleton).
/// </summary>
public class ImagePairCircuitHandler : CircuitHandler
{
    private readonly ClientStartupService _startup;
    private readonly IBus _bus;
    private readonly ImagePairStore _store;
    private readonly string _serverAddress;

    public ImagePairCircuitHandler(
        ClientStartupService startup,
        IBus bus,
        ImagePairStore store,
        GrpcServerConfig grpcConfig)
    {
        _startup = startup;
        _bus = bus;
        _store = store;
        _serverAddress = grpcConfig.Address;
    }

    public override async Task OnCircuitOpenedAsync(Circuit circuit, CancellationToken ct)
    {
        Console.WriteLine($"[Circuit] Opened: {circuit.Id}");

        try
        {
            // FIX: Server-Adresse aus Konfiguration statt hardcoded
            await _startup.StartAsync(
                subscribeAll:          GeneratedWiring.SubscribeAll,
                registerQueries:       GeneratedWiring.RegisterQueries,
                serverEventTypes:      GeneratedWiring.ServerEventTypes,
                commandTypes:          GeneratedWiring.CommandTypes,
                serverAddress:         _serverAddress,
                commandAggregateTypes: GeneratedWiring.CommandAggregateTypes,
                ct:                    ct);

            // Store mit Bus verbinden — NACH SubscribeAll,
            // damit die Handle-Methoden bereits registriert sind.
            _store.Initialisiere(_bus);

            Console.WriteLine($"[Circuit] Connected: {circuit.Id}");

            // Initiale Daten über den Store laden — nutzt BaueFilter
            // mit der konfigurierten SeitenGroesse statt dem Server-Default.
            _store.LadeInitialeDaten();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[Circuit] Connection failed: {ex.Message}");
        }
    }

    public override async Task OnCircuitClosedAsync(Circuit circuit, CancellationToken ct)
    {
        Console.WriteLine($"[Circuit] Closed: {circuit.Id}");

        try
        {
            await _startup.StopAsync();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[Circuit] Shutdown error: {ex.Message}");
        }
    }
}