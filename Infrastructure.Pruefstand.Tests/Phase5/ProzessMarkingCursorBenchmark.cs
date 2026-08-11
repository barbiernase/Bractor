using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Abstractions;
using Domain.Sammelauftrag;
using Domain.Sammelueberweisung;
using Infrastructure.Prozess;
using Infrastructure.Testing;
using Xunit;
using Xunit.Abstractions;

namespace Infrastructure.Pruefstand.Phase5;

/// <summary>
/// P5b — Skalierungs-Benchmark: „hat sich der Cursor gelohnt?" Treibt den ECHTEN <see cref="ProzessManager"/>
/// über einen wachsenden Fan-out (Sammelüberweisung, N Ziele), einmal mit Voll-Fold (Cursor AUS) und einmal
/// mit Tail-Cursor (AN), und misst je N die gelesenen Ziel-Events UND die Wall-Clock des Folds. In-memory,
/// deterministisch — isoliert genau die optimierte Kost (gegen eine echte DB ist der Gewinn GRÖSSER, weil
/// jeder vermiedene Read ein DB-Roundtrip ist). Schreibt eine Tabelle in den Scratchpad.
/// </summary>
public class ProzessMarkingCursorBenchmark
{
    private static readonly CancellationToken Ct = CancellationToken.None;
    private readonly ITestOutputHelper _out;
    public ProzessMarkingCursorBenchmark(ITestOutputHelper o) => _out = o;

    private sealed record Messung(int N, long Reads, long Wakes, double Ms);

    private static async Task<Messung> LaufAsync(int n, bool cursorAn)
    {
        var korr = Guid.NewGuid();
        var trigger = Guid.NewGuid();
        var quelle = Guid.NewGuid();
        var ziele = Enumerable.Range(0, n).Select(_ => Guid.NewGuid()).ToList();
        var regeln = new SammelueberweisungsProzess().Regeln;

        var store = new MutableEventStore();
        var welt = new KontoWelt(store);
        await welt.EröffneKontoAsync(quelle, 1_000_000_000m);
        foreach (var z in ziele) await welt.EröffneKontoAsync(z, 0);
        await store.AppendEventsAsync(trigger, 0, new IEvent[] { new SammelUeberweisungBeauftragt(quelle, ziele, 1) },
            correlationId: korr.ToString(), causationId: Guid.NewGuid().ToString(), aggregateType: "SammelueberweisungsProzess");

        var registry = new Dictionary<string, ProzessRegeln> { ["SammelueberweisungsProzess"] = regeln };
        IProzessMarkingStore? markingStore = cursorAn ? new InMemoryProzessMarkingStore() : null;
        var mgr = new ProzessManager(store, registry, welt.DispatchAsync, markingStore: markingStore);

        // Die reine Treib-Phase messen (Seeding ist in beiden Modi identisch und außerhalb).
        var sw = Stopwatch.StartNew();
        long wakes = 0;
        await mgr.StarteAsync(korr, "SammelueberweisungsProzess", trigger, 1, Ct);
        while (!(await mgr.LadeStatusAsync(korr, Ct)).Beendet)
        {
            await mgr.WakeAsync(korr, Ct);
            if (++wakes > 5_000_000) throw new InvalidOperationException("kein Terminal");
        }
        sw.Stop();
        return new Messung(n, store.GeleseneEvents, wakes, sw.Elapsed.TotalMilliseconds);
    }

    [Fact]
    public async Task Benchmark_Fold_Skalierung_Vollfold_vs_Cursor()
    {
        var größen = new[] { 50, 100, 200, 400, 800 };

        // Warmup (JIT/Fold-Pfade), nicht gemessen.
        await LaufAsync(20, cursorAn: false);
        await LaufAsync(20, cursorAn: true);

        var sb = new StringBuilder();
        sb.AppendLine("P5b Fold-Skalierung — Sammelüberweisung (Fan-out N), in-memory, echter ProzessManager");
        sb.AppendLine();
        sb.AppendLine("   N |    Voll-Fold Reads |  Cursor Reads | Reads-Faktor |  Voll ms |  Cursor ms | Zeit-Faktor");
        sb.AppendLine("-----+--------------------+---------------+--------------+----------+------------+------------");

        foreach (var n in größen)
        {
            var aus = await LaufAsync(n, cursorAn: false);
            var an = await LaufAsync(n, cursorAn: true);

            // Gleiche Arbeit (gleiche Feuer-/Terminal-Entscheidung) → gleiche Weckungszahl; nur Reads/Zeit unterscheiden sich.
            Assert.Equal(aus.Wakes, an.Wakes);

            var readFaktor = (double)aus.Reads / Math.Max(1, an.Reads);
            var zeitFaktor = aus.Ms / Math.Max(0.001, an.Ms);
            sb.AppendLine($"{n,4} | {aus.Reads,18:n0} | {an.Reads,13:n0} | {readFaktor,10:0.0}× | {aus.Ms,8:0.0} | {an.Ms,10:0.0} | {zeitFaktor,9:0.0}×");

            // Kern-Assertion: der Cursor liest bei wachsendem N drastisch weniger (die Trennung wächst mit N).
            Assert.True(an.Reads < aus.Reads, $"N={n}: Cursor {an.Reads} < Voll {aus.Reads} erwartet");
        }

        var text = sb.ToString();
        _out.WriteLine(text);
        var pfad = Environment.GetEnvironmentVariable("P5B_BENCH_OUT");
        if (!string.IsNullOrEmpty(pfad)) File.WriteAllText(pfad, text);
    }
}
