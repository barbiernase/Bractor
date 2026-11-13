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
/// Der LÄUFER eines Eingangs aus einer Quelle (docs/04 §4.7, Actor <c>funktionen/eingang-&lt;Client&gt;-&lt;Eingang&gt;</c>, nur auf dem Host, der den Rechner
/// des Eingangs trägt): er aktiviert die Quellart wie ein Kind seine Funktion (Konfig-Werte und Technik-Wahl aus dem Eingang, Zustand aus
/// <c>cqrs_zustaende</c> unter <c>eingang:&lt;Client&gt;/&lt;Eingang&gt;</c>) und ruft <c>Lausche</c>. Je Meldung:
/// <list type="number">
/// <item>Kennung des Elements aus Eingang und Kennung der Meldung (<see cref="Kettenvertrag.Element"/>) — nie aus einer Reihenfolge;</item>
/// <item>übersetzen mit den Übersetzungen der Akteur-Seite, die eine Nachricht ergeben, die der Akteur darf;</item>
/// <item>hineingeben wie eine App, im Namen des Akteurs: ein Command an ein Aggregat über den Emitter (Id aus Kennung, Übersetzung, Stelle; die Inbox ist
///   durabel), ein Command ohne Ziel als offener Eingang am Kopf (<c>cqrs_eingaenge</c>, dann der schnelle Weg);</item>
/// <item>ist die Nachricht durabel, den Zustand der Quellart sichern (was sie gesehen hat).</item>
/// </list>
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
    public const int Versuche = 8;

    private FunktionsAktivierung? _aktivierung;
    private readonly List<Guid> _offen = new();

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
        _offen.Clear();
        var technikZustand = dienste.GetService<TechnikWahl>()?.MitZustand(plan.Kontext.Ort) == true;
        _aktivierung = new FunktionsAktivierung(dienste, plan.Art.Baue!, plan.Art.Zustand, plan.Art.Name, technikZustand, plan.Kontext, ablage, quelle: true);
        await _aktivierung.BeginneAsync();
        var a = _aktivierung;
        var herkunft = new Herkunft(Guid.Empty, 0, DateTimeOffset.UtcNow, plan.Akteur, WirkerId.FuerEingang(plan.Client, plan.Name));
        await foreach (var x in plan.Art.Rechne!(a.Umsetzung, plan.Art.Lausche!.Value, [], herkunft, null, ct).WithCancellation(ct))
        {
            if (x is not IMeldung meldung)
                throw new InvalidOperationException($"{plan.Ort}: {plan.Art.Name} meldete {x.GetType().Name} — eine Quellart meldet Meldungen (IMeldung).");
            await PlatzAsync(ct);
            await GibHineinAsync(meldung, ct);
            a.Nummer++;
            if (ablage is not null && a.Schnappschuss() is { } bild)
                await ablage.SichereAsync([(plan.Kontext.Instanz, plan.Kontext.Knoten, new GesicherterZustand(bild, a.Nummer))], ct);
        }
    }

    /// <summary>Wartet, solange so viele hineingegebene Elemente offen sind, wie der Eingang erlaubt (fertig gerechnete fallen aus der Liste).</summary>
    private async Task PlatzAsync(CancellationToken ct)
    {
        while (_offen.Count >= plan.Grenze && offene is not null)
        {
            var noch = (await offene.NochOffenAsync(_offen, ct)).ToHashSet();
            _offen.RemoveAll(k => !noch.Contains(k));
            if (_offen.Count >= plan.Grenze) await Task.Delay(Nachsehen, ct);
        }
    }

    /// <summary>Übersetzt die Meldung und gibt jede Nachricht im Namen des Akteurs hinein — erst wenn alle durabel sind, ist das Element hineingegeben.</summary>
    private async Task GibHineinAsync(IMeldung meldung, CancellationToken ct)
    {
        if (!plan.Uebersetzungen.TryGetValue(meldung.GetType(), out var uebersetzungen))
        {
            logger?.LogWarning("[Quellen] {Eingang}: keine Übersetzung für {Meldung} — das Element geht nicht hinein", plan.Ort, meldung.GetType().Name);
            return;
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
                    _offen.AddRange(eingaenge.Select(e => e.Eingang.Kennung));
                    if (wege is not null) Folgenbeilagen.StelleZu(eingaenge, wege, kennung);
                    break;
                case var y:
                    throw new InvalidOperationException($"{plan.Ort}: die Übersetzung {u.Name} gab {y.GetType().Name} aus — sie erzeugt einen Command.");
            }
        }
    }

    private async Task SendeAsync(IAggregatCommand c, Guid id, Guid korrelation, CancellationToken ct)
    {
        for (var versuch = 1; ; versuch++)
        {
            var antwort = await sende(c, id, korrelation, ct);
            if (antwort.Ausgang.IstEntschieden()) return;
            if (versuch >= Versuche)
                throw new InvalidOperationException($"{plan.Ort}: Command {c.GetType().Name} an {c.Ziel} bleibt nach {versuch} Versuchen unbekannt.");
            await Task.Delay(FunktionsKindActor.Pause(versuch), ct);
        }
    }
}
