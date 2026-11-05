using Infrastructure.Lebenszyklus;
using Microsoft.Extensions.Logging;
using Proto;

namespace Infrastructure.Funktionen;


/// <summary>
/// Der PARENT eines Knotens auf einem Cluster-Knoten (docs/04 §4.4, Actor <c>funktionen/&lt;instanz&gt;/&lt;knoten&gt;</c>, Kind des Graph-Actors) — ein
/// Schritt der Kette. Er nimmt Aufrufe an (eine Nachricht je Aufruf; voll bei <see cref="FunktionsOrtKonfig.Warteschlange"/> offenen: „später“), seine
/// Kinder rechnen sie — bis <see cref="FunktionsOrtKonfig.Parallel"/> zugleich, erzeugt beim ersten Aufruf oder beim Start
/// (<see cref="FunktionsOrtKonfig.Vorwaermen"/>); ein scheiternder Aufruf wird bis <see cref="FunktionsOrtKonfig.Wiederholungen"/> erneut gerechnet.
/// Danach schlägt er jede Ausgabe in der Wegtabelle nach und gibt sie als Aufruf an die Parents ihrer Knoten: gesichert mit sich als Antwort, verlierbar
/// ohne. Sind alle gesicherten fertig, meldet er dem Absender „fertig“; scheitert einer, „gescheitert“. Eine Kennung, die er schon fertig meldete,
/// rechnet er nicht neu. Mit Zustand sichert er den letzten Schnappschuss, dessen Aufruf und alle früheren beendet sind, mit den Kennungen dieser
/// Aufrufe (so zählt nach einem Absturz nichts doppelt). Reihenfolge nur bei Parallel 1.
/// </summary>
public sealed class FunktionsParent(
    FunktionsTraeger traeger,
    FunktionsOrtKonfig konfig,
    Func<FunktionsKindActor> neuesKind,
    KnotenWege? wege = null,
    IZustandsAblage? ablage = null,
    ILogger? logger = null) : RahmenActor(logger)
{
    private sealed class Offen(KnotenAufruf aufruf)
    {
        public KnotenAufruf Aufruf { get; } = aufruf;
        public List<PID> Warten { get; } = new();
        public int Versuche;
        public int Ausstehend;
    }

    /// <summary>So viele fertige Kennungen merkt sich ein Parent (eine Wiederholung danach rechnet neu — die Inbox verwirft ihre Commands).</summary>
    public const int Gedaechtnis = 10_000;

    /// <summary>So viele Kennungen sichert ein Knoten mit Zustand mit seinem Schnappschuss.</summary>
    public const int GesichertGesehen = 256;

    /// <summary>Der Schlüssel der gesicherten Kennungen im Bild des Zustands.</summary>
    public const string GesehenSchluessel = "§gesehen";

    /// <summary>So lange wartet ein Absender, bevor er einen Aufruf nach „später“ erneut schickt.</summary>
    public static readonly TimeSpan SpaeterPause = TimeSpan.FromMilliseconds(200);

    /// <summary>So lange wartet der Parent, bevor eine Quelle, deren Lauschen endete, neu beginnt.</summary>
    public static readonly TimeSpan LauschPause = TimeSpan.FromSeconds(1);

    private readonly LinkedList<Offen> _schlange = new();
    private readonly Dictionary<Guid, Offen> _offen = new();
    private readonly Dictionary<Guid, (Guid Eigene, PID Ziel, KnotenAufruf Aufruf)> _weiter = new();
    private readonly HashSet<Guid> _fertig = new();
    private readonly Queue<Guid> _fertigReihe = new();
    private readonly Queue<(Guid Kennung, byte[] Bild)> _schnappschuesse = new();
    private readonly Queue<Guid> _gesehen = new();
    private GesicherterZustand? _zuSichern;
    private readonly Stack<PID> _frei = new();
    private int _kinder;
    private readonly int _parallel = Math.Max(1, konfig.Parallel);
    private PID? _lauscher;
    private bool _lauschen;

    /// <summary>Kein Passivieren mit offenen Aufrufen.</summary>
    protected override bool Beschaeftigt => base.Beschaeftigt || _offen.Count > 0;

    protected override async Task BeginneAsync(IContext context)
    {
        // Mit Zustand: die Kennungen, die der gesicherte Schnappschuss schon enthält, gelten als fertig.
        if (ablage is not null && wege is not null && await ablage.LadeAsync(wege.Instanz.Name, wege.Knoten, default) is { } gesichert
            && Zustandsbild.Entpacke(gesichert.Bild).TryGetValue(GesehenSchluessel, out var gesehen))
            foreach (var k in LiesKennungen(gesehen))
            {
                _gesehen.Enqueue(k);
                Merke(k);
            }
        if (konfig.Vorwaermen)
            while (_kinder < _parallel) _frei.Push(Kind(context));
    }

    protected override Task BehandleAsync(IContext context)
    {
        switch (context.Message)
        {
            case KnotenAufruf a: Nimm(context, a); break;
            case KindFertig f: Fertig(context, f); break;
            case KnotenRueckmeldung r: Rueckmeldung(context, r); break;
            case Lausche l: Lauschen(context, l.An); break;
            case LauschenEndete:
                // Endete der Strom (oder warf die Quelle), beginnt sie nach einer Pause neu — mit dem Stand, den sie zuletzt gesichert fand.
                if (_lauscher is { } alt) { context.Stop(alt); _lauscher = null; }
                if (_lauschen) Neben(context, ct => Task.Delay(LauschPause, ct), t => { if (t.IsCompletedSuccessfully && _lauschen) Lauschen(context, true); return Task.CompletedTask; });
                break;
        }
        return Task.CompletedTask;
    }

    private void Nimm(IContext context, KnotenAufruf a)
    {
        if (_fertig.Contains(a.Kennung)) { Antworte(context, a.Antwort, new KnotenRueckmeldung(a.Kennung, Rueckmeldeart.Fertig)); return; }
        if (_offen.TryGetValue(a.Kennung, out var schon))
        {
            if (a.Antwort is { } w && !schon.Warten.Contains(w)) schon.Warten.Add(w);
            return;
        }
        if (_offen.Count >= Math.Max(1, konfig.Warteschlange))
        {
            Antworte(context, a.Antwort, new KnotenRueckmeldung(a.Kennung, Rueckmeldeart.Spaeter));   // verlierbar: fällt weg
            return;
        }
        var o = new Offen(a);
        if (a.Antwort is { } antwort) o.Warten.Add(antwort);
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
            context.Send(_frei.Count > 0 ? _frei.Pop() : Kind(context), new KindRechne(o.Aufruf));
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
        if (f.Fehler is not null && o.Versuche < konfig.Wiederholungen)
        {
            o.Versuche++;
            _schlange.AddLast(o);
            Verteile(context);
            return;
        }
        if (f.Fehler is not null)
        {
            logger?.LogWarning("[Funktionen] {Ort}: Aufruf {Kennung} gescheitert — {Fehler}", traeger.Ort, f.Kennung, f.Fehler);
            Beende(context, o, new KnotenRueckmeldung(f.Kennung, Rueckmeldeart.Gescheitert, traeger.Knoten, f.Fehler));
            Verteile(context);
            return;
        }
        if (f.Zustand is { } bild && ablage is not null) _schnappschuesse.Enqueue((f.Kennung, bild));
        // Jede Ausgabe an die Knoten, die sie laut Wegtabelle nehmen — Kennung aus diesem Aufruf, dem Ort und der Stelle.
        if (wege is not null)
            foreach (var x in f.Ausgaben)
            {
                var nachricht = Kettenvertrag.Weiter(f.Kennung, wege.Ort, x.Stelle);
                foreach (var w in x.Ueber is { } ueber ? wege.Instanz.Graph.Wege(ueber, 0, x.Nachricht.GetType())
                             : wege.Instanz.Graph.Wege(wege.Knoten, o.Aufruf.Operation, x.Nachricht.GetType()))
                {
                    var kennung = Kettenvertrag.Aufruf(nachricht, wege.Instanz.Ort(w.Knoten), w.Operation);
                    var ziel = KnotenWege.Adresse(context, w.Knoten);
                    var aufruf = new KnotenAufruf(kennung, w.Knoten, w.Operation, x.Nachricht, o.Aufruf.Kontext, w.Verlierbar ? null : context.Self);
                    if (!w.Verlierbar)
                    {
                        _weiter[kennung] = (f.Kennung, ziel, aufruf);
                        o.Ausstehend++;
                    }
                    context.Send(ziel, aufruf);
                }
            }
        if (o.Ausstehend == 0) Beende(context, o, new KnotenRueckmeldung(f.Kennung, Rueckmeldeart.Fertig));
        Verteile(context);
    }

    /// <summary>Die Antwort eines Nachfolgers auf eine gesicherte Ausgabe.</summary>
    private void Rueckmeldung(IContext context, KnotenRueckmeldung r)
    {
        if (!_weiter.TryGetValue(r.Kennung, out var weiter)) return;
        if (r.Art == Rueckmeldeart.Spaeter)
        {
            // Die Warteschlange des Nachfolgers ist voll: nach einer Pause erneut (der Rückstau reicht so bis zum Kopf).
            Neben(context, ct => Task.Delay(SpaeterPause, ct), t =>
            {
                if (t.IsCompletedSuccessfully && _weiter.ContainsKey(r.Kennung)) context.Send(weiter.Ziel, weiter.Aufruf);
                return Task.CompletedTask;
            });
            return;
        }
        _weiter.Remove(r.Kennung);
        if (!_offen.TryGetValue(weiter.Eigene, out var o)) return;   // schon gescheitert
        if (r.Art == Rueckmeldeart.Gescheitert) { Beende(context, o, r with { Kennung = o.Aufruf.Kennung }); return; }
        if (--o.Ausstehend == 0) Beende(context, o, new KnotenRueckmeldung(o.Aufruf.Kennung, Rueckmeldeart.Fertig));
    }

    /// <summary>Der Aufruf ist fertig oder gescheitert: die Wartenden erfahren es, eine fertige Kennung bleibt im Gedächtnis; danach die Sicherung.</summary>
    private void Beende(IContext context, Offen o, KnotenRueckmeldung r)
    {
        _offen.Remove(o.Aufruf.Kennung);
        foreach (var w in o.Warten) context.Send(w, r);
        if (r.Art == Rueckmeldeart.Fertig) Merke(o.Aufruf.Kennung);
        PruefeSicherung(context);
    }

    private static void Antworte(IContext context, PID? an, KnotenRueckmeldung r)
    {
        if (an is not null) context.Send(an, r);
    }

    private void Merke(Guid kennung)
    {
        if (!_fertig.Add(kennung)) return;
        _fertigReihe.Enqueue(kennung);
        while (_fertigReihe.Count > Gedaechtnis) _fertig.Remove(_fertigReihe.Dequeue());
    }

    /// <summary>
    /// Der letzte Schnappschuss, dessen Aufruf und alle früheren beendet sind, wird gesichert — mit den Kennungen dieser Aufrufe: was er enthält, rechnet
    /// nach einem Absturz niemand neu; was später kam, fehlt ihm und wird neu gerechnet.
    /// </summary>
    private void PruefeSicherung(IContext context)
    {
        if (ablage is null || wege is null) return;
        byte[]? letzter = null;
        while (_schnappschuesse.Count > 0 && !_offen.ContainsKey(_schnappschuesse.Peek().Kennung))
        {
            var (kennung, bild) = _schnappschuesse.Dequeue();
            _gesehen.Enqueue(kennung);
            while (_gesehen.Count > GesichertGesehen) _gesehen.Dequeue();
            letzter = bild;
        }
        if (letzter is null) return;
        var je = Zustandsbild.Entpacke(letzter);
        je[GesehenSchluessel] = SchreibeKennungen(_gesehen);
        _zuSichern = new GesicherterZustand(Zustandsbild.Packe(je), 0);
        Wecke(context, SichereAsync);
    }

    /// <summary>Sichert den jüngsten Stand neben der Mailbox (was dabei dazukommt, sichert der nächste Durchlauf).</summary>
    private async Task SichereAsync(CancellationToken ct)
    {
        if (_zuSichern is not { } zustand || wege is null) return;
        _zuSichern = null;
        try { await ablage!.SichereAsync([(wege.Instanz.Name, wege.Knoten, zustand)], ct); }
        catch (Exception ex) when (!ct.IsCancellationRequested)
        {
            logger?.LogWarning(ex, "[Funktionen] {Ort}: Zustand nicht gesichert — der nächste fertige Aufruf sichert ihn", traeger.Ort);
        }
    }

    private static byte[] SchreibeKennungen(IEnumerable<Guid> kennungen) => kennungen.SelectMany(k => k.ToByteArray()).ToArray();

    private static IEnumerable<Guid> LiesKennungen(byte[] daten)
    {
        for (var i = 0; i + 16 <= daten.Length; i += 16) yield return new Guid(daten.AsSpan(i, 16));
    }

    /// <summary>Die Quelle lauscht in einem eigenen Kind (mit ihrer Aktivierung und ihrem gesicherten Zustand), solange die Instanz hier gehalten wird.</summary>
    private void Lauschen(IContext context, bool an)
    {
        _lauschen = an;
        if (an && _lauscher is null && context.Parent is { } graph)
        {
            _lauscher = context.SpawnNamed(Props.FromProducer(() => neuesKind()), "lauscher-" + Guid.NewGuid().ToString("N")[..6]);
            context.Send(_lauscher, new KindLausche(graph));
        }
        else if (!an && _lauscher is { } l)
        {
            context.Stop(l);
            _lauscher = null;
        }
    }
}
