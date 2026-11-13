using System.Collections.Concurrent;
using Abstractions;
using Infrastructure.Akteure;
using Infrastructure.Eingaenge;
using Infrastructure.Emit;
using Infrastructure.Funktionen;
using Microsoft.Extensions.Logging;

namespace Infrastructure.Quellen;

/// <summary>
/// Das HINEINGEBEN einer Meldung eines Eingangs (docs/04 §4.7) — EIN Weg für den Läufer einer lauschenden Quellart und die Annahme des Hosts:
/// <list type="number">
/// <item>Kennung des Elements aus Eingang und Kennung der Meldung (<see cref="Kettenvertrag.Element"/>) — nie aus einer Reihenfolge;</item>
/// <item>übersetzen mit den Übersetzungen der Akteur-Seite, die eine Nachricht ergeben, die der Akteur darf;</item>
/// <item>hineingeben wie eine App, im Namen des Akteurs und mit der Spur des Eingangs: ein Command an ein Aggregat über den Emitter (Id aus Kennung,
///   Übersetzung, Stelle; gewartet bis zur Antwort, die Inbox ist durabel — ein unbekannter Ausgang wird wiederholt), ein Command ohne Ziel als offener
///   Eingang am Kopf (<c>cqrs_eingaenge</c>, dann der schnelle Weg).</item>
/// </list>
/// Kehrt es zurück, ist jede Nachricht des Elements durabel. Dasselbe Element (dieselbe Kennung) ergibt dieselben Ids: eine offene Zeile wird nicht
/// doppelt gelegt, ein Command wirkt über die Inbox einmal. GEGENDRUCK: die Kennungen der hineingegebenen offenen Eingänge merkt sie sich (je Eingang und
/// Prozess); <see cref="HatPlatzAsync"/> sagt, ob weniger offen sind, als der Eingang erlaubt.
/// </summary>
public sealed class Hineingabe(
    EingangPlan plan,
    Func<IAggregatCommand, Guid, Guid, CancellationToken, Task<EmitAntwort>> sende,
    Verkettung koepfe,
    IOffeneEingaenge? offene = null,
    IFunktionsWege? wege = null,
    ILogger? logger = null,
    int versuche = Hineingabe.StandardVersuche)
{
    /// <summary>So oft wird ein Command, dessen Ausgang unbekannt blieb, versucht, bevor das Hineingeben scheitert.</summary>
    public const int StandardVersuche = 8;

    // Ohne lock (Hausregel): die Annahme gibt nebenläufig hinein, der Läufer der Reihe nach.
    private readonly ConcurrentDictionary<Guid, byte> _offen = new();

    public EingangPlan Plan => plan;

    /// <summary>Wie viele hineingegebene Elemente nach dem letzten Nachsehen noch offen sind.</summary>
    public int Offen => _offen.Count;

    /// <summary>Vergisst die offenen Kennungen (ein neuer Lauf des Läufers beginnt mit dem gesicherten Stand).</summary>
    public void Vergiss() => _offen.Clear();

    /// <summary>
    /// Sind weniger hineingegebene Elemente offen, als der Eingang erlaubt? Ist die Grenze erreicht, fragt sie <c>cqrs_eingaenge</c>, welche noch offen
    /// sind (fertig gerechnete fallen aus der Liste), und antwortet danach.
    /// </summary>
    public async Task<bool> HatPlatzAsync(CancellationToken ct)
    {
        if (_offen.Count < plan.Grenze || offene is null) return true;
        var noch = (await offene.NochOffenAsync(_offen.Keys.ToList(), ct)).ToHashSet();
        foreach (var k in _offen.Keys)
            if (!noch.Contains(k)) _offen.TryRemove(k, out _);
        return _offen.Count < plan.Grenze;
    }

    /// <summary>
    /// Übersetzt die Meldung und gibt jede Nachricht im Namen des Akteurs hinein — kehrt zurück, wenn alle durabel sind, mit der Kennung des Elements;
    /// null = keine Übersetzung nimmt die Meldung (das Element geht nicht hinein). Wirft, wenn eine Nachricht nicht durabel wurde.
    /// </summary>
    public async Task<Guid?> GibHineinAsync(IMeldung meldung, CancellationToken ct)
    {
        if (!plan.Uebersetzungen.TryGetValue(meldung.GetType(), out var uebersetzungen))
        {
            logger?.LogWarning("[Quellen] {Eingang}: keine Übersetzung für {Meldung} — das Element geht nicht hinein", plan.Ort, meldung.GetType().Name);
            return null;
        }
        var kennung = Kettenvertrag.Element(plan.Ort, meldung);
        var ursache = Spur.Ursache(kennung, 0);
        using var akteur = AkteurHerkunft.Aus(plan.Akteur);
        using var spur = Spur.Von(ursache, WirkerId.FuerEingang(plan.Client, plan.Name));
        for (var i = 0; i < uebersetzungen.Length; i++)
        {
            var u = uebersetzungen[i];
            switch (u.Rechne(meldung))
            {
                case IAggregatCommand c:
                    await SendeAsync(c, Kettenvertrag.Befehl(kennung, plan.Ort, i, c), kennung, ct);
                    break;
                case ICommand ohneZiel:
                    var id = Herkunft.Ableite($"eingang:{plan.Ort}:{kennung:N}:{i}");
                    var eingaenge = Folgenbeilagen.VonAkteur(ohneZiel, koepfe, id, plan.Akteur, kennung.ToString());
                    if (eingaenge.Count == 0)
                        throw new InvalidOperationException($"{plan.Ort}: {ohneZiel.GetType().Name} (aus {u.Name}) nimmt kein Graph am Kopf — fehlt eine Instanz?");
                    if (offene is null) throw new InvalidOperationException($"{plan.Ort}: dieser Host nimmt keine Commands ohne Ziel an.");
                    await offene.LegeAsync(eingaenge.Select(e => e.Eingang).ToList(), kennung, ct);
                    foreach (var e in eingaenge) _offen.TryAdd(e.Eingang.Kennung, 0);
                    if (wege is not null) Folgenbeilagen.StelleZu(eingaenge, wege, kennung);
                    break;
                case var y:
                    throw new InvalidOperationException($"{plan.Ort}: die Übersetzung {u.Name} gab {y.GetType().Name} aus — sie erzeugt einen Command.");
            }
        }
        return kennung;
    }

    private async Task SendeAsync(IAggregatCommand c, Guid id, Guid korrelation, CancellationToken ct)
    {
        for (var versuch = 1; ; versuch++)
        {
            var antwort = await sende(c, id, korrelation, ct);
            if (antwort.Ausgang.IstEntschieden()) return;
            if (versuch >= versuche)
                throw new InvalidOperationException($"{plan.Ort}: Command {c.GetType().Name} an {c.Ziel} bleibt nach {versuch} Versuchen unbekannt.");
            await Task.Delay(FunktionsKindActor.Pause(versuch), ct);
        }
    }
}
