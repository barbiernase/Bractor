using System.Collections.Concurrent;
using System.Diagnostics;
using Abstractions;
using Cqrs.Leitung;
using Grpc.Core;
using Grpc.Net.Client;

namespace Verhalten.Tests.Geruest;

/// <summary>
/// Die Leitung ohne Client-Stack (<c>Cqrs.Leitung/cqrs.proto</c>): ein Handshake mit Vertrag und Token, danach jede Server-Nachricht
/// unverändert gesammelt. Für das, was der C#-Client nicht kann oder verdeckt: eine getragene Funktion anbieten und ihre Eingänge beantworten
/// (wie die Python-Basen, Plan Pub/Sub-Neustrukturierung §2.7), einen Command mit behaupteter Version schicken, und ALLES sehen, was eine
/// Sitzung bekommt.
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

    /// <summary>Eine Seite des Logs ab <paramref name="ab"/> (Lesen ab Position).</summary>
    public async Task<LogSeite> LiesLogAsync(long ab, int hoechstens = 1000)
    {
        var korrelation = Guid.NewGuid().ToString();
        await _ruf.RequestStream.WriteAsync(new ClientMessage { LiesLog = new LiesLog { Korrelation = korrelation, Ab = ab, Hoechstens = hoechstens } });
        var bis = DateTime.UtcNow + TimeSpan.FromSeconds(15);
        while (DateTime.UtcNow < bis)
        {
            if (_empfangen.FirstOrDefault(m => m.LogSeite?.Korrelation == korrelation) is { } m) return m.LogSeite;
            if (_empfangen.FirstOrDefault(m => m.Error?.CorrelationId == korrelation) is { } f) throw new InvalidOperationException(f.Error.Message);
            await Task.Delay(20);
        }
        throw new TimeoutException($"keine Seite des Logs ab {ab}");
    }

    /// <summary>Liest das Log ab <paramref name="ab"/> bis zum Horizont; die Einträge und die Position zum Weiterlesen.</summary>
    public async Task<(List<LogEintrag> Eintraege, long Bis)> LiesLogBisZumEndeAsync(long ab)
    {
        var alle = new List<LogEintrag>();
        while (true)
        {
            var seite = await LiesLogAsync(ab);
            alle.AddRange(seite.Eintraege);
            ab = seite.Bis;
            if (!seite.Voll) return (alle, ab);
        }
    }

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

    /// <summary>Ein Command auf der Leitung. Version &lt; 0 = ohne behauptete Version.</summary>
    public async Task SendeAsync(ICommand command, string aggregatTyp, int erwarteteVersion, string korrelation)
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
        await SchreibeAsync(new ClientMessage { Command = new CommandRequest { Envelope = dto } });
    }

    // ── Funktionen tragen (ITraegt): anbieten, Eingänge sehen, beantworten ──

    /// <summary>Bietet eine getragene Funktion an (der Server holt dann Eingänge für diese Sitzung beim Vermittler).</summary>
    public Task BieteAnAsync(string funktion, int slots = 1) =>
        SchreibeAsync(new ClientMessage { FunktionenAnbieten = new FunktionenAnbieten { Angebote = { new FunktionsAngebot { Funktion = funktion, Slots = slots } } } });

    /// <summary>Die Eingänge, die diese Sitzung bekommen hat.</summary>
    public IEnumerable<ArbeitsAuftrag> Eingaenge => _empfangen.Where(m => m.ArbeitsAuftrag != null).Select(m => m.ArbeitsAuftrag);

    /// <summary>Die Fehler, die der Server dieser Sitzung meldete (Code).</summary>
    public IEnumerable<string> Fehler => _empfangen.Where(m => m.Error != null).Select(m => m.Error.Code);

    public static IEvent Eingang(ArbeitsAuftrag a) => Tabelle.Event(a.Eingang);

    public async Task<ArbeitsAuftrag> WarteAufEingangAsync(Func<ArbeitsAuftrag, bool> bedingung, TimeSpan frist, string meldung)
    {
        var sw = Stopwatch.StartNew();
        while (sw.Elapsed < frist)
        {
            if (Eingaenge.FirstOrDefault(bedingung) is { } a) return a;
            await Task.Delay(50);
        }
        throw new TimeoutException($"{meldung} (nach {frist.TotalSeconds:0} s, Sitzung {SitzungsId}); Eingänge: "
                                   + string.Join(", ", Eingaenge.Select(a => $"{a.Funktion}:{a.Eingang.Typ}@{a.Stream}")));
    }

    /// <summary>Beantwortet einen Eingang mit Commands (ganz, <c>fertig</c>).</summary>
    public Task AntworteAsync(ArbeitsAuftrag eingang, params ICommand[] ausgaben) => StueckAsync(eingang, 0, true, ausgaben);

    /// <summary>Ein Stück eines Stroms: die Ausgaben ab Stelle <paramref name="ab"/>; <paramref name="fertig"/> beendet den Strom.</summary>
    public Task StueckAsync(ArbeitsAuftrag eingang, int ab, bool fertig, params ICommand[] ausgaben)
    {
        var e = new ArbeitsErgebnis { Vorgang = eingang.Vorgang, Ab = ab, Fertig = fertig };
        foreach (var c in ausgaben) e.Ausgaben.Add(new FunktionsAusgabe { Command = Tabelle.PackeCommand(c) });
        return SchreibeAsync(new ClientMessage { ArbeitsErgebnis = e });
    }

    private async Task SchreibeAsync(ClientMessage m)
    {
        await _schreiben.WaitAsync();
        try { await _ruf.RequestStream.WriteAsync(m); }
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
