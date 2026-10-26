using System.Collections.Concurrent;
using System.Diagnostics;
using Abstractions;
using Cqrs.Leitung;
using Grpc.Core;
using Grpc.Net.Client;

namespace Verhalten.Tests.Geruest;

/// <summary>
/// Die Leitung ohne Client-Stack (<c>Cqrs.Leitung/cqrs.proto</c>): ein Handshake mit Vertrag und Token, danach jede Server-Nachricht
/// unverändert gesammelt. Für das, was der C#-Client nicht kann oder verdeckt: eine Zusage mit Ursache beantworten (wie die Python-Basen,
/// docs/konzept-akteure.md §5.3), einen Command mit behaupteter Version schicken, und ALLES sehen, was eine Sitzung bekommt.
/// </summary>
public sealed class RoheLeitung : IAsyncDisposable
{
    private static readonly INutzlastTabelle Tabelle = Domain.Leitung.GeneratedNutzlast.Instanz;
    private readonly GrpcChannel _kanal;
    private readonly AsyncDuplexStreamingCall<ClientMessage, ServerMessage> _ruf;
    private readonly ConcurrentQueue<ServerMessage> _empfangen = new();
    private readonly SemaphoreSlim _schreiben = new(1, 1);
    private readonly CancellationTokenSource _cts = new();
    private Task _lesen = Task.CompletedTask;

    private RoheLeitung(GrpcChannel kanal, AsyncDuplexStreamingCall<ClientMessage, ServerMessage> ruf)
    {
        _kanal = kanal;
        _ruf = ruf;
    }

    public string SitzungsId { get; private set; } = "";

    /// <summary>Verbindet als <paramref name="vertrag"/> (Handshake-Name, ohne führendes I); lehnt der Server ab, wirft es mit seinem Grund.</summary>
    public static async Task<RoheLeitung> VerbindeAsync(Hostlauf host, string vertrag, string token)
    {
        var kanal = GrpcChannel.ForAddress(host.GrpcAdresse);
        var ruf = new CqrsClientService.CqrsClientServiceClient(kanal).Connect(new Metadata { { "akteur-token", token } });
        var l = new RoheLeitung(kanal, ruf);
        try
        {
            await ruf.RequestStream.WriteAsync(new ClientMessage { Capabilities = new CapabilitiesRequest { Vertrag = vertrag } });
            using var frist = new CancellationTokenSource(TimeSpan.FromSeconds(20));
            if (!await ruf.ResponseStream.MoveNext(frist.Token)) throw new InvalidOperationException("Leitung ohne Antwort geschlossen");
            var erste = ruf.ResponseStream.Current;
            if (erste.CapabilitiesResponse is not { } antwort)
                throw new InvalidOperationException($"Handshake abgelehnt: {erste.Error?.Code} {erste.Error?.Message}");
            if (antwort.Vertrag != vertrag) throw new InvalidOperationException($"Vertrag nicht angenommen (Antwort '{antwort.Vertrag}')");
            l.SitzungsId = antwort.SessionId;
            l._lesen = l.LiesAsync();
            return l;
        }
        catch
        {
            await l.DisposeAsync();
            throw;
        }
    }

    private async Task LiesAsync()
    {
        try
        {
            await foreach (var m in _ruf.ResponseStream.ReadAllAsync(_cts.Token)) _empfangen.Enqueue(m);
        }
        catch (Exception e) when (e is OperationCanceledException or RpcException) { }
    }

    public IReadOnlyCollection<ServerMessage> Alles => _empfangen;

    /// <summary>Die Events (auch CommandFailed), die diese Sitzung bekommen hat.</summary>
    public IEnumerable<EventEnvelopeDto> Events => _empfangen.Where(m => m.Event != null).Select(m => m.Event.Envelope);

    public static IEvent Payload(EventEnvelopeDto e) => Tabelle.Event(e.Payload);

    public async Task<EventEnvelopeDto> WarteAufEventAsync(Func<EventEnvelopeDto, bool> bedingung, TimeSpan frist, string meldung)
    {
        var sw = Stopwatch.StartNew();
        while (sw.Elapsed < frist)
        {
            if (Events.FirstOrDefault(bedingung) is { } e) return e;
            await Task.Delay(50);
        }
        throw new TimeoutException($"{meldung} (nach {frist.TotalSeconds:0} s, Sitzung {SitzungsId}); empfangen: "
                                   + string.Join(", ", Events.Select(e => $"{e.Payload.Typ}[{e.CorrelationId}]")));
    }

    /// <summary>Ein Command auf der Leitung. Version &lt; 0 = ohne behauptete Version; <paramref name="ursache"/> macht ihn zur Zusage-Antwort.</summary>
    public async Task SendeAsync(ICommand command, string aggregatTyp, int erwarteteVersion, string korrelation,
        (EventEnvelopeDto Event, int Index)? ursache = null)
    {
        var dto = new CommandEnvelopeDto
        {
            CommandId = Guid.NewGuid().ToString(),
            AggregateId = command.Ziel.ToString(),
            AggregateType = aggregatTyp,
            ExpectedVersion = erwarteteVersion,
            CreatedAtUtc = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
            CorrelationId = korrelation,
            Payload = Tabelle.PackeCommand(command),
        };
        if (ursache is { } u)
        {
            dto.CausationStreamId = u.Event.AggregateId;
            dto.CausationVersion = u.Event.AggregateVersion;
            dto.CausationType = u.Event.Payload.Typ;
            dto.CausationIndex = u.Index;
        }
        await _schreiben.WaitAsync();
        try { await _ruf.RequestStream.WriteAsync(new ClientMessage { Command = new CommandRequest { Envelope = dto } }); }
        finally { _schreiben.Release(); }
    }

    public async ValueTask DisposeAsync()
    {
        try { await _ruf.RequestStream.CompleteAsync(); } catch (Exception e) when (e is InvalidOperationException or RpcException) { }
        _cts.Cancel();
        try { await _lesen.WaitAsync(TimeSpan.FromSeconds(5)); } catch (TimeoutException) { }
        _ruf.Dispose();
        _kanal.Dispose();
    }
}
