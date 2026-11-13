using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;
using Abstractions;
using Client.Infrastructure.Connection;

namespace Infrastructure.Pruefstand.Quellen;

/// <summary>
/// Die Leitung eines Rechnerdiensts im Speicher (docs/10 E18): was abgegeben wurde (Command, Id, Ursache, ob an ein Aggregat) und die Quittung
/// der Sitzung mit der Korrelation — außer sie schweigt (<see cref="Schweigt"/>) oder hält an (<see cref="Antworte"/>).
/// </summary>
internal sealed class Leitungsprobe : IGrpcProxy
{
    private readonly Channel<EventEnvelope> _events = Channel.CreateUnbounded<EventEnvelope>();
    public readonly ConcurrentQueue<(ICommand Command, Guid Id, string? Ursache, bool AnAggregat)> Abgegeben = new();
    /// <summary>So oft schweigt sie noch (keine Quittung), bevor sie wieder antwortet.</summary>
    public int Schweigt;
    /// <summary>Hält die Quittung an, bis die Aufgabe fertig ist.</summary>
    public Func<ICommand, Task> Antworte = _ => Task.CompletedTask;
    public bool Verbunden = true;

    public ConnectionState CurrentState => Verbunden ? ConnectionState.Connected : ConnectionState.Disconnected;
    public string? SessionId => "knoten#0001";
    public bool IsConnected => Verbunden;
    public ChannelReader<EventEnvelope> Events => _events.Reader;
    public ChannelReader<ConnectionState> StateChanges { get; } = Channel.CreateUnbounded<ConnectionState>().Reader;
    public ChannelReader<IQuery> Aktualisierungen { get; } = Channel.CreateUnbounded<IQuery>().Reader;

    public Task<ClientCapabilities> ConnectAsync(string serverAddress, string vertrag, string vertragHash, CancellationToken ct = default) =>
        throw new NotSupportedException();

    public Task SendCommandAsync(CommandEnvelope envelope, CancellationToken ct = default) =>
        Gib(envelope.Payload, envelope.CommandId, envelope.CorrelationId, envelope.Ursache, true);

    public Task SendeNachrichtAsync(ICommand nachricht, Guid commandId, string correlationId, CancellationToken ct = default, string? ursache = null) =>
        Gib(nachricht, commandId, correlationId, ursache, false);

    private Task Gib(ICommand command, Guid id, string korrelation, string? ursache, bool anAggregat)
    {
        Abgegeben.Enqueue((command, id, ursache, anAggregat));
        if (Schweigt > 0) { Schweigt--; return Task.CompletedTask; }
        _ = Antworte(command).ContinueWith(_ =>
            _events.Writer.TryWrite(new EventEnvelope { CorrelationId = korrelation, Payload = new CommandQuittung(command.GetType().Name, id, 1, true) }),
            TaskScheduler.Default);
        return Task.CompletedTask;
    }

    public Task<QueryResponse<TResponse>> QueryAsync<TResponse>(IQuery query, string correlationId, IReadOnlyList<StreamStand>? mindestens = null,
        CancellationToken ct = default) where TResponse : IQueryResponse => throw new NotSupportedException();

    public Task<LogLesung> LiesLogAsync(long ab, int hoechstens = 500, CancellationToken ct = default) => throw new NotSupportedException();

    public Task DisconnectAsync() => Task.CompletedTask;
}
