using Abstractions;
using Infrastructure.Lebenszyklus;
using Microsoft.Extensions.Logging;
using Proto;

namespace Infrastructure.Funktionen;

/// <summary>Der Parent gibt einem Kind einen Eingang zu rechnen.</summary>
public sealed record KindRechne(EingangZustellen Eingang);

/// <summary>Das Kind meldet den Eingang fertig — oder gescheitert (<paramref name="Fehler"/>) — und ist wieder frei.</summary>
public sealed record KindFertig(Guid Kennung, string? Fehler, PID Kind);

/// <summary>
/// Ein KIND einer Funktion (docs/04 §4.4): eine Umsetzung mit ihrer Aktivierung (Bereich, Dienste, Zustand, Lebenszyklus), ein Eingang zugleich — oder,
/// draußen, der Platz eines Arbeiters in der <see cref="Werkstatt"/>. Es rechnet das <c>Auf</c> und bringt jede Ausgabe hinaus — einen Command über den Emitter mit der Id aus Eingang, Funktion und Stelle (die Inbox
/// verwirft eine Wiederholung), ein Zwischenergebnis an jede Funktion, die es hört (gewartet wird, bis sie fertig ist), einen Hinweis verlierbar.
/// Fertig ist der Eingang, wenn alle Commands beantwortet und alle Nachfolger fertig sind; das meldet das Kind seinem Parent.
/// </summary>
public sealed class FunktionsKindActor(
    FunktionsTraeger traeger,
    IServiceProvider dienste,
    IFunktionsWege wege,
    Verkettung verkettung,
    Func<ICommand, Guid, Guid, CancellationToken, Task<Infrastructure.Emit.EmitAntwort>> sende,
    IFaehigkeitsFabrik? faehigkeiten = null,
    ILogger? logger = null,
    TimeSpan? frist = null,
    Action<HinweisZustellen>? hinweise = null) : RahmenActor(logger)
{
    /// <summary>So lange wartet ein Kind auf die Antwort eines Nachfolgers, bevor es den Eingang erneut zustellt (derselbe, dieselbe Kennung).</summary>
    public static readonly TimeSpan StandardFrist = TimeSpan.FromSeconds(60);

    /// <summary>So oft wird ein unbekannter Ausgang (Command ohne Antwort, Nachfolger „später“) versucht, bevor der Eingang scheitert.</summary>
    public const int Versuche = 8;

    private readonly TimeSpan _frist = frist ?? StandardFrist;
    private FunktionsAktivierung? _aktivierung;

    protected override async Task BeginneAsync(IContext context)
    {
        if (traeger.Aktivierung is null) return;   // draußen: ein Arbeiter rechnet
        _aktivierung = traeger.Aktivierung(dienste);
        await _aktivierung.BeginneAsync();
        await _aktivierung.AktiviereAsync(Stopp);
    }

    protected override async ValueTask FreigebenAsync()
    {
        if (_aktivierung is not null) await _aktivierung.DisposeAsync();
    }

    protected override Task BehandleAsync(IContext context)
    {
        if (context.Message is KindRechne r)
        {
            var parent = context.Parent;
            var selbst = context.Self;
            Neben(context, ct => RechneAsync(r.Eingang, ct), t =>
            {
                var fehler = t.IsFaulted ? t.Exception!.GetBaseException().Message : t.IsCanceled ? "abgebrochen" : null;
                if (fehler is not null)
                    logger?.LogWarning(t.Exception?.GetBaseException(), "[Funktionen] {Funktion}: Eingang {Kennung} gescheitert", traeger.Name, r.Eingang.Kennung);
                if (parent is not null) context.Send(parent, new KindFertig(r.Eingang.Kennung, fehler, selbst));
                return Task.CompletedTask;
            });
        }
        return Task.CompletedTask;
    }

    private async Task RechneAsync(EingangZustellen e, CancellationToken ct)
    {
        using var akteur = Akteure.AkteurHerkunft.Aus(e.Akteur);
        using var bereich = traeger.Liest
            ? (faehigkeiten ?? throw new InvalidOperationException($"{traeger.Name} liest Fähigkeiten, aber es gibt keine Fähigkeits-Fabrik.")).Oeffne()
            : null;
        var warten = new List<Task>();
        await RechneAsync(e, e.Nachricht, e.Herkunft(), e.Kennung, e.Ursache ?? Spur.Ursache(e.Stream, e.Version), bereich, warten, ct);
        await Task.WhenAll(warten);
    }

    /// <summary>
    /// Rechnet die Funktion zu einer Nachricht — dem Eingang selbst oder einem Event, das ein Ziel auf einen ihrer Commands hin entschied (eine Antwort,
    /// Kennung aus Eingang, Stelle und Nummer); Zwischenergebnisse kommen in <paramref name="warten"/>, auf ihre Nachfolger wird am Ende gewartet.
    /// </summary>
    private async Task RechneAsync(EingangZustellen e, object nachricht, Herkunft herkunft, Guid kennung, string ursache, IFaehigkeiten? bereich,
        List<Task> warten, CancellationToken ct)
    {
        using var spur = Spur.Von(ursache, WirkerId.FuerFunktion(traeger.Name));
        var stelle = 0;
        var ausgaben = traeger.Draussen is { } draussen
            ? draussen(kennung, nachricht, herkunft, e.Korrelation, e.Art == Eingangsart.Hinweis, ct)
            : traeger.Rechne!(_aktivierung!.Umsetzung, nachricht, herkunft, bereich, ct);
        await foreach (var a in ausgaben.WithCancellation(ct))
        {
            var i = stelle++;
            switch (a)
            {
                case ICommand c:
                    // Commands der Reihe nach (ein Ziel sieht sie in der Reihenfolge der Funktion); die Antwort rechnet die Funktion gleich weiter.
                    var antwort = await SendeAsync(c, Kettenvertrag.Befehl(kennung, traeger.Name, i, c), e, ct);
                    for (var j = 0; j < antwort.Events.Count; j++)
                    {
                        var (ev, version) = (antwort.Events[j].Event, antwort.Events[j].Version);
                        if (traeger.Antworten?.Contains(ev.GetType()) != true) continue;
                        await RechneAsync(e, ev, new Herkunft(c.Ziel, version, e.Zeit, herkunft.Akteur, WirkerId.FuerFunktion(traeger.Name)), Kettenvertrag.Antwort(kennung, traeger.Name, i, j),
                            Spur.Ursache(c.Ziel, version), bereich, warten, ct);
                    }
                    break;
                case ITransientEvent h:
                    // Über das Hinweis-Netz, wenn es eins gibt (es erreicht auch Funktionen mit Log-Eingängen und Clients), sonst über die Wege.
                    if (hinweise is not null)
                        hinweise(new HinweisZustellen(Kettenvertrag.Ausgabe(kennung, traeger.Name, i), h, e.Stream, e.Version, e.Zeit, e.Korrelation, e.Akteur,
                            ursache));
                    else
                        foreach (var f in verkettung.Hoerer(h.GetType()))
                            wege.Melde(Weiter(e, h, Kettenvertrag.Ausgabe(kennung, traeger.Name, i), f, Eingangsart.Hinweis));
                    break;
                case IEvent ev:
                    throw new InvalidOperationException($"{traeger.Name} gab das Event {ev.GetType().Name} aus — eine Funktion schreibt nie ins Log (docs/04 §4.4).");
                default:
                    var ausgabe = Kettenvertrag.Ausgabe(kennung, traeger.Name, i);
                    foreach (var f in verkettung.Hoerer(a.GetType()))
                        warten.Add(ReicheWeiterAsync(Weiter(e, a, ausgabe, f, Eingangsart.Kette), ct));
                    break;
            }
        }
    }

    /// <summary>Der Eingang einer Ausgabe bei der nächsten Funktion: Kennung aus Ausgabe und Funktion, die Herkunft der Kette reist mit.</summary>
    private static EingangZustellen Weiter(EingangZustellen e, object nachricht, Guid ausgabe, string funktion, Eingangsart art) =>
        new(Eingangsvertrag.Eingang(ausgabe, funktion), funktion, nachricht, art, e.Stream, e.Version, e.Zeit, e.Akteur, e.Korrelation, e.Ursache);

    private async Task<Infrastructure.Emit.EmitAntwort> SendeAsync(ICommand c, Guid id, EingangZustellen e, CancellationToken ct)
    {
        var korrelation = Guid.TryParse(e.Korrelation, out var k) ? k : e.Kennung;
        for (var versuch = 1; ; versuch++)
        {
            var antwort = await sende(c, id, korrelation, ct);
            if (antwort.Ausgang.IstEntschieden()) return antwort;
            if (versuch >= Versuche)
                throw new InvalidOperationException($"{traeger.Name}: Command {c.GetType().Name} an {c.Ziel} bleibt nach {versuch} Versuchen unbekannt.");
            await Task.Delay(Pause(versuch), ct);
        }
    }

    private async Task ReicheWeiterAsync(EingangZustellen weiter, CancellationToken ct)
    {
        for (var versuch = 1; ; versuch++)
        {
            switch ((await wege.StelleZuAsync(weiter, _frist, ct))?.Ausgang)
            {
                case Quittungsart.Fertig: return;
                case Quittungsart.Gescheitert:
                    throw new InvalidOperationException($"{traeger.Name}: der Nachfolger {weiter.Funktion} ist an {weiter.Kennung} gescheitert.");
            }
            if (versuch >= Versuche)
                throw new InvalidOperationException($"{traeger.Name}: {weiter.Funktion} nimmt {weiter.Kennung} nach {versuch} Versuchen nicht an.");
            await Task.Delay(Pause(versuch), ct);
        }
    }

    private static TimeSpan Pause(int versuch) => TimeSpan.FromMilliseconds(Math.Min(5_000, 100 * (1 << Math.Min(versuch, 6))));
}
