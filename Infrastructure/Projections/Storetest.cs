using Abstractions;

namespace Infrastructure.Projections;

/// <summary>
/// Was der allgemeine Store-Test von einem Treiber braucht: ein frisches Ziel (wie in einer neuen Aktivierung), eine Wirkung je Event über die
/// Fähigkeiten des Stores und was ein Leser danach sieht. Die Wirkung schreibt je Event einen Eintrag (append-artig — eine doppelte Wirkung wäre
/// sichtbar) und je Stream den Stand (die Version des Events — eine zweite Aktivierung mit älterem Stand darf ihn nicht zurückdrehen); wie, ist
/// Sache des Treibers und seiner Klasse (Klasse 1 in der Transaktion, Klasse 2 mit der Event-Identität im Schlüssel bzw. bedingt).
/// </summary>
public interface IStoretestTreiber
{
    IProjektionsZiel NeuesZiel();

    Task WirkeAsync(IProjektionsZiel ziel, Herkunft h);

    /// <summary>Je Stream: wie viele Einträge ein Leser sieht und welchen Stand.</summary>
    Task<IReadOnlyDictionary<Guid, (int Eintraege, int Stand)>> LiesAsync(IReadOnlyCollection<Guid> streams);
}

/// <summary>
/// Der <b>allgemeine Store-Test</b> (Plan Pub/Sub-Neustrukturierung §2.4, §4.4 Nr. 2), den das Framework mitliefert: jeder Treiber besteht ihn einmal
/// — gegen ein echtes Log (eigenes Schema, nur seine Events) und den echten Batch-Leser. Vier Fälle:
/// <list type="number">
///   <item><b>Batch-Abbruch:</b> der erste Batch bricht nach dem Anwenden vor dem Schreiben ab; eine neue Aktivierung liest von vorn.</item>
///   <item><b>Wiederholung:</b> die Wirkungen sind geschrieben, der Fortschritt nicht (Klasse 2 und 3; bei Klasse 1 schreibt beides eine Transaktion,
///     Fall 1 deckt es ab); eine neue Aktivierung wendet denselben Batch noch einmal an.</item>
///   <item><b>Zweite Aktivierung mit älterem Stand:</b> A hat geladen und angewandt; bevor A schreibt, kommen neue Events und B verarbeitet alles; dann
///     schreibt A — ihre Wirkungen dürfen nichts verdoppeln und keinen Stand zurückdrehen, ihr Fortschritt verliert.</item>
///   <item><b>Neuaufbau:</b> neue Generation, Fortschritt auf Anfang; eine alte Aktivierung schreibt danach noch (neue Events), dann baut der
///     Neuaufbau auf und schaltet um — gelesen wird genau das Log.</item>
/// </list>
/// Erwartet wird je Stream: Klasse 1 und 2 genau so viele Einträge wie Events und der Stand des letzten; Klasse 3 mindestens so viele (nichts geht
/// verloren) und der Stand des letzten.
/// </summary>
public static class Storetest
{
    /// <summary>Ein Lauf: das Log (eigenes Schema), der Fortschritt beim Framework (für Klasse 2/3) und das Event, das der Test schreibt (ein im Log
    /// registrierter, persistenter Typ — sein Inhalt ist egal).</summary>
    public sealed record Lauf(IEventStoreRepository Log, IFortschrittBeimFramework Framework, Func<IEvent> Ereignis);

    /// <summary>Führt die vier Fälle aus; liefert die Abweichungen (leer = bestanden).</summary>
    public static async Task<IReadOnlyList<string>> PruefeAsync(IStoretestTreiber treiber, Lauf lauf, CancellationToken ct = default)
    {
        var fehler = new List<string>();
        var klasse = Projektionsnaht.Klassenzahl(treiber.NeuesZiel());
        var konsum = new Konsum(1, 50);
        var typ = lauf.Ereignis().GetType();
        var typen = BatchLeser.TypenAus(new[] { typ });
        var lauf_ = Guid.NewGuid().ToString("N")[..8];

        Projektionsnaht Naht() => new(treiber.NeuesZiel(), lauf.Framework);
        Func<EventEnvelope, CancellationToken, Task> Dispatch(Projektionsnaht n, string kennung) => (e, _) =>
            e.Payload.GetType() == typ ? treiber.WirkeAsync(n.Ziel, new Herkunft(e.AggregateId, e.AggregateVersion, e.CreatedAtUtc, null, kennung)) : Task.CompletedTask;
        BatchLeser Leser(Projektionsnaht n, string kennung, Func<Task>? absturz = null) =>
            new(lauf.Log, n, kennung, 0, konsum, typen, Dispatch(n, kennung), absturzVorCommit: absturz);

        async Task<Dictionary<Guid, int>> SchreibeAsync(Dictionary<Guid, int> streams, int je)
        {
            foreach (var s in streams.Keys.ToList())
            {
                await lauf.Log.AppendEventsAsync(s, streams[s], Enumerable.Range(0, je).Select(_ => lauf.Ereignis()).ToList());
                streams[s] += je;
            }
            return streams;
        }

        async Task VergleicheAsync(string fall, Dictionary<Guid, int> soll)
        {
            var ist = await treiber.LiesAsync(soll.Keys);
            foreach (var (s, n) in soll)
            {
                var (eintraege, stand) = ist.TryGetValue(s, out var w) ? w : (0, 0);
                var eintraegeOk = klasse == 3 ? eintraege >= n : eintraege == n;
                if (!eintraegeOk || stand != n)
                    fehler.Add($"{fall}: Stream {s:N} — {eintraege} Einträge, Stand {stand}; erwartet {(klasse == 3 ? "mindestens " : "")}{n} Einträge, Stand {n}");
            }
        }

        // ── 1. Batch-Abbruch ──
        {
            var kennung = $"storetest-{lauf_}-abbruch";
            var soll = await SchreibeAsync(Enumerable.Range(0, 3).ToDictionary(_ => Guid.NewGuid(), _ => 0), 3);
            try
            {
                await Leser(Naht(), kennung, () => throw new StoretestAbbruch()).LaufAsync(ct);
                fehler.Add("Batch-Abbruch: der Lauf brach nicht ab");
            }
            catch (StoretestAbbruch) { }
            await Leser(Naht(), kennung).LaufAsync(ct);
            await VergleicheAsync("Batch-Abbruch", soll);
        }

        // ── 2. Wiederholung (Wirkung geschrieben, Fortschritt nicht) ──
        if (klasse != 1)
        {
            var kennung = $"storetest-{lauf_}-wiederholung";
            var soll = await SchreibeAsync(Enumerable.Range(0, 3).ToDictionary(_ => Guid.NewGuid(), _ => 0), 3);
            var n = new Projektionsnaht(treiber.NeuesZiel(), new FortschrittScheitert(lauf.Framework));
            try
            {
                // Die Wirkungen gehen durch (der Leser versucht die Seite getrennt erneut — auch das wiederholt sie), der Fortschritt scheitert.
                await Leser(n, kennung).LaufAsync(ct);
                fehler.Add("Wiederholung: der Fortschritt scheiterte nicht");
            }
            catch (StoretestAbbruch) { }
            await Leser(Naht(), kennung).LaufAsync(ct);
            await VergleicheAsync("Wiederholung", soll);
        }

        // ── 3. Zweite Aktivierung mit älterem Stand ──
        {
            var kennung = $"storetest-{lauf_}-zwei";
            var soll = await SchreibeAsync(Enumerable.Range(0, 3).ToDictionary(_ => Guid.NewGuid(), _ => 0), 3);
            var einmal = true;
            var a = Leser(Naht(), kennung, async () =>
            {
                if (!einmal) return;
                einmal = false;
                await SchreibeAsync(soll, 2);                      // neue Events, die A nicht kennt
                await Leser(Naht(), kennung).LaufAsync(ct);         // B verarbeitet alles
            });
            await a.LaufAsync(ct);                                   // A schreibt mit älterem Stand, verliert, lädt neu
            await VergleicheAsync("Zweite Aktivierung", soll);
        }

        // ── 4. Neuaufbau über eine neue Generation ──
        {
            var kennung = $"storetest-{lauf_}-neuaufbau";
            var soll = await SchreibeAsync(Enumerable.Range(0, 3).ToDictionary(_ => Guid.NewGuid(), _ => 0), 2);
            var alt = Naht();
            var alterLeser = Leser(alt, kennung);
            await alterLeser.LaufAsync(ct);                         // der alte Stand steht

            var generation = Generationen.Neu(DateTimeOffset.UtcNow);
            var neu = Naht();
            await neu.Ziel.NeueGenerationAsync(generation, ct);
            await neu.Ablage.FortschrittZuruecksetzenAsync(kennung, konsum.Partitionen, generation, ct);

            await SchreibeAsync(soll, 1);
            await alterLeser.LaufAsync(ct);                         // die alte Aktivierung schreibt noch — verliert, lädt die neue Generation
            await Leser(neu, kennung).LaufAsync(ct);
            await neu.Ziel.UmschaltenAsync(generation, ct);
            await VergleicheAsync("Neuaufbau", soll);
        }

        return fehler;
    }

    private sealed class StoretestAbbruch : Exception
    {
        public StoretestAbbruch() : base("Store-Test: Abbruch") { }
    }

    /// <summary>Ein Fortschritt beim Framework, dessen Schreiben scheitert — die Wirkungen davor sind geschrieben.</summary>
    private sealed class FortschrittScheitert(IFortschrittBeimFramework echt) : IFortschrittBeimFramework
    {
        public Task<PartitionsCheckpoint> LadeFortschrittAsync(string projektion, int partition, CancellationToken ct)
            => echt.LadeFortschrittAsync(projektion, partition, ct);
        public Task<IReadOnlyDictionary<Guid, int>> LadeGuardsAsync(string projektion, IReadOnlyCollection<Guid> streams, CancellationToken ct)
            => echt.LadeGuardsAsync(projektion, streams, ct);
        public Task FortschrittZuruecksetzenAsync(string projektion, int partitionen, string generation, CancellationToken ct)
            => echt.FortschrittZuruecksetzenAsync(projektion, partitionen, generation, ct);
        public Task<int> SchreibeFortschrittAsync(PartitionsCheckpoint fortschritt, IReadOnlyDictionary<Guid, int> guards, CancellationToken ct)
            => throw new StoretestAbbruch();
    }
}
