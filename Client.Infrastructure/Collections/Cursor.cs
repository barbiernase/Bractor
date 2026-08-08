namespace Client.Infrastructure.Collections;

public class Cursor<TKey> : IVersioned, ITrackedCollection where TKey : struct
{
    public TKey? Id { get; private set; }
    public int Index { get; private set; }
    public int Version { get; private set; }
    private int? _pending;
    private bool _hasChanges;

    public void Select(int index, TKey id) { Index = index; Id = id; Version++; _hasChanges = true; }
    public void Clear() { Index = 0; Id = default; Version++; _hasChanges = true; }
    public void SetPending(int index) => _pending = index;
    public int? ConsumePending() { var v = _pending; _pending = null; return v; }

    /// <summary>
    /// Gibt true zurück wenn seit dem letzten Aufruf Select/Clear lief.
    /// Wird von StoreBase.Flush() abgefragt.
    /// </summary>
    public bool ConsumeHasChanges()
    {
        var had = _hasChanges;
        _hasChanges = false;
        return had;
    }
}
