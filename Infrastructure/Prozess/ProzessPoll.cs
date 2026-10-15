using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Abstractions;

namespace Infrastructure.Prozess;

/// <summary>
/// Der Poll des Dirigenten (docs/04 §4.5): heilt verlorene Weckungen nach Korrelation, das Gegenstück zum <c>Poller</c> der Emittenten.
///
/// PF-4: er liest das Log seitenweise ab seiner Hochwassermarke (<see cref="IEventStoreRepository.ReadPartitionAsync"/>, eine Partition)
/// und routet die geladenen Events direkt (<see cref="ProzessPollFilter"/>) — früher las er jeden geänderten Stream ab Version 0 nach und
/// warf die schon geladenen Events weg. Starts weckt er nur für Vorgänge, die es noch nicht gibt oder die offen sind (eine Abfrage je
/// Seite); je Korrelation weckt er einmal je Seite.
///
/// G-8: die Weckungen sind bestätigt (<c>wecke</c> meldet, ob das WakeAck kam). Die Marke rückt nur bis vor das erste Event, dessen
/// Weckung unbestätigt blieb — ein Vorgang mit verlorenem Start-Signal wird so beim nächsten Durchlauf wieder geweckt, statt für immer
/// ungestartet zu bleiben. Gespeichert wird die Marke nach jeder Seite: ein Neustart beginnt nicht wieder am Anfang des Durchlaufs.
/// </summary>
public sealed class ProzessPoll
{
    /// <summary>Events je Seite.</summary>
    public const int SeitenGroesse = 500;
    /// <summary>Gleichzeitige Weckungen je Seite.</summary>
    public const int Parallel = 32;

    private readonly IEventStoreRepository _store;
    private readonly IProzessOffenIndex _offenIndex;
    private readonly IReadOnlyDictionary<Type, string> _auslöserZuProzess;
    private readonly Func<ProzessPollWeckung, CancellationToken, Task<bool>> _wecke;
    private readonly Func<long, CancellationToken, Task> _merke;
    private long _hochwasser;

    /// <param name="wecke">Weckt einen Manager und meldet, ob er bestätigt hat (<c>false</c>: Zeitüberschreitung, Fehler, kein Ack).</param>
    /// <param name="merke">Schreibt die Hochwassermarke durabel (Poll-Cursor).</param>
    public ProzessPoll(
        IEventStoreRepository store,
        IProzessOffenIndex offenIndex,
        IReadOnlyDictionary<Type, string> auslöserZuProzess,
        Func<ProzessPollWeckung, CancellationToken, Task<bool>> wecke,
        Func<long, CancellationToken, Task> merke,
        long hochwasser)
    {
        _store = store;
        _offenIndex = offenIndex;
        _auslöserZuProzess = auslöserZuProzess;
        _wecke = wecke;
        _merke = merke;
        _hochwasser = hochwasser;
    }

    public long Hochwasser => _hochwasser;

    /// <summary>Ein Durchlauf: Seiten lesen und routen, bis eine nicht voll war oder eine Weckung unbestätigt blieb. Liefert die Zahl der Weckungen.</summary>
    public async Task<int> DurchlaufAsync(CancellationToken ct = default)
    {
        var geweckt = 0;
        while (true)
        {
            var seite = await _store.ReadPartitionAsync(_hochwasser, 0, 1, null, Array.Empty<long>(), SeitenGroesse, ct);
            var events = seite.Events.Where(e => e.Sequenz > _hochwasser).ToList();
            if (events.Count == 0)
            {
                // Nichts zu routen: die Marke läuft bis zur gesetzten Sequenz (Tombstones, Lücken).
                await RueckeVorAsync(seite.Gesetzt, ct);
                return geweckt;
            }

            var offene = new HashSet<Guid>(await _offenIndex.ListeOffeneAsync(ct));
            var kandidaten = events
                .Select(e => (e.Sequenz, Weckung: ProzessPollFilter.Weckung(e.Event, _auslöserZuProzess, offene)))
                .Where(x => x.Weckung is not null)
                .Select(x => (x.Sequenz, Weckung: x.Weckung!))
                .ToList();
            var starts = kandidaten.Where(x => x.Weckung.ProzessNameFürStart is not null && !offene.Contains(x.Weckung.Korrelation))
                .Select(x => x.Weckung.Korrelation).Distinct().ToList();
            var vorhanden = starts.Count == 0
                ? new HashSet<Guid>()
                : new HashSet<Guid>(await _store.StreamsVorhandenAsync(starts, ct));

            // Je Korrelation eine Weckung (ein Start geht vor, er trägt den Prozess-Namen); für die Marke zählt ihr frühestes Event.
            var weckungen = kandidaten
                .Where(x => x.Weckung.ProzessNameFürStart is null || ProzessPollFilter.StartNoetig(x.Weckung.Korrelation, offene, vorhanden))
                .GroupBy(x => x.Weckung.Korrelation)
                .Select(g => (Erste: g.Min(x => x.Sequenz),
                              Weckung: g.Select(x => x.Weckung).FirstOrDefault(w => w.ProzessNameFürStart is not null) ?? g.First().Weckung))
                .ToList();

            long? ersteUnbestaetigte = null;
            var sperre = new object();
            await System.Threading.Tasks.Parallel.ForEachAsync(weckungen,
                new ParallelOptions { MaxDegreeOfParallelism = Parallel, CancellationToken = ct },
                async (w, c) =>
                {
                    bool bestaetigt;
                    try { bestaetigt = await _wecke(w.Weckung, c); }
                    catch (OperationCanceledException) when (ct.IsCancellationRequested) { throw; }
                    catch { bestaetigt = false; }
                    if (!bestaetigt)
                        lock (sperre) ersteUnbestaetigte = ersteUnbestaetigte is long e ? Math.Min(e, w.Erste) : w.Erste;
                });
            geweckt += weckungen.Count;

            // Bis wohin die Seite erledigt ist: höchstens bis zur gesetzten Sequenz (Nachzügler-Regel), bei voller Seite bis zu ihrem
            //   letzten Event, und nie über eine unbestätigte Weckung (G-8).
            var grenze = seite.Gesetzt;
            if (seite.Voll) grenze = Math.Min(grenze, events[^1].Sequenz);
            if (ersteUnbestaetigte is long u) grenze = Math.Min(grenze, u - 1);
            var vorher = _hochwasser;
            await RueckeVorAsync(grenze, ct);
            if (!seite.Voll || ersteUnbestaetigte is not null || _hochwasser == vorher) return geweckt;
        }
    }

    private async Task RueckeVorAsync(long grenze, CancellationToken ct)
    {
        if (grenze <= _hochwasser) return;
        _hochwasser = grenze;
        await _merke(grenze, ct);
    }
}
