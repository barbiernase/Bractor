using Client.Infrastructure.Abstractions;

namespace Client.Infrastructure.Bus;

/// <summary>
/// Implementierung des hybriden Client-Bus (E2).
/// 
/// Kein Reflection, kein dynamic — alles über Dictionary&lt;Type, List&gt; Lookup.
///
/// Generische und nicht-generische Subscriber leben im SELBEN Dictionary.
/// Generische Subscribe&lt;T&gt; wrappen den typisierten Handler in einen object-Handler.
/// Publish(object) und Publish&lt;T&gt; nutzen denselben Dispatch-Pfad.
/// </summary>
public class ClientBus : IBus
{
    private readonly Dictionary<Type, List<SyncEntry>> _syncSubs = new();
    private readonly Dictionary<Type, List<AsyncEntry>> _asyncSubs = new();
    private readonly SynchronizationContext? _syncContext;

    private const int MaxDepth = 16;
    private int _depth;

    public ClientBus(SynchronizationContext? syncContext = null)
    {
        _syncContext = syncContext;
    }

    // ═══════════════════════════════════════════════════
    // PUBLISH
    // ═══════════════════════════════════════════════════

    /// <summary>Generisch — delegiert an nicht-generische Variante.</summary>
    public void Publish<T>(T message, MessageContext context) where T : notnull
        => Publish((object)message, context);

    public void Publish<T>(T message) where T : notnull
        => Publish((object)message, MessageContext.Local());

    /// <summary>
    /// Nicht-generischer Publish. Routet via message.GetType() Lookup.
    /// Kein Reflection, kein dynamic — nur Dictionary + Delegate-Invoke.
    /// </summary>
    public void Publish(object message, MessageContext context)
    {
        if (_depth >= MaxDepth)
            throw new InvalidOperationException(
                $"Bus dispatch depth {_depth} exceeded maximum of {MaxDepth}. " +
                $"Possible cycle involving {message.GetType().Name}.");

        var messageType = message.GetType();

        _depth++;
        try
        {
            // 1. Sync-Subscriber: sofort, depth-first, gleicher Thread
            if (_syncSubs.TryGetValue(messageType, out var syncEntries))
            {
                // Snapshot weil Handler während Dispatch subscriben/unsubscriben können
                foreach (var entry in syncEntries.ToArray())
                {
                    if (!entry.IsDisposed)
                        entry.Handler(message, context);
                }
            }

            // 2. Async-Subscriber: fire-and-forget
            if (_asyncSubs.TryGetValue(messageType, out var asyncEntries))
            {
                foreach (var entry in asyncEntries.ToArray())
                {
                    if (!entry.IsDisposed)
                        _ = InvokeAsyncSafe(entry.Handler, message, context);
                }
            }
        }
        finally
        {
            _depth--;
        }
    }

    // ═══════════════════════════════════════════════════
    // SUBSCRIBE — generisch (wrappen in object-Handler)
    // ═══════════════════════════════════════════════════

    public IDisposable Subscribe<T>(Action<T, MessageContext> handler) where T : notnull
        => Subscribe(typeof(T), (obj, ctx) => handler((T)obj, ctx));

    public IDisposable SubscribeAsync<T>(Func<T, MessageContext, Task> handler) where T : notnull
        => SubscribeAsync(typeof(T), (obj, ctx) => handler((T)obj, ctx));

    // ═══════════════════════════════════════════════════
    // SUBSCRIBE — nicht-generisch (kein Reflection)
    // ═══════════════════════════════════════════════════

    public IDisposable Subscribe(Type messageType, Action<object, MessageContext> handler)
    {
        if (!_syncSubs.TryGetValue(messageType, out var list))
        {
            list = new List<SyncEntry>();
            _syncSubs[messageType] = list;
        }

        var entry = new SyncEntry(handler);
        list.Add(entry);

        return new Subscription(() =>
        {
            entry.IsDisposed = true;
            list.Remove(entry);
        });
    }

    public IDisposable SubscribeAsync(Type messageType, Func<object, MessageContext, Task> handler)
    {
        if (!_asyncSubs.TryGetValue(messageType, out var list))
        {
            list = new List<AsyncEntry>();
            _asyncSubs[messageType] = list;
        }

        var entry = new AsyncEntry(handler);
        list.Add(entry);

        return new Subscription(() =>
        {
            entry.IsDisposed = true;
            list.Remove(entry);
        });
    }

    // ═══════════════════════════════════════════════════
    // ASYNC → UI-THREAD BRIDGE
    // ═══════════════════════════════════════════════════

    /// <summary>
    /// Postet eine Aktion auf den UI-Thread (SynchronizationContext).
    /// Führt die Aktion direkt aus wenn kein SyncContext gesetzt ist (Tests).
    /// </summary>
    public void PostToSyncContext(Action action)
    {
        if (_syncContext != null)
            _syncContext.Post(_ => action(), null);
        else
            action();
    }

    /// <summary>Aktuelle Dispatch-Tiefe. Nützlich für Diagnostik.</summary>
    public int CurrentDepth => _depth;

    // ═══════════════════════════════════════════════════
    // INTERNALS
    // ═══════════════════════════════════════════════════

    private async Task InvokeAsyncSafe(
        Func<object, MessageContext, Task> handler, object message, MessageContext context)
    {
        try
        {
            await handler(message, context);
        }
        catch (Exception ex)
        {
            PostToSyncContext(() =>
                Publish(new BusError(message.GetType().Name, ex.Message, ex), MessageContext.Local()));
        }
    }

    // ─── Einträge ───

    private sealed class SyncEntry(Action<object, MessageContext> handler)
    {
        public Action<object, MessageContext> Handler { get; } = handler;
        public bool IsDisposed { get; set; }
    }

    private sealed class AsyncEntry(Func<object, MessageContext, Task> handler)
    {
        public Func<object, MessageContext, Task> Handler { get; } = handler;
        public bool IsDisposed { get; set; }
    }

    private sealed class Subscription(Action unsubscribe) : IDisposable
    {
        private bool _disposed;
        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            unsubscribe();
        }
    }
}