using Abstractions;

namespace Infrastructure.Funktionen;

// ═══════════════════════════════════════════════════════════════════════════════════════════════════════════════════════
// VERMITTLUNG (Plan Pub/Sub-Neustrukturierung §2.2): je Funktion, die draußen rechnet (Laufort Extern oder Hand), EIN Vermittler, der die
// offenen Eingänge hält. Die Träger HOLEN sich Arbeit, sobald sie einen Slot frei haben (Pull) — ein Python-Worker über gRPC, ein Mensch an der
// Werkbank. So verteilt sich die Last von selbst, auch über Rechner und Sprachen. Jeder geholte Eingang hat eine Lease; läuft sie ab (der Träger
// ist weg), wird er neu ausgegeben. Die Antwort wirkt trotzdem genau einmal (festgehalten unter „Funktion F, Eingang X“, die erste gewinnt).
//
// Der Vermittler ist NICHT maßgeblich (Invariante 1): verliert man ihn, verliert man nichts — der Funktions-Actor bietet jeden offenen Eingang
// erneut an, und der Vermittler baut sich daraus wieder auf.
// ═══════════════════════════════════════════════════════════════════════════════════════════════════════════════════════

/// <summary>Was ein Vermittler verteilt: eine Arbeit mit Schlüssel (<see cref="Vorgang"/>), ihr Inhalt (Typname) und ihre Ursache — für den Einblick.</summary>
public interface IArbeit
{
    Guid Vorgang { get; }
    Guid Korrelation { get; }
    string Inhalt { get; }
    string? Ursache { get; }
}

/// <summary>
/// Ein Eingang einer Funktion in Baustein-Form, die draußen rechnet (Plan Pub/Sub-Neustrukturierung §2.2): Schlüssel „F, X“ (<see cref="Vorgang"/>,
/// der Stream des Festhaltens), Funktion, der Eingang selbst und seine Herkunft. Wer antwortet, hält die Ausgabe unter dem Schlüssel fest — die
/// erste Antwort gewinnt; gesendet wird beim Funktions-Actor.
/// </summary>
public sealed record EingangAnbieten(Guid Vorgang, string Funktion, IEvent Eingang, Guid Stream, int Version, DateTimeOffset Zeit,
    string? KorrelationsText, string? Akteur, string? Ursache = null, string? Wirker = null) : IWireMessage, IArbeit
{
    [System.Text.Json.Serialization.JsonIgnore]
    public Guid Korrelation => Guid.TryParse(KorrelationsText, out var k) ? k : Stream;
    string IArbeit.Inhalt => Eingang.GetType().Name;
}

/// <summary>Ein Ausführer holt bis zu <paramref name="Max"/> Eingänge (Baustein-Form); ist keiner da, wartet die Anfrage bis zu <paramref name="WarteMs"/>.</summary>
public sealed record HoleEingaenge(string Arbeiter, int Max, int WarteMs) : IWireMessage;

/// <summary>Die zugeteilten Eingänge (leer, wenn die Wartezeit ohne Arbeit verstrich).</summary>
public sealed record EingaengeZugeteilt(IReadOnlyList<EingangAnbieten> Eingaenge) : IWireMessage;

/// <summary>
/// An den Funktions-Actor: die Antwort auf den Eingang mit diesem Schlüssel liegt vor (festgehalten), oder draußen ist er gescheitert
/// (<paramref name="Fehler"/>); mit <paramref name="Teil"/> nur ein Stück eines Stroms — es kommt noch mehr. Verlierbar — der Actor sieht nach einer
/// Geduld ohnehin nach.
/// </summary>
public sealed record EingangBeantwortet(Guid Schluessel, string? Fehler = null, bool Teil = false) : IWireMessage;

/// <summary>Lebenszeichen eines laufenden Eingangs — verlängert seine Lease (lange Läufe, z. B. Training).</summary>
public sealed record ArbeitLebt(Guid Vorgang, string Arbeiter) : IWireMessage;

/// <summary>Der Eingang ist erledigt (seine Antwort liegt im Log) — der Vermittler vergisst ihn.</summary>
public sealed record ArbeitErledigt(Guid Vorgang) : IWireMessage;

/// <summary>Einblick (Werkbank P4.7): welche Eingänge hält der Vermittler gerade — wartend oder an einen Träger vergeben?</summary>
public sealed record ListeOffene : IWireMessage;

/// <summary>Ein offener Eingang: wartend (<paramref name="Arbeiter"/> null) oder vergeben bis <paramref name="LeaseBis"/>.</summary>
public sealed record OffenerAuftrag(Guid Vorgang, Guid Korrelation, string Auftrag, string? Ursache, string? Arbeiter, DateTimeOffset? LeaseBis);

/// <summary>Die Antwort auf <see cref="ListeOffene"/>.</summary>
public sealed record OffeneAuftraege(IReadOnlyList<OffenerAuftrag> Auftraege) : IWireMessage;

/// <summary>
/// Der reine Kern eines Vermittlers (je Funktion): Warteschlange, Leases, Lebenszeichen, kürzlich Erledigtes. Ohne Uhr und
/// ohne Transport — die Zeit kommt als Argument; im Prüfstand vollständig beweisbar (<see cref="EingangAnbieten"/>).
/// </summary>
public class Vermittlung<TArbeit> where TArbeit : class, IArbeit
{
    private readonly TimeSpan _lease;
    private readonly int _erinnerung;
    private readonly LinkedList<Guid> _warteschlange = new();
    private readonly Dictionary<Guid, TArbeit> _offen = new();
    private readonly Dictionary<Guid, (TArbeit Auftrag, string Arbeiter, DateTimeOffset Bis)> _vergeben = new();
    private readonly HashSet<Guid> _erledigt = new();
    private readonly Queue<Guid> _erledigtReihenfolge = new();

    /// <param name="lease">Wie lange ein geholter Auftrag einem Ausführer gehört, ohne Lebenszeichen.</param>
    /// <param name="erinnerung">Wie viele erledigte Aufträge der Vermittler sich merkt (gegen erneute Angebote aus alten Weckungen).</param>
    public Vermittlung(TimeSpan lease, int erinnerung = 10_000)
    {
        _lease = lease;
        _erinnerung = erinnerung;
    }

    public int Wartend => _offen.Count;
    public int Vergeben => _vergeben.Count;

    /// <summary>Nimmt ein Angebot auf. Bekannt (wartend, vergeben, gerade erledigt) → ignoriert. Liefert, ob es neu war.</summary>
    public bool Biete(TArbeit a)
    {
        if (_offen.ContainsKey(a.Vorgang) || _vergeben.ContainsKey(a.Vorgang) || _erledigt.Contains(a.Vorgang)) return false;
        _offen[a.Vorgang] = a;
        _warteschlange.AddLast(a.Vorgang);
        return true;
    }

    /// <summary>Teilt bis zu <paramref name="max"/> Aufträge zu (älteste zuerst); abgelaufene Leases kommen vorher zurück in die Schlange.</summary>
    public IReadOnlyList<TArbeit> Hole(string arbeiter, int max, DateTimeOffset jetzt)
    {
        GibAbgelaufeneZurück(jetzt);
        var los = new List<TArbeit>();
        while (los.Count < max && _warteschlange.First is { } knoten)
        {
            _warteschlange.RemoveFirst();
            if (!_offen.Remove(knoten.Value, out var a)) continue;
            _vergeben[a.Vorgang] = (a, arbeiter, jetzt + _lease);
            los.Add(a);
        }
        return los;
    }

    /// <summary>Verlängert die Lease eines laufenden Auftrags (nur für den Ausführer, dem er gehört).</summary>
    public void Lebenszeichen(Guid vorgang, string arbeiter, DateTimeOffset jetzt)
    {
        if (_vergeben.TryGetValue(vorgang, out var v) && v.Arbeiter == arbeiter)
            _vergeben[vorgang] = (v.Auftrag, arbeiter, jetzt + _lease);
    }

    /// <summary>Der Auftrag ist erledigt — vergessen und kurz merken (ein späteres Angebot derselben Ausführung verpufft).</summary>
    public void Erledigt(Guid vorgang)
    {
        _vergeben.Remove(vorgang);
        _offen.Remove(vorgang);
        if (_erledigt.Add(vorgang))
        {
            _erledigtReihenfolge.Enqueue(vorgang);
            while (_erledigtReihenfolge.Count > _erinnerung) _erledigt.Remove(_erledigtReihenfolge.Dequeue());
        }
    }

    /// <summary>Die offenen Aufträge: zuerst die vergebenen, dann die wartenden in Schlangen-Reihenfolge.</summary>
    public IReadOnlyList<OffenerAuftrag> Offen()
        => _vergeben.Values.Select(v => new OffenerAuftrag(v.Auftrag.Vorgang, v.Auftrag.Korrelation, v.Auftrag.Inhalt,
                                                           v.Auftrag.Ursache, v.Arbeiter, v.Bis))
            .Concat(_warteschlange.Where(_offen.ContainsKey).Select(g => _offen[g])
                .Select(a => new OffenerAuftrag(a.Vorgang, a.Korrelation, a.Inhalt, a.Ursache, null, null)))
            .ToList();

    /// <summary>Abgelaufene Leases zurück an den ANFANG der Schlange (sie warten am längsten).</summary>
    public void GibAbgelaufeneZurück(DateTimeOffset jetzt)
    {
        foreach (var (vorgang, v) in _vergeben.Where(kv => kv.Value.Bis <= jetzt).ToList())
        {
            _vergeben.Remove(vorgang);
            _offen[vorgang] = v.Auftrag;
            _warteschlange.AddFirst(vorgang);
        }
    }
}
