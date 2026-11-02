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
    ILogger? logger = null) : RahmenActor(logger)
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
            case KnotenErgebnis r when _laeufe.TryGetValue(r.Aufruf.Kontext.Lauf, out var o):
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

    private static void Antworte(IContext context, EingangZustellen e, Quittungsart ausgang)
    {
        if (e.Art != Eingangsart.Hinweis && context.Sender is not null) context.Respond(new EingangQuittung(e.Kennung, ausgang));
    }

    /// <summary>Ist der Lauf fertig oder gescheitert, antwortet er den Wartenden und verlässt die Buchführung.</summary>
    private void Abschluss(IContext context, Offen o)
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
        if (kopf.Art == Eingangsart.Hinweis) return;
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
        _auszutragen.Clear();
        _zuParken.Clear();
        try
        {
            if (aus.Length > 0) await offene!.ErledigtAsync(aus, ct);
            if (parken.Length > 0) await offene!.ParkeAsync(parken, ct);
        }
        catch (Exception ex) when (!ct.IsCancellationRequested)
        {
            logger?.LogWarning(ex, "[Funktionen] {Instanz}: {Aus} fertige bzw. {Parken} gescheiterte Köpfe nicht geschrieben — der Zusteller bringt sie erneut",
                instanz.Name, aus.Length, parken.Length);
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
