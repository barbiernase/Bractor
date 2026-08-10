using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Abstractions;
using FluentAssertions;
using Infrastructure.Deadlines;
using Infrastructure.Testing;

namespace Infrastructure.Pruefstand.Deadlines;

/// <summary>
/// Ebene 1 (reiner Kontrollfluss, Fakes) — der entkoppelte Deadline-Kern <see cref="FristScheduler.TickAsync"/>:
/// die aktuelle DB-Zeit holen, die fälligen Fristen feuern und danach entfernen. Das TIMING (der Loop) und die
/// echte DB-Uhr deckt der Integrationstest. Bewiesen wird: die Fälligkeit entscheidet die (Fake-)DB-Uhr, eine
/// gefeuerte Frist wird entfernt, eine künftige bleibt liegen.
/// </summary>
public class FristSchedulerTests
{
    private sealed class FakeUhr(DateTimeOffset jetzt) : IDbClock
    {
        public Task<DateTimeOffset> JetztAsync(CancellationToken ct = default) => Task.FromResult(jetzt);
    }

    private static readonly DateTimeOffset T0 = new(2026, 1, 1, 12, 0, 0, TimeSpan.Zero);

    private static Frist FristBei(DateTimeOffset fällig)
        => new(Guid.NewGuid(), fällig, Guid.NewGuid(), "kontext");

    [Fact]
    public async Task Feuert_nur_die_faelligen_und_entfernt_sie()
    {
        var plan = new InMemoryFristplan();
        var überfällig = FristBei(T0.AddMinutes(-1));
        var künftig = FristBei(T0.AddMinutes(+1));
        await plan.PlaneAsync(überfällig);
        await plan.PlaneAsync(künftig);

        var gefeuert = new List<Guid>();
        await FristScheduler.TickAsync(new FakeUhr(T0), plan,
            feuere: f => { gefeuert.Add(f.Id); return Task.CompletedTask; });

        // nur die gegen die DB-Uhr überfällige Frist feuert
        gefeuert.Should().Equal(überfällig.Id);
        // die gefeuerte Frist ist entfernt, die künftige bleibt liegen
        (await plan.FälligeAsync(T0.AddYears(1))).Select(f => f.Id).Should().Equal(künftig.Id);
    }

    [Fact]
    public async Task Die_DB_Uhr_entscheidet_die_Faelligkeit_nicht_die_Node_Zeit()
    {
        var plan = new InMemoryFristplan();
        var frist = FristBei(T0);
        await plan.PlaneAsync(frist);

        // DB-Uhr steht VOR der Frist → nichts feuert (auch wenn die reale Node-Zeit längst danach liegt).
        var gefeuert = new List<Guid>();
        await FristScheduler.TickAsync(new FakeUhr(T0.AddSeconds(-1)), plan,
            feuere: f => { gefeuert.Add(f.Id); return Task.CompletedTask; });
        gefeuert.Should().BeEmpty("die Fake-DB-Uhr steht vor der Fälligkeit");

        // DB-Uhr genau auf der Frist → sie feuert (≤-Grenze inklusiv).
        await FristScheduler.TickAsync(new FakeUhr(T0), plan,
            feuere: f => { gefeuert.Add(f.Id); return Task.CompletedTask; });
        gefeuert.Should().Equal(frist.Id);
    }

    [Fact]
    public async Task Ein_Feuer_Fehler_laesst_die_Frist_liegen_fuer_den_naechsten_Tick()
    {
        var plan = new InMemoryFristplan();
        var frist = FristBei(T0.AddMinutes(-1));
        await plan.PlaneAsync(frist);

        // Erster Tick: Feuern wirft → Frist NICHT entfernt (nächster Tick wiederholt, dedup-sicher).
        await FristScheduler.TickAsync(new FakeUhr(T0), plan,
            feuere: _ => throw new InvalidOperationException("Draht weg"));
        (await plan.FälligeAsync(T0)).Should().ContainSingle().Which.Id.Should().Be(frist.Id);

        // Zweiter Tick: Feuern klappt → jetzt entfernt.
        await FristScheduler.TickAsync(new FakeUhr(T0), plan, feuere: _ => Task.CompletedTask);
        (await plan.FälligeAsync(T0)).Should().BeEmpty();
    }
}
