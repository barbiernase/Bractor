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
/// den Knoten, der ihn nimmt. Kommt dessen „fertig“, antwortet er dem wartenden Absender bzw.
/// trägt den durablen Eingang aus; kommt „gescheitert“, parkt er den durablen Kopf und meldet <see cref="LaufGescheitert"/>. Sind schon so viele Ketten
/// offen, wie die Instanz Stränge hat (<see cref="GraphInstanz.Gleichzeitig"/>), heißt die Antwort „später“ — der Rückstau
/// reicht bis zum durablen Rand. Eine Kopf-Übersetzung rechnet schon der Eingang (<see cref="Verkettung.Ziele"/>); hier kommt ihr Ergebnis an.
/// </summary>
public sealed class GraphActor(
    GraphInstanz instanz,
    IReadOnlyDictionary<string, Func<FunktionsParent>> knoten,
    IOffeneEingaenge? offene = null,
    Action<HinweisZustellen>? hinweise = null,
    ILogger? logger = null,
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
        if (ziele.Count == 0)
        {
            logger?.LogWarning("[Funktionen] {Instanz}: kein Knoten nimmt {Typ} am Kopf — gescheitert", instanz.Name, typ.Name);
            Antworte(context, e, Quittungsart.Gescheitert);
            return;
        }
        if (_ketten.Count >= instanz.Gleichzeitig) { Antworte(context, e, Quittungsart.Spaeter); return; }
        var o = new Offen(e);
        if (e.Art == Eingangsart.Kette && context.Sender is { } absender) o.Warten.Add(absender);
        else Antworte(context, e, Quittungsart.Angenommen);
        _ketten[e.Kennung] = o;
        // Der Akteur der Kette: der ihres Auslösers.
        var kontext = new KettenKontext(e.Kennung, e.Stream, e.Version, e.Zeit, e.Akteur, e.Korrelation, e.Ursache);
        foreach (var (k, op) in ziele) Rufe(context, o, k, op!.Auf, e.Nachricht, e.Kennung, kontext, verlierbar: false);
        if (_ketten.ContainsKey(e.Kennung) && o.Ausstehend == 0) Beende(context, o, null);
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
    /// geparkt, ein gescheiterter meldet <see cref="LaufGescheitert"/>.
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
            logger?.LogWarning(ex, "[Funktionen] {Instanz}: {Aus} fertige bzw. {Parken} gescheiterte Köpfe nicht geschrieben — der Zusteller "
                + "bringt die Köpfe erneut", instanz.Name, aus.Length, parken.Length);
        }
    }
}
