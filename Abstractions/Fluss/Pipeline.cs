using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;

namespace Abstractions;

// ═══════════════════════════════════════════════════════════════════════════════════════════════════════════════════════
// PIPELINE ALS FLUSS (docs/konzept-editor-pipelines.md §14)
//
// Eine Pipeline ist eine Fläche aus Knoten, die mit Drähten verbunden sind: eine QUELLE (Datei, Timer, … oder ein Event aus dem
// Log), Katalog-FUNKTIONEN und Commands an AGGREGATE. Jeder Knoten hat dieselbe Form: ein Eingang, OneOf-Ausgänge (je Fall ein
// Port). Der Code wird so geschrieben, wie der Editor zeichnet — vom Draht aus:
//
//     var datei    = p.Quelle<DateiErkannt>();
//     var vorschau = datei.Rufe<IBildVerkleinerung>(d => new VerkleinereBild(d.Pfad, 512)).Zeitlimit(TimeSpan.FromMinutes(5));
//     var kontrast = vorschau.Bei<BildVerkleinert>().Rufe<IHistogrammAusgleich>(v => new GleicheHistogrammAus(v.Pfad));
//
// Der Variablenname ist die Knoten-Identität im Editor; zur Laufzeit zählt die Deklarations-Reihenfolge. Weil man nur schon
// deklarierte Knoten verdrahten kann, ist ein Fluss per Konstruktion azyklisch. Übersetzt wird er in Prozess-Regeln mit
// Knoten-Herkunft — der Dirigent (Prozess-Manager, ein Actor je Vorgang) führt ihn aus.
// ═══════════════════════════════════════════════════════════════════════════════════════════════════════════════════════

/// <summary>Eine Pipeline: eine Klasse, die ihren <see cref="PipelineFluss"/> liefert (wie <see cref="IProzessDefinition"/>).</summary>
public interface IPipeline
{
    PipelineFluss Fluss { get; }
}

/// <summary>
/// Die Nachricht einer QUELLE (Datei erkannt, Webhook eingegangen, Takt …). Sie startet genau einen Vorgang: das Framework hängt sie
/// als erstes Event eines Vorgangs-Streams an, dessen Id deterministisch aus <see cref="Kennung"/> folgt — dieselbe Datei zweimal
/// gesehen (oder von zwei Knoten) startet den Vorgang genau einmal.
/// </summary>
public interface IQuellNachricht : IEvent
{
    /// <summary>Was diese Nachricht fachlich eindeutig macht (z. B. Pfad + Größe + Zeitstempel einer Datei).</summary>
    string Kennung { get; }
}

/// <summary>Nicht-generische Sicht auf eine Quelle (für die Bindung im Host).</summary>
public interface IQuelle { }

/// <summary>
/// Eine Katalog-QUELLE: liefert fortlaufend Nachrichten, bis sie abgebrochen wird. Sie kennt weder Pipeline noch Log — das
/// Framework schreibt jede Nachricht genau einmal ins Log und weckt die Pipelines, die mit <c>p.Quelle&lt;T&gt;()</c> beginnen.
/// Die Einstellungen (Pfad, Intervall …) bekommt sie über den Konstruktor (Bindung im Host: <c>AddQuelle</c>).
/// </summary>
public interface IQuelle<TNachricht> : IQuelle where TNachricht : IQuellNachricht
{
    IAsyncEnumerable<TNachricht> LaufeAsync(CancellationToken abbruch);
}

/// <summary>Token am ⏳-Port eines Knotens: sein Aufruf hat das Zeitlimit überschritten (nur im Dirigenten, nie im Log).</summary>
public sealed record ZeitlimitAbgelaufen(string Grund) : IEvent, IProzessIntern;

/// <summary>Token am ✕-Port eines Knotens: das Aggregat hat abgelehnt bzw. die Funktion ist gescheitert (nur im Dirigenten).</summary>
public sealed record SchrittAbgelehnt(string Grund) : IEvent, IProzessIntern;

/// <summary>
/// Der Strom, aus dem die Nachricht der Quelle stammt: bei <c>p.Auf&lt;E&gt;()</c> das Aggregat (Stream-Id) und die Version des Events,
/// bei <c>p.Quelle&lt;T&gt;()</c> der Vorgangs-Stream. Ein Draht wie jeder andere (<c>quelle.Strom()</c>) — so bekommt ein Knoten die Id
/// des auslösenden Aggregats über die Zuordnung, ohne dass das Event sie tragen muss. Nur im Dirigenten, nie im Log.
/// </summary>
public sealed record QuellStrom(Guid Id, int Version) : IEvent, IProzessIntern;

/// <summary>Die Art eines Fluss-Knotens (für Editor, Diagnose und Boot-Prüfungen).</summary>
public enum PipelineKnotenArt { Quelle, Auf, Funktion, Command, Je, Warte }

/// <summary>Ein Knoten des übersetzten Flusses: Deklarations-Index, Art und Typ (Nachricht, Funktion, Command bzw. Element).</summary>
public sealed record PipelineKnotenInfo(int Index, PipelineKnotenArt Art, Type Typ, TimeSpan? Zeitlimit);

/// <summary>Der übersetzte Fluss: die Prozess-Regeln für den Dirigenten und die Knoten-Liste.</summary>
public sealed class PipelineFluss
{
    internal PipelineFluss(ProzessRegeln regeln, IReadOnlyList<PipelineKnotenInfo> knoten)
    {
        Regeln = regeln;
        Knoten = knoten;
    }

    public ProzessRegeln Regeln { get; }
    public IReadOnlyList<PipelineKnotenInfo> Knoten { get; }

    /// <summary>Einstieg: <c>public PipelineFluss Fluss =&gt; PipelineFluss.Definiere(p =&gt; { … });</c></summary>
    public static PipelineFluss Definiere(Action<PipelineBauer> baue)
    {
        if (baue is null) throw new ArgumentNullException(nameof(baue));
        var bauer = new PipelineBauer();
        baue(bauer);
        return bauer.Baue();
    }
}

/// <summary>Der Bauer einer Pipeline: Quelle setzen, Drähte bündeln (∧). Alles Weitere geht vom Draht aus.</summary>
public sealed class PipelineBauer
{
    private readonly List<PipelineKnoten> _knoten = new();
    private PipelineKnoten? _quelle;
    private Type? _quellTyp;

    internal PipelineBauer() { }

    /// <summary>Die Quelle der Pipeline: eine Katalog-Quelle (Datei, Timer, …), deren Nachricht je einen Vorgang startet.</summary>
    public QuellKnoten<T> Quelle<T>() where T : class, IQuellNachricht => SetzeQuelle<T>(PipelineKnotenArt.Quelle);

    /// <summary>Die Quelle der Pipeline: ein Event aus dem Log (z. B. eines Aggregats) — jedes Vorkommen startet einen Vorgang.</summary>
    public QuellKnoten<T> Auf<T>() where T : class, IEvent => SetzeQuelle<T>(PipelineKnotenArt.Auf);

    /// <summary>∧ — der nächste Knoten wartet auf BEIDE Drähte (desselben Vorgangs, desselben Elements).</summary>
    public Verbund<T1, T2> Alle<T1, T2>(Draht<T1> a, Draht<T2> b)
        where T1 : class, IEvent where T2 : class, IEvent
        => new(this, Prüfe(a), Prüfe(b));

    /// <summary>∧ über drei Drähte.</summary>
    public Verbund<T1, T2, T3> Alle<T1, T2, T3>(Draht<T1> a, Draht<T2> b, Draht<T3> c)
        where T1 : class, IEvent where T2 : class, IEvent where T3 : class, IEvent
        => new(this, Prüfe(a), Prüfe(b), Prüfe(c));

    /// <summary>∧ über vier Drähte.</summary>
    public Verbund<T1, T2, T3, T4> Alle<T1, T2, T3, T4>(Draht<T1> a, Draht<T2> b, Draht<T3> c, Draht<T4> d)
        where T1 : class, IEvent where T2 : class, IEvent where T3 : class, IEvent where T4 : class, IEvent
        => new(this, Prüfe(a), Prüfe(b), Prüfe(c), Prüfe(d));

    private QuellKnoten<T> SetzeQuelle<T>(PipelineKnotenArt art) where T : class, IEvent
    {
        if (_quelle is not null)
            throw new InvalidOperationException(
                "Eine Pipeline hat genau eine Quelle — jede Quell-Nachricht startet einen Vorgang. Für eine zweite Quelle eine zweite Pipeline anlegen.");
        var k = new QuellKnoten<T>(this, art);
        _quelle = k.Von;
        _quellTyp = typeof(T);
        return k;
    }

    internal int Registriere(PipelineKnoten k)
    {
        _knoten.Add(k);
        return _knoten.Count - 1;
    }

    internal Draht<T> Prüfe<T>(Draht<T> d) where T : class, IEvent
    {
        if (d is null) throw new ArgumentNullException(nameof(d));
        if (!ReferenceEquals(d.Von.Bauer, this))
            throw new InvalidOperationException("Ein Draht kommt aus einer anderen Pipeline — Drähte verbinden nur Knoten derselben Pipeline.");
        return d;
    }

    internal PipelineFluss Baue()
    {
        if (_quelle is null || _quellTyp is null)
            throw new InvalidOperationException("Eine Pipeline braucht eine Quelle: p.Quelle<Nachricht>() oder p.Auf<Event>().");

        var regeln = new List<Regel>();
        foreach (var k in _knoten.OfType<AufrufKnoten>())
        {
            if (k.Eingänge.Count == 0)
                throw new InvalidOperationException($"Knoten {k.Index} ({k.ZielTyp.Name}) hat keinen Eingang.");
            regeln.AddRange(k.BaueRegeln());
        }

        // Fehler-Ports: ein Knoten, aus dessen ⏳/✕ ein Draht führt, leitet seinen Fehlschlag als Token weiter.
        var zeitlimit = new HashSet<int>();
        var abgelehnt = new HashSet<int>();
        foreach (var r in regeln)
        {
            var drähte = r.Bedingung.Zip(r.VonKnoten, (t, v) => (t, v))
                .Concat(r.Sammel?.Drähte ?? Array.Empty<(Type, int?)>());
            foreach (var (t, v) in drähte)
            {
                if (v is not int von) continue;
                if (t == typeof(ZeitlimitAbgelaufen)) zeitlimit.Add(von);
                if (t == typeof(SchrittAbgelehnt)) abgelehnt.Add(von);
            }
        }
        foreach (var von in zeitlimit)
            if (_knoten[von] is not AufrufKnoten { Limit: not null })
                throw new InvalidOperationException($"Der ⏳-Port von Knoten {von} ist verdrahtet, aber der Knoten hat kein Zeitlimit (.Zeitlimit(…)).");

        foreach (var w in _knoten.OfType<WarteKnoten>())
            if (w.Limit is null)
                throw new InvalidOperationException($"Knoten {w.Index} wartet ohne Zeitlimit — ein Warten braucht .Zeitlimit(…) (sonst bleibt der Vorgang offen, wenn das Event nie kommt).");

        var infos = _knoten.Select(k => new PipelineKnotenInfo(k.Index, k.Art, k.ZielTyp, (k as AufrufKnoten)?.Limit)).ToList();
        return new PipelineFluss(new ProzessRegeln(_quellTyp, regeln, _quelle.Index, zeitlimit, abgelehnt), infos);
    }
}

/// <summary>Basis aller Fluss-Knoten: gehört genau einer Pipeline, hat einen Deklarations-Index.</summary>
public abstract class PipelineKnoten
{
    internal PipelineKnoten(PipelineBauer bauer, PipelineKnotenArt art, Type zielTyp)
    {
        Bauer = bauer;
        Art = art;
        ZielTyp = zielTyp;
        Index = bauer.Registriere(this);
    }

    internal PipelineBauer Bauer { get; }
    internal int Index { get; }
    internal PipelineKnotenArt Art { get; }
    internal Type ZielTyp { get; }
}

/// <summary>Ein Draht: der Ausgang (Fall <typeparamref name="T"/>) eines Knotens. Von hier aus wird der nächste Knoten gerufen.</summary>
public class Draht<T> where T : class, IEvent
{
    internal Draht(PipelineKnoten von) { Von = von; }

    internal PipelineKnoten Von { get; }
    internal (Type Typ, int Von) Def => (typeof(T), Von.Index);

    /// <summary>Ruft eine Katalog-Funktion: der Auftrag wird aus der Nachricht am Draht gebaut.</summary>
    public RufKnoten<F> Rufe<F>(Func<T, IAuftrag<F>> baue) where F : IFunktion
        => new RufKnoten<F>(Von.Bauer).Eingang(new[] { Def }, ps => new object[] { baue((T)ps[0]) });

    /// <summary>Sendet ein Command an ein Aggregat — der Weg in die Aggregat-Welt.</summary>
    public SendeKnoten<C> Sende<C>(Func<T, C> baue) where C : class, ICommand
        => new SendeKnoten<C>(Von.Bauer).Eingang(new[] { Def }, ps => new object[] { baue((T)ps[0]) });

    /// <summary>JE-Rahmen: die folgenden Knoten laufen einmal je Element der Liste, parallel; <c>Sammle</c> wartet auf alle.</summary>
    public JeKnoten<T, E> Je<E>(Func<T, IEnumerable<E>> liste)
        => new(this, liste ?? throw new ArgumentNullException(nameof(liste)));
}

/// <summary>Die Quelle einer Pipeline — Knoten und Ausgangs-Draht zugleich; dazu ihr Strom (<see cref="Strom"/>).</summary>
public sealed class QuellKnoten<T> : Draht<T> where T : class, IEvent
{
    internal QuellKnoten(PipelineBauer bauer, PipelineKnotenArt art) : base(new Knoten(bauer, art)) { }

    /// <summary>Der zweite Ausgang der Quelle: der Strom (Aggregat-Id + Version), aus dem die Nachricht stammt.</summary>
    public StromDraht Strom() => new(Von);

    private sealed class Knoten : PipelineKnoten
    {
        public Knoten(PipelineBauer bauer, PipelineKnotenArt art) : base(bauer, art, typeof(T)) { }
    }
}

/// <summary>
/// Der Strom-Draht einer Quelle. Von hier aus kann der Fluss auf ein späteres Event DESSELBEN Stroms warten — das Rennen „Event
/// gegen Zeitlimit“ (z. B. „Training endet“ gegen „6 h“) ist damit ein Knoten statt einer Frist mit Storno.
/// </summary>
public sealed class StromDraht : Draht<QuellStrom>
{
    internal StromDraht(PipelineKnoten von) : base(von) { }

    /// <summary>Warte auf das erste <typeparamref name="E1"/> im Strom nach der Quell-Nachricht.</summary>
    public WarteKnoten Warte<E1>() where E1 : class, IEvent => new(Von.Bauer, Def, typeof(E1));

    /// <summary>Warte auf das erste der Events im Strom nach der Quell-Nachricht — jeder Typ ist ein Ausgang.</summary>
    public WarteKnoten Warte<E1, E2>() where E1 : class, IEvent where E2 : class, IEvent
        => new(Von.Bauer, Def, typeof(E1), typeof(E2));

    public WarteKnoten Warte<E1, E2, E3>() where E1 : class, IEvent where E2 : class, IEvent where E3 : class, IEvent
        => new(Von.Bauer, Def, typeof(E1), typeof(E2), typeof(E3));

    public WarteKnoten Warte<E1, E2, E3, E4>()
        where E1 : class, IEvent where E2 : class, IEvent where E3 : class, IEvent where E4 : class, IEvent
        => new(Von.Bauer, Def, typeof(E1), typeof(E2), typeof(E3), typeof(E4));
}

/// <summary>
/// Ein Knoten, der WARTET: auf das erste der genannten Events im Strom der Quelle (nach ihrer Version). Ausgänge = die Event-Typen,
/// dazu ⏳ (Pflicht: <see cref="Zeitlimit"/>). Er ruft nichts — der Dirigent liest den Strom bei jeder Weckung nach.
/// </summary>
public sealed class WarteKnoten : AufrufKnoten
{
    internal WarteKnoten(PipelineBauer bauer, (Type Typ, int Von) strom, params Type[] typen)
        : base(bauer, PipelineKnotenArt.Warte, typen[0])
    {
        Typen = typen;
        NeuerEingang(new FlussEingang(new[] { strom }, _ => Array.Empty<object>()));
    }

    /// <summary>Die Events, auf die gewartet wird (in Code-Reihenfolge).</summary>
    public IReadOnlyList<Type> Typen { get; }

    /// <summary>Wie lange gewartet wird; danach feuert ⏳ (<c>BeiZeitlimit()</c>).</summary>
    public WarteKnoten Zeitlimit(TimeSpan limit) { SetzeLimit(limit); return this; }

    internal override IEnumerable<Regel> BaueRegeln()
        => Eingänge.Select(e => new Regel(
            e.Bedingung.Select(b => b.Typ).ToList(),
            sende: null, rückgängigDurch: null, zeitlimit: Limit, knoten: Index,
            vonKnoten: e.Bedingung.Select(b => (int?)b.Von).ToList(), wartetAuf: Typen));
}

/// <summary>Ein Eingang eines Aufruf-Knotens: Bedingung (Drähte), Bau der Aufrufe, optional Je-Auffächern bzw. Sammeln.</summary>
internal sealed record FlussEingang(
    IReadOnlyList<(Type Typ, int Von)> Bedingung,
    Func<IReadOnlyList<IEvent>, IReadOnlyList<object>> Baue,
    int? Je = null,
    SammelBedingung? Sammel = null);

/// <summary>Ein Knoten, der etwas aufruft (Funktion oder Aggregat): seine Ausgänge sind die Fälle, dazu ⏳ und ✕.</summary>
public abstract class AufrufKnoten : PipelineKnoten
{
    internal AufrufKnoten(PipelineBauer bauer, PipelineKnotenArt art, Type zielTyp) : base(bauer, art, zielTyp) { }

    internal List<FlussEingang> Eingänge { get; } = new();
    internal TimeSpan? Limit { get; set; }

    /// <summary>Der Ausgang für den Fall <typeparamref name="E"/> (ein Ergebnis der Funktion bzw. ein Event des Aggregats).</summary>
    public Draht<E> Bei<E>() where E : class, IEvent => new(this);

    /// <summary>⏳-Port: der Aufruf hat sein Zeitlimit überschritten. Verdrahtet = eigener Weg; frei = der Vorgang scheitert.</summary>
    public Draht<ZeitlimitAbgelaufen> BeiZeitlimit() => new(this);

    /// <summary>✕-Port: das Aggregat hat abgelehnt bzw. die Funktion ist gescheitert. Verdrahtet = eigener Weg; frei = Vorgang scheitert.</summary>
    public Draht<SchrittAbgelehnt> BeiAbgelehnt() => new(this);

    internal void NeuerEingang(FlussEingang e)
    {
        foreach (var (_, von) in e.Bedingung)
            if (von >= Index) throw new InvalidOperationException("Ein Draht darf nur von einem früher deklarierten Knoten kommen.");
        Eingänge.Add(e);
    }

    internal void SetzeLimit(TimeSpan limit)
    {
        if (limit <= TimeSpan.Zero) throw new ArgumentOutOfRangeException(nameof(limit), "Ein Zeitlimit muss positiv sein.");
        Limit = limit;
    }

    internal abstract IEnumerable<Regel> BaueRegeln();
}

/// <summary>Ein Knoten, der die Katalog-Funktion <typeparamref name="F"/> ruft.</summary>
public sealed class RufKnoten<F> : AufrufKnoten where F : IFunktion
{
    internal RufKnoten(PipelineBauer bauer) : base(bauer, PipelineKnotenArt.Funktion, typeof(F)) { }

    internal RufKnoten<F> Eingang(IReadOnlyList<(Type Typ, int Von)> bedingung, Func<IReadOnlyList<IEvent>, IReadOnlyList<object>> baue,
        int? je = null, SammelBedingung? sammel = null)
    {
        NeuerEingang(new FlussEingang(bedingung, baue, je, sammel));
        return this;
    }

    /// <summary>Zeitlimit des Aufrufs (ab dem Moment, in dem der Knoten bereit ist).</summary>
    public RufKnoten<F> Zeitlimit(TimeSpan limit) { SetzeLimit(limit); return this; }

    /// <summary>∨ — ein weiterer Draht in denselben Knoten: jeder ankommende Weg ruft die Funktion.</summary>
    public RufKnoten<F> Oder<T>(Draht<T> draht, Func<T, IAuftrag<F>> baue) where T : class, IEvent
        => Eingang(new[] { Bauer.Prüfe(draht).Def }, ps => new object[] { baue((T)ps[0]) });

    /// <summary>∨ mit einem ∧-Verbund als zusätzlichem Eingang.</summary>
    public RufKnoten<F> Oder<T1, T2>(Verbund<T1, T2> v, Func<T1, T2, IAuftrag<F>> baue)
        where T1 : class, IEvent where T2 : class, IEvent
        => Eingang(v.Defs, ps => new object[] { baue((T1)ps[0], (T2)ps[1]) });

    /// <summary>∨ mit einem ∧-Verbund über drei Drähte.</summary>
    public RufKnoten<F> Oder<T1, T2, T3>(Verbund<T1, T2, T3> v, Func<T1, T2, T3, IAuftrag<F>> baue)
        where T1 : class, IEvent where T2 : class, IEvent where T3 : class, IEvent
        => Eingang(v.Defs, ps => new object[] { baue((T1)ps[0], (T2)ps[1], (T3)ps[2]) });

    /// <summary>∨ mit einem ∧-Verbund über vier Drähte.</summary>
    public RufKnoten<F> Oder<T1, T2, T3, T4>(Verbund<T1, T2, T3, T4> v, Func<T1, T2, T3, T4, IAuftrag<F>> baue)
        where T1 : class, IEvent where T2 : class, IEvent where T3 : class, IEvent where T4 : class, IEvent
        => Eingang(v.Defs, ps => new object[] { baue((T1)ps[0], (T2)ps[1], (T3)ps[2], (T4)ps[3]) });

    internal override IEnumerable<Regel> BaueRegeln()
        => Eingänge.Select(e => new Regel(
            e.Bedingung.Select(b => b.Typ).ToList(),
            sende: null, rückgängigDurch: null, sammel: e.Sammel, produziertCommands: null,
            ruft: ps => e.Baue(ps).Cast<IAuftrag>().ToList(),
            gerufeneFunktionen: new[] { typeof(F) },
            zeitlimit: Limit, knoten: Index,
            vonKnoten: e.Bedingung.Select(b => (int?)b.Von).ToList(), jeKnoten: e.Je));
}

/// <summary>Ein Knoten, der das Command <typeparamref name="C"/> an ein Aggregat sendet; Ausgänge = die Events seines Decide.</summary>
public sealed class SendeKnoten<C> : AufrufKnoten where C : class, ICommand
{
    internal SendeKnoten(PipelineBauer bauer) : base(bauer, PipelineKnotenArt.Command, typeof(C)) { }

    internal SendeKnoten<C> Eingang(IReadOnlyList<(Type Typ, int Von)> bedingung, Func<IReadOnlyList<IEvent>, IReadOnlyList<object>> baue,
        int? je = null, SammelBedingung? sammel = null)
    {
        NeuerEingang(new FlussEingang(bedingung, baue, je, sammel));
        return this;
    }

    /// <summary>Zeitlimit, bis das Aggregat geantwortet haben muss.</summary>
    public SendeKnoten<C> Zeitlimit(TimeSpan limit) { SetzeLimit(limit); return this; }

    /// <summary>∨ — ein weiterer Draht in denselben Knoten.</summary>
    public SendeKnoten<C> Oder<T>(Draht<T> draht, Func<T, C> baue) where T : class, IEvent
        => Eingang(new[] { Bauer.Prüfe(draht).Def }, ps => new object[] { baue((T)ps[0]) });

    /// <summary>∨ mit einem ∧-Verbund als zusätzlichem Eingang.</summary>
    public SendeKnoten<C> Oder<T1, T2>(Verbund<T1, T2> v, Func<T1, T2, C> baue)
        where T1 : class, IEvent where T2 : class, IEvent
        => Eingang(v.Defs, ps => new object[] { baue((T1)ps[0], (T2)ps[1]) });

    /// <summary>∨ mit einem ∧-Verbund über drei Drähte.</summary>
    public SendeKnoten<C> Oder<T1, T2, T3>(Verbund<T1, T2, T3> v, Func<T1, T2, T3, C> baue)
        where T1 : class, IEvent where T2 : class, IEvent where T3 : class, IEvent
        => Eingang(v.Defs, ps => new object[] { baue((T1)ps[0], (T2)ps[1], (T3)ps[2]) });

    /// <summary>∨ mit einem ∧-Verbund über vier Drähte.</summary>
    public SendeKnoten<C> Oder<T1, T2, T3, T4>(Verbund<T1, T2, T3, T4> v, Func<T1, T2, T3, T4, C> baue)
        where T1 : class, IEvent where T2 : class, IEvent where T3 : class, IEvent where T4 : class, IEvent
        => Eingang(v.Defs, ps => new object[] { baue((T1)ps[0], (T2)ps[1], (T3)ps[2], (T4)ps[3]) });

    internal override IEnumerable<Regel> BaueRegeln()
        => Eingänge.Select(e => new Regel(
            e.Bedingung.Select(b => b.Typ).ToList(),
            sende: ps => e.Baue(ps).Cast<ICommand>().ToList(),
            rückgängigDurch: null, sammel: e.Sammel,
            produziertCommands: new[] { typeof(C) },
            zeitlimit: Limit, knoten: Index,
            vonKnoten: e.Bedingung.Select(b => (int?)b.Von).ToList(), jeKnoten: e.Je));
}

/// <summary>JE-Rahmen über die Liste aus <typeparamref name="TQ"/>: Knoten auf <c>je</c> laufen einmal je Element.</summary>
public sealed class JeKnoten<TQ, E> : PipelineKnoten, IJeRahmen where TQ : class, IEvent
{
    private readonly Draht<TQ> _quelle;
    private readonly Func<TQ, IEnumerable<E>> _liste;

    internal JeKnoten(Draht<TQ> quelle, Func<TQ, IEnumerable<E>> liste)
        : base(quelle.Von.Bauer, PipelineKnotenArt.Je, typeof(E))
    {
        _quelle = quelle;
        _liste = liste;
    }

    /// <summary>Je Element eine Katalog-Funktion rufen (parallel).</summary>
    public RufKnoten<F> Rufe<F>(Func<E, IAuftrag<F>> baue) where F : IFunktion
        => new RufKnoten<F>(Bauer).Eingang(new[] { _quelle.Def },
            ps => _liste((TQ)ps[0]).Select(e => (object)baue(e)).ToList(), je: Index);

    /// <summary>Je Element ein Command senden.</summary>
    public SendeKnoten<C> Sende<C>(Func<E, C> baue) where C : class, ICommand
        => new SendeKnoten<C>(Bauer).Eingang(new[] { _quelle.Def },
            ps => _liste((TQ)ps[0]).Select(e => (object)baue(e)).ToList(), je: Index);

    /// <summary>
    /// Das Ende des Rahmens: wartet, bis JEDES Element über einen der Drähte geliefert hat, und reicht die Nachrichten als Liste
    /// (in Element-Reihenfolge) weiter. Mehrere Drähte = alternative Wege desselben Typs (z. B. zwei Zweige, die beide bewerten).
    /// </summary>
    public SammelDraht<TQ, T> Sammle<T>(params Draht<T>[] drähte) where T : class, IEvent
    {
        if (drähte is null || drähte.Length == 0) throw new ArgumentException("Sammle braucht mindestens einen Draht.", nameof(drähte));
        foreach (var d in drähte) Bauer.Prüfe(d);
        return new SammelDraht<TQ, T>(this, drähte.Select(d => d.Def).ToList());
    }

    PipelineBauer IJeRahmen.Bauer => Bauer;
    int IJeRahmen.Index => Index;
    (Type Typ, int Von) IJeRahmen.QuellDef => _quelle.Def;
    int IJeRahmen.Anzahl(IEvent quelle) => _liste((TQ)quelle).Count();
}

/// <summary>Innere Sicht auf einen JE-Rahmen (für <see cref="SammelDraht{TQ, T}"/>).</summary>
internal interface IJeRahmen
{
    PipelineBauer Bauer { get; }
    int Index { get; }
    (Type Typ, int Von) QuellDef { get; }
    int Anzahl(IEvent quelle);
}

/// <summary>Das gesammelte Ergebnis eines JE-Rahmens: die Quell-Nachricht des Rahmens plus die Liste je Element.</summary>
public sealed class SammelDraht<TQ, T> where TQ : class, IEvent where T : class, IEvent
{
    private readonly IJeRahmen _je;
    private readonly IReadOnlyList<(Type Typ, int Von)> _drähte;

    internal SammelDraht(IJeRahmen je, IReadOnlyList<(Type Typ, int Von)> drähte)
    {
        _je = je;
        _drähte = drähte;
    }

    private (Type Typ, int Von) _quelle => _je.QuellDef;

    private SammelBedingung Bedingung()
        => new(_drähte.Select(d => (d.Typ, (int?)d.Von)).ToList(), _je.Anzahl, _je.Index);

    /// <summary>Ruft eine Funktion mit der Quell-Nachricht des Rahmens und der gesammelten Liste.</summary>
    public RufKnoten<F> Rufe<F>(Func<TQ, IReadOnlyList<T>, IAuftrag<F>> baue) where F : IFunktion
        => new RufKnoten<F>(_je.Bauer).Eingang(new[] { _quelle },
            ps => new object[] { baue((TQ)ps[0], ps.Skip(1).Cast<T>().ToList()) }, sammel: Bedingung());

    /// <summary>Sendet ein Command mit der Quell-Nachricht des Rahmens und der gesammelten Liste.</summary>
    public SendeKnoten<C> Sende<C>(Func<TQ, IReadOnlyList<T>, C> baue) where C : class, ICommand
        => new SendeKnoten<C>(_je.Bauer).Eingang(new[] { _quelle },
            ps => new object[] { baue((TQ)ps[0], ps.Skip(1).Cast<T>().ToList()) }, sammel: Bedingung());
}

/// <summary>∧ über zwei Drähte.</summary>
public sealed class Verbund<T1, T2> where T1 : class, IEvent where T2 : class, IEvent
{
    private readonly PipelineBauer _bauer;
    internal IReadOnlyList<(Type Typ, int Von)> Defs { get; }
    internal Verbund(PipelineBauer bauer, Draht<T1> a, Draht<T2> b) { _bauer = bauer; Defs = new[] { a.Def, b.Def }; }

    public RufKnoten<F> Rufe<F>(Func<T1, T2, IAuftrag<F>> baue) where F : IFunktion
        => new RufKnoten<F>(_bauer).Eingang(Defs, ps => new object[] { baue((T1)ps[0], (T2)ps[1]) });

    public SendeKnoten<C> Sende<C>(Func<T1, T2, C> baue) where C : class, ICommand
        => new SendeKnoten<C>(_bauer).Eingang(Defs, ps => new object[] { baue((T1)ps[0], (T2)ps[1]) });
}

/// <summary>∧ über drei Drähte.</summary>
public sealed class Verbund<T1, T2, T3> where T1 : class, IEvent where T2 : class, IEvent where T3 : class, IEvent
{
    private readonly PipelineBauer _bauer;
    internal IReadOnlyList<(Type Typ, int Von)> Defs { get; }
    internal Verbund(PipelineBauer bauer, Draht<T1> a, Draht<T2> b, Draht<T3> c) { _bauer = bauer; Defs = new[] { a.Def, b.Def, c.Def }; }

    public RufKnoten<F> Rufe<F>(Func<T1, T2, T3, IAuftrag<F>> baue) where F : IFunktion
        => new RufKnoten<F>(_bauer).Eingang(Defs, ps => new object[] { baue((T1)ps[0], (T2)ps[1], (T3)ps[2]) });

    public SendeKnoten<C> Sende<C>(Func<T1, T2, T3, C> baue) where C : class, ICommand
        => new SendeKnoten<C>(_bauer).Eingang(Defs, ps => new object[] { baue((T1)ps[0], (T2)ps[1], (T3)ps[2]) });
}

/// <summary>∧ über vier Drähte.</summary>
public sealed class Verbund<T1, T2, T3, T4>
    where T1 : class, IEvent where T2 : class, IEvent where T3 : class, IEvent where T4 : class, IEvent
{
    private readonly PipelineBauer _bauer;
    internal IReadOnlyList<(Type Typ, int Von)> Defs { get; }
    internal Verbund(PipelineBauer bauer, Draht<T1> a, Draht<T2> b, Draht<T3> c, Draht<T4> d)
    { _bauer = bauer; Defs = new[] { a.Def, b.Def, c.Def, d.Def }; }

    public RufKnoten<F> Rufe<F>(Func<T1, T2, T3, T4, IAuftrag<F>> baue) where F : IFunktion
        => new RufKnoten<F>(_bauer).Eingang(Defs, ps => new object[] { baue((T1)ps[0], (T2)ps[1], (T3)ps[2], (T4)ps[3]) });

    public SendeKnoten<C> Sende<C>(Func<T1, T2, T3, T4, C> baue) where C : class, ICommand
        => new SendeKnoten<C>(_bauer).Eingang(Defs, ps => new object[] { baue((T1)ps[0], (T2)ps[1], (T3)ps[2], (T4)ps[3]) });
}
