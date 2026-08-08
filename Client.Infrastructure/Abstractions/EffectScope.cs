using Client.Infrastructure.Collections;
namespace Client.Infrastructure.Abstractions;

public sealed class EffectScope : IDisposable
{
    private readonly IBus _bus;
    private readonly List<IDisposable> _busSubs = new();
    private readonly List<(StoreBase Store, Action Handler)> _storeSubs = new();

    public EffectScope(IBus bus) => _bus = bus;

    /// <summary>Store Changed → immer Re-Render.</summary>
    public void OnChanged(StoreBase store, Action handler)
    {
        store.Changed += handler;
        _storeSubs.Add((store, handler));
    }

    /// <summary>Store Changed → nur wenn facet.Version gestiegen ist.</summary>
    public void OnChanged(StoreBase store, IVersioned facet, Action handler)
    {
        var last = facet.Version;
        void Check() { var v = facet.Version; if (v == last) return; last = v; handler(); }
        store.Changed += Check;
        _storeSubs.Add((store, Check));

        // Initiale Synchronisation: waren beim Subscribe schon Daten da
        // (Version > 0), einmal rendern. Sonst geht die Erst-Befüllung
        // verloren, wenn die Daten VOR OnInitialized ankamen — genau das
        // "leere Liste beim Start"-Symptom.
        if (facet.Version > 0) handler();
    }

    /// <summary>Store Changed → nur wenn selector() neuen Wert liefert.</summary>
    public void OnChanged<T>(StoreBase store, Func<T> selector, Action handler)
    {
        var lastValue = selector();
        void Check() { var c = selector(); if (EqualityComparer<T>.Default.Equals(c, lastValue)) return; lastValue = c; handler(); }
        store.Changed += Check;
        _storeSubs.Add((store, Check));
    }

    public void On<TEvent>(Action<TEvent> effect) where TEvent : notnull
        => _busSubs.Add(_bus.Subscribe<TEvent>((evt, _) => effect(evt)));

    public void Dispatch(object message) => _bus.Publish(message);

    public void Dispose()
    {
        foreach (var sub in _busSubs) sub.Dispose(); _busSubs.Clear();
        foreach (var (store, handler) in _storeSubs) store.Changed -= handler; _storeSubs.Clear();
    }
}
