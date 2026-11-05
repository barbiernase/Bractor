using Abstractions;
using Infrastructure.Eingaenge;
using Infrastructure.Lebenszyklus;
using Microsoft.Extensions.Logging;
using Proto;

namespace Infrastructure.Funktionen;

/// <summary>
/// Der GRAPH-ACTOR einer Instanz auf einem Cluster-Knoten (docs/04 §4.4, <c>funktionen/&lt;instanz&gt;</c>) — der Halter ihrer Schritte und ihr Kopf.
/// Seine Kinder sind die Parents der Knoten (<c>funktionen/&lt;instanz&gt;/&lt;knoten&gt;</c>); zwischen ihnen reisen die Aufrufe direkt (Kette.cs). Er
/// koordiniert nichts: ein Eingang am Kopf (Kennung = die des Eingangs; ein doppelter hängt sich an bzw. hört „fertig“) geht als gesicherter Aufruf an
/// den Knoten, der ihn nimmt, ein Element der Quelle an die Knoten, die sie nennen. Kommt dessen „fertig“, antwortet er dem wartenden Absender bzw.
/// trägt den durablen Eingang aus; kommt „gescheitert“, parkt er den durablen Kopf und meldet <see cref="LaufGescheitert"/>. Sind schon so viele Ketten
/// offen, wie die Warteschlange des Kopfs erlaubt, heißt die Antwort „später“ — der Rückstau reicht bis zum durablen Rand. Ist der Kopf eine
/// ÜBERSETZUNG, rechnet er sie beim Annehmen inline: ihr Ergebnis geht an die Knoten, die sie nennen, oder als Command an ein Aggregat
/// (Id aus Kopf und Übersetzung; die Kette ist fertig, wenn das Ziel entschieden hat).
/// </summary>
public sealed class GraphActor(
    GraphInstanz instanz,
    IReadOnlyDictionary<string, Func<FunktionsParent>> knoten,
    IOffeneEingaenge? offene = null,
    Action<HinweisZustellen>? hinweise = null,
    ILogger? logger = null,
    IZustandsAblage? ablage = null,
    Func<IAggregatCommand, Guid, Guid, CancellationToken, Task<Infrastructure.Emit.EmitAntwort>>? sende = null) : RahmenActor(logger)
{
    /// <summary>So viele fertige Kennungen merkt sich ein Graph-Actor (ein doppelter Eingang danach rechnet neu — die Inbox verwirft seine Commands).</summary>
    public const int Gedaechtnis = 10_000;

    private sealed class Offen(EingangZustellen kopf)
    {
        public EingangZustellen Kopf { get; } = kopf;
        public List<PID> Warten { get; } = new();
        /// <summary>Die gesicherten Aufrufe, die der Kopf gab (ihre Kennungen) — beim Ende fallen sie aus der Tabelle.</summary>
        public List<Guid> Aufrufe { get; } = new();
        public int Ausstehend;
    }

    private readonly Dictionary<string, PID> _parents = new(StringComparer.Ordinal);
    private readonly Dictionary<Guid, Offen> _ketten = new();
    private readonly Dictionary<Guid, (Guid Kopf, PID Ziel, KnotenAufruf Aufruf)> _aufrufe = new();
    private readonly HashSet<Guid> _fertig = new();
    private readonly Queue<Guid> _fertigReihe = new();
    private readonly List<Guid> _auszutragen = new();
    private readonly List<(Guid Kennung, string Grund)> _zuParken = new();
    // Der Zustand der Quelle: die Schnappschüsse in Reihenfolge, mit der Kette, die ihr Element startete; gesichert wird der letzte, dessen Kette und
    //   alle früheren beendet sind (ein Element, dessen Kette nicht fertig war, meldet sie nach einem Neustart unter derselben Nummer erneut).
    private readonly Queue<(Guid Kette, GesicherterZustand Zustand)> _quellStaende = new();
    private GesicherterZustand? _zuSichern;
    private readonly Queue<QuellMeldung> _quellRueckstau = new();

    /// <summary>Kein Passivieren mit offenen Ketten.</summary>
    protected override bool Beschaeftigt => base.Beschaeftigt || _ketten.Count > 0;

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
                if (_ketten.Count >= QuellGrenze) _quellRueckstau.Enqueue(m);
                else Quelle(context, m);
                break;
            case KnotenRueckmeldung r: Rueckmeldung(context, r); break;
        }
        return Task.CompletedTask;
    }

    private void Kopf(IContext context, EingangZustellen e)
    {
        if (_fertig.Contains(e.Kennung)) { Antworte(context, e, Quittungsart.Fertig); return; }
        if (_ketten.TryGetValue(e.Kennung, out var schon))
        {
            if (e.Art == Eingangsart.Kette && context.Sender is { } s) schon.Warten.Add(s);
            else Antworte(context, e, Quittungsart.Angenommen);
            return;
        }
        var typ = e.Nachricht.GetType();
        var koepfe = instanz.KopfFuer(typ);
        var ziele = koepfe.Select(k => (Knoten: k, Op: instanz.Graph.KnotenNamens(k)?.Nimmt(typ))).Where(x => x.Op is not null).ToList();
        var uebersetzungen = koepfe.Select(instanz.Graph.Uebersetzung).OfType<UebersetzungPlan>().Where(u => u.Ein == typ).ToList();
        if (ziele.Count == 0 && uebersetzungen.Count == 0)
        {
            logger?.LogWarning("[Funktionen] {Instanz}: kein Knoten nimmt {Typ} am Kopf — gescheitert", instanz.Name, typ.Name);
            Antworte(context, e, Quittungsart.Gescheitert);
            return;
        }
        if (_ketten.Count >= instanz.Ketten(typ)) { Antworte(context, e, Quittungsart.Spaeter); return; }
        var o = new Offen(e);
        if (e.Art == Eingangsart.Kette && context.Sender is { } absender) o.Warten.Add(absender);
        else Antworte(context, e, Quittungsart.Angenommen);
        _ketten[e.Kennung] = o;
        // Der Akteur der Kette: der der Instanz (die Arbeit eines Akteurs gibt er selbst hinein), sonst der des Auslösers.
        var kontext = new KettenKontext(e.Kennung, e.Stream, e.Version, e.Zeit, instanz.Akteur ?? e.Akteur, e.Korrelation, e.Ursache);
        foreach (var (k, op) in ziele) Rufe(context, o, k, op!.Auf, e.Nachricht, e.Kennung, kontext, verlierbar: false);
        foreach (var u in uebersetzungen) Uebersetze(context, o, u, kontext);
        if (_ketten.ContainsKey(e.Kennung) && o.Ausstehend == 0) Beende(context, o, null);
    }

    /// <summary>
    /// Die Übersetzung am Kopf, inline: ihr Ergebnis an die Knoten, die sie nennen (Kennung aus Kopf und Übersetzung), oder — verlässt es den Graphen —
    /// als Command an sein Aggregat; die Kette wartet, bis das Ziel entschieden hat (eine Wiederholung verwirft seine Inbox).
    /// </summary>
    private void Uebersetze(IContext context, Offen o, UebersetzungPlan u, KettenKontext kontext)
    {
        var ort = instanz.Ort(u.Name);
        object y;
        try { y = u.Rechne(o.Kopf.Nachricht); }
        catch (Exception ex) { Beende(context, o, (ort, ex.Message)); return; }
        var wege = instanz.Graph.Wege(u.Name, 0, y.GetType());
        foreach (var w in wege) Rufe(context, o, w.Knoten, w.Operation, y, Kettenvertrag.Weiter(o.Kopf.Kennung, ort, 0), kontext, w.Verlierbar);
        if (wege.Count > 0) return;
        if (y is not IAggregatCommand c || sende is null)
        {
            Beende(context, o, (ort, $"die Übersetzung am Kopf gab {y.GetType().Name} aus, und kein Knoten nimmt es"
                + (y is IAggregatCommand ? " (kein Emitter)" : "")));
            return;
        }
        o.Ausstehend++;
        var kopf = o.Kopf.Kennung;
        var id = Kettenvertrag.Befehl(kopf, ort, 0, c);
        var korrelation = Guid.TryParse(kontext.Korrelation, out var k) ? k : kopf;
        Neben(context, async ct =>
        {
            using var akteur = Akteure.AkteurHerkunft.Aus(kontext.Akteur);
            using var spur = Spur.Von(kontext.Ursache ?? Spur.Ursache(kontext.Stream, kontext.Version), WirkerId.FuerKnoten(instanz.Name, u.Name));
            for (var versuch = 1; ; versuch++)
            {
                if ((await sende(c, id, korrelation, ct)).Ausgang.IstEntschieden()) return;
                if (versuch >= FunktionsKindActor.Versuche)
                    throw new InvalidOperationException($"{ort}: Command {c.GetType().Name} an {c.Ziel} bleibt nach {versuch} Versuchen unbekannt.");
                await Task.Delay(FunktionsKindActor.Pause(versuch), ct);
            }
        }, t =>
        {
            if (!_ketten.TryGetValue(kopf, out var offen)) return Task.CompletedTask;
            if (!t.IsCompletedSuccessfully) Beende(context, offen, (ort, t.Exception?.GetBaseException().Message ?? "abgebrochen"));
            else if (--offen.Ausstehend == 0) Beende(context, offen, null);
            return Task.CompletedTask;
        });
    }

    /// <summary>So viele Ketten nimmt die Quelle zugleich (die Warteschlange ihres Knotens); darüber wartet sie (Gegendruck).</summary>
    private int QuellGrenze => instanz.Graph.Quelle is { } q && instanz.Konfig(q).Warteschlange is > 0 and var w ? w : FunktionsOrtKonfig.StandardWarteschlange;

    /// <summary>
    /// Ein Element der Quelle startet eine Kette — Kennung aus Instanz und Nummer (nach einem Neustart rechnet sie mit derselben Nummer dieselben Ids),
    /// Herkunft aus Instanz, Nummer und Zeit, der Akteur der Instanz; es geht an die Knoten, die die Quelle nennen. Ein Hinweis der Quelle geht an die,
    /// die ihn hören. Ihr Schnappschuss wird gesichert, sobald diese Kette und alle früheren beendet sind.
    /// </summary>
    private void Quelle(IContext context, QuellMeldung m)
    {
        var quelle = instanz.Graph.Quelle!;
        var kennung = Herkunft.Ableite($"quelle:{instanz.Name}:{m.Nummer}");
        var stream = Herkunft.Ableite($"quelle:{instanz.Name}");
        var zustand = new GesicherterZustand(m.Zustand, m.Nummer);
        if (m.Nachricht is ITransientEvent h)
        {
            hinweise?.Invoke(new HinweisZustellen(kennung, h, stream, (int)m.Nummer, DateTimeOffset.UtcNow, kennung.ToString(), instanz.Akteur, null));
            if (ablage is not null) _quellStaende.Enqueue((Guid.Empty, zustand));
            m.Angenommen.TrySetResult();
            PruefeSicherung(context);
            return;
        }
        if (_ketten.ContainsKey(kennung) || _fertig.Contains(kennung)) { m.Angenommen.TrySetResult(); return; }   // nach einem Neustart der Quelle
        var kopf = new EingangZustellen(kennung, instanz.Name, m.Nachricht, Eingangsart.Quelle, stream, (int)m.Nummer, DateTimeOffset.UtcNow, instanz.Akteur,
            kennung.ToString(), null);
        var o = new Offen(kopf);
        _ketten[kennung] = o;
        if (ablage is not null) _quellStaende.Enqueue((kennung, zustand));
        m.Angenommen.TrySetResult();
        var kontext = new KettenKontext(kennung, stream, (int)m.Nummer, kopf.Zeit, instanz.Akteur, kopf.Korrelation, null);
        var op = instanz.Graph.KnotenNamens(quelle)!.Operationen.Single(x => x.Eingang is null);
        var nachricht = Kettenvertrag.Weiter(kennung, instanz.Ort(quelle), 0);
        foreach (var w in instanz.Graph.Wege(quelle, op.Auf, m.Nachricht.GetType()))
            Rufe(context, o, w.Knoten, w.Operation, m.Nachricht, nachricht, kontext, w.Verlierbar);
        if (o.Ausstehend == 0) Beende(context, o, null);
    }

    /// <summary>Ein Aufruf aus dem Kopf: gesichert mit diesem Actor als Antwort (die Kette ist offen, bis er „fertig“ meldet), verlierbar ohne.</summary>
    private void Rufe(IContext context, Offen o, string ziel, int operation, object nachricht, Guid nachrichtKennung, KettenKontext kontext, bool verlierbar)
    {
        if (!_parents.TryGetValue(ziel, out var pid))
        {
            Beende(context, o, ($"{instanz.Ort(ziel)}", $"der Knoten {ziel} hat hier keinen Parent"));
            return;
        }
        var kennung = Kettenvertrag.Aufruf(nachrichtKennung, instanz.Ort(ziel), operation);
        var aufruf = new KnotenAufruf(kennung, ziel, operation, nachricht, kontext, verlierbar ? null : context.Self);
        if (!verlierbar)
        {
            _aufrufe[kennung] = (o.Kopf.Kennung, pid, aufruf);
            o.Aufrufe.Add(kennung);
            o.Ausstehend++;
        }
        context.Send(pid, aufruf);
    }

    /// <summary>Die Antwort eines Kopf-Knotens: „später“ schickt nach einer Pause erneut, „fertig“ zählt ab, „gescheitert“ beendet die Kette.</summary>
    private void Rueckmeldung(IContext context, KnotenRueckmeldung r)
    {
        if (!_aufrufe.TryGetValue(r.Kennung, out var a)) return;
        if (r.Art == Rueckmeldeart.Spaeter)
        {
            Neben(context, ct => Task.Delay(FunktionsParent.SpaeterPause, ct), t =>
            {
                if (t.IsCompletedSuccessfully && _aufrufe.ContainsKey(r.Kennung)) context.Send(a.Ziel, a.Aufruf);
                return Task.CompletedTask;
            });
            return;
        }
        _aufrufe.Remove(r.Kennung);
        if (!_ketten.TryGetValue(a.Kopf, out var o)) return;
        if (r.Art == Rueckmeldeart.Gescheitert) Beende(context, o, (r.Knoten ?? a.Aufruf.Knoten, r.Grund ?? "gescheitert"));
        else if (--o.Ausstehend == 0) Beende(context, o, null);
    }

    /// <summary>
    /// Die Kette ist fertig (<paramref name="gescheitert"/> null) oder gescheitert: die Wartenden erfahren es, ein durabler Kopf wird ausgetragen bzw.
    /// geparkt, ein gescheiterter meldet <see cref="LaufGescheitert"/>; danach Sicherung und Rückstau der Quelle.
    /// </summary>
    private void Beende(IContext context, Offen o, (string Knoten, string Grund)? gescheitert)
    {
        var kopf = o.Kopf;
        if (!_ketten.Remove(kopf.Kennung)) return;
        foreach (var x in o.Aufrufe) _aufrufe.Remove(x);
        if (gescheitert is var (knotenName, grund))
        {
            foreach (var w in o.Warten) context.Send(w, new EingangQuittung(kopf.Kennung, Quittungsart.Gescheitert));
            logger?.LogWarning("[Funktionen] {Instanz}: Kette {Kopf} gescheitert an {Knoten} — {Grund}", instanz.Name, kopf.Kennung, knotenName, grund);
            if (kopf.Art == Eingangsart.Durabel && offene is not null)
            {
                _zuParken.Add((kopf.Kennung, $"{knotenName}: {grund}"));
                Wecke(context, SchreibeAsync);
            }
            hinweise?.Invoke(new HinweisZustellen(Herkunft.Ableite($"gescheitert:{instanz.Name}:{kopf.Kennung:N}"),
                new LaufGescheitert(instanz.Name, kopf.Kennung, knotenName, grund), kopf.Stream, kopf.Version, kopf.Zeit, kopf.Korrelation, kopf.Akteur, kopf.Ursache));
        }
        else
        {
            foreach (var w in o.Warten) context.Send(w, new EingangQuittung(kopf.Kennung, Quittungsart.Fertig));
            Merke(kopf.Kennung);
            if (kopf.Art == Eingangsart.Durabel && offene is not null)
            {
                _auszutragen.Add(kopf.Kennung);
                Wecke(context, SchreibeAsync);
            }
        }
        PruefeSicherung(context);
        while (_quellRueckstau.Count > 0 && _ketten.Count < QuellGrenze) Quelle(context, _quellRueckstau.Dequeue());
    }

    /// <summary>Der letzte Schnappschuss der Quelle, dessen Kette und alle früheren beendet (fertig oder gescheitert) sind — zum Sichern.</summary>
    private void PruefeSicherung(IContext context)
    {
        GesicherterZustand? letzter = null;
        while (_quellStaende.Count > 0 && !_ketten.ContainsKey(_quellStaende.Peek().Kette)) letzter = _quellStaende.Dequeue().Zustand;
        if (letzter is null) return;
        _zuSichern = letzter;
        Wecke(context, SchreibeAsync);
    }

    private static void Antworte(IContext context, EingangZustellen e, Quittungsart ausgang)
    {
        if (context.Sender is not null) context.Respond(new EingangQuittung(e.Kennung, ausgang));
    }

    private void Merke(Guid kennung)
    {
        if (!_fertig.Add(kennung)) return;
        _fertigReihe.Enqueue(kennung);
        while (_fertigReihe.Count > Gedaechtnis) _fertig.Remove(_fertigReihe.Dequeue());
    }

    /// <summary>Fertige durable Köpfe gebündelt austragen, gescheiterte parken, den Stand der Quelle sichern (neben der Mailbox; was dabei dazukommt, schreibt der nächste Durchlauf).</summary>
    private async Task SchreibeAsync(CancellationToken ct)
    {
        var aus = _auszutragen.ToArray();
        var parken = _zuParken.ToArray();
        var sichern = _zuSichern is { } z && instanz.Graph.Quelle is { } q ? new[] { (instanz.Name, q, z) } : Array.Empty<(string, string, GesicherterZustand)>();
        _auszutragen.Clear();
        _zuParken.Clear();
        _zuSichern = null;
        try
        {
            if (sichern.Length > 0) await ablage!.SichereAsync(sichern, ct);
            if (aus.Length > 0) await offene!.ErledigtAsync(aus, ct);
            if (parken.Length > 0) await offene!.ParkeAsync(parken, ct);
        }
        catch (Exception ex) when (!ct.IsCancellationRequested)
        {
            logger?.LogWarning(ex, "[Funktionen] {Instanz}: Stand der Quelle, {Aus} fertige bzw. {Parken} gescheiterte Köpfe nicht geschrieben — der Zusteller "
                + "bringt die Köpfe erneut, den Stand sichert die nächste fertige Kette", instanz.Name, aus.Length, parken.Length);
        }
    }
}
