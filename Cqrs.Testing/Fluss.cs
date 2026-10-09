using Abstractions;

namespace Cqrs.Testing;

// ═══════════════════════════════════════════════════════════════════════════════════════════════════════════════════════
// PIPELINE ALS FLUSS durchspielen (docs/konzept-editor-pipelines.md §14) — store-frei, ohne Implementierung der Funktionen.
//
// Der Dirigent entscheidet mit DEMSELBEN Kern wie der Prozess-Manager (Abstractions.FlussBelegung: Herkunft, Je-Teile, Sammeln),
// nur hält er die Tokens im Speicher statt sie aus dem Log zu falten. Was eine Funktion liefert, sagt die WAHL (je Knoten der
// Ausgangsfall: ein Ergebnis, ⏳ Zeitlimit oder ✕ abgelehnt) — Entwurf zuerst. Commands laufen in das echte Aggregat-Kompilat
// (SagaLaufwerk, inkl. klassischer Sagas); ihre Events starten Flüsse mit <c>p.Auf&lt;E&gt;()</c>.
// ═══════════════════════════════════════════════════════════════════════════════════════════════════════════════════════

/// <summary>
/// Ein Knoten im Fluss, dessen Ausgang die Wahl bestimmt: ein Funktionsaufruf (<see cref="Auftrag"/>) oder ein Warten auf ein Event im
/// Strom der Quelle (<see cref="WartetAuf"/>; Antwort = eines dieser Events oder ⏳).
/// </summary>
public sealed record FlussAufruf(string Pipeline, int Vorgang, int Knoten, int Aufruf, IAuftrag? Auftrag, IReadOnlyList<IEvent> Eingang)
{
    public IReadOnlyList<Type> WartetAuf { get; init; } = Array.Empty<Type>();
}

/// <summary>Was eine Funktion im Durchspielen liefert.</summary>
public abstract record FlussAntwort
{
    private FlussAntwort() { }

    /// <summary>Ein Ergebnis-Fall der Funktion (Event aus ihrem OneOf).</summary>
    public sealed record Ergebnis(IEvent Event) : FlussAntwort;

    /// <summary>⏳ — der Aufruf überschreitet sein Zeitlimit.</summary>
    public sealed record Zeitlimit : FlussAntwort;

    /// <summary>✕ — die Funktion scheitert.</summary>
    public sealed record Abgelehnt(string Grund) : FlussAntwort;
}

/// <summary>Ausgang eines Knotens im Durchspielen.</summary>
public static class FlussAusgang
{
    public const string Fall = "fall";
    public const string Zeitlimit = "zeitlimit";
    public const string Abgelehnt = "abgelehnt";
    /// <summary>Der Strom der Quelle (nur als Eingangs-Draht, <c>quelle.Strom()</c>).</summary>
    public const string Strom = "strom";
    /// <summary>Das Aggregat hat das Command angenommen, aber kein Event geschrieben (Noop) — kein Token.</summary>
    public const string Noop = "noop";
    /// <summary>Ein Fehler an einem freien Port: der Vorgang scheitert.</summary>
    public const string Gescheitert = "gescheitert";
}

/// <summary>Ein eingegangener Draht eines Schritts: Herkunfts-Knoten und Fall (bzw. ⏳/✕).</summary>
public sealed record FlussEinDraht(int Von, string Fall, string Port);

/// <summary>Ein Schritt des Dirigenten: ein Knoten bekommt seine Drähte und ruft (Funktion/Command) — oder die Quelle startet.</summary>
public sealed record FlussSchritt
{
    public required string Pipeline { get; init; }
    /// <summary>Laufende Nummer des Vorgangs (je Laufwerk, ab 1).</summary>
    public required int Vorgang { get; init; }
    public required int Knoten { get; init; }
    public required PipelineKnotenArt Art { get; init; }
    public required Type Typ { get; init; }
    public IReadOnlyList<FlussEinDraht> Ein { get; init; } = Array.Empty<FlussEinDraht>();
    /// <summary>Auftrag, Command bzw. die Quell-Nachricht.</summary>
    public required object Nachricht { get; init; }
    /// <summary>Gesetzt bei Je-Auffächern: der Element-Index.</summary>
    public int? Element { get; init; }
    /// <summary><see cref="FlussAusgang"/>.</summary>
    public required string Ausgang { get; init; }
    /// <summary>Das Ergebnis-Token (Fall) bzw. <see cref="ZeitlimitAbgelaufen"/>/<see cref="SchrittAbgelehnt"/>; null bei Noop/Scheitern.</summary>
    public IEvent? Ergebnis { get; init; }
    public string? Grund { get; init; }
    /// <summary>Bei Commands: die Kaskade im Aggregat-Kompilat (inkl. klassischer Sagas).</summary>
    public SagaTrace? Kaskade { get; init; }
}

/// <summary>Ein Vorgang (eine Quell-Nachricht): beendet, wenn nichts mehr feuern kann; gescheitert an einem freien Fehler-Port.</summary>
public sealed record FlussVorgang(string Pipeline, int Nummer, bool Erfolg, string? Grund, IReadOnlyList<string> Wartend);

/// <summary>Der Lauf: Wurzel-Kaskade (wenn ein Command von außen kam), die Fluss-Schritte und die Vorgänge.</summary>
public sealed record FlussTrace(SagaTrace? Wurzel, IReadOnlyList<FlussSchritt> Schritte, IReadOnlyList<FlussVorgang> Vorgänge);

/// <summary>
/// Der Dirigent im Speicher: führt Flüsse Welle für Welle aus (alle bereiten Aufrufe einer Welle sind parallel), Commands über das
/// <see cref="SagaLaufwerk"/>. Ein Vorgang endet, wenn keine Regel mehr eine neue Belegung findet.
/// </summary>
public sealed class FlussLaufwerk
{
    private const int MaxSchritte = 500;
    private readonly SagaLaufwerk _lauf;
    private readonly IReadOnlyList<(string Name, PipelineFluss Fluss)> _fluesse;
    private int _vorgänge;

    public FlussLaufwerk(SagaLaufwerk lauf, IReadOnlyList<(string Name, PipelineFluss Fluss)> fluesse)
    {
        _lauf = lauf;
        _fluesse = fluesse;
    }

    public SagaLaufwerk Aggregate => _lauf;
    public IReadOnlyList<(string Name, PipelineFluss Fluss)> Fluesse => _fluesse;

    /// <summary>Ein Command von außen: seine Kaskade, danach die Flüsse, die seine Events starten (<c>p.Auf&lt;E&gt;()</c>).</summary>
    public FlussTrace Fahre(ICommand wurzel, Func<FlussAufruf, FlussAntwort> wahl)
    {
        var trace = _lauf.Fahre(wurzel);
        var schritte = new List<FlussSchritt>();
        var vorgänge = new List<FlussVorgang>();
        var warte = new Queue<(string, PipelineFluss, IEvent, Guid)>();
        foreach (var (e, strom) in Events(trace)) foreach (var s in StartetVon(e, strom)) warte.Enqueue(s);
        Treibe(warte, wahl, schritte, vorgänge);
        return new FlussTrace(trace, schritte, vorgänge);
    }

    /// <summary>
    /// Eine Quell-Nachricht (oder ein Event) in die Pipeline <paramref name="pipeline"/> einspeisen. <paramref name="strom"/> = der Strom,
    /// aus dem sie stammt (bei <c>p.Auf&lt;E&gt;()</c> das Aggregat; sonst ein Vorgangs-Strom) — ihn liefert <c>quelle.Strom()</c>.
    /// </summary>
    public FlussTrace Starte(string pipeline, IEvent quelle, Func<FlussAufruf, FlussAntwort> wahl, Guid? strom = null)
    {
        var fluss = _fluesse.FirstOrDefault(f => f.Name == pipeline).Fluss
            ?? throw new ArgumentException($"Pipeline '{pipeline}' ist nicht im Kompilat.", nameof(pipeline));
        if (!fluss.Regeln.AuslöserTyp.IsInstanceOfType(quelle))
            throw new ArgumentException($"{quelle.GetType().Name} ist nicht die Quelle von '{pipeline}' ({fluss.Regeln.AuslöserTyp.Name}).");
        var schritte = new List<FlussSchritt>();
        var vorgänge = new List<FlussVorgang>();
        var warte = new Queue<(string, PipelineFluss, IEvent, Guid)>();
        warte.Enqueue((pipeline, fluss, quelle, strom ?? Guid.NewGuid()));
        Treibe(warte, wahl, schritte, vorgänge);
        return new FlussTrace(null, schritte, vorgänge);
    }

    private IEnumerable<(string, PipelineFluss, IEvent, Guid)> StartetVon(IEvent e, Guid strom)
        => _fluesse.Where(f => f.Fluss.Regeln.AuslöserTyp == e.GetType()).Select(f => (f.Name, f.Fluss, e, strom));

    /// <summary>Die persistenten Events einer Kaskade mit ihrem Strom (dem Aggregat, das sie schrieb).</summary>
    private static IEnumerable<(IEvent Event, Guid Strom)> Events(SagaTrace t)
        => t.Schritte.Where(s => !s.Unrouted).SelectMany(s => s.Ausgang.Where(a => a.Persistent).Select(a => (a.Event, s.AggregatId)));

    private void Treibe(Queue<(string Name, PipelineFluss Fluss, IEvent Quelle, Guid Strom)> warte, Func<FlussAufruf, FlussAntwort> wahl,
        List<FlussSchritt> schritte, List<FlussVorgang> vorgänge)
    {
        while (warte.Count > 0 && schritte.Count < MaxSchritte)
        {
            var (name, fluss, quelle, strom) = warte.Dequeue();
            var neu = Vorgang(name, fluss, quelle, strom, wahl, schritte, vorgänge);
            foreach (var (e, s2) in neu) foreach (var s in StartetVon(e, s2)) warte.Enqueue(s);
        }
    }

    private sealed record Token(int Id, IEvent Payload, int Herkunft, JeTeile Teile) : IFlussToken;

    /// <summary>Ein Vorgang bis zum Fixpunkt. Liefert die Aggregat-Events seiner Commands (sie können weitere Flüsse starten).</summary>
    private List<(IEvent, Guid)> Vorgang(string name, PipelineFluss fluss, IEvent quelle, Guid strom, Func<FlussAufruf, FlussAntwort> wahl,
        List<FlussSchritt> schritte, List<FlussVorgang> vorgänge)
    {
        var regeln = fluss.Regeln;
        var nummer = ++_vorgänge;
        var quellKnoten = regeln.QuellKnoten ?? 0;
        // Wie im Dirigenten: die Quell-Nachricht und ihr Strom (zweiter Ausgang der Quelle, quelle.Strom()).
        var tokens = new List<Token> { new(0, quelle, quellKnoten, JeTeile.Leer), new(1, new QuellStrom(strom, 0), quellKnoten, JeTeile.Leer) };
        schritte.Add(new FlussSchritt
        {
            Pipeline = name, Vorgang = nummer, Knoten = quellKnoten, Art = Art(fluss, quellKnoten),
            Typ = quelle.GetType(), Nachricht = quelle, Ausgang = FlussAusgang.Fall, Ergebnis = quelle,
        });

        var erledigt = new HashSet<string>();
        var aggregatEvents = new List<(IEvent, Guid)>();
        string? gescheitert = null;

        while (gescheitert is null && schritte.Count < MaxSchritte)
        {
            // Eine WELLE: alle Aufrufe, die auf dem aktuellen Stand bereit sind (der Dirigent gibt sie zugleich ab).
            var welle = new List<(Regel Regel, IReadOnlyList<Token> Belegung, int Aufruf, object Ziel)>();
            for (var ri = 0; ri < regeln.Regeln.Count; ri++)
            {
                var regel = regeln.Regeln[ri];
                foreach (var belegung in FlussBelegung.Belegungen(regel, tokens))
                {
                    var payloads = belegung.Select(t => t.Payload).ToList();
                    var ziele = regel.WartetAuf.Count > 0 ? new List<object> { payloads[0] }    // Warten: der Strom selbst
                        : regel.Sende is not null
                        ? regel.Sende(payloads).Cast<object>().ToList()
                        : regel.Ruft!(payloads).Cast<object>().ToList();
                    for (var ci = 0; ci < ziele.Count; ci++)
                    {
                        var schlüssel = $"{ri}|{string.Join(",", belegung.Select(t => t.Id))}|{ci}";
                        if (erledigt.Add(schlüssel)) welle.Add((regel, belegung, ci, ziele[ci]));
                    }
                }
            }
            if (welle.Count == 0) break;

            foreach (var (regel, belegung, ci, ziel) in welle)
            {
                var knoten = regel.Knoten ?? -1;
                var teile = FlussBelegung.TeileDesAufrufs(regel, belegung, ci);
                var ein = belegung.Select(t => new FlussEinDraht(t.Herkunft, t.Payload.GetType().Name, Port(t.Payload))).ToList();
                var basis = new FlussSchritt
                {
                    Pipeline = name, Vorgang = nummer, Knoten = knoten, Art = Art(fluss, knoten), Typ = ziel.GetType(),
                    Ein = ein, Nachricht = ziel, Element = regel.JeKnoten is null ? null : ci, Ausgang = FlussAusgang.Fall,
                };

                FlussSchritt schritt;
                if (regel.WartetAuf.Count > 0)
                {
                    // Warten: die Wahl sagt, ob eines der Events im Strom kam (Ergebnis) oder das Zeitlimit ablief (⏳).
                    var antwort = wahl(new FlussAufruf(name, nummer, knoten, ci, null, belegung.Select(t => t.Payload).ToList()) { WartetAuf = regel.WartetAuf });
                    schritt = antwort switch
                    {
                        FlussAntwort.Ergebnis r when regel.WartetAuf.Any(t => t.IsInstanceOfType(r.Event)) => basis with { Typ = typeof(QuellStrom), Ergebnis = r.Event },
                        FlussAntwort.Zeitlimit => Fehler(basis with { Typ = typeof(QuellStrom) }, regeln.UmleitenZeitlimit.Contains(knoten), FlussAusgang.Zeitlimit,
                            $"Zeitlimit ({regel.Zeitlimit}) beim Warten auf {string.Join("|", regel.WartetAuf.Select(t => t.Name))}"),
                        _ => throw new InvalidOperationException(
                            $"Knoten {knoten} wartet auf {string.Join("|", regel.WartetAuf.Select(t => t.Name))} — als Ausgang geht nur eines davon oder ⏳."),
                    };
                }
                else if (ziel is IAuftrag auftrag)
                {
                    var antwort = wahl(new FlussAufruf(name, nummer, knoten, ci, auftrag, belegung.Select(t => t.Payload).ToList()));
                    schritt = antwort switch
                    {
                        FlussAntwort.Ergebnis r => basis with { Ergebnis = r.Event },
                        FlussAntwort.Zeitlimit when regel.Zeitlimit is null =>
                            throw new InvalidOperationException($"Knoten {knoten} hat kein Zeitlimit — ⏳ ist dort kein Ausgang."),
                        FlussAntwort.Zeitlimit => Fehler(basis, regeln.UmleitenZeitlimit.Contains(knoten), FlussAusgang.Zeitlimit,
                            $"Zeitlimit ({regel.Zeitlimit}) für {auftrag.GetType().Name}"),
                        FlussAntwort.Abgelehnt a => Fehler(basis, regeln.UmleitenAbgelehnt.Contains(knoten), FlussAusgang.Abgelehnt, a.Grund),
                        _ => throw new NotSupportedException(antwort.GetType().Name),
                    };
                }
                else
                {
                    var cmd = (ICommand)ziel;
                    var kaskade = _lauf.Fahre(cmd);
                    var eigen = kaskade.Schritte.FirstOrDefault();
                    aggregatEvents.AddRange(Events(kaskade));
                    // Die Wirkung = das erste Domänen-Event des Commands; nur Ablehnungen (transient) = ✕; nichts = Noop.
                    var wirkung = eigen?.Ausgang.FirstOrDefault(a => a.Persistent)?.Event;
                    schritt = eigen is null || eigen.Unrouted
                        ? Fehler(basis, regeln.UmleitenAbgelehnt.Contains(knoten), FlussAusgang.Abgelehnt, $"{cmd.GetType().Name}: kein Aggregat entscheidet")
                        : wirkung is not null ? basis with { Ergebnis = wirkung }
                        : eigen.Ausgang.Count > 0
                            ? Fehler(basis, regeln.UmleitenAbgelehnt.Contains(knoten), FlussAusgang.Abgelehnt,
                                string.Join(", ", eigen.Ausgang.Select(a => a.Event.GetType().Name)))
                            : basis with { Ausgang = FlussAusgang.Noop };
                    schritt = schritt with { Kaskade = kaskade };
                }

                schritte.Add(schritt);
                if (schritt.Ausgang == FlussAusgang.Gescheitert) { gescheitert = schritt.Grund; break; }
                if (schritt.Ergebnis is not null) tokens.Add(new Token(tokens.Count, schritt.Ergebnis, knoten, teile));
            }
        }

        var gefeuert = schritte.Where(x => x.Pipeline == name && x.Vorgang == nummer).Select(x => x.Knoten).ToHashSet();
        var wartend = gescheitert is null ? Wartend(regeln, tokens, gefeuert) : new List<string>();
        vorgänge.Add(new FlussVorgang(name, nummer, gescheitert is null, gescheitert, wartend));
        return aggregatEvents;
    }

    /// <summary>Fehler an einem Port: verdrahtet → Token am ⏳/✕-Port, frei → der Vorgang scheitert.</summary>
    private static FlussSchritt Fehler(FlussSchritt basis, bool umleiten, string port, string grund)
        => umleiten
            ? basis with
            {
                Ausgang = port, Grund = grund,
                Ergebnis = port == FlussAusgang.Zeitlimit ? new ZeitlimitAbgelaufen(grund) : new SchrittAbgelehnt(grund),
            }
            : basis with { Ausgang = FlussAusgang.Gescheitert, Grund = $"{(port == FlussAusgang.Zeitlimit ? "⏳" : "✕")} {grund}" };

    private static string Port(IEvent e) => e switch
    {
        QuellStrom => FlussAusgang.Strom,
        ZeitlimitAbgelaufen => FlussAusgang.Zeitlimit,
        SchrittAbgelehnt => FlussAusgang.Abgelehnt,
        _ => FlussAusgang.Fall,
    };

    private static PipelineKnotenArt Art(PipelineFluss f, int knoten)
        => f.Knoten.FirstOrDefault(k => k.Index == knoten)?.Art ?? PipelineKnotenArt.Funktion;

    /// <summary>Eingänge, die nur zum Teil belegt sind (ein ∧ wartet auf einen Weg, der in diesem Vorgang nicht lieferte).</summary>
    private static List<string> Wartend(ProzessRegeln regeln, List<Token> tokens, HashSet<int> gefeuert)
    {
        var aus = new List<string>();
        foreach (var r in regeln.Regeln)
        {
            // Ein Knoten, der schon über einen anderen ∨-Weg lief, wartet nicht mehr.
            if (r.Knoten is int k && gefeuert.Contains(k)) continue;
            if (FlussBelegung.Belegungen(r, tokens).Any()) continue;
            var da = r.Bedingung.Select((t, i) => tokens.Any(tok => t.IsInstanceOfType(tok.Payload) &&
                (r.VonKnoten[i] is not int v || tok.Herkunft == v))).ToList();
            if (da.Count(x => x) == 0 || da.All(x => x)) continue;
            aus.Add($"Knoten {r.Knoten}: wartet auf " + string.Join(" ∧ ",
                r.Bedingung.Select((t, i) => (da[i] ? "✓" : "…") + t.Name)));
        }
        return aus;
    }
}
