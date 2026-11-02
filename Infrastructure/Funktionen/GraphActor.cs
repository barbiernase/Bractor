using Abstractions;
using Infrastructure.Eingaenge;
using Infrastructure.Lebenszyklus;
using Microsoft.Extensions.Logging;
using Proto;

namespace Infrastructure.Funktionen;

/// <summary>
/// Der GRAPH-ACTOR einer Instanz auf einem Cluster-Knoten (docs/04 §4.4, <c>funktionen/&lt;instanz&gt;</c>) — der Koordinator ihrer Läufe. Seine Kinder
/// sind die Parents der Knoten (<c>funktionen/&lt;instanz&gt;/&lt;knoten&gt;</c>). Ein Eingang am Kopf startet einen Lauf (<see cref="Lauf"/>, Kennung =
/// die des Eingangs; ein doppelter hängt sich an bzw. hört „fertig“); bereite Aufrufe gehen an die Parents, ihre Ergebnisse kommen hierher zurück und
/// längs der Kanten weiter. Ein fertiger Lauf antwortet dem wartenden Absender bzw. trägt den durablen Eingang aus; ein gescheiterter bricht seine
/// offenen Zweige ab, parkt den durablen Kopf und meldet <see cref="LaufGescheitert"/>. Läuft schon so viel, wie die Warteschlange des Kopfs erlaubt,
/// heißt die Antwort „später“ — der Rückstau reicht bis zum durablen Rand.
/// </summary>
public sealed class GraphActor(
    GraphInstanz instanz,
    IReadOnlyDictionary<string, Func<FunktionsParent>> knoten,
    IOffeneEingaenge? offene = null,
    Action<HinweisZustellen>? hinweise = null,
    ILogger? logger = null,
    IZustandsAblage? ablage = null) : RahmenActor(logger)
{
    /// <summary>So viele fertige Kennungen merkt sich ein Graph-Actor (ein doppelter Eingang danach rechnet neu — die Inbox verwirft seine Commands).</summary>
    public const int Gedaechtnis = 10_000;

    private sealed class Offen(Lauf lauf, EingangZustellen kopf)
    {
        public Lauf Lauf { get; } = lauf;
        public EingangZustellen Kopf { get; } = kopf;
        public List<PID> Warten { get; } = new();
    }

    private readonly Dictionary<string, PID> _parents = new(StringComparer.Ordinal);
    private readonly Dictionary<Guid, Offen> _laeufe = new();
    private readonly HashSet<Guid> _fertig = new();
    private readonly Queue<Guid> _fertigReihe = new();
    private readonly List<Guid> _auszutragen = new();
    private readonly List<(Guid Kennung, string Grund)> _zuParken = new();
    // Gesicherter Zustand: je Knoten die Schnappschüsse in Reihenfolge, mit dem Lauf, der sie erzeugte; gesichert wird der letzte, dessen Lauf und alle
    //   früheren fertig sind (Entscheidung 9).
    private readonly Dictionary<string, Queue<(Guid Lauf, GesicherterZustand Zustand)>> _schnappschuesse = new(StringComparer.Ordinal);
    private readonly Dictionary<string, GesicherterZustand> _zuSichern = new(StringComparer.Ordinal);
    private readonly Queue<QuellMeldung> _quellRueckstau = new();

    /// <summary>Kein Passivieren mit offenen Läufen.</summary>
    protected override bool Beschaeftigt => base.Beschaeftigt || _laeufe.Count > 0;

    protected override Task BeginneAsync(IContext context)
    {
        foreach (var (name, neu) in knoten)
            _parents[name] = context.SpawnNamed(Props.FromProducer(() => neu()), name);
        return Task.CompletedTask;
    }

    protected override Task BehandleAsync(IContext context)
    {
        switch (context.Message)
        {
            case EingangZustellen e: Kopf(context, e); break;
            case QuelleHalten h:
                if (instanz.Graph.Quelle is { } quelle && _parents.TryGetValue(quelle, out var lauscher)) context.Send(lauscher, new Lausche(h.Halten));
                break;
            case QuellMeldung m:
                if (_laeufe.Count >= QuellGrenze) _quellRueckstau.Enqueue(m);
                else Quelle(context, m);
                break;
            case KnotenErgebnis r when _laeufe.TryGetValue(r.Aufruf.Kontext.Lauf, out var o):
                if (r.Zustand is { } bild && r.Fehler is null) Schnappschuss(r.Aufruf.Knoten, r.Aufruf.Kontext.Lauf, new GesicherterZustand(bild, 0));
                if (r.Fehler is not null)
                    logger?.LogWarning("[Funktionen] {Ort}: Aufruf {Kennung} ({Pfad}) gescheitert — {Fehler}", instanz.Ort(r.Aufruf.Knoten), r.Aufruf.Kennung,
                        r.Aufruf.Pfad.Length == 0 ? "Kopf" : r.Aufruf.Pfad, r.Fehler);
                o.Lauf.Ergebnis(r, new Wirkung(this, context));
                Abschluss(context, o);
                break;
            case ZeitlimitAbgelaufen z when _laeufe.TryGetValue(z.Lauf, out var o):
                o.Lauf.Zeitlimit(z, new Wirkung(this, context));
                Abschluss(context, o);
                break;
        }
        return Task.CompletedTask;
    }

    private void Kopf(IContext context, EingangZustellen e)
    {
        if (_fertig.Contains(e.Kennung)) { Antworte(context, e, Quittungsart.Fertig); return; }
        if (_laeufe.TryGetValue(e.Kennung, out var schon))
        {
            if (e.Art == Eingangsart.Kette && context.Sender is { } s) schon.Warten.Add(s);
            else Antworte(context, e, Quittungsart.Angenommen);
            return;
        }
        var typ = e.Nachricht.GetType();
        if (instanz.KopfFuer(typ).Count == 0)
        {
            logger?.LogWarning("[Funktionen] {Instanz}: kein Knoten nimmt {Typ} am Kopf — gescheitert", instanz.Name, typ.Name);
            Antworte(context, e, Quittungsart.Gescheitert);
            return;
        }
        if (e.Art != Eingangsart.Hinweis && _laeufe.Count >= instanz.Laeufe(typ)) { Antworte(context, e, Quittungsart.Spaeter); return; }
        var o = new Offen(new Lauf(instanz, new LaufKontext(e.Kennung, e.Stream, e.Version, e.Zeit, e.Akteur, e.Korrelation, e.Ursache)), e);
        if (e.Art == Eingangsart.Kette && context.Sender is { } absender) o.Warten.Add(absender);
        else Antworte(context, e, Quittungsart.Angenommen);
        _laeufe[e.Kennung] = o;
        o.Lauf.Beginne(e.Nachricht, new Wirkung(this, context));
        Abschluss(context, o);
    }

    /// <summary>So viele Läufe nimmt die Quelle zugleich (die Warteschlange ihres Knotens); darüber wartet sie (Gegendruck).</summary>
    private int QuellGrenze => instanz.Graph.Quelle is { } q && instanz.Konfig(q).Warteschlange is > 0 and var w ? w : FunktionsOrtKonfig.StandardWarteschlange;

    /// <summary>
    /// Ein Element der Quelle startet einen Lauf — Kennung aus Instanz und Nummer (nach einem Neustart rechnet ein Lauf mit derselben Nummer dieselben Ids),
    /// Herkunft aus Instanz, Nummer und Zeit, der Akteur der Instanz. Ein Hinweis der Quelle geht an die, die ihn hören. Ihr Schnappschuss wird gesichert,
    /// sobald dieser Lauf und alle früheren fertig sind.
    /// </summary>
    private void Quelle(IContext context, QuellMeldung m)
    {
        var quelle = instanz.Graph.Quelle!;
        var kennung = Herkunft.Ableite($"quelle:{instanz.Name}:{m.Nummer}");
        var stream = Herkunft.Ableite($"quelle:{instanz.Name}");
        if (m.Nachricht is ITransientEvent h)
        {
            hinweise?.Invoke(new HinweisZustellen(kennung, h, stream, (int)m.Nummer, DateTimeOffset.UtcNow, kennung.ToString(), instanz.Akteur, null));
            Schnappschuss(quelle, Guid.Empty, new GesicherterZustand(m.Zustand, m.Nummer));
            m.Angenommen.TrySetResult();
            PruefeSicherung(context);
            return;
        }
        if (_laeufe.ContainsKey(kennung) || _fertig.Contains(kennung)) { m.Angenommen.TrySetResult(); return; }   // nach einem Neustart der Quelle
        var kopf = new EingangZustellen(kennung, instanz.Name, m.Nachricht, Eingangsart.Quelle, stream, (int)m.Nummer, DateTimeOffset.UtcNow, instanz.Akteur,
            kennung.ToString(), null);
        var o = new Offen(new Lauf(instanz, new LaufKontext(kennung, stream, (int)m.Nummer, kopf.Zeit, instanz.Akteur, kopf.Korrelation, null)), kopf);
        _laeufe[kennung] = o;
        Schnappschuss(quelle, kennung, new GesicherterZustand(m.Zustand, m.Nummer));
        m.Angenommen.TrySetResult();
        var op = instanz.Graph.KnotenNamens(quelle)!.Operationen.Single(x => x.Ports.Count == 0);
        o.Lauf.BeginneMitQuelle(quelle, op.Auf, m.Nachricht, new Wirkung(this, context));
        Abschluss(context, o);
    }

    private void Schnappschuss(string knoten, Guid lauf, GesicherterZustand zustand)
    {
        if (ablage is null) return;
        if (!_schnappschuesse.TryGetValue(knoten, out var q)) _schnappschuesse[knoten] = q = new Queue<(Guid, GesicherterZustand)>();
        q.Enqueue((lauf, zustand));
    }

    /// <summary>Je Knoten der letzte Schnappschuss, dessen Lauf und alle früheren fertig (oder gescheitert) sind — zum Sichern.</summary>
    private void PruefeSicherung(IContext context)
    {
        var neu = false;
        foreach (var (knoten, q) in _schnappschuesse)
        {
            GesicherterZustand? letzter = null;
            while (q.Count > 0 && !_laeufe.ContainsKey(q.Peek().Lauf)) letzter = q.Dequeue().Zustand;
            if (letzter is null) continue;
            _zuSichern[knoten] = letzter;
            neu = true;
        }
        if (neu) Wecke(context, SchreibeAsync);
    }

    private static void Antworte(IContext context, EingangZustellen e, Quittungsart ausgang)
    {
        if (e.Art != Eingangsart.Hinweis && context.Sender is not null) context.Respond(new EingangQuittung(e.Kennung, ausgang));
    }

    /// <summary>Ist der Lauf fertig oder gescheitert, antwortet er den Wartenden und verlässt die Buchführung; danach Sicherung und Rückstau der Quelle.</summary>
    private void Abschluss(IContext context, Offen o)
    {
        Beende(context, o);
        if (_laeufe.ContainsKey(o.Kopf.Kennung)) return;
        PruefeSicherung(context);
        while (_quellRueckstau.Count > 0 && _laeufe.Count < QuellGrenze) Quelle(context, _quellRueckstau.Dequeue());
    }

    private void Beende(IContext context, Offen o)
    {
        var kopf = o.Kopf;
        if (o.Lauf.Gescheitert is var (knotenName, grund))
        {
            _laeufe.Remove(kopf.Kennung);
            foreach (var w in o.Warten) context.Send(w, new EingangQuittung(kopf.Kennung, Quittungsart.Gescheitert));
            logger?.LogWarning("[Funktionen] {Instanz}: Lauf {Lauf} gescheitert an {Knoten} — {Grund}", instanz.Name, kopf.Kennung, knotenName, grund);
            if (kopf.Art == Eingangsart.Durabel && offene is not null)
            {
                _zuParken.Add((kopf.Kennung, $"{instanz.Ort(knotenName)}: {grund}"));
                Wecke(context, SchreibeAsync);
            }
            hinweise?.Invoke(new HinweisZustellen(Herkunft.Ableite($"gescheitert:{instanz.Name}:{kopf.Kennung:N}"),
                new LaufGescheitert(instanz.Name, kopf.Kennung, knotenName, grund), kopf.Stream, kopf.Version, kopf.Zeit, kopf.Korrelation, kopf.Akteur, kopf.Ursache));
            return;
        }
        if (!o.Lauf.Fertig) return;
        _laeufe.Remove(kopf.Kennung);
        foreach (var w in o.Warten) context.Send(w, new EingangQuittung(kopf.Kennung, Quittungsart.Fertig));
        if (kopf.Art is Eingangsart.Hinweis) return;
        Merke(kopf.Kennung);
        if (kopf.Art == Eingangsart.Durabel && offene is not null)
        {
            _auszutragen.Add(kopf.Kennung);
            Wecke(context, SchreibeAsync);
        }
    }

    private void Merke(Guid kennung)
    {
        if (!_fertig.Add(kennung)) return;
        _fertigReihe.Enqueue(kennung);
        while (_fertigReihe.Count > Gedaechtnis) _fertig.Remove(_fertigReihe.Dequeue());
    }

    /// <summary>Fertige durable Köpfe gebündelt austragen, gescheiterte parken (neben der Mailbox; was dabei dazukommt, schreibt der nächste Durchlauf).</summary>
    private async Task SchreibeAsync(CancellationToken ct)
    {
        var aus = _auszutragen.ToArray();
        var parken = _zuParken.ToArray();
        var sichern = _zuSichern.Select(x => (instanz.Name, x.Key, x.Value)).ToArray();
        _auszutragen.Clear();
        _zuParken.Clear();
        _zuSichern.Clear();
        try
        {
            if (sichern.Length > 0) await ablage!.SichereAsync(sichern, ct);
            if (aus.Length > 0) await offene!.ErledigtAsync(aus, ct);
            if (parken.Length > 0) await offene!.ParkeAsync(parken, ct);
        }
        catch (Exception ex) when (!ct.IsCancellationRequested)
        {
            logger?.LogWarning(ex, "[Funktionen] {Instanz}: {Sichern} Zustände, {Aus} fertige bzw. {Parken} gescheiterte Köpfe nicht geschrieben — der Zusteller "
                + "bringt die Köpfe erneut, die Zustände sichert der nächste fertige Lauf", instanz.Name, sichern.Length, aus.Length, parken.Length);
        }
    }

    /// <summary>Was ein Lauf bewirkt, im Turn dieses Actors.</summary>
    private sealed class Wirkung(GraphActor actor, IContext context) : ILaufWirkung
    {
        public void Rechne(KnotenAufruf aufruf)
        {
            if (actor._parents.TryGetValue(aufruf.Knoten, out var p)) context.Send(p, aufruf);
            else context.Send(context.Self, new KnotenErgebnis(aufruf, Array.Empty<KnotenAusgabe>(), $"der Knoten {aufruf.Knoten} hat hier keinen Parent"));
        }

        public void Breche(string knoten, Guid lauf, IReadOnlyCollection<Guid> aufrufe)
        {
            if (actor._parents.TryGetValue(knoten, out var p)) context.Send(p, new LaufAbbrechen(lauf, aufrufe));
        }

        public void Warte(TimeSpan zeitlimit, ZeitlimitAbgelaufen wecker) =>
            actor.Neben(context, ct => Task.Delay(zeitlimit, ct), t =>
            {
                if (t.IsCompletedSuccessfully) context.Send(context.Self, wecker);
                return Task.CompletedTask;
            });
    }
}
