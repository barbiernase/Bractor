using Infrastructure.Lebenszyklus;
using Microsoft.Extensions.Logging;
using Proto;

namespace Infrastructure.Funktionen;


/// <summary>
/// Der PARENT eines Knotens auf einem Cluster-Knoten (docs/04 §4.4, Actor <c>funktionen/&lt;instanz&gt;/&lt;knoten&gt;</c>, Kind des Graph-Actors): seine
/// Warteschlange nimmt die bereiten Aufrufe, seine Kinder sind die Umsetzungen — bis <see cref="FunktionsOrtKonfig.Parallel"/>, erzeugt beim ersten
/// Aufruf oder beim Start (<see cref="FunktionsOrtKonfig.Vorwaermen"/>). Scheitert ein Aufruf, rechnet ihn ein Kind erneut, bis
/// <see cref="FunktionsOrtKonfig.Wiederholungen"/>; das Ergebnis geht an den Graph-Actor. Braucht der Lauf einen Aufruf nicht mehr
/// (<see cref="LaufAbbrechen"/>), fällt er aus der Schlange bzw. sein Kind bekommt den Abbruch. Reihenfolge nur bei Parallel 1.
/// </summary>
public sealed class FunktionsParent(
    FunktionsTraeger traeger,
    FunktionsOrtKonfig konfig,
    Func<FunktionsKindActor> neuesKind,
    ILogger? logger = null) : RahmenActor(logger)
{
    private sealed class Offen(KnotenAufruf aufruf)
    {
        public KnotenAufruf Aufruf { get; } = aufruf;
        public int Versuche;
        public PID? Kind;
        public bool Abgebrochen;
    }

    private readonly LinkedList<Offen> _schlange = new();
    private readonly Dictionary<Guid, Offen> _offen = new();
    private readonly Stack<PID> _frei = new();
    private int _kinder;
    private readonly int _parallel = Math.Max(1, konfig.Parallel);

    /// <summary>Kein Passivieren mit offenen Aufrufen.</summary>
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
            case KnotenAufruf a: Nimm(context, a); break;
            case KindFertig f: Fertig(context, f); break;
            case LaufAbbrechen b: Breche(context, b); break;
        }
        return Task.CompletedTask;
    }

    private void Nimm(IContext context, KnotenAufruf a)
    {
        if (_offen.ContainsKey(a.Kennung)) return;   // der Graph-Actor schickt jeden Aufruf einmal
        var o = new Offen(a);
        _offen[a.Kennung] = o;
        _schlange.AddLast(o);
        Verteile(context);
    }

    private void Verteile(IContext context)
    {
        // Höchstens so viele rechnen zugleich, wie Parallel sagt.
        while (_schlange.Count > 0 && _kinder - _frei.Count < _parallel)
        {
            var o = _schlange.First!.Value;
            _schlange.RemoveFirst();
            o.Kind = _frei.Count > 0 ? _frei.Pop() : Kind(context);
            context.Send(o.Kind, new KindRechne(o.Aufruf));
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
        o.Kind = null;
        if (f.Fehler is not null && !o.Abgebrochen && o.Versuche < konfig.Wiederholungen)
        {
            o.Versuche++;
            _schlange.AddLast(o);
            Verteile(context);
            return;
        }
        _offen.Remove(f.Kennung);
        if (context.Parent is { } graph)
            context.Send(graph, new KnotenErgebnis(o.Aufruf, f.Ausgaben, o.Abgebrochen ? null : f.Fehler, o.Abgebrochen));
        Verteile(context);
    }

    private void Breche(IContext context, LaufAbbrechen b)
    {
        foreach (var o in _offen.Values.Where(o => o.Aufruf.Kontext.Lauf == b.Lauf && (b.Aufrufe is null || b.Aufrufe.Contains(o.Aufruf.Kennung))).ToList())
        {
            o.Abgebrochen = true;
            if (o.Kind is { } kind) { context.Send(kind, new KindAbbrechen(o.Aufruf.Kennung)); continue; }
            _schlange.Remove(o);
            _offen.Remove(o.Aufruf.Kennung);
        }
    }
}
