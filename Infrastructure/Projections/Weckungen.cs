using System.Collections.Concurrent;

namespace Infrastructure.Projections;

/// <summary>
/// Fasst Weckungen je Ziel zusammen (Batch-Partition, Funktions-Partition): je Schlüssel ist höchstens eine Weckung unterwegs; kommen
/// währenddessen weitere, geht nach ihrer Quittung genau eine hinterher (das Ziel kann die erste vor dem neuen Event begonnen haben).
/// Ohne das wird jedes Signal eine eigene Weckung und jede Weckung ein eigener Read — unter Last staut sich die Mailbox des Ziels bis in den
/// Leerlauf (PF-12). Verlust ist folgenlos (Invariante 2): der Poll heilt.
/// </summary>
public sealed class Weckungen<TKey>(Func<TKey, Task> sende) where TKey : notnull
{
    // fehlt = frei, 1 = unterwegs, 2 = unterwegs und noch eine angemeldet
    private readonly ConcurrentDictionary<TKey, int> _zustand = new();

    public void Wecke(TKey ziel)
    {
        while (true)
        {
            if (_zustand.TryAdd(ziel, 1)) { _ = SendeAsync(ziel); return; }
            if (!_zustand.TryGetValue(ziel, out var alt)) continue;
            if (alt == 2 || _zustand.TryUpdate(ziel, 2, alt)) return;
        }
    }

    private async Task SendeAsync(TKey ziel)
    {
        while (true)
        {
            try { await sende(ziel); }
            catch { /* der Absender protokolliert; verloren ist folgenlos */ }
            if (_zustand.TryUpdate(ziel, 1, 2)) continue;                              // eine war angemeldet → hinterher
            if (_zustand.TryRemove(new KeyValuePair<TKey, int>(ziel, 1))) return;      // frei
        }
    }
}
