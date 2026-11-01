using Infrastructure.Eingaenge;
using Infrastructure.Lebenszyklus;
using Microsoft.Extensions.Logging;
using Proto;

namespace Infrastructure.Funktionen;

/// <summary>An den Parent: so viele Kinder dürfen jetzt zugleich rechnen (draußen: die Slots aller Arbeiter auf diesem Knoten).</summary>
public sealed record ParallelSetzen(int Parallel);

/// <summary>
/// Der PARENT einer Funktion auf einem Knoten (docs/04 §4.4, Actor <c>funktionen/&lt;Name&gt;</c>): seine Mailbox ist eine begrenzte Warteschlange,
/// seine Kinder sind die Umsetzungen — bis <see cref="FunktionsOrtKonfig.Parallel"/>, erzeugt beim ersten Eingang oder beim Start
/// (<see cref="FunktionsOrtKonfig.Vorwaermen"/>). Ein Eingang wird an seiner Kennung erkannt: ist er in Arbeit, hängt sich ein doppelter an; ist er
/// fertig, bekommt er sofort „fertig“. Ist die Schlange voll, heißt die Antwort „später“ — der Rückstau reicht bis zum durablen Rand. Scheitert ein
/// Eingang, rechnet ihn ein Kind erneut, bis <see cref="FunktionsOrtKonfig.Wiederholungen"/>. Reihenfolge nur bei Parallel 1.
/// </summary>
public sealed class FunktionsParent(
    FunktionsTraeger traeger,
    FunktionsOrtKonfig konfig,
    Func<FunktionsKindActor> neuesKind,
    IOffeneEingaenge? offene = null,
    ILogger? logger = null) : RahmenActor(logger)
{
    /// <summary>So viele fertige Kennungen merkt sich ein Parent (ein doppelter Eingang danach rechnet neu — die Inbox verwirft seine Commands).</summary>
    public const int Gedaechtnis = 10_000;

    private sealed class Offen(EingangZustellen eingang)
    {
        public EingangZustellen Eingang { get; } = eingang;
        public List<PID> Warten { get; } = new();
        public int Versuche;
    }

    private readonly Queue<Offen> _schlange = new();
    private readonly Dictionary<Guid, Offen> _offen = new();
    private readonly HashSet<Guid> _fertig = new();
    private readonly Queue<Guid> _fertigReihe = new();
    private readonly Stack<PID> _frei = new();
    private readonly List<Guid> _auszutragen = new();
    private int _kinder;
    private int _parallel = Math.Max(1, konfig.Parallel);

    /// <summary>Kein Passivieren mit offenen Eingängen.</summary>
    protected override bool Beschaeftigt => base.Beschaeftigt || _offen.Count > 0;

    protected override Task BeginneAsync(IContext context)
    {
        if (konfig.Vorwaermen)
            while (_kinder < _parallel) _frei.Push(Kind(context));
        return Task.CompletedTask;
    }

    protected override Task BehandleAsync(IContext context)
    {
        switch (context.Message)
        {
            case EingangZustellen e: Nimm(context, e); break;
            case KindFertig f: Fertig(context, f); break;
            case ParallelSetzen p: _parallel = Math.Max(1, p.Parallel); Verteile(context); break;
        }
        return Task.CompletedTask;
    }

    private void Nimm(IContext context, EingangZustellen e)
    {
        if (_fertig.Contains(e.Kennung)) { Antworte(context, e, Quittungsart.Fertig); return; }
        if (_offen.TryGetValue(e.Kennung, out var schon))
        {
            if (e.Art == Eingangsart.Kette && context.Sender is { } s) schon.Warten.Add(s);
            else Antworte(context, e, Quittungsart.Angenommen);
            return;
        }
        if (_schlange.Count >= konfig.Warteschlange) { Antworte(context, e, Quittungsart.Spaeter); return; }
        var o = new Offen(e);
        if (e.Art == Eingangsart.Kette && context.Sender is { } absender) o.Warten.Add(absender);
        else Antworte(context, e, Quittungsart.Angenommen);
        _offen[e.Kennung] = o;
        _schlange.Enqueue(o);
        Verteile(context);
    }

    private static void Antworte(IContext context, EingangZustellen e, Quittungsart ausgang)
    {
        if (e.Art != Eingangsart.Hinweis && context.Sender is not null) context.Respond(new EingangQuittung(e.Kennung, ausgang));
    }

    private void Verteile(IContext context)
    {
        // Höchstens so viele rechnen zugleich, wie Parallel sagt (sinkt es, bleiben die übrigen Kinder frei, bis es wieder steigt).
        while (_schlange.Count > 0 && _kinder - _frei.Count < _parallel)
        {
            var kind = _frei.Count > 0 ? _frei.Pop() : Kind(context);
            context.Send(kind, new KindRechne(_schlange.Dequeue().Eingang));
        }
    }

    private PID Kind(IContext context)
    {
        _kinder++;
        return context.SpawnNamed(Props.FromProducer(() => neuesKind()), "k" + _kinder);
    }

    private void Fertig(IContext context, KindFertig f)
    {
        _frei.Push(f.Kind);
        if (!_offen.TryGetValue(f.Kennung, out var o)) { Verteile(context); return; }
        if (f.Fehler is not null && o.Eingang.Art != Eingangsart.Hinweis && o.Versuche < konfig.Wiederholungen)
        {
            o.Versuche++;
            _schlange.Enqueue(o);
            Verteile(context);
            return;
        }
        _offen.Remove(f.Kennung);
        var ausgang = f.Fehler is null ? Quittungsart.Fertig : Quittungsart.Gescheitert;
        foreach (var w in o.Warten) context.Send(w, new EingangQuittung(f.Kennung, ausgang));
        if (f.Fehler is null)
        {
            Merke(f.Kennung);
            if (o.Eingang.Art == Eingangsart.Durabel && offene is not null) { _auszutragen.Add(f.Kennung); Wecke(context, TrageAusAsync); }
        }
        // Gescheitert und durabel: die Zeile bleibt offen, der Zusteller bringt den Eingang nach seiner Frist erneut.
        Verteile(context);
    }

    private void Merke(Guid kennung)
    {
        if (!_fertig.Add(kennung)) return;
        _fertigReihe.Enqueue(kennung);
        while (_fertigReihe.Count > Gedaechtnis) _fertig.Remove(_fertigReihe.Dequeue());
    }

    /// <summary>Die fertigen durablen Eingänge gebündelt austragen (neben der Mailbox; was dabei dazukommt, trägt der nächste Durchlauf aus).</summary>
    private async Task TrageAusAsync(CancellationToken ct)
    {
        var jetzt = _auszutragen.ToArray();
        _auszutragen.Clear();
        try { await offene!.ErledigtAsync(jetzt, ct); }
        catch (Exception ex) when (!ct.IsCancellationRequested)
        {
            logger?.LogWarning(ex, "[Funktionen] {Funktion}: {Anzahl} fertige Eingänge nicht ausgetragen — der Zusteller bringt sie erneut, sie sind bekannt",
                traeger.Name, jetzt.Length);
        }
    }
}
