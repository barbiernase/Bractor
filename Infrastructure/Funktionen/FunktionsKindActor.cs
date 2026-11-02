using Abstractions;
using Infrastructure.Lebenszyklus;
using Microsoft.Extensions.Logging;
using Proto;

namespace Infrastructure.Funktionen;

/// <summary>Der Parent gibt einem Kind einen Aufruf zu rechnen.</summary>
public sealed record KindRechne(KnotenAufruf Aufruf);

/// <summary>Der Parent bricht den Aufruf ab, den das Kind gerade rechnet (der Lauf braucht ihn nicht mehr).</summary>
public sealed record KindAbbrechen(Guid Kennung);

/// <summary>
/// Das Kind meldet den Aufruf fertig — mit seinen Ausgaben an Knoten des Graphen und dem Schnappschuss seiner Zustände — oder gescheitert
/// (<paramref name="Fehler"/>) und ist wieder frei.
/// </summary>
public sealed record KindFertig(Guid Kennung, string? Fehler, PID Kind, IReadOnlyList<KnotenAusgabe> Ausgaben, byte[]? Zustand = null);

/// <summary>Der Parent lässt das Kind des Quellknotens lauschen; was es meldet, geht an den Graph-Actor <paramref name="Graph"/>.</summary>
public sealed record KindLausche(PID Graph);

/// <summary>Das Lauschen endete (die Quelle warf oder ihr Strom endete) — der Parent beginnt es nach einer Pause neu, solange die Instanz hier gehalten wird.</summary>
public sealed record LauschenEndete(string? Fehler);

/// <summary>
/// Ein KIND eines Knotens (docs/04 §4.4): eine Umsetzung seiner Funktion mit ihrer Aktivierung (Bereich, Technik, Zustand), ein Aufruf zugleich — oder,
/// draußen, der Platz eines Arbeiters in der <see cref="Werkstatt"/>. Es rechnet das <c>Auf</c> und bringt jede Ausgabe hinaus — einen Command an ein
/// Aggregat sofort über den Emitter mit der Id aus Aufruf, Ort und Stelle (die Inbox verwirft eine Wiederholung; die Antwort rechnet die Funktion gleich
/// weiter), einen Command ohne Ziel an einen Knoten desselben Graphen erst mit dem Erfolg (im Ergebnis an den Graph-Actor), einen anderen an den Kopf der
/// Instanz, die ihn nimmt (gewartet wird, bis ihr Lauf fertig ist), einen Hinweis verlierbar. Das <see cref="KnotenKonfig.Zeitlimit"/> begrenzt eine
/// Rechnung; danach gilt sie als gescheitert.
/// </summary>
public sealed class FunktionsKindActor(
    FunktionsTraeger traeger,
    IServiceProvider dienste,
    IFunktionsWege wege,
    Verkettung koepfe,
    Func<IAggregatCommand, Guid, Guid, CancellationToken, Task<Infrastructure.Emit.EmitAntwort>> sende,
    IFaehigkeitsFabrik? faehigkeiten = null,
    ILogger? logger = null,
    TimeSpan? frist = null,
    Action<HinweisZustellen>? hinweise = null) : RahmenActor(logger)
{
    /// <summary>So lange wartet ein Kind auf die Antwort eines fremden Kopfs, bevor es den Eingang erneut zustellt (derselbe, dieselbe Kennung).</summary>
    public static readonly TimeSpan StandardFrist = TimeSpan.FromSeconds(60);

    /// <summary>So oft wird ein unbekannter Ausgang (Command ohne Antwort, fremder Kopf „später“) versucht, bevor der Aufruf scheitert.</summary>
    public const int Versuche = 8;

    private readonly TimeSpan _frist = frist ?? StandardFrist;
    private FunktionsAktivierung? _aktivierung;
    private (Guid Kennung, CancellationTokenSource Abbruch)? _laufend;

    protected override async Task BeginneAsync(IContext context)
    {
        if (traeger.Aktivierung is null) return;   // draußen: ein Arbeiter rechnet
        _aktivierung = traeger.Aktivierung(dienste);
        await _aktivierung.BeginneAsync();
    }

    protected override async ValueTask FreigebenAsync()
    {
        if (_aktivierung is not null) await _aktivierung.DisposeAsync();
    }

    protected override Task BehandleAsync(IContext context)
    {
        switch (context.Message)
        {
            case KindRechne r:
                var parent = context.Parent;
                var selbst = context.Self;
                var abbruch = CancellationTokenSource.CreateLinkedTokenSource(Stopp);
                if (traeger.Zeitlimit > TimeSpan.Zero) abbruch.CancelAfter(traeger.Zeitlimit);
                _laufend = (r.Aufruf.Kennung, abbruch);
                var ausgaben = new List<KnotenAusgabe>();
                Neben(context, _ => RechneAsync(r.Aufruf, ausgaben, abbruch.Token), t =>
                {
                    _laufend = null;
                    abbruch.Dispose();
                    var fehler = t.IsFaulted ? t.Exception!.GetBaseException().Message : t.IsCanceled ? "abgebrochen (Zeitlimit oder Lauf)" : null;
                    if (t.IsFaulted)
                        logger?.LogWarning(t.Exception?.GetBaseException(), "[Funktionen] {Ort}: Aufruf {Kennung} gescheitert", traeger.Ort, r.Aufruf.Kennung);
                    if (parent is not null)
                        context.Send(parent, fehler is null
                            ? new KindFertig(r.Aufruf.Kennung, null, selbst, ausgaben, _aktivierung?.Schnappschuss())
                            : new KindFertig(r.Aufruf.Kennung, fehler, selbst, Array.Empty<KnotenAusgabe>()));
                    return Task.CompletedTask;
                });
                break;
            case KindLausche l:
                var oben = context.Parent;
                Neben(context, ct => LauscheAsync(context.System, l.Graph, ct), t =>
                {
                    var fehler = t.IsFaulted ? t.Exception!.GetBaseException().Message : null;
                    if (fehler is not null) logger?.LogWarning(t.Exception!.GetBaseException(), "[Funktionen] {Ort}: die Quelle warf", traeger.Ort);
                    if (oben is not null && !t.IsCanceled) context.Send(oben, new LauschenEndete(fehler));
                    return Task.CompletedTask;
                });
                break;
            case KindAbbrechen b when _laufend is { } l && l.Kennung == b.Kennung:
                l.Abbruch.Cancel();
                break;
        }
        return Task.CompletedTask;
    }

    /// <summary>
    /// Die Quelle lauscht: je Element die nächste Nummer (weiter ab der gesicherten), der Schnappschuss ihres Zustands und die Meldung an den Graph-Actor;
    /// das nächste Element erst, wenn er den Lauf angenommen hat (Gegendruck).
    /// </summary>
    private async Task LauscheAsync(ActorSystem system, PID graph, CancellationToken ct)
    {
        var a = _aktivierung ?? throw new InvalidOperationException($"{traeger.Ort}: eine Quelle lauscht im Host");
        var herkunft = new Herkunft(Guid.Empty, 0, DateTimeOffset.UtcNow, null, WirkerId.FuerFunktion(traeger.Name));
        await foreach (var x in traeger.Rechne!(a.Umsetzung, traeger.Lausche!.Value, [], herkunft, null, ct).WithCancellation(ct))
        {
            a.Nummer++;
            var angenommen = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            system.Root.Send(graph, new QuellMeldung(x, a.Nummer, a.Schnappschuss() ?? Zustandsbild.Packe(new Dictionary<string, byte[]>()), angenommen));
            await angenommen.Task.WaitAsync(ct);
        }
    }

    private async Task RechneAsync(KnotenAufruf a, List<KnotenAusgabe> ausgaben, CancellationToken ct)
    {
        using var akteur = Akteure.AkteurHerkunft.Aus(a.Kontext.Akteur);
        using var bereich = traeger.Liest
            ? (faehigkeiten ?? throw new InvalidOperationException($"{traeger.Name} liest Fähigkeiten, aber es gibt keine Fähigkeits-Fabrik.")).Oeffne()
            : null;
        var warten = new List<Task>();
        await RechneAsync(a, a.Operation, a.Argumente, a.Kontext.Herkunft(traeger.Name), a.Kennung, a.Kontext.Ursache ?? Spur.Ursache(a.Kontext.Stream, a.Kontext.Version),
            bereich, ausgaben, warten, ct);
        await Task.WhenAll(warten);
    }

    /// <summary>
    /// Rechnet ein <c>Auf</c> — die Operation des Aufrufs oder die Antwort auf einen ihrer Commands (ein Event, das ein Ziel darauf hin entschied; Kennung
    /// aus Aufruf, Stelle und Nummer). Ausgaben an Knoten des Graphen sammelt es in <paramref name="ausgaben"/>, Wege zu fremden Köpfen in <paramref name="warten"/>.
    /// </summary>
    private async Task RechneAsync(KnotenAufruf a, int auf, object?[] argumente, Herkunft herkunft, Guid kennung, string ursache, IFaehigkeiten? bereich,
        List<KnotenAusgabe> ausgaben, List<Task> warten, CancellationToken ct)
    {
        using var spur = Spur.Von(ursache, WirkerId.FuerFunktion(traeger.Name));
        var stelle = 0;
        var strom = traeger.Draussen is { } draussen
            ? draussen(kennung, argumente.Length == 1 ? argumente[0]! : throw new InvalidOperationException($"{traeger.Ort}: draußen rechnet ein Auf mit genau einem Eingang"),
                herkunft, a.Kontext.Korrelation, false, ct)
            : traeger.Rechne!(_aktivierung!.Umsetzung, auf, argumente, herkunft, bereich, ct);
        await foreach (var x in strom.WithCancellation(ct))
        {
            var i = stelle++;
            switch (x)
            {
                case IAggregatCommand c:
                    // Commands an Aggregate der Reihe nach (ein Ziel sieht sie in der Reihenfolge der Funktion); die Antwort rechnet die Funktion gleich weiter.
                    var antwort = await SendeAsync(c, Kettenvertrag.Befehl(kennung, traeger.Ort, i, c), a, ct);
                    for (var j = 0; j < antwort.Events.Count; j++)
                    {
                        var (ev, version) = (antwort.Events[j].Event, antwort.Events[j].Version);
                        if (traeger.Antworten?.TryGetValue(ev.GetType(), out var antwortAuf) != true) continue;
                        await RechneAsync(a, antwortAuf, [ev], new Herkunft(c.Ziel, version, a.Kontext.Zeit, herkunft.Akteur, WirkerId.FuerFunktion(traeger.Name)),
                            Kettenvertrag.Antwort(kennung, traeger.Ort, i, j), Spur.Ursache(c.Ziel, version), bereich, ausgaben, warten, ct);
                    }
                    break;
                case ITransientEvent h:
                    // Über das Hinweis-Netz, wenn es eins gibt (es erreicht auch Clients), sonst an die Köpfe, die ihn nehmen.
                    if (hinweise is not null)
                        hinweise(new HinweisZustellen(Kettenvertrag.Ausgabe(kennung, traeger.Ort, i), h, a.Kontext.Stream, a.Kontext.Version, a.Kontext.Zeit,
                            a.Kontext.Korrelation, a.Kontext.Akteur, ursache));
                    else
                        foreach (var instanz in koepfe.Hoerer(h.GetType()))
                            wege.Melde(Fremd(a, h, Kettenvertrag.Ausgabe(kennung, traeger.Ort, i), instanz, Eingangsart.Hinweis));
                    break;
                case ICommand weiter when traeger.ImGraphen.Contains(weiter.GetType()):
                    // An einen Knoten desselben Graphen: erst mit dem Erfolg sichtbar (der Graph-Actor gibt ihn längs der Kanten weiter).
                    ausgaben.Add(new KnotenAusgabe(ausgaben.Count, weiter));   // Stelle über den ganzen Aufruf (auch aus Antworten): Unterläufe, Ids
                    break;
                case ICommand weiter:
                    // An den Kopf der Instanz, die ihn nimmt (Entscheidung 2: genau ein Empfänger); gewartet wird, bis ihr Lauf fertig ist.
                    var ausgabe = Kettenvertrag.Ausgabe(kennung, traeger.Ort, i);
                    if (koepfe.Hoerer(weiter.GetType()) is { Count: > 0 } instanzen)
                        foreach (var instanz in instanzen)
                            warten.Add(ReicheWeiterAsync(Fremd(a, weiter, ausgabe, instanz, Eingangsart.Kette), ct));
                    else
                        throw new InvalidOperationException($"{traeger.Ort}: {weiter.GetType().Name} nimmt kein Knoten des Graphen und keine Instanz am Kopf "
                            + "(fehlt eine Instanz in der Umgebung?)");
                    break;
                default:
                    throw new InvalidOperationException($"{traeger.Name} gab {x.GetType().Name} aus — eine Funktion gibt Commands und Hinweise aus, "
                        + "nie ein Event (docs/04 §4.4).");
            }
        }
    }

    /// <summary>Der Eingang einer Ausgabe am Kopf einer Instanz: Kennung aus Ausgabe und Instanz, die Herkunft des Laufs reist mit.</summary>
    private static EingangZustellen Fremd(KnotenAufruf a, object nachricht, Guid ausgabe, string instanz, Eingangsart art) =>
        new(Eingangsvertrag.Eingang(ausgabe, instanz), instanz, nachricht, art, a.Kontext.Stream, a.Kontext.Version, a.Kontext.Zeit,
            a.Kontext.Akteur, a.Kontext.Korrelation, a.Kontext.Ursache);

    private async Task<Infrastructure.Emit.EmitAntwort> SendeAsync(IAggregatCommand c, Guid id, KnotenAufruf a, CancellationToken ct)
    {
        var korrelation = Guid.TryParse(a.Kontext.Korrelation, out var k) ? k : a.Kontext.Lauf;
        for (var versuch = 1; ; versuch++)
        {
            var antwort = await sende(c, id, korrelation, ct);
            if (antwort.Ausgang.IstEntschieden()) return antwort;
            if (versuch >= Versuche)
                throw new InvalidOperationException($"{traeger.Ort}: Command {c.GetType().Name} an {c.Ziel} bleibt nach {versuch} Versuchen unbekannt.");
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
                    throw new InvalidOperationException($"{traeger.Ort}: der Kopf von {weiter.Instanz} ist an {weiter.Kennung} gescheitert.");
            }
            if (versuch >= Versuche)
                throw new InvalidOperationException($"{traeger.Ort}: {weiter.Instanz} nimmt {weiter.Kennung} nach {versuch} Versuchen nicht an.");
            await Task.Delay(Pause(versuch), ct);
        }
    }

    private static TimeSpan Pause(int versuch) => TimeSpan.FromMilliseconds(Math.Min(5_000, 100 * (1 << Math.Min(versuch, 6))));
}
