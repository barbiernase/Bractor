using System.Collections.Concurrent;
using Abstractions;
using Client.Infrastructure.Connection;
using Microsoft.Extensions.Logging;

namespace Client.Infrastructure.Reaktionen;

// ═══════════════════════════════════════════════════════════════════════════════════════════════════════════════════════
// REAKTIONEN EINES CLIENTS (docs/konzept-akteure.md §3): nur Clients und Quellen stoßen Funktionen an. Was nach einem Fakt
// weitergehen soll, stößt ein Client an — er hört das Event (IHoert seines Akteurs), liest, was er braucht (Queries), und gibt Commands hinein
// (IDarf). Genau einmal ohne Festhalten beim Framework: der Client liest das Log ab seiner gesicherten Position (LiesLog, nur Hörbares, bis zum
// Horizont), jede Command-Id leitet der Läufer aus Reaktion, Position und Stelle ab — nach einem Neustart rechnet er dieselben Ids, und die Inbox
// des Ziels bzw. der offene Eingang am Kopf verwirft die Wiederholung. Weiter rückt die Position erst, wenn jeder Command der Seite beantwortet ist.
// ═══════════════════════════════════════════════════════════════════════════════════════════════════════════════════════

/// <summary>Wo die Reaktionen eines Clients stehen: die Position im Log, bis zu der alles gewirkt hat — durabel über einen Neustart.</summary>
public interface IReaktionsStand
{
    Task<long> LadeAsync(string name, CancellationToken ct);
    Task SichereAsync(string name, long position, CancellationToken ct);
}

/// <summary>Der Stand im Speicher — übersteht ein Wiederverbinden, keinen Neustart (Tests).</summary>
public sealed class ReaktionsStandImSpeicher : IReaktionsStand
{
    private readonly ConcurrentDictionary<string, long> _je = new(StringComparer.Ordinal);
    public Task<long> LadeAsync(string name, CancellationToken ct) => Task.FromResult(_je.TryGetValue(name, out var p) ? p : 0);
    public Task SichereAsync(string name, long position, CancellationToken ct) { _je[name] = position; return Task.CompletedTask; }
}

/// <summary>Der Stand in einer Datei je Name (<c>&lt;verzeichnis&gt;/&lt;name&gt;.stand</c>), geschrieben über eine temporäre Datei und Umbenennen.</summary>
public sealed class DateiReaktionsStand(string verzeichnis) : IReaktionsStand
{
    private string Pfad(string name) => Path.Combine(verzeichnis, name + ".stand");

    public async Task<long> LadeAsync(string name, CancellationToken ct) =>
        File.Exists(Pfad(name)) && long.TryParse((await File.ReadAllTextAsync(Pfad(name), ct)).Trim(), out var p) ? p : 0;

    public async Task SichereAsync(string name, long position, CancellationToken ct)
    {
        Directory.CreateDirectory(verzeichnis);
        var tmp = Pfad(name) + ".tmp";
        await File.WriteAllTextAsync(tmp, position.ToString(System.Globalization.CultureInfo.InvariantCulture), ct);
        File.Move(tmp, Pfad(name), overwrite: true);
    }
}

/// <summary>
/// Was eine Reaktion auf ein Event tun kann: fragen und Commands hineingeben — an ein Aggregat oder ohne Ziel an den Kopf eines Graphen. Die
/// Command-Id leitet der Läufer ab (Reaktion, Position, Stelle), nie der Entwickler; gesendet wird, wenn die Reaktion fertig ist.
/// </summary>
public sealed class ReaktionsKontext
{
    private readonly IGrpcProxy _proxy;
    internal readonly List<ICommand> Commands = new();

    internal ReaktionsKontext(IGrpcProxy proxy, long position, EventEnvelope ereignis)
    {
        _proxy = proxy;
        Position = position;
        Ereignis = ereignis;
    }

    /// <summary>Die Position des Events im Log.</summary>
    public long Position { get; }

    /// <summary>Das Event mit seiner Hülle (Stream, Version, Korrelation).</summary>
    public EventEnvelope Ereignis { get; }

    /// <summary>Ein Command an ein Aggregat oder ohne Ziel an den Kopf eines Graphen (der Akteur des Clients muss ihn dürfen).</summary>
    public void Sende(ICommand command) => Commands.Add(command);

    /// <summary>Eine Query an einen Reader oder ein Aggregat.</summary>
    public Task<QueryResponse<TAntwort>> FrageAsync<TAntwort>(IQuery query, CancellationToken ct = default) where TAntwort : IQueryResponse =>
        _proxy.QueryAsync<TAntwort>(query, Guid.NewGuid().ToString(), null, ct);
}

/// <summary>Die Reaktion ist unterbrochen (die Verbindung fehlt): wer sie trägt, verbindet neu und lässt sie weiterlaufen — ab dem gesicherten Stand.</summary>
public sealed class ReaktionUnterbrochen(string grund) : Exception(grund);

/// <summary>
/// Der LÄUFER der Reaktionen eines Clients: je Event-Typ höchstens eine Reaktion (<see cref="Auf{TEvent}"/>). Er besitzt den Event-Kanal der
/// Verbindung (Quittungen, Weckruf) — ein Client, der reagiert, ist ein eigener Client (z. B. ein Worker), keine Oberfläche.
/// </summary>
/// <param name="name">Der Name der Reaktionen (Schlüssel des Stands und der Command-Ids) — je Client einer, über Neustarts gleich.</param>
public sealed class Reaktionen(IGrpcProxy proxy, IReaktionsStand stand, string name, ILogger? logger = null, TimeSpan? takt = null,
    TimeSpan? antwortfrist = null)
{
    /// <summary>So lange wartet ein Command auf seine Antwort, bevor er mit derselben Id noch einmal geht.</summary>
    public static readonly TimeSpan StandardAntwortfrist = TimeSpan.FromSeconds(5);
    public const int Versuche = 5;

    private readonly TimeSpan _antwortfrist = antwortfrist ?? StandardAntwortfrist;

    private readonly Dictionary<Type, Func<object, ReaktionsKontext, Task>> _je = new();
    private readonly ConcurrentDictionary<string, TaskCompletionSource<EventEnvelope>> _offen = new(StringComparer.Ordinal);
    private readonly TimeSpan _takt = takt ?? TimeSpan.FromSeconds(10);
    private TaskCompletionSource _weckruf = NeuerWeckruf();

    private static TaskCompletionSource NeuerWeckruf() => new(TaskCreationOptions.RunContinuationsAsynchronously);

    /// <summary>Die Reaktion auf ein Event <typeparamref name="TEvent"/> (der Akteur des Clients hört es, IHoert).</summary>
    public Reaktionen Auf<TEvent>(Func<TEvent, ReaktionsKontext, Task> reaktion) where TEvent : IEvent
    {
        if (!_je.TryAdd(typeof(TEvent), (e, k) => reaktion((TEvent)e, k)))
            throw new InvalidOperationException($"Auf {typeof(TEvent).Name} reagiert schon eine Reaktion — je Event-Typ eine.");
        return this;
    }

    /// <summary>Die Command-Id der Stelle <paramref name="stelle"/> der Reaktion auf das <paramref name="nummer"/>-te Event an <paramref name="position"/>.</summary>
    public static Guid CommandId(string name, long position, int nummer, int stelle) => Herkunft.Ableite($"reaktion:{name}:{position}:{nummer}:{stelle}");

    /// <summary>
    /// Läuft, bis <paramref name="ct"/> endet: liest ab dem gesicherten Stand, reagiert, sendet, sichert den Stand je Seite; ist nichts mehr da,
    /// wartet er auf ein neues Event (Push) oder den Takt. Fehlt die Verbindung, endet er mit <see cref="ReaktionUnterbrochen"/>; endet
    /// <paramref name="ct"/>, endet er still.
    /// </summary>
    public async Task LaufeAsync(CancellationToken ct)
    {
        using var aus = CancellationTokenSource.CreateLinkedTokenSource(ct);
        var leser = LiesEventsAsync(aus.Token);
        try
        {
            var ab = await stand.LadeAsync(name, ct);
            while (!ct.IsCancellationRequested)
            {
                if (!proxy.IsConnected) throw new ReaktionUnterbrochen("keine Verbindung");
                var weckruf = _weckruf.Task;
                var seite = await proxy.LiesLogAsync(ab, ct: ct);
                if (seite.Horizont >= 0 && seite.Horizont < ab)
                {
                    // Das Log ist kürzer als der eigene Stand: eine neue Datenbank. Von vorn — dieselben Positionen ergäben dieselben Ids.
                    logger?.LogWarning("[Reaktionen] {Name}: das Log endet bei {Horizont}, der Stand steht bei {Ab} — neu, von vorn", name, seite.Horizont, ab);
                    ab = 0;
                    await stand.SichereAsync(name, ab, ct);
                    continue;
                }
                // Mehrere Events an einer Position (ein Split-Upcast) zählen der Reihe nach — eine volle Seite trennt eine Position nie.
                var nummer = 0;
                long vorige = -1;
                foreach (var e in seite.Eintraege)
                {
                    nummer = e.Position == vorige ? nummer + 1 : 0;
                    vorige = e.Position;
                    if (!_je.TryGetValue(e.Event.Payload.GetType(), out var reaktion)) continue;
                    var kontext = new ReaktionsKontext(proxy, e.Position, e.Event);
                    await reaktion(e.Event.Payload, kontext);
                    for (var stelle = 0; stelle < kontext.Commands.Count; stelle++)
                        await SendeAsync(kontext.Commands[stelle], CommandId(name, e.Position, nummer, stelle),
                            Spur.Ursache(e.Event.AggregateId, e.Event.AggregateVersion), ct);
                }
                if (seite.Bis > ab)
                {
                    ab = seite.Bis;
                    await stand.SichereAsync(name, ab, ct);
                }
                if (seite.Voll) continue;
                await Task.WhenAny(weckruf, Task.Delay(_takt, ct));
            }
        }
        catch (Exception) when (ct.IsCancellationRequested)
        {
            // Beendet: die Leitung meldet einen Abbruch auf ihre Art (RpcException „Cancelled“) — der Stand bleibt, wie er gesichert ist.
        }
        finally
        {
            await aus.CancelAsync();
            try { await leser; } catch { /* beendet */ }
        }
    }

    /// <summary>
    /// Sendet einen Command mit seiner abgeleiteten Id und wartet auf seine Antwort (Quittung, Event, Ablehnung oder Fehler mit dieser Korrelation);
    /// bleibt sie aus, geht er mit derselben Id noch einmal — die Inbox des Ziels bzw. der offene Eingang verwirft das Zweite. Jeder Command trägt
    /// das Event, auf das reagiert wird, als <paramref name="ursache"/> (Spur): an ein Aggregat stempelt der Server es an dessen Events, an den Kopf
    /// eines Graphen ist es die Herkunft des Eingangs.
    /// </summary>
    private async Task SendeAsync(ICommand command, Guid id, string ursache, CancellationToken ct)
    {
        var korrelation = id.ToString();
        var antwort = new TaskCompletionSource<EventEnvelope>(TaskCreationOptions.RunContinuationsAsynchronously);
        _offen[korrelation] = antwort;
        try
        {
            for (var versuch = 1; versuch <= Versuche; versuch++)
            {
                if (!proxy.IsConnected) throw new ReaktionUnterbrochen($"{command.GetType().Name} nicht gesendet — keine Verbindung");
                if (command is IAggregatCommand an)
                    await proxy.SendCommandAsync(new CommandEnvelope
                    {
                        CommandId = id, Payload = an, Modus = new CommandModus.Client(-1), CorrelationId = korrelation, OriginSessionId = proxy.SessionId,
                        Ursache = ursache,
                    }, ct);
                else
                    await proxy.SendeNachrichtAsync(command, id, korrelation, ct, ursache);
                if (await Task.WhenAny(antwort.Task, Task.Delay(_antwortfrist, ct)) == antwort.Task)
                {
                    if (antwort.Task.Result.Payload is CommandFailed f)
                        logger?.LogWarning("[Reaktionen] {Name}: {Command} scheiterte — {Grund}", name, command.GetType().Name, f.Reason);
                    return;
                }
            }
            throw new ReaktionUnterbrochen($"{command.GetType().Name} bleibt nach {Versuche} Versuchen ohne Antwort");
        }
        finally { _offen.TryRemove(korrelation, out _); }
    }

    /// <summary>Liest den Event-Kanal der Verbindung: eine korrelierte Antwort beendet das Warten ihres Commands, jedes Event weckt den Läufer.</summary>
    private async Task LiesEventsAsync(CancellationToken ct)
    {
        try
        {
            await foreach (var e in proxy.Events.ReadAllAsync(ct))
            {
                if (!string.IsNullOrEmpty(e.CorrelationId) && _offen.TryGetValue(e.CorrelationId, out var antwort)) antwort.TrySetResult(e);
                Interlocked.Exchange(ref _weckruf, NeuerWeckruf()).TrySetResult();
            }
        }
        catch (OperationCanceledException) { }
        catch (System.Threading.Channels.ChannelClosedException) { }
    }
}
