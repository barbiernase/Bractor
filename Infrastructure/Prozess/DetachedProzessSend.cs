using System;
using System.Threading;
using System.Threading.Tasks;
using Abstractions;

namespace Infrastructure.Prozess;

/// <summary>
/// Macht das Feuern einer Transition AUS SICHT DES MANAGER-TURNS nicht-blockierend (Spec §4, der
/// strukturelle (A)-Hang-Fix). Der Manager ist ein virtueller Cluster-Actor: ein im Turn awaiteter
/// <c>cluster.RequestAsync</c> ans Ziel-Aggregat verklemmte den Cluster (die alte (A)-Klasse). Hier wird
/// der Ziel-Send DETACHED angestoßen und der Wrapper kehrt SOFORT zurück — der Manager feuert und vergisst.
///
/// Die Quittung wird trotzdem beobachtet, aber OUT-OF-TURN: kommt sie NEGATIV zurück, meldet die
/// Continuation den Fehlschlag an den Manager (<see cref="ProzessManager.NotiereFehlschlagAsync"/> über
/// seinen Mailbox-Weg) — so wird die Ablehnung durabel (§5.2), ohne den Turn zu blockieren. Eine erfolgreiche
/// Quittung braucht keine Meldung: das durable Ziel-Event ist die Wahrheit und weckt den Manager neu.
///
/// Transport-agnostisch: kennt weder Cluster noch Router, nur „feuere und vergiss, beobachte die Quittung".
/// Deshalb in-memory mit einem Fake beweisbar (ein nie zurückkehrender Send blockiert den Turn nicht).
/// </summary>
public static class DetachedProzessSend
{
    /// <param name="innererSend">Der eigentliche (bounded) Ziel-Send — live ein <c>cluster.RequestAsync</c>.</param>
    /// <param name="beiFehlschlag">Rückmeldung einer negativen Quittung an den Manager: (Korrelation, Vorgang, Grund).</param>
    /// <param name="danach">
    /// Läuft nach einem ERFOLGREICHEN Send (out-of-turn): weckt den Manager neu, damit er Fortschritt/Terminal
    /// prüft. Nötig, weil das ERGEBNIS-Event der letzten Transition kein Auslöser einer Regel ist → sein Signal
    /// abonniert der Router NICHT → nur diese Selbst-Weckung erkennt „terminal". Für Zwischenschritte doppelt
    /// (das Ergebnis-Event weckt ohnehin), aber idempotent.
    /// </param>
    public static Func<Guid, ICommand, Guid, CancellationToken, Task> Wrap(
        Func<ICommand, Guid, CancellationToken, Task<CommandResult?>> innererSend,
        Func<Guid, Guid, string, CancellationToken, Task> beiFehlschlag,
        Func<Guid, CancellationToken, Task> danach)
        => (korrelation, cmd, vorgang, ct) =>
        {
            _ = RunDetached(innererSend, beiFehlschlag, danach, korrelation, cmd, vorgang, ct);
            return Task.CompletedTask;   // SOFORT zurück — der Turn läuft weiter
        };

    private static async Task RunDetached(
        Func<ICommand, Guid, CancellationToken, Task<CommandResult?>> innererSend,
        Func<Guid, Guid, string, CancellationToken, Task> beiFehlschlag,
        Func<Guid, CancellationToken, Task> danach,
        Guid korrelation, ICommand cmd, Guid vorgang, CancellationToken ct)
    {
        try
        {
            var result = await innererSend(cmd, vorgang, ct);
            if (result is { Success: false })
            {
                var grund = result.RejectionEvent?.GetType().Name ?? result.ErrorMessage ?? "abgelehnt";
                await beiFehlschlag(korrelation, vorgang, grund, ct);
            }
            else
            {
                // Erfolg ODER verlorene/timeout Quittung (result == null): den Manager neu wecken (Befund 2).
                // null ist KEINE Ablehnung, nur ein unbekannter Ausgang — der Effekt kann durabel persistiert
                // sein (nur die Quittung ging verloren). Die Selbst-Weckung lässt den Manager die Ziel-Streams
                // neu falten: sieht er den Effekt → Fortschritt/Terminal (das Ergebnis-Event der letzten
                // Transition weckt sonst NIEMANDEN — Auslöser keiner Regel → weder Router noch Poll); sieht er
                // ihn nicht → die Transition feuert erneut. Ohne diese Weckung hängt eine terminale
                // Join-Transition eines Diamanten für immer ohne ProzessBeendet.
                await danach(korrelation, ct);
            }
        }
        catch (Exception ex)
        {
            // Fire-and-forget: unbeobachtete Ausnahme nur protokollieren, nie den (schon zurückgekehrten)
            // Turn stören. Korrektheit sichert der deterministische Vorgang + Re-Wake/Poll.
            Console.WriteLine($"[DetachedProzessSend] Send fehlgeschlagen ({cmd.GetType().Name}): {ex.Message}");
        }
    }
}
