namespace Client.Infrastructure.Collections;

public class PagedCollection<TKey, TRecord> : IVersioned, ITrackedCollection
    where TKey : notnull where TRecord : notnull
{
    private readonly List<TRecord> _items = new();
    private readonly Dictionary<TKey, int> _index = new();
    private readonly Func<TRecord, TKey> _keyOf;
    private bool _hasChanges;
    public PagedCollection(Func<TRecord, TKey> keyExtractor) => _keyOf = keyExtractor;

    public int Version { get; private set; }
    public int GesamtAnzahl { get; private set; }
    public int Seite { get; private set; } = 1;
    public int SeitenGroesse { get; private set; } = 50;
    public int GesamtSeiten => SeitenGroesse > 0 ? (int)Math.Ceiling((double)GesamtAnzahl / SeitenGroesse) : 0;

    public int Count => _items.Count;
    public TRecord this[int index] => _items[index];
    public TRecord? Get(TKey id) => _index.TryGetValue(id, out var i) ? _items[i] : default;
    public IEnumerable<TRecord> All => _items;

    public void ReplaceAll(IEnumerable<TRecord> items, int gesamt, int seite, int groesse)
    {
        _items.Clear(); _index.Clear();
        foreach (var item in items) { _index[_keyOf(item)] = _items.Count; _items.Add(item); }
        GesamtAnzahl = gesamt; Seite = seite; SeitenGroesse = groesse; Version++; _hasChanges = true;
    }
    public void Patch(TKey id, Func<TRecord, TRecord> mutate)
    { if (!_index.TryGetValue(id, out var i)) return; _items[i] = mutate(_items[i]); Version++; _hasChanges = true; }
    public void Put(TRecord item)
    {
        var key = _keyOf(item);
        if (_index.TryGetValue(key, out var i)) _items[i] = item;
        else { _index[key] = _items.Count; _items.Add(item); }
        Version++; _hasChanges = true;
    }

    /// <summary>
    /// Gibt true zurück wenn seit dem letzten Aufruf eine Schreiboperation lief.
    /// Wird von StoreBase.Flush() abgefragt.
    /// </summary>
    public bool ConsumeHasChanges()
    {
        var had = _hasChanges;
        _hasChanges = false;
        return had;
    }
}
