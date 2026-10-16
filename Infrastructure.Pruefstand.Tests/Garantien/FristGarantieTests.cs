using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Abstractions;
using Domain.Sammelvorgang;
using FluentAssertions;
using Infrastructure.Deadlines;
using Infrastructure.PubSub;
using Infrastructure.Testing;

namespace Infrastructure.Pruefstand.Garantien;

/// <summary>
/// Garantie (docs/11 §11.7): <b>eine Frist feuert mindestens einmal</b> — entfernt wird sie erst, wenn der Command
/// zugestellt (oder fachlich entschieden) ist. Geprüft unter Störung des Emits über den Send-Seam des ECHTEN
/// <see cref="CommandEmitter"/> (kein Cluster): Timeout, technischer Fehlschlag, Wiederholung nach Absturz.
/// Store-frei: der Fristplan ist der In-Memory-Randstore (nicht autoritativ, Inv. 6), die DB-Uhr ein Fake.
/// </summary>
public class FristGarantieTests
{
    private static readonly DateTimeOffset T0 = new(2026, 1, 1, 12, 0, 0, TimeSpan.Zero);

    private sealed class FakeUhr(DateTimeOffset jetzt) : IDbClock
    {
        public Task<DateTimeOffset> JetztAsync(CancellationToken ct = default) => Task.FromResult(jetzt);
    }

    private static async Task<(InMemoryFristplan Plan, Frist Frist)> PlanMitFaelligerFrist()
    {
        var plan = new InMemoryFristplan();
        var frist = new Frist(Guid.NewGuid(), T0.AddMinutes(-1), Guid.NewGuid(), "kontext");
        await plan.PlaneAsync(frist);
        return (plan, frist);
    }

    /// <summary>Feuert die Frist so, wie der <see cref="FristScheduler"/> es live tut: über das eine Emit-Primitiv.</summary>
    private static Func<Frist, Task<EmitAusgang>> FeuereUeber(CommandEmitter emitter)
        => f => emitter.EmitAsync(new MeldeTeilFertig(f.ZielAggregatId), FristId.FürZustellung(f.Id), f.Id, CancellationToken.None);

    [Fact]
    public async Task Emit_Timeout_laesst_die_Frist_liegen()
    {
        var (plan, frist) = await PlanMitFaelligerFrist();
        // Der Empfänger antwortet nie: der Emitter läuft in seine Sendefrist.
        var emitter = new CommandEmitter(async (_, _, ct) => { await Task.Delay(Timeout.Infinite, ct); return null; },
            sendeFrist: TimeSpan.FromMilliseconds(50));

        await FristScheduler.TickAsync(new FakeUhr(T0), plan, FeuereUeber(emitter));

        (await plan.FälligeAsync(T0)).Select(f => f.Id).Should().Equal(new[] { frist.Id },
            "ein Command, dessen Zustellung unbekannt ist, ist nicht gefeuert — die Frist muss für den nächsten Tick liegen bleiben");
    }

    [Fact]
    public async Task Technischer_Fehlschlag_laesst_die_Frist_liegen()
    {
        var (plan, frist) = await PlanMitFaelligerFrist();
        // Das Ziel antwortet mit einem technischen Fehler (z. B. DB weg vor dem Append) — nichts ist gewirkt.
        var emitter = new CommandEmitter((_, _, _) => Task.FromResult<CommandResult?>(
            new CommandResult { Success = false, ErrorMessage = "DB-Timeout vor dem Append" }));

        await FristScheduler.TickAsync(new FakeUhr(T0), plan, FeuereUeber(emitter));

        (await plan.FälligeAsync(T0)).Select(f => f.Id).Should().Equal(new[] { frist.Id });
    }

    [Fact]
    public async Task Zugestellte_Frist_wird_entfernt()
    {
        var (plan, _) = await PlanMitFaelligerFrist();
        var emitter = new CommandEmitter((_, _, _) => Task.FromResult<CommandResult?>(new CommandResult { Success = true }));

        await FristScheduler.TickAsync(new FakeUhr(T0), plan, FeuereUeber(emitter));

        (await plan.FälligeAsync(T0)).Should().BeEmpty();
    }

    [Fact]
    public async Task Fachlich_abgelehnte_Frist_ist_entschieden_und_wird_entfernt()
    {
        // Die Ablehnung ist am Ziel durabel (KommandoAbgelehnt); eine Wiederholung brächte dieselbe.
        var (plan, _) = await PlanMitFaelligerFrist();
        var emitter = new CommandEmitter((_, _, _) => Task.FromResult<CommandResult?>(new CommandResult { Success = false, Abgelehnt = true }));

        await FristScheduler.TickAsync(new FakeUhr(T0), plan, FeuereUeber(emitter));

        (await plan.FälligeAsync(T0)).Should().BeEmpty();
    }

    [Fact]
    public async Task Doppeltes_Feuern_traegt_dieselbe_CommandId()
    {
        // Absturz zwischen Emit und Entfernen: der nächste Tick feuert erneut — mit DERSELBEN CommandId, damit die
        // Inbox des Ziels die Wiederholung verwirft (Wirkung genau einmal, docs/11 §11.3).
        var (plan, frist) = await PlanMitFaelligerFrist();
        var ids = new List<Guid>();
        var emitter = new CommandEmitter((_, env, _) => { ids.Add(env.CommandId); return Task.FromResult<CommandResult?>(null); });

        await FeuereUeber(emitter)(frist);
        await FeuereUeber(emitter)(frist);

        ids.Should().HaveCount(2).And.OnlyContain(id => id == FristId.FürZustellung(frist.Id));
    }
}
