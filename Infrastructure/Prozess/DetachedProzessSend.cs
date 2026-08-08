using System;
using System.Threading;
using System.Threading.Tasks;
using Abstractions;

namespace Infrastructure.Prozess;

/// <summary>
/// Macht das Feuern einer Transition AUS SICHT DES MANAGER-TURNS nicht-blockierend (Spec §4, der
/// strukturelle (A)-Hang-Fix). Der Manager ist ein virtueller Cluster-Actor: ein im Turn awaiteter
/// Ziel-Send verklemmte den Cluster (die alte (A)-Klasse). Hier wird der Ziel-Send DETACHED angestoßen
/// und der Wrapper kehrt SOFORT zurück — der Manager feuert und vergisst.
///
/// ★ Treiber-Fold (EM-1): Der Send läuft jetzt FIRE-AND-FORGET über das EINE Emit-Primitiv
/// (<c>CommandEmitter</c>) — es gibt KEINE Quittung mehr zu beobachten. Die Fehlschlag-Erkennung trägt
/// allein der Fold (der Manager faltet den durablen Ausgang der Transition: eine Wirkung, die
/// <c>KommandoVerarbeitet</c>-Noop-Marke oder die <c>KommandoAbgelehnt</c>-Ablehnungs-Marke). Deshalb
/// läuft <paramref name="danach"/> (die Selbst-Weckung) nach JEDEM Send — bei Erfolg, Ablehnung UND
/// Timeout —, damit der Manager neu faltet und den durablen Marker sieht. Der frühere
/// Quittungs-/<c>MeldeFehlschlag</c>-Pfad ist gelöscht.
///
/// Transport-agnostisch: kennt weder Cluster noch Emitter, nur „feuere und vergiss, wecke danach".
/// Deshalb in-memory mit Fakes beweisbar (ein nie zurückkehrender Send blockiert den Turn nicht).
/// </summary>
public static class DetachedProzessSend
{
    /// <param name="emit">Der eigentliche (bounded, best-effort) Ziel-Send — live <c>CommandEmitter.EmitAsync</c>.</param>
    /// <param name="danach">
    /// Läuft nach JEDEM Send (out-of-turn): weckt den Manager neu, damit er Fortschritt/Terminal/Fehlschlag
    /// aus dem Fold prüft. Nötig, weil (a) das ERGEBNIS-Event der letzten Transition Auslöser keiner Regel ist
    /// → sein Signal abonniert der Router NICHT, und (b) die Ablehnungs-Marke als <see cref="IProzessIntern"/>
    /// gar kein Signal erzeugt → nur diese Selbst-Weckung (bzw. der Poll-Backstop) bringt den Fold dazu, den
    /// durablen Ausgang zu lesen. Idempotent (WakeAsync faltet frisch).
    /// </param>
    public static Func<Guid, ICommand, Guid, CancellationToken, Task> Wrap(
        Func<Guid, ICommand, Guid, CancellationToken, Task> emit,
        Func<Guid, CancellationToken, Task> danach)
        => (korrelation, cmd, vorgang, ct) =>
        {
            _ = RunDetached(emit, danach, korrelation, cmd, vorgang, ct);
            return Task.CompletedTask;   // SOFORT zurück — der Turn läuft weiter
        };

    private static async Task RunDetached(
        Func<Guid, ICommand, Guid, CancellationToken, Task> emit,
        Func<Guid, CancellationToken, Task> danach,
        Guid korrelation, ICommand cmd, Guid vorgang, CancellationToken ct)
    {
        try
        {
            await emit(korrelation, cmd, vorgang, ct);
        }
        catch (Exception ex)
        {
            // Fire-and-forget: unbeobachtete Ausnahme nur protokollieren, nie den (schon zurückgekehrten)
            // Turn stören. Korrektheit sichert der deterministische Vorgang + Re-Wake/Poll + Empfänger-Inbox.
            Console.WriteLine($"[DetachedProzessSend] Send fehlgeschlagen ({cmd.GetType().Name}): {ex.Message}");
        }
        finally
        {
            // ★ Nach JEDEM Send wecken (Erfolg, Ablehnung, Timeout) — der Fold trägt jetzt die Fehlschlag-
            //   Erkennung, also MUSS der Manager auch nach einer Ablehnung neu falten. Ohne diese Weckung
            //   hinge ein Prozess, dessen Ablehnungs-Marke (kein Signal) niemanden weckt, bis zum Poll-Backstop.
            try { await danach(korrelation, ct); }
            catch (Exception ex)
            {
                Console.WriteLine($"[DetachedProzessSend] Selbst-Weckung fehlgeschlagen ({korrelation}): {ex.Message}");
            }
        }
    }
}
