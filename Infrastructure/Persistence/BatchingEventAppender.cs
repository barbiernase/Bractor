using System.Threading.Channels;
using Abstractions;
using Microsoft.Extensions.Logging;
using IEvent = Abstractions.IEvent;

namespace Infrastructure.Persistence;

/// <summary>
/// Node-lokaler Group-Commit-Dekorator um einen <see cref="IEventStoreRepository"/>: sammelt die
/// Aggregat-Appends der auf DIESEM Node ansässigen Actors und schreibt sie gebündelt in EINER
/// Transaktion (über <see cref="IEventBatchWriter"/>). Lesezugriffe reicht er unverändert an den
/// inneren Store durch — nur der Schreib-Hotpath wird gebündelt.
///
/// <para><b>Warum node-lokal (verteilt-tauglich):</b> Proto.Cluster garantiert Single-Activation —
/// ein Stream lebt zu jedem Zeitpunkt auf genau einem Node. Zwei Nodes bündeln also nie denselben
/// Stream; OCC pro Stream bleibt korrekt, ohne Cross-Node-Koordination. N Nodes → N unabhängige
/// Batch-Ströme gegen das geteilte Postgres.</para>
///
/// <para><b>Die tragende Regel (Durabilitäts-Grenze):</b> Der zurückgegebene Task löst erst, wenn der
/// BATCH committet hat — nicht beim Einreihen. Der Actor mutiert seinen State und quittiert Erfolg erst
/// danach ("append vor Mutation" bleibt). Enqueue ≠ durabel.</para>
///
/// <para><b>Fehlermodell (append-only, nie verwerfen):</b> Der Batch ist EINE Transaktion → alles-oder-
/// nichts, kein Teil-Commit. Wirft der Group-Commit (z.B. OCC-Konflikt EINES Streams), wurde nichts
/// angehängt; der Appender wiederholt dann jeden Auftrag EINZELN über den inneren Store
/// (echte OCC-Semantik/Exception-Mapping) → nur der Schuldige fällt, die Unschuldigen committen.</para>
///
/// <para><b>Single-Writer bleibt:</b> Der Actor awaited seine Batch-Quittung, bevor er den nächsten
/// eigenen Command verarbeitet → nie zwei Appends desselben Streams gleichzeitig im Batch.</para>
/// </summary>
public sealed class BatchingEventAppender : IEventStoreRepository, IAsyncDisposable
{
    private sealed class Pending
    {
        public Guid StreamId;
        public int ExpectedVersion;
        public IReadOnlyList<IEvent> Events = Array.Empty<IEvent>();
        public string? Corr;
        public string? Caus;
        public string? AggType;
        // RunContinuationsAsynchronously: die Actor-Fortsetzung darf NICHT inline auf dem Flush-Loop-Thread
        // laufen (sonst serialisiert der Loop hinter Actor-Arbeit und der Group-Commit verpufft).
        public readonly TaskCompletionSource Tcs = new(TaskCreationOptions.RunContinuationsAsynchronously);
    }

    private readonly IEventStoreRepository _inner;
    private readonly IEventBatchWriter _writer;
    private readonly ILogger<BatchingEventAppender> _logger;
    private readonly int _maxBatch;
    private readonly int _lingerMs;

    private readonly Channel<Pending> _channel =
        Channel.CreateUnbounded<Pending>(new UnboundedChannelOptions { SingleReader = true });
    private readonly CancellationTokenSource _disposeCts = new();
    private readonly Task _loop;

    public BatchingEventAppender(
        IEventStoreRepository inner,
        IEventBatchWriter writer,
        ILogger<BatchingEventAppender> logger,
        int maxBatch = 256,
        int lingerMs = 0)
    {
        _inner = inner ?? throw new ArgumentNullException(nameof(inner));
        _writer = writer ?? throw new ArgumentNullException(nameof(writer));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _maxBatch = maxBatch > 0 ? maxBatch : 256;
        _lingerMs = lingerMs < 0 ? 0 : lingerMs;
        _loop = Task.Run(RunAsync);
    }

    // ── Schreib-Hotpath: einreihen, Task = Durabilitäts-Quittung des Batches ──
    public Task AppendEventsAsync(
        Guid aggregateId,
        int expectedVersion,
        IReadOnlyList<IEvent> events,
        string? correlationId = null,
        string? causationId = null,
        string? aggregateType = null)
    {
        if (expectedVersion < 0)
            throw new ArgumentException($"ExpectedVersion must be >= 0, was {expectedVersion}.", nameof(expectedVersion));
        if (events.Count == 0)
            return Task.CompletedTask;

        var p = new Pending
        {
            StreamId = aggregateId,
            ExpectedVersion = expectedVersion,
            Events = events,
            Corr = correlationId,
            Caus = causationId,
            AggType = aggregateType
        };

        if (!_channel.Writer.TryWrite(p))
            return Task.FromException(new ObjectDisposedException(nameof(BatchingEventAppender)));

        return p.Tcs.Task;
    }

    // ── Lesezugriffe unverändert an den inneren Store durchreichen ──
    public Task<StreamChanges> ReadChangedStreamsAsync(long afterGlobalSequence, CancellationToken ct)
        => _inner.ReadChangedStreamsAsync(afterGlobalSequence, ct);

    public Task<IReadOnlyList<EventEnvelope>> ReadStreamAsync(Guid streamId, int fromVersion, CancellationToken ct)
        => _inner.ReadStreamAsync(streamId, fromVersion, ct);

    public Task<TState?> LoadStateAsync<TState>(Guid aggregateId) where TState : class, IState, new()
        => _inner.LoadStateAsync<TState>(aggregateId);

    // ── Der eine Hintergrund-Reader: drainen → bündeln → committen ──
    private async Task RunAsync()
    {
        var reader = _channel.Reader;
        var buffer = new List<Pending>(_maxBatch);
        try
        {
            while (await reader.WaitToReadAsync(_disposeCts.Token).ConfigureAwait(false))
            {
                buffer.Clear();
                if (!reader.TryRead(out var first)) continue;
                buffer.Add(first);

                // Optionales Linger-Fenster: kurz sammeln, um größere Batches zu formen (bounded Latenz).
                // Default 0 = opportunistisch (unter Last füllt sich die Queue von selbst, während der
                // vorige Commit läuft — selbstregulierender Group-Commit).
                if (_lingerMs > 0)
                {
                    try { await Task.Delay(_lingerMs, _disposeCts.Token).ConfigureAwait(false); }
                    catch (OperationCanceledException) { /* Shutdown: trotzdem flushen, was da ist */ }
                }

                while (buffer.Count < _maxBatch && reader.TryRead(out var p)) buffer.Add(p);
                await FlushAsync(buffer).ConfigureAwait(false);
            }
        }
        catch (OperationCanceledException) { /* wird disposed */ }

        // Graceful Drain: nach Completion/Cancel verbliebene Aufträge isoliert schreiben, nicht verlieren.
        var rest = new List<Pending>();
        while (reader.TryRead(out var p)) rest.Add(p);
        if (rest.Count > 0) await IsolateAsync(rest).ConfigureAwait(false);
    }

    private async Task FlushAsync(List<Pending> batch)
    {
        var appends = new BatchAppend[batch.Count];
        for (var i = 0; i < batch.Count; i++)
        {
            var p = batch[i];
            appends[i] = new BatchAppend(p.StreamId, p.ExpectedVersion, p.Events, p.Corr, p.Caus, p.AggType);
        }

        try
        {
            await _writer.WriteBatchAsync(appends, CancellationToken.None).ConfigureAwait(false);
            foreach (var p in batch) p.Tcs.TrySetResult();
        }
        catch (Exception ex)
        {
            // Der Group-Commit rollte atomar zurück (nichts angehängt) → jeden Auftrag EINZELN über den
            // inneren Store. Der Schuldige (OCC/technischer Fehler) fällt allein, die Unschuldigen committen.
            _logger.LogWarning(ex,
                "Group-Commit ({Count} Appends) fehlgeschlagen — Isolations-Retry einzeln.", batch.Count);
            await IsolateAsync(batch).ConfigureAwait(false);
        }
    }

    private async Task IsolateAsync(List<Pending> batch)
    {
        foreach (var p in batch)
        {
            try
            {
                await _inner.AppendEventsAsync(p.StreamId, p.ExpectedVersion, p.Events, p.Corr, p.Caus, p.AggType)
                    .ConfigureAwait(false);
                p.Tcs.TrySetResult();
            }
            catch (Exception ex)
            {
                p.Tcs.TrySetException(ex);
            }
        }
    }

    public async ValueTask DisposeAsync()
    {
        _channel.Writer.TryComplete();
        _disposeCts.Cancel();
        try { await _loop.ConfigureAwait(false); }
        catch { /* best effort */ }
        _disposeCts.Dispose();
    }
}
