using Abstractions;
using Infrastructure.Akteure;
using Infrastructure.Eingaenge;
using Infrastructure.Emit;
using Infrastructure.Funktionen;
using Infrastructure.Lebenszyklus;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Proto;

namespace Infrastructure.Quellen;

/// <summary>
/// Der LÄUFER eines Eingangs aus einer lauschenden Quellart (docs/04 §4.7, Actor <c>funktionen/eingang-&lt;Client&gt;-&lt;Eingang&gt;</c>, nur auf dem Host,
/// der den Rechner des Eingangs trägt): er aktiviert die Quellart wie ein Kind seine Funktion (Konfig-Werte und Technik-Wahl aus dem Eingang, Zustand
/// aus <c>cqrs_zustaende</c> unter <c>eingang:&lt;Client&gt;/&lt;Eingang&gt;</c>) und ruft <c>Lausche</c>. Je Meldung gibt er über die
/// <see cref="Hineingabe"/> hinein (derselbe Weg wie die Annahme eines Webhooks: Kennung aus der Meldung, Übersetzung der Akteur-Seite, durabel im
/// Namen des Akteurs); ist die Nachricht durabel, sichert er den Zustand der Quellart (was sie gesehen hat).
/// Was der gesicherte Zustand enthält, meldet die Quelle nicht wieder; was danach kam, meldet sie mit derselben Kennung erneut — eine offene Zeile wird
/// nicht doppelt gelegt, ein Command wirkt über die Inbox einmal. GEGENDRUCK: sind so viele hineingegebene Elemente offen, wie der Eingang erlaubt,
/// wartet der Läufer, bevor er die nächste Meldung nimmt. Endet <c>Lausche</c> oder wirft es, beginnt er nach einer Pause neu, mit dem gesicherten Stand.
/// </summary>
public sealed class QuellLaeufer(
    EingangPlan plan,
    IServiceProvider dienste,
    Func<IAggregatCommand, Guid, Guid, CancellationToken, Task<EmitAntwort>> sende,
    Verkettung koepfe,
    IOffeneEingaenge? offene = null,
    IFunktionsWege? wege = null,
    IZustandsAblage? ablage = null,
    ILogger? logger = null) : RahmenActor(logger)
{
    /// <summary>So lange wartet der Läufer, bevor er nach einem Ende des Lauschens neu beginnt.</summary>
    public static readonly TimeSpan Pause = TimeSpan.FromSeconds(1);

    /// <summary>So lange wartet er, bevor er bei voller Grenze erneut nachsieht, was noch offen ist.</summary>
    public static readonly TimeSpan Nachsehen = TimeSpan.FromMilliseconds(500);

    /// <summary>So oft wird ein Command, dessen Ausgang unbekannt blieb, versucht, bevor das Lauschen neu beginnt.</summary>
    public const int Versuche = Hineingabe.StandardVersuche;

    private FunktionsAktivierung? _aktivierung;
    private Hineingabe? _h;
    /// <summary>Der eine Weg hinein (mit den offenen Kennungen des Eingangs für den Gegendruck).</summary>
    private Hineingabe Hinein => _h ??= new Hineingabe(plan, sende, koepfe, offene, wege, logger);

    protected override Task BeginneAsync(IContext context)
    {
        Starte(context);
        return Task.CompletedTask;
    }

    protected override Task BehandleAsync(IContext context) => Task.CompletedTask;

    protected override async ValueTask FreigebenAsync()
    {
        if (_aktivierung is not null) await _aktivierung.DisposeAsync();
    }

    /// <summary>Lauscht neben der Mailbox; endet es (Fehler oder Ende des Stroms), beginnt es nach einer Pause neu.</summary>
    private void Starte(IContext context) =>
        Neben(context, LauscheAsync, t =>
        {
            if (t.IsFaulted)
                logger?.LogWarning(t.Exception?.GetBaseException(), "[Quellen] {Eingang}: das Lauschen endete mit einem Fehler — neu nach {Pause}", plan.Ort, Pause);
            if (!t.IsCanceled)
                Neben(context, ct => Task.Delay(Pause, ct), w =>
                {
                    if (w.IsCompletedSuccessfully) Starte(context);
                    return Task.CompletedTask;
                });
            return Task.CompletedTask;
        });

    private async Task LauscheAsync(CancellationToken ct)
    {
        // Je Lauf eine frische Aktivierung mit dem gesicherten Stand (was der vorige Lauf nach dem letzten Sichern sah, meldet die Quelle erneut).
        if (_aktivierung is not null) await _aktivierung.DisposeAsync();
        Hinein.Vergiss();
        var technikZustand = dienste.GetService<TechnikWahl>()?.MitZustand(plan.Kontext.Ort) == true;
        _aktivierung = new FunktionsAktivierung(dienste, plan.Art.Baue!, plan.Art.Zustand, plan.Art.Name, technikZustand, plan.Kontext, ablage, quelle: true);
        await _aktivierung.BeginneAsync();
        var a = _aktivierung;
        var herkunft = new Herkunft(Guid.Empty, 0, DateTimeOffset.UtcNow, plan.Akteur, WirkerId.FuerEingang(plan.Client, plan.Name));
        await foreach (var x in plan.Art.Rechne!(a.Umsetzung, plan.Art.Lausche!.Value, [], herkunft, null, ct).WithCancellation(ct))
        {
            if (x is not IMeldung meldung)
                throw new InvalidOperationException($"{plan.Ort}: {plan.Art.Name} meldete {x.GetType().Name} — eine Quellart meldet Meldungen (IMeldung).");
            // Gegendruck: warten, bis weniger offen sind, als der Eingang erlaubt (fertig gerechnete fallen aus der Liste).
            while (!await Hinein.HatPlatzAsync(ct)) await Task.Delay(Nachsehen, ct);
            await Hinein.GibHineinAsync(meldung, ct);
            a.Nummer++;
            if (ablage is not null && a.Schnappschuss() is { } bild)
                await ablage.SichereAsync([(plan.Kontext.Instanz, plan.Kontext.Knoten, new GesicherterZustand(bild, a.Nummer))], ct);
        }
    }
}
