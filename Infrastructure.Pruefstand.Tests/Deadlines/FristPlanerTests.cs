using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Abstractions;
using FluentAssertions;
using Infrastructure.Deadlines;
using Infrastructure.Testing;

namespace Infrastructure.Pruefstand.Deadlines;

/// <summary>
/// Ebene 1 — die Senke der <c>Frist&lt;TCmd&gt;</c>-Ausgänge (<see cref="FristPlaner"/>). Bewiesen wird die Basis der
/// Fälligkeit: kommt die Frist aus einem Log-Event, zählt dessen Zeit — der Poll liest einen Emittenten ab 0 neu, und
/// jedes erneute Planen desselben Events muss DIESELBE Fälligkeit ergeben (sonst rückte die Frist bei jeder
/// Stream-Bewegung nach hinten und feuerte nie).
/// </summary>
public class FristPlanerTests
{
    private sealed class Uhr : IDbClock
    {
        public DateTimeOffset Jetzt;
        public Task<DateTimeOffset> JetztAsync(CancellationToken ct = default) => Task.FromResult(Jetzt);
    }

    private static readonly DateTimeOffset T0 = new(2026, 1, 1, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task Erneutes_Planen_desselben_Events_schiebt_die_Frist_nicht()
    {
        var plan = new InMemoryFristplan();
        var uhr = new Uhr { Jetzt = T0 };
        var planer = new FristPlaner(plan, uhr);
        var ziel = Guid.NewGuid();
        var auftrag = new FristAuftrag("Probe.Cmd", ziel, TimeSpan.FromHours(6), Ab: T0);

        await planer.PlaneAsync(auftrag);
        uhr.Jetzt = T0.AddHours(5);           // Poll-Replay Stunden später: dasselbe Event, dieselbe Basis
        await planer.PlaneAsync(auftrag);

        var alle = await plan.FälligeAsync(DateTimeOffset.MaxValue);
        alle.Should().ContainSingle().Which.Fällig.Should().Be(T0.AddHours(6));
        (await plan.FälligeAsync(T0.AddHours(6))).Should().ContainSingle("die Frist feuert zur ursprünglichen Zeit");
    }

    [Fact]
    public async Task Ohne_Event_Basis_zaehlt_die_DB_Uhr()
    {
        var plan = new InMemoryFristplan();
        var planer = new FristPlaner(plan, new Uhr { Jetzt = T0 });

        await planer.PlaneAsync(new FristAuftrag("Probe.Cmd", Guid.NewGuid(), TimeSpan.FromMinutes(5)));

        (await plan.FälligeAsync(DateTimeOffset.MaxValue)).Single().Fällig.Should().Be(T0.AddMinutes(5));
    }

    [Fact]
    public async Task Storno_raeumt_dieselbe_Frist_ab()
    {
        var plan = new InMemoryFristplan();
        var planer = new FristPlaner(plan, new Uhr { Jetzt = T0 });
        var ziel = Guid.NewGuid();

        await planer.PlaneAsync(new FristAuftrag("Probe.Cmd", ziel, TimeSpan.FromHours(1), Ab: T0));
        await planer.PlaneAsync(new FristAuftrag("Probe.Cmd", ziel, null));

        (await plan.FälligeAsync(DateTimeOffset.MaxValue)).Should().BeEmpty();
    }
}
