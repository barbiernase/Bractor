using System;
using System.Collections.Generic;

namespace Infrastructure.Aggregate;

/// <summary>
/// Der Cache der Inbox im Aggregat-Actor (docs/04 §4.6): die zuletzt entschiedenen CommandIds mit HARTER Obergrenze (FIFO-Verdrängung in
/// Einfüge-Reihenfolge). Er erspart der häufigen Wiederholung kurz nach dem ersten Mal den Weg in die Datenbank; die Wahrheit ist die
/// Inbox-Tabelle — was der Cache vergessen hat, fängt ihr eindeutiger Schlüssel.
///
/// Reine store-freie Logik → in-memory (Prüfstand) beweisbar.
/// </summary>
public sealed class BoundedInbox
{
    private readonly int _cap;
    private readonly HashSet<Guid> _set;      // O(1)-Contains
    private readonly Queue<Guid> _order;      // Einfüge-Reihenfolge = Stream-Reihenfolge (ältestes zuerst raus)

    public BoundedInbox(int cap)
    {
        _cap = Math.Max(1, cap);
        _set = new HashSet<Guid>();
        _order = new Queue<Guid>();
    }

    /// <summary>
    /// Baut die Inbox aus einer GEORDNETEN Folge von CommandIds (ältestes zuerst) und
    /// behält nur die letzten <paramref name="cap"/>. Reihenfolge-erhaltend, damit der Snapshot-Roundtrip
    /// (<see cref="ToArray"/> → Guid[] → hier zurück) dieselbe Verdrängungs-Ordnung liefert.
    /// </summary>
    public static BoundedInbox FromOrdered(IEnumerable<Guid> orderedOldestFirst, int cap)
    {
        var inbox = new BoundedInbox(cap);
        foreach (var id in orderedOldestFirst) inbox.Add(id);
        return inbox;
    }

    public bool Contains(Guid id) => _set.Contains(id);

    /// <summary>Vermerkt eine verarbeitete CommandId; überschreitet das die Grenze, fällt die älteste raus.</summary>
    public void Add(Guid id)
    {
        if (!_set.Add(id)) return;   // schon vorhanden (defensiv) → nicht doppelt einreihen (Ordnung/Count bündig)
        _order.Enqueue(id);
        while (_order.Count > _cap)
        {
            var evicted = _order.Dequeue();
            _set.Remove(evicted);
        }
    }

    public int Count => _set.Count;

    /// <summary>Die aktuellen Ids in Einfüge-/Stream-Reihenfolge (ältestes zuerst) — so wandern sie in den Snapshot.</summary>
    public Guid[] ToArray() => _order.ToArray();
}
