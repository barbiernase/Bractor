using Abstractions;
using Infrastructure.Funktionen;
using Infrastructure.Quellen;
using Microsoft.Extensions.Logging;

namespace Client.Infrastructure.Quellen;

/// <summary>
/// Der LÄUFER einer lauschenden Quelle im Rechnerdienst (docs/04 §4.7, docs/10 E18): er aktiviert die Quellart (Konfig-Werte und Technik-Wahl
/// aus der App-Instanz, <see cref="FunktionsAktivierung"/>) und ruft <c>Lausche</c>. Je Meldung gibt er über die <see cref="Hineingabe"/> hinein und
/// wartet auf die Quittung, dann nimmt er die nächste — einen Gegendruck darüber hinaus gibt es nicht; was im Backend wartet, hält der offene Eingang am
/// Kopf. Endet <c>Lausche</c> oder wirft es (auch weil die Verbindung fehlt), beginnt er nach einer Pause mit einer frischen Aktivierung neu; was die
/// Quellart dann noch einmal meldet, trägt dieselbe Kennung, und das Backend verwirft es. Das Framework sichert für die Quelle nichts.
/// </summary>
public sealed class QuellLaeufer(EingangPlan plan, IServiceProvider dienste, Hineingabe hinein, ILogger? logger = null)
{
    /// <summary>So lange wartet der Läufer, bevor er nach einem Ende des Lauschens neu beginnt.</summary>
    public static readonly TimeSpan Pause = TimeSpan.FromSeconds(1);

    /// <summary>Wie viele Elemente dieser Läufer hineingab (seit seinem Start).</summary>
    public long Hineingegeben { get; private set; }

    /// <summary>Lauscht, bis <paramref name="ct"/> endet; endet ein Lauf, beginnt der nächste nach einer Pause.</summary>
    public async Task LaufeAsync(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            try { await LauscheAsync(ct); }
            catch (Exception e) when (!ct.IsCancellationRequested)
            {
                logger?.LogWarning(e is AbgabeUnterbrochen ? null : e, "[Quellen] {Quelle}: das Lauschen endete ({Grund}) — neu nach {Pause}", plan.Ort,
                    e.GetBaseException().Message, Pause);
            }
            catch (OperationCanceledException) { return; }
            try { await Task.Delay(Pause, ct); }
            catch (OperationCanceledException) { return; }
        }
    }

    private async Task LauscheAsync(CancellationToken ct)
    {
        await using var a = new FunktionsAktivierung(dienste, plan.Art.Baue!, name: plan.Art.Name, knoten: plan.Kontext);
        await a.BeginneAsync();
        var herkunft = new Herkunft(Guid.Empty, 0, DateTimeOffset.UtcNow, plan.Akteur, WirkerId.FuerEingang(plan.Instanz, plan.Name));
        await foreach (var x in plan.Art.Rechne!(a.Umsetzung, plan.Art.Lausche!.Value, [], herkunft, null, ct).WithCancellation(ct))
        {
            if (x is not IMeldung meldung)
                throw new InvalidOperationException($"{plan.Ort}: {plan.Art.Name} meldete {x.GetType().Name} — eine Quellart meldet Meldungen (IMeldung).");
            if (await hinein.GibHineinAsync(meldung, ct) is not null) Hineingegeben++;
        }
    }
}
