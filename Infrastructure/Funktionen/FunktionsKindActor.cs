using Abstractions;
using Infrastructure.Lebenszyklus;
using Microsoft.Extensions.Logging;
using Proto;

namespace Infrastructure.Funktionen;

/// <summary>Der Parent gibt einem Kind einen Aufruf zu rechnen.</summary>
public sealed record KindRechne(KnotenAufruf Aufruf);

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
/// Ein KIND eines Knotens (docs/04 §4.4): eine Umsetzung seiner Funktion mit ihrer Aktivierung (Bereich, Technik, Zustand), ein Aufruf zugleich. Es rechnet das <c>Auf</c> und bringt jede Ausgabe hinaus — einen Command an ein
/// Aggregat sofort über den Emitter mit der Id aus Aufruf, Ort und Stelle (die Inbox verwirft eine Wiederholung; die Antwort rechnet die Funktion gleich
/// weiter), einen Command ohne Ziel an einen Knoten desselben Graphen erst mit dem Erfolg (im Ergebnis an den Parent, der ihn nach der Wegtabelle
/// weitergibt), einen anderen an den Kopf der Instanz, die ihn nimmt (gewartet wird, bis ihre Kette fertig ist), einen Hinweis verlierbar. Geht eine
/// Ausgabe in eine ÜBERSETZUNG, rechnet das Kind sie inline und bringt ihr Ergebnis ebenso hinaus — mit Ort und Wirker der Übersetzung (docs/04 §4.4).
/// Das <see cref="KnotenKonfig.Zeitlimit"/> begrenzt eine Rechnung; danach gilt sie als gescheitert.
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
                var ausgaben = new List<KnotenAusgabe>();
                Neben(context, _ => RechneAsync(r.Aufruf, ausgaben, abbruch.Token), t =>
                {
                    abbruch.Dispose();
                    var fehler = t.IsFaulted ? t.Exception!.GetBaseException().Message : t.IsCanceled ? "abgebrochen (Zeitlimit oder Stopp)" : null;
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
        }
        return Task.CompletedTask;
    }

    /// <summary>
    /// Die Quelle lauscht: je Element die nächste Nummer (weiter ab der gesicherten), der Schnappschuss ihres Zustands und die Meldung an den Graph-Actor;
    /// das nächste Element erst, wenn er die Kette angenommen hat (Gegendruck).
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
        await RechneAsync(a, a.Operation, [a.Nachricht], a.Kontext.Herkunft(traeger.Name), a.Kennung, a.Kontext.Ursache ?? Spur.Ursache(a.Kontext.Stream, a.Kontext.Version),
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
        // Die Spur nennt den Schritt der Kette (Instanz und Knoten) — seine Karte; Herkunft und Ids leiten weiter aus der Funktion ab.
        using var spur = Spur.Von(ursache, traeger.Instanz is { Length: > 0 } inst ? WirkerId.FuerKnoten(inst, traeger.Knoten) : WirkerId.FuerFunktion(traeger.Name));
        var stelle = 0;
        var strom = traeger.Rechne!(_aktivierung!.Umsetzung, auf, argumente, herkunft, bereich, ct);
        await foreach (var x in strom.WithCancellation(ct))
        {
            var i = stelle++;
            if (traeger.Uebersetzungen.TryGetValue(x.GetType(), out var uebersetzungen))
            {
                foreach (var u in uebersetzungen) await UebersetzeAsync(a, u, x, i, kennung, ursache, ausgaben, warten, ct);
                if (!traeger.ImGraphen.Contains(x.GetType())) continue;   // sonst nimmt ihn auch eine Funktion des Graphen
            }
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
                    // Nur an die Akteure draußen, die ihn hören (über die Verteiler); Funktionen hören keine Hinweise.
                    hinweise?.Invoke(new HinweisZustellen(Kettenvertrag.Ausgabe(kennung, traeger.Ort, i), h, a.Kontext.Stream, a.Kontext.Version, a.Kontext.Zeit,
                        a.Kontext.Korrelation, a.Kontext.Akteur, ursache));
                    break;
                case ICommand weiter when traeger.ImGraphen.Contains(weiter.GetType()):
                    // An einen Knoten desselben Graphen: erst mit dem Erfolg sichtbar (der Parent gibt ihn nach der Wegtabelle weiter).
                    ausgaben.Add(new KnotenAusgabe(ausgaben.Count, weiter));   // Stelle über den ganzen Aufruf (auch aus Antworten): Unterläufe, Ids
                    break;
                case ICommand weiter:
                    ZuFremdemKopf(a, weiter, Kettenvertrag.Ausgabe(kennung, traeger.Ort, i), warten, ct);
                    break;
                default:
                    throw new InvalidOperationException($"{traeger.Name} gab {x.GetType().Name} aus — eine Funktion gibt Commands und Hinweise aus, "
                        + "nie ein Event (docs/04 §4.4).");
            }
        }
    }

    /// <summary>
    /// Eine Übersetzung inline (docs/04 §4.4): aus der Ausgabe <paramref name="x"/> genau eine Nachricht, Feld für Feld. Ein Command an ein Aggregat geht
    /// sofort über den Emitter (Id aus Aufruf, Ort der Übersetzung und Stelle); jedes Event der Antwort, das eine Antwort-Übersetzung nimmt, rechnet sie
    /// gleich weiter (Kennung aus Aufruf, Ort, Stelle und Nummer, Ursache das Event). Einer an Knoten des Graphen geht mit dem Erfolg an den Parent (die
    /// Wege der Übersetzung gelten), ein anderer an den Kopf der Instanz, die ihn nimmt. Die Spur nennt die Übersetzung als Wirker.
    /// </summary>
    private async Task UebersetzeAsync(KnotenAufruf a, UebersetzungPlan u, object x, int i, Guid kennung, string ursache, List<KnotenAusgabe> ausgaben,
        List<Task> warten, CancellationToken ct)
    {
        var ort = traeger.Instanz + "/" + u.Name;
        using var spur = Spur.Von(ursache, WirkerId.FuerKnoten(traeger.Instanz, u.Name));
        switch (u.Rechne(x))
        {
            case IAggregatCommand c:
                var antwort = await SendeAsync(c, Kettenvertrag.Befehl(kennung, ort, i, c), a, ct);
                if (!traeger.AntwortUebersetzungen.TryGetValue(u.Name, out var antworten) || antworten.Count == 0) break;
                for (var j = 0; j < antwort.Events.Count; j++)
                {
                    var (ev, version) = (antwort.Events[j].Event, antwort.Events[j].Version);
                    if (!antworten.TryGetValue(ev.GetType(), out var plaene)) continue;
                    foreach (var p in plaene)
                        await UebersetzeAsync(a, p, ev, 0, Kettenvertrag.Antwort(kennung, ort, i, j), Spur.Ursache(c.Ziel, version), ausgaben, warten, ct);
                }
                break;
            case ICommand weiter when u.Wege.Any(w => w.Typ == weiter.GetType()):
                ausgaben.Add(new KnotenAusgabe(ausgaben.Count, weiter, u.Name));
                break;
            case ICommand weiter:
                ZuFremdemKopf(a, weiter, Kettenvertrag.Ausgabe(kennung, ort, i), warten, ct);
                break;
            case var y:
                throw new InvalidOperationException($"{ort}: die Übersetzung gab {y.GetType().Name} aus — sie erzeugt einen Command.");
        }
    }

    /// <summary>An den Kopf der Instanz, die ihn nimmt (Entscheidung 2: genau ein Empfänger); gewartet wird, bis ihre Kette fertig ist.</summary>
    private void ZuFremdemKopf(KnotenAufruf a, ICommand weiter, Guid ausgabe, List<Task> warten, CancellationToken ct)
    {
        if (koepfe.Hoerer(weiter.GetType()) is { Count: > 0 } instanzen)
            foreach (var instanz in instanzen)
                warten.Add(ReicheWeiterAsync(Fremd(a, weiter, ausgabe, instanz, Eingangsart.Kette), ct));
        else
            throw new InvalidOperationException($"{traeger.Ort}: {weiter.GetType().Name} nimmt kein Knoten des Graphen und keine Instanz am Kopf "
                + "(fehlt eine Instanz in der Umgebung?)");
    }

    /// <summary>Der Eingang einer Ausgabe am Kopf einer Instanz: Kennung aus Ausgabe und Instanz, die Herkunft der Kette reist mit.</summary>
    private static EingangZustellen Fremd(KnotenAufruf a, object nachricht, Guid ausgabe, string instanz, Eingangsart art) =>
        new(Eingangsvertrag.Eingang(ausgabe, instanz), instanz, nachricht, art, a.Kontext.Stream, a.Kontext.Version, a.Kontext.Zeit,
            a.Kontext.Akteur, a.Kontext.Korrelation, a.Kontext.Ursache);

    private async Task<Infrastructure.Emit.EmitAntwort> SendeAsync(IAggregatCommand c, Guid id, KnotenAufruf a, CancellationToken ct)
    {
        var korrelation = Guid.TryParse(a.Kontext.Korrelation, out var k) ? k : a.Kontext.Kopf;
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

    internal static TimeSpan Pause(int versuch) => TimeSpan.FromMilliseconds(Math.Min(5_000, 100 * (1 << Math.Min(versuch, 6))));
}
