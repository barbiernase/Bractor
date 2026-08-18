using System.Threading.Channels;
using Abstractions;

namespace Client.Infrastructure.Connection;

/// <summary>
/// Interface für den gRPC-Proxy — ermöglicht Testbarkeit des Client-Stacks
/// ohne echte gRPC-Verbindung.
///
/// Implementierungen:
///   GrpcProxy     — echte gRPC-Verbindung (Produktion)
///   FakeGrpcProxy — In-Memory (Tests/Szenarien)
/// </summary>
public interface IGrpcProxy
{
    /// <summary>Aktuelle Verbindungszustand.</summary>
    ConnectionState CurrentState { get; }

    /// <summary>Session-ID nach erfolgreichem Handshake.</summary>
    string? SessionId { get; }

    /// <summary>True wenn verbunden.</summary>
    bool IsConnected { get; }

    /// <summary>Channel für eingehende Events vom Server.</summary>
    ChannelReader<EventEnvelope> Events { get; }

    /// <summary>Channel für Verbindungszustandsänderungen.</summary>
    ChannelReader<ConnectionState> StateChanges { get; }

    /// <summary>Verbindet zum Server und führt Capabilities-Handshake durch.</summary>
    Task<ClientCapabilities> ConnectAsync(
        string serverAddress,
        IEnumerable<string> eventTypes,
        CancellationToken ct = default);

    /// <summary>Sendet einen Command (Fire-and-Forget).</summary>
    Task SendCommandAsync(CommandEnvelope envelope, CancellationToken ct = default);

    /// <summary>
    /// Sendet eine Query und wartet auf Response. <paramref name="expectedFreshIds"/> sind
    /// Aggregat-IDs, die der Client zuletzt beschrieben hat — die Leseseite trackt sie als Deps,
    /// damit das bounded Read-Your-Writes nachfasst, bis die Projektion die Writes eingeholt hat.
    /// </summary>
    Task<QueryResponse<TResponse>> QueryAsync<TResponse>(
        IQuery query,
        string correlationId,
        IReadOnlyList<string>? expectedFreshIds = null,
        CancellationToken ct = default) where TResponse : IQueryResponse;

    /// <summary>Subscribes für einen Event-Typ.</summary>
    Task SubscribeAsync(string eventType, CancellationToken ct = default);

    /// <summary>Unsubscribes von einem Event-Typ.</summary>
    Task UnsubscribeAsync(string eventType, CancellationToken ct = default);

    /// <summary>Trennt die Verbindung.</summary>
    Task DisconnectAsync();
}