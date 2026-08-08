using Abstractions;
using Domain.Konto;
using FluentAssertions;
using Infrastructure.Prozess;

namespace Infrastructure.Pruefstand.Phase5;

/// <summary>
/// Ebene 1 (reine Kontrollfluss-Logik, Fake-Send) — Befund 2 des Backend-Audits.
///
/// <see cref="DetachedProzessSend"/> beobachtet die Quittung eines Ziel-Sends out-of-turn und
/// verzweigt: negativ → <c>beiFehlschlag</c> (Ablehnung durabel machen), sonst → <c>danach</c>
/// (den Manager selbst wecken, damit er Fortschritt/Terminal prüft).
///
/// Der Bug: bei <c>result == null</c> (verlorene/timeout Quittung) lief WEDER Zweig → keine
/// Selbst-Weckung. Eine terminale Join-Transition eines Diamanten (z.B. <c>Versende</c>) persistiert
/// dann ihr Ergebnis-Event, aber der Manager erfährt es nie: das Ergebnis-Event ist Auslöser KEINER
/// Regel → der Router abonniert sein Signal nicht → der Poll filtert es weg. Der Prozess hängt für
/// immer ohne <c>ProzessBeendet</c>, obwohl alle Effekte da sind.
///
/// Der Fix weckt bei <c>null</c> ebenfalls selbst (der Effekt kann durabel sein → Fortschritt; ist er
/// es nicht → die Transition feuert erneut). <c>null</c> ist KEINE Ablehnung.
/// </summary>
public class DetachedProzessSendTests
{
    private static ICommand EinCommand() => new ReserviereBetrag(Guid.NewGuid(), 30);

    [Fact]
    public async Task Verlorene_Quittung_result_null_weckt_den_Manager_selbst()
    {
        var danach = new TaskCompletionSource();
        var beiFehlschlagAufgerufen = false;

        var dispatch = DetachedProzessSend.Wrap(
            innererSend: (_, _, _) => Task.FromResult<CommandResult?>(null),   // Quittung verloren
            beiFehlschlag: (_, _, _, _) => { beiFehlschlagAufgerufen = true; return Task.CompletedTask; },
            danach: (_, _) => { danach.TrySetResult(); return Task.CompletedTask; });

        await dispatch(Guid.NewGuid(), EinCommand(), Guid.NewGuid(), default);

        var fired = await Task.WhenAny(danach.Task, Task.Delay(TimeSpan.FromSeconds(3)));
        fired.Should().BeSameAs(danach.Task,
            "bei verlorener Quittung (null) MUSS der Manager selbst geweckt werden — sonst hängt eine " +
            "terminale Join-Transition für immer ohne ProzessBeendet");
        beiFehlschlagAufgerufen.Should().BeFalse("null ist KEINE Ablehnung, nur ein unbekannter Ausgang");
    }

    [Fact]
    public async Task Erfolgreiche_Quittung_weckt_den_Manager_selbst_nicht_als_Fehlschlag()
    {
        var danach = new TaskCompletionSource();
        var beiFehlschlagAufgerufen = false;

        var dispatch = DetachedProzessSend.Wrap(
            innererSend: (_, _, _) => Task.FromResult<CommandResult?>(new CommandResult { Success = true }),
            beiFehlschlag: (_, _, _, _) => { beiFehlschlagAufgerufen = true; return Task.CompletedTask; },
            danach: (_, _) => { danach.TrySetResult(); return Task.CompletedTask; });

        await dispatch(Guid.NewGuid(), EinCommand(), Guid.NewGuid(), default);

        var fired = await Task.WhenAny(danach.Task, Task.Delay(TimeSpan.FromSeconds(3)));
        fired.Should().BeSameAs(danach.Task, "Erfolg → Manager neu wecken (Fortschritt/Terminal prüfen)");
        beiFehlschlagAufgerufen.Should().BeFalse();
    }

    [Fact]
    public async Task Negative_Quittung_meldet_Fehlschlag_und_weckt_nicht_selbst()
    {
        var fehlschlag = new TaskCompletionSource();
        var danachAufgerufen = false;

        var abgelehnt = new CommandResult { Success = false, ErrorMessage = "Deckung fehlt" };
        var dispatch = DetachedProzessSend.Wrap(
            innererSend: (_, _, _) => Task.FromResult<CommandResult?>(abgelehnt),
            beiFehlschlag: (_, _, _, _) => { fehlschlag.TrySetResult(); return Task.CompletedTask; },
            danach: (_, _) => { danachAufgerufen = true; return Task.CompletedTask; });

        await dispatch(Guid.NewGuid(), EinCommand(), Guid.NewGuid(), default);

        var fired = await Task.WhenAny(fehlschlag.Task, Task.Delay(TimeSpan.FromSeconds(3)));
        fired.Should().BeSameAs(fehlschlag.Task, "eine Ablehnung wird durabel gemeldet");
        danachAufgerufen.Should().BeFalse("eine Ablehnung ist kein Fortschritt → keine Selbst-Weckung");
    }
}
