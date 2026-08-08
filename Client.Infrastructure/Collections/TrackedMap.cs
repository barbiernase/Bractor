// ── Client.Infrastructure/Collections/TrackedMap.cs ──

namespace Client.Infrastructure.Collections;

/// <summary>
/// Dictionary-backed Collection mit Change-Tracking.
///
/// Alle Schreiboperationen setzen ein internes Change-Flag.
/// StoreBase.Flush() fragt das Flag ab und feuert Changed
/// wenn Mutationen stattfanden.
///
/// Kein Delta-Tracking (Added, Replaced, Removed, Reset) —
/// Blazor macht einen vollständigen Component-Diff,
/// feingranulare Deltas sind tote Komplexität.
///
/// Unterbau: Dictionary für O(1) Lookup + _order-Liste für stabile Index-Semantik.
/// </summary>
public class TrackedMap<TKey, TValue> : ITrackedCollection
    where TKey : notnull
{
    private readonly Dictionary<TKey, TValue> _inner = new();
    private readonly List<TKey> _order = new();
    private readonly Func<TValue, TKey> _keySelector;
    private bool _hasChanges;

    public TrackedMap(Func<TValue, TKey> keySelector)
    {
        _keySelector = keySelector;
    }

    // ═══════════════════════════════════════════════════
    // SCHREIBEN — jede Operation setzt das Change-Flag
    // ═══════════════════════════════════════════════════

    /// <summary>
    /// Fügt einen neuen Eintrag hinzu oder ersetzt einen vorhandenen.
    /// </summary>
    public void Put(TValue value)
    {
        var key = _keySelector(value);

        if (_inner.ContainsKey(key))
        {
            _inner[key] = value;
        }
        else
        {
            _inner[key] = value;
            _order.Add(key);
        }

        _hasChanges = true;
    }

    /// <summary>
    /// Wendet eine Transformation auf einen vorhandenen Eintrag an.
    /// Gedacht für Record-with-Semantik: e => e with { Label = "neu" }
    /// Tut nichts wenn Key nicht existiert.
    /// </summary>
    public void Update(TKey key, Func<TValue, TValue> transform)
    {
        if (!_inner.TryGetValue(key, out var existing))
            return;

        _inner[key] = transform(existing);
        _hasChanges = true;
    }

    /// <summary>
    /// Entfernt einen Eintrag.
    /// Tut nichts wenn Key nicht existiert.
    /// </summary>
    public void Remove(TKey key)
    {
        if (!_inner.Remove(key))
            return;

        _order.Remove(key);
        _hasChanges = true;
    }

    /// <summary>
    /// Ersetzt alle Einträge atomisch.
    /// </summary>
    public void ReplaceAll(IEnumerable<TValue> items)
    {
        _inner.Clear();
        _order.Clear();

        foreach (var item in items)
        {
            var key = _keySelector(item);
            _inner[key] = item;
            _order.Add(key);
        }

        _hasChanges = true;
    }

    // ═══════════════════════════════════════════════════
    // LESEN — kein Change-Tracking
    // ═══════════════════════════════════════════════════

    /// <summary>Gibt den Eintrag für einen Key zurück, oder default wenn nicht vorhanden.</summary>
    public TValue? Get(TKey key)
        => _inner.TryGetValue(key, out var value) ? value : default;

    /// <summary>Anzahl der Einträge.</summary>
    public int Count => _inner.Count;

    /// <summary>Zugriff über Position in der Einfügereihenfolge.</summary>
    public TValue this[int index] => _inner[_order[index]];

    /// <summary>Alle Werte in Einfügereihenfolge.</summary>
    public IEnumerable<TValue> Values
    {
        get
        {
            foreach (var key in _order)
                yield return _inner[key];
        }
    }

    /// <summary>True wenn der Key vorhanden ist.</summary>
    public bool ContainsKey(TKey key) => _inner.ContainsKey(key);

    // ═══════════════════════════════════════════════════
    // CHANGE-TRACKING
    // ═══════════════════════════════════════════════════

    /// <summary>
    /// Gibt true zurück wenn seit dem letzten Aufruf
    /// Schreiboperationen stattfanden. Setzt das Flag zurück.
    ///
    /// Wird von StoreBase.Flush() aufgerufen — nie direkt von außen.
    /// </summary>
    public bool ConsumeHasChanges()
    {
        var had = _hasChanges;
        _hasChanges = false;
        return had;
    }
}
