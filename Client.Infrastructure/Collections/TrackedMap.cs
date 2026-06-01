namespace Client.Infrastructure.Collections;

/// <summary>
/// Dictionary-backed Collection mit Delta-Tracking.
///
/// Alle Schreiboperationen erzeugen typisierte Deltas (Added, Replaced, Removed, Reset).
/// Lesezugriff erzeugt keine Deltas — kein Overhead beim Lesen durch Stores oder Handler.
///
/// FlushDeltas() gibt alle akkumulierten Deltas zurück und leert die Liste.
/// Wird von StoreBase.Flush nach DispatchCompleted aufgerufen — nie direkt von der View.
///
/// Unterbau: Dictionary für O(1) Lookup + separates _order-Array für stabile Index-Semantik.
/// API ist identisch zu einer ObservableCollections-basierten Variante —
/// der Unterbau kann gewechselt werden ohne Auswirkung auf Stores oder Views.
/// </summary>
public class TrackedMap<TKey, TValue> : ITrackedCollection
    where TKey : notnull
{
    private readonly Dictionary<TKey, TValue> _inner = new();
    private readonly List<TKey> _order = new();
    private readonly Func<TValue, TKey> _keySelector;
    private readonly List<CollectionDelta> _deltas = new();

    public TrackedMap(Func<TValue, TKey> keySelector)
    {
        _keySelector = keySelector;
    }

    // ═══════════════════════════════════════════════════
    // SCHREIBEN — jede Operation akkumuliert ein Delta
    // ═══════════════════════════════════════════════════

    /// <summary>
    /// Fügt einen neuen Eintrag hinzu oder ersetzt einen vorhandenen.
    /// → Added-Delta wenn Key neu, Replaced-Delta wenn Key schon existiert.
    /// </summary>
    public void Put(TValue value)
    {
        var key = _keySelector(value);

        if (_inner.ContainsKey(key))
        {
            _inner[key] = value;
            _deltas.Add(new Replaced<TKey>(key));
        }
        else
        {
            _inner[key] = value;
            _order.Add(key);
            _deltas.Add(new Added<TKey>(key, _order.Count - 1));
        }
    }

    /// <summary>
    /// Wendet eine Transformation auf einen vorhandenen Eintrag an.
    /// Gedacht für Record-with-Semantik: e => e with { Label = "neu" }
    /// → Replaced-Delta. Tut nichts wenn Key nicht existiert.
    /// </summary>
    public void Update(TKey key, Func<TValue, TValue> transform)
    {
        if (!_inner.TryGetValue(key, out var existing))
            return;

        _inner[key] = transform(existing);
        _deltas.Add(new Replaced<TKey>(key));
    }

    /// <summary>
    /// Entfernt einen Eintrag.
    /// → Removed-Delta. Tut nichts wenn Key nicht existiert.
    /// </summary>
    public void Remove(TKey key)
    {
        if (!_inner.Remove(key))
            return;

        _order.Remove(key);
        _deltas.Add(new Removed<TKey>(key));
    }

    /// <summary>
    /// Ersetzt alle Einträge atomisch.
    /// → Einzelnes Reset-Delta unabhängig von der Anzahl der Einträge.
    /// Vorherige Deltas im selben Dispatch-Zyklus bleiben erhalten —
    /// das Reset überschreibt sie semantisch beim Flush.
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

        _deltas.Add(new Reset<TKey>());
    }

    // ═══════════════════════════════════════════════════
    // LESEN — kein Delta-Tracking
    // ═══════════════════════════════════════════════════

    /// <summary>Gibt den Eintrag für einen Key zurück, oder default wenn nicht vorhanden.</summary>
    public TValue? Get(TKey key)
        => _inner.TryGetValue(key, out var value) ? value : default;

    /// <summary>Anzahl der Einträge.</summary>
    public int Count => _inner.Count;

    /// <summary>Zugriff über Position in der Einfügereihenfolge.</summary>
    public TValue this[int index] => _inner[_order[index]];

    /// <summary>Alle Werte in Einfügereihenfolge. Kein Delta.</summary>
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
    // TRANSAKTION
    // ═══════════════════════════════════════════════════

    /// <summary>
    /// Gibt alle akkumulierten Deltas zurück und leert die Liste.
    /// Rückgabetyp ist CollectionDelta (kein object-Boxing).
    /// Wird von StoreBase.Flush aufgerufen — nie direkt von außen.
    /// </summary>
    public IReadOnlyList<CollectionDelta> FlushDeltas()
    {
        if (_deltas.Count == 0)
            return Array.Empty<CollectionDelta>();

        var result = _deltas.ToArray();
        _deltas.Clear();
        return result;
    }
}
