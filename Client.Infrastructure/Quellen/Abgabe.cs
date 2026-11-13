using System.Collections.Concurrent;
using Abstractions;
using Client.Infrastructure.Connection;
using Microsoft.Extensions.Logging;

namespace Client.Infrastructure.Quellen;

/// <summary>Die Abgabe ist unterbrochen (die Verbindung fehlt oder eine Antwort bleibt aus): wer sie trägt, verbindet neu — die Quelle meldet erneut.</summary>
public sealed class AbgabeUnterbrochen(string grund) : Exception(grund);

/// <summary>
/// Die ABGABE an der Leitung einer App-Instanz (docs/10 E18): ein Command mit seiner abgeleiteten Id hinein — an ein
/// Aggregat (Modus ohne Versionsprüfung, die Inbox des Ziels dedupliziert über die Id) oder ohne Ziel an den Kopf eines Graphen (die Sitzung legt den
/// offenen Eingang mit dieser Id an) — und warten, bis die Antwort mit seiner Korrelation da ist (die <c>CommandQuittung</c> der Sitzung, eine Ablehnung,
/// ein <c>CommandFailed</c>). Bleibt sie aus, geht er mit derselben Id noch einmal; das Zweite verwirft das Backend. Eine Abgabe je Verbindung: sie
/// besitzt den Event-Kanal der Verbindung (eine App, deren Quellen hineingeben, hört nichts anderes).
/// </summary>
public sealed class Abgabe : IAsyncDisposable
{
    /// <summary>So lange wartet ein Command auf seine Antwort, bevor er mit derselben Id noch einmal geht.</summary>
    public static readonly TimeSpan StandardAntwortfrist = TimeSpan.FromSeconds(5);

    /// <summary>So oft geht ein Command, bevor die Abgabe als unterbrochen gilt.</summary>
    public const int Versuche = 5;

    private readonly IGrpcProxy _proxy;
    private readonly TimeSpan _antwortfrist;
    private readonly ILogger? _logger;
    private readonly ConcurrentDictionary<string, TaskCompletionSource<EventEnvelope>> _offen = new(StringComparer.Ordinal);
    private readonly CancellationTokenSource _ende = new();
    private readonly Task _leser;

    public Abgabe(IGrpcProxy proxy, TimeSpan? antwortfrist = null, ILogger? logger = null)
    {
        _proxy = proxy;
        _antwortfrist = antwortfrist ?? StandardAntwortfrist;
        _logger = logger;
        _leser = LiesAsync(_ende.Token);
    }

    /// <summary>Steht die Verbindung?</summary>
    public bool Verbunden => _proxy.IsConnected;

    /// <summary>
    /// Gibt <paramref name="command"/> mit der Id <paramref name="id"/> hinein und kehrt mit der Antwort zurück; <paramref name="ursache"/> ist die Spur
    /// (<c>Stream:Version</c>, hier die Kennung des Elements). Wirft <see cref="AbgabeUnterbrochen"/>, wenn die Verbindung fehlt oder nach
    /// <see cref="Versuche"/> Versuchen keine Antwort kam.
    /// </summary>
    public async Task<EventEnvelope> GibAbAsync(ICommand command, Guid id, string ursache, CancellationToken ct)
    {
        var korrelation = id.ToString();
        var antwort = new TaskCompletionSource<EventEnvelope>(TaskCreationOptions.RunContinuationsAsynchronously);
        _offen[korrelation] = antwort;
        try
        {
            for (var versuch = 1; versuch <= Versuche; versuch++)
            {
                if (!_proxy.IsConnected) throw new AbgabeUnterbrochen($"{command.GetType().Name} nicht abgegeben — keine Verbindung");
                if (command is IAggregatCommand an)
                    await _proxy.SendCommandAsync(new CommandEnvelope
                    {
                        CommandId = id, Payload = an, Modus = new CommandModus.Client(-1), CorrelationId = korrelation, OriginSessionId = _proxy.SessionId,
                        Ursache = ursache,
                    }, ct);
                else
                    await _proxy.SendeNachrichtAsync(command, id, korrelation, ct, ursache);
                if (await Task.WhenAny(antwort.Task, Task.Delay(_antwortfrist, ct)) == antwort.Task)
                {
                    if (antwort.Task.Result.Payload is CommandFailed f)
                        _logger?.LogWarning("[Quellen] {Command} scheiterte — {Grund}", command.GetType().Name, f.Reason);
                    return antwort.Task.Result;
                }
                _logger?.LogInformation("[Quellen] {Command} ({Id}): keine Antwort nach {Frist} — noch einmal mit derselben Id", command.GetType().Name, id,
                    _antwortfrist);
            }
            throw new AbgabeUnterbrochen($"{command.GetType().Name} bleibt nach {Versuche} Versuchen ohne Antwort");
        }
        finally { _offen.TryRemove(korrelation, out _); }
    }

    /// <summary>Liest den Event-Kanal der Verbindung: eine korrelierte Antwort beendet das Warten ihres Commands.</summary>
    private async Task LiesAsync(CancellationToken ct)
    {
        try
        {
            await foreach (var e in _proxy.Events.ReadAllAsync(ct))
                if (!string.IsNullOrEmpty(e.CorrelationId) && _offen.TryGetValue(e.CorrelationId, out var antwort)) antwort.TrySetResult(e);
        }
        catch (OperationCanceledException) { }
        catch (System.Threading.Channels.ChannelClosedException) { }
    }

    public async ValueTask DisposeAsync()
    {
        await _ende.CancelAsync();
        try { await _leser; } catch { /* beendet */ }
        _ende.Dispose();
    }
}
