using System;
using System.Collections.Generic;

namespace Infrastructure.Aggregate;

/// <summary>
/// Die Framework-Inbox (verarbeitete CommandIds des idempotenten Reaktions-/Prozess-Pfads) mit HARTER
/// Obergrenze (Audit-Fix #4/H3). Bisher wuchs die Dedup-Menge — und ihre Kopie in JEDEM Snapshot
/// (<see cref="Abstractions.Snapshot{T}.ProcessedCommandIds"/>) — MONOTON: heiße, langlebige Reaktions-/
/// Prozess-Ziele (z.B. Konto) sammelten O(n) CommandIds für immer → Speicher + Snapshot-Größe +
/// Serialisierungskosten stiegen linear ohne Grenze.
///
/// Diese Struktur behält nur die letzten <c>cap</c> Ids (FIFO-Verdrängung in Einfüge-/Stream-Reihenfolge).
/// Das ist praktisch sicher, weil das Re-Delivery-Fenster KURZ ist: ein Prozess/eine Reaktion re-sendet
/// einen Vorgang nur, BIS er die Marke sieht — danach faltet er die Transition als „aufgelöst" und feuert
/// sie nie wieder; der Poll-Backstop re-liefert innerhalb ~30s. Eine Id, die älter als <c>cap</c> distinkte
/// Commands zurückliegt, wird also nicht mehr zugestellt. Der Tradeoff ist bewusst: airtight-Dedup bräuchte
/// unbegrenzten Speicher (siehe Audit-Diskussion — Weg A statt eines separaten Inbox-Stores/Weg C).
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
    /// Baut die Inbox aus einer GEORDNETEN Marken-Folge (ältestes zuerst — Snapshot-Seed dann Tail) und
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
