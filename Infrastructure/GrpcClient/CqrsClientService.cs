using System.Collections.Concurrent;
using Abstractions;
using Grpc.Core;
using Infrastructure.Akteure;
using Infrastructure.Funktionen;
using Infrastructure.Projections;
using Infrastructure.Serialization;
using Infrastructure.Verteilen;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Proto;

namespace Infrastructure.GrpcClient;

/// <summary>
/// Der gRPC-Dienst der Leitung (docs/konzept-akteure.md §5): je Verbindung prüft er das Akteur-Token, startet die Sitzung als Actor
/// (<see cref="SitzungsActor"/>, einziger Schreiber auf den Antwort-Stream) und reicht in seiner Lese-Schleife jede Nachricht des Clients nur
/// weiter. Endet der Stream oder die Sitzung von sich aus (Ablehnung am Handshake), stoppt er die Sitzung, bevor der Aufruf endet — danach
/// schreibt niemand mehr.
/// </summary>
public class CqrsClientServiceImpl : Cqrs.Leitung.CqrsClientService.CqrsClientServiceBase
{
    private readonly ActorSystem _actorSystem;
    private readonly AkteurTor? _akteurTor;
    private readonly SitzungsDienste _dienste;
    private readonly ILogger _logger;
    private static int _sessionCounter;

    public CqrsClientServiceImpl(
        ActorSystem actorSystem,
        ProtoMessageMapper mapper,
        IAggregateDispatcher dispatcher,
        IQueryDienst queryService,
        QueryHandlerRegistry queryHandlerRegistry,
        Verteilung? verteilung = null,
        AkteurTor? akteurTor = null,
        ILogger<CqrsClientServiceImpl>? logger = null,
        IAusgabeAblage? ausgaben = null,
        Konsum? konsum = null,
        HinweisNetz? hinweisNetz = null,
        IEventStoreRepository? log = null)
    {
        _actorSystem = actorSystem ?? throw new ArgumentNullException(nameof(actorSystem));
        _akteurTor = akteurTor;   // null = Akteure nicht konfiguriert → offen (opt-in)
        _logger = logger ?? NullLogger<CqrsClientServiceImpl>.Instance;
        _dienste = new SitzungsDienste(mapper, dispatcher, queryService, queryHandlerRegistry, verteilung, akteurTor,
            ausgaben,   // null = keine Funktion draußen in dieser Anwendung → Anbieten wird abgewiesen
            konsum ?? Konsum.Standard,
            hinweisNetz,   // null = keine Funktion hört Hinweise
            log,
            new ConcurrentDictionary<string, TaskCompletionSource<Cqrs.Leitung.QueryResponseFromClient>>(), _logger);
    }

    public override async Task Connect(
        IAsyncStreamReader<Cqrs.Leitung.ClientMessage> requestStream,
        IServerStreamWriter<Cqrs.Leitung.ServerMessage> responseStream,
        ServerCallContext context)
    {
        // Die Sitzungs-Id nennt den Knoten: gezielte Antworten finden so den Verteiler, bei dem die Sitzung angemeldet ist.
        var sessionId = Verteilung.SitzungsId(_actorSystem.Address, Interlocked.Increment(ref _sessionCounter));
        var ct = context.CancellationToken;
        _logger.LogInformation("New connection {Session} from {Peer}", sessionId, context.Peer);

        // Akteur (docs/konzept-akteure.md): wer sich anmeldet, ist EIN Akteur; seine Befugnisse gelten für die ganze Sitzung. Ohne Tor bleibt
        //   akteur = null (offen).
        AkteurRechte? akteur = null;
        if (_akteurTor != null)
        {
            akteur = _akteurTor.Erkenne(context.RequestHeaders.GetValue(AkteurOptionen.TokenHeader));
            if (akteur == null)
            {
                _logger.LogWarning("{Session} abgewiesen: kein gültiges Akteur-Token ({Peer})", sessionId, context.Peer);
                throw new RpcException(new Status(StatusCode.Unauthenticated, "Kein gültiges Akteur-Token"));
            }
            _logger.LogInformation("{Session} Akteur: {Akteur}", sessionId, akteur.Name);
        }

        var ende = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var sitzung = _actorSystem.Root.Spawn(Props.FromProducer(() => new SitzungsActor(responseStream, sessionId, akteur, _dienste, ende)));
        using var lesen = CancellationTokenSource.CreateLinkedTokenSource(ct);
        try
        {
            var schleife = LiesAsync(requestStream, sitzung, lesen.Token);
            if (await Task.WhenAny(schleife, ende.Task) == ende.Task)
            {
                lesen.Cancel();
                try { await schleife; } catch { /* abgebrochen */ }
                await ende.Task;   // der Status der Sitzung (z. B. Vertrag abgelehnt) geht an den Client
            }
            else
            {
                await schleife;
                _logger.LogInformation("{Session} client closed stream normally", sessionId);
            }
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("{Session} cancelled", sessionId);
        }
        catch (Exception ex) when (ex is RpcException { StatusCode: StatusCode.Cancelled } or IOException)
        {
            _logger.LogInformation("{Session} client disconnected", sessionId);
        }
        catch (RpcException ex)
        {
            _logger.LogWarning("{Session} beendet: {Status} {Detail}", sessionId, ex.StatusCode, ex.Status.Detail);
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "{Session} error", sessionId);
        }
        finally
        {
            try { await _actorSystem.Root.StopAsync(sitzung); }
            catch (Exception ex) { _logger.LogWarning(ex, "{Session} error stopping session", sessionId); }
            _logger.LogInformation("{Session} disconnected", sessionId);
        }
    }

    /// <summary>Die Lese-Schleife: reicht jede Nachricht des Clients an seine Sitzung weiter, mehr nicht.</summary>
    private async Task LiesAsync(IAsyncStreamReader<Cqrs.Leitung.ClientMessage> requestStream, PID sitzung, CancellationToken ct)
    {
        while (await requestStream.MoveNext(ct))
            _actorSystem.Root.Send(sitzung, new VonClient(requestStream.Current));
    }
}
