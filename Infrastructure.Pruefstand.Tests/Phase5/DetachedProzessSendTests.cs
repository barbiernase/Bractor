using Abstractions;
using Domain.Konto;
using FluentAssertions;
using Infrastructure.Prozess;

namespace Infrastructure.Pruefstand.Phase5;

/// <summary>
/// Ebene 1 (reine Kontrollfluss-Logik, Fake-Emit) — der Treiber-Fold (EM-1).
///
/// <see cref="DetachedProzessSend"/> stößt den Ziel-Emit FIRE-AND-FORGET an (das EINE Emit-Primitiv, keine
/// Quittung mehr) und weckt DANACH — nach JEDEM Send — den Manager, damit er den durablen Ausgang faltet
/// (Wirkung / KommandoVerarbeitet-Noop / KommandoAbgelehnt-Ablehnung). Bewiesen:
///  • der Wrapper kehrt SOFORT zurück, auch wenn der Emit nie zurückkehrt (der (A)-Hang ist strukturell weg);
///  • die Selbst-Weckung läuft nach einem NORMAL abgeschlossenen Emit;
///  • die Selbst-Weckung läuft AUCH, wenn der Emit wirft (finally) — sonst hinge ein abgelehnter Schritt,
///    dessen Ablehnungs-Marke kein Signal erzeugt, bis zum Poll-Backstop.
/// </summary>
public class DetachedProzessSendTests
{
    private static ICommand EinCommand() => new ReserviereBetrag(Guid.NewGuid(), 30);

    [Fact]
    public async Task Wrapper_kehrt_sofort_zurueck_auch_wenn_der_Emit_nie_zurueckkehrt()
    {
        var emitGestartet = new TaskCompletionSource();

        var dispatch = DetachedProzessSend.Wrap(
            emit: (_, _, _, ct) => { emitGestartet.TrySetResult(); return Task.Delay(Timeout.Infinite, ct); },
            danach: (_, _) => Task.CompletedTask);

        // Der Turn (dispatch) MUSS sofort zurückkehren, obwohl der Emit für immer hängt.
        var turn = dispatch(Guid.NewGuid(), EinCommand(), Guid.NewGuid(), default);
        var fertig = await Task.WhenAny(turn, Task.Delay(TimeSpan.FromSeconds(3)));

        fertig.Should().BeSameAs(turn, "der Ziel-Send läuft detached — der Manager-Turn blockiert nie (A-Hang weg)");
        await turn;
        (await Task.WhenAny(emitGestartet.Task, Task.Delay(TimeSpan.FromSeconds(3))))
            .Should().BeSameAs(emitGestartet.Task, "der Emit wurde dennoch angestoßen");
    }

    [Fact]
    public async Task Selbst_Weckung_laeuft_nach_einem_erfolgreichen_Send()
    {
        var danach = new TaskCompletionSource();

        var dispatch = DetachedProzessSend.Wrap(
            emit: (_, _, _, _) => Task.CompletedTask,             // Emit fertig (Erfolg/Ablehnung egal — keine Quittung)
            danach: (_, _) => { danach.TrySetResult(); return Task.CompletedTask; });

        await dispatch(Guid.NewGuid(), EinCommand(), Guid.NewGuid(), default);

        (await Task.WhenAny(danach.Task, Task.Delay(TimeSpan.FromSeconds(3))))
            .Should().BeSameAs(danach.Task, "nach JEDEM Send weckt sich der Manager selbst und faltet den Ausgang");
    }

    [Fact]
    public async Task Selbst_Weckung_laeuft_AUCH_wenn_der_Emit_wirft()
    {
        var danach = new TaskCompletionSource();

        var dispatch = DetachedProzessSend.Wrap(
            emit: (_, _, _, _) => throw new InvalidOperationException("Draht weg"),
            danach: (_, _) => { danach.TrySetResult(); return Task.CompletedTask; });

        await dispatch(Guid.NewGuid(), EinCommand(), Guid.NewGuid(), default);

        (await Task.WhenAny(danach.Task, Task.Delay(TimeSpan.FromSeconds(3))))
            .Should().BeSameAs(danach.Task,
                "die Selbst-Weckung steht im finally — ein geworfener Emit darf sie nicht verschlucken " +
                "(sonst hinge ein abgelehnter Schritt bis zum Poll-Backstop)");
    }
}
