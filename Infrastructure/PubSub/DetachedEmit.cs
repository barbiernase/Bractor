using Abstractions;

namespace Infrastructure.PubSub;

/// <summary>
/// Macht ein <c>emit</c> AUS SICHT DES AUFRUFERS nicht-blockierend (Schritt A / Spec 8).
///
/// Der Pull-Adapter ist ein virtueller Cluster-Actor: ein im Message-Turn awaiteter
/// <c>cluster.RequestAsync</c> an ein Fremd-Aggregat (der Reaktions-Command) verklemmt den
/// Cluster. Der Reaktions-Send ist aber <b>at-least-once</b> (Empfänger dedupliziert, Re-Wake
/// heilt, Spec 9.3) — er darf den Turn NICHT blockieren. Dieser Wrapper stößt das innere
/// <c>emit</c> detached an und kehrt sofort zurück; der Send begrenzt sich intern selbst.
///
/// Transport-agnostisch: kennt weder Cluster noch Router — nur „feuere und vergiss, aber
/// beobachte die Ausnahme". Dadurch in-memory mit einem Fake-Emit prüfbar (ohne echten Cluster).
/// </summary>
public static class DetachedEmit
{
    public static Func<IPipelineOutput, Task> Wrap(Func<IPipelineOutput, Task> inner)
        => payload =>
        {
            _ = RunDetached(inner, payload);
            return Task.CompletedTask;
        };

    private static async Task RunDetached(Func<IPipelineOutput, Task> inner, IPipelineOutput payload)
    {
        try
        {
            await inner(payload);
        }
        catch (Exception ex)
        {
            // Fire-and-forget: die unbeobachtete Ausnahme nur protokollieren, nie den (bereits
            // zurückgekehrten) Actor-Turn stören. Korrektheit sichert der Empfänger-Dedup + Re-Wake.
            Console.WriteLine($"[DetachedEmit] Ausgabe fehlgeschlagen ({payload.GetType().Name}): {ex.Message}");
        }
    }
}
