using Abstractions;
using Infrastructure.Funktionen;
using Infrastructure.Quellen;
using Microsoft.Extensions.Logging;

namespace Client.Infrastructure.Quellen;

/// <summary>
/// Das HINEINGEBEN einer Meldung einer Quelle (docs/04 §4.7, docs/10 E18) — EIN Weg für den Läufer einer lauschenden Quellart und die Annahme
/// eines Webhooks, beide im Rechnerdienst:
/// <list type="number">
/// <item>Kennung des Elements aus App-Instanz, Quelle und Kennung der Meldung (<see cref="Kettenvertrag.Element"/>) — nie aus einer Reihenfolge;</item>
/// <item>übersetzen mit den Übersetzungen, die der Parameter der App nennt (<c>[Uebersetzt&lt;U&gt;]</c>, generierte Tabelle);</item>
/// <item>über die Leitung hineingeben wie eine App, im Namen des Tokens der Instanz (<see cref="Abgabe"/>): ein Command an ein Aggregat mit der Id aus
///   Kennung, Übersetzung und Stelle, ein Command ohne Ziel mit der Id aus Kennung und Stelle; gewartet wird bis zur Quittung.</item>
/// </list>
/// Kehrt es zurück, ist jede Nachricht des Elements durabel. Dasselbe Element (dieselbe Kennung) ergibt dieselben Ids: die Inbox des Ziels bzw. der
/// offene Eingang am Kopf verwirft die Wiederholung. Ursache in der Spur ist das Element (<c>Kennung:0</c>).
/// </summary>
public sealed class Hineingabe(EingangPlan plan, Abgabe abgabe, ILogger? logger = null)
{
    public EingangPlan Plan => plan;

    /// <summary>Steht die Verbindung der Instanz?</summary>
    public bool Verbunden => abgabe.Verbunden;

    /// <summary>
    /// Übersetzt die Meldung und gibt jede Nachricht hinein — kehrt zurück, wenn alle durabel sind, mit der Kennung des Elements; null = keine Übersetzung
    /// nimmt die Meldung (das Element geht nicht hinein). Wirft <see cref="AbgabeUnterbrochen"/>, wenn eine Nachricht nicht durabel wurde.
    /// </summary>
    public async Task<Guid?> GibHineinAsync(IMeldung meldung, CancellationToken ct)
    {
        if (!plan.Uebersetzungen.TryGetValue(meldung.GetType(), out var uebersetzungen))
        {
            logger?.LogWarning("[Quellen] {Quelle}: keine Übersetzung für {Meldung} — das Element geht nicht hinein", plan.Ort, meldung.GetType().Name);
            return null;
        }
        var kennung = Kettenvertrag.Element(plan.Ort, meldung);
        var ursache = Spur.Ursache(kennung, 0);
        for (var i = 0; i < uebersetzungen.Length; i++)
        {
            var u = uebersetzungen[i];
            (ICommand Command, Guid Id) ab = u.Rechne(meldung) switch
            {
                IAggregatCommand c => (c, Kettenvertrag.Befehl(kennung, plan.Ort, i, c)),
                ICommand ohneZiel => (ohneZiel, Herkunft.Ableite($"eingang:{plan.Ort}:{kennung:N}:{i}")),
                var y => throw new InvalidOperationException($"{plan.Ort}: die Übersetzung {u.Name} gab {y.GetType().Name} aus — sie erzeugt einen Command."),
            };
            await abgabe.GibAbAsync(ab.Command, ab.Id, ursache, ct);
        }
        return kennung;
    }
}
