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

    /// <summary>Queries, die der Server zum neuen Fragen nennt: eine Projektion hat weiterverarbeitet, was ihre letzte Antwort zeigte.</summary>
    ChannelReader<IQuery> Aktualisierungen { get; }

    /// <summary>
    /// Verbindet zum Server und führt den Capabilities-Handshake durch: der Client nennt seinen Vertrag (Name + Hash, docs/konzept-akteure.md
    /// §5.2) — Fähigkeiten und Abos nimmt der Server aus seiner Tabelle, nicht aus einer Selbstauskunft.
    /// </summary>
    Task<ClientCapabilities> ConnectAsync(
        string serverAddress,
        string vertrag,
        string vertragHash,
        CancellationToken ct = default);

    /// <summary>Sendet einen Command (Fire-and-Forget).</summary>
    Task SendCommandAsync(CommandEnvelope envelope, CancellationToken ct = default);

    /// <summary>
    /// Sendet eine Query und wartet auf die Antwort. <paramref name="mindestens"/>: „mindestens S auf v“ je Stream, den der Client zuletzt
    /// beschrieben hat — die Leseseite wartet begrenzt, bis ihre Projektion so weit ist (sonst <c>NochNicht</c>).
    /// </summary>
    Task<QueryResponse<TResponse>> QueryAsync<TResponse>(
        IQuery query,
        string correlationId,
        IReadOnlyList<StreamStand>? mindestens = null,
        CancellationToken ct = default) where TResponse : IQueryResponse;

    /// <summary>Trennt die Verbindung.</summary>
    Task DisconnectAsync();
}