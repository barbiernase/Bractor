using Abstractions;

namespace Infrastructure.Funktionen;

// ═══════════════════════════════════════════════════════════════════════════════════════════════════════════════════════
// VERMITTLUNG (docs/konzept-editor-pipelines.md §14.5): je Katalog-Funktion EIN Vermittler, der die offenen Aufträge hält. Die
// Ausführer HOLEN sich Arbeit, sobald sie einen Slot frei haben (Pull) — C# im Host, Python-Worker über gRPC, ein GPU-Rechner.
// So verteilt sich die Last von selbst, auch über Rechner und Sprachen. Jeder geholte Auftrag hat eine Lease; läuft sie ab (der
// Ausführer ist weg), wird er neu ausgegeben. Das Ergebnis wird trotzdem genau einmal wirksam (StartStream im Ausführungs-Stream).
//
// Der Vermittler ist NICHT maßgeblich (Invariante 1): verliert man ihn, verliert man nichts — der Dirigent bietet jeden offenen
// Auftrag bei jeder Weckung erneut an (§3-Backstop alle 15 s), und der Vermittler baut sich daraus wieder auf.
// ═══════════════════════════════════════════════════════════════════════════════════════════════════════════════════════

/// <summary>Ein angebotener Auftrag: wer ihn will (Korrelation des Vorgangs), seine Ausführungs-Id (Vorgang) und der Inhalt.</summary>
public sealed record AuftragAnbieten(Guid Vorgang, Guid Korrelation, string? Akteur, IAuftrag Auftrag) : IWireMessage;

/// <summary>Ein Ausführer holt bis zu <paramref name="Max"/> Aufträge; ist keiner da, wartet die Anfrage bis zu <paramref name="WarteMs"/>.</summary>
public sealed record HoleArbeit(string Arbeiter, int Max, int WarteMs) : IWireMessage;

/// <summary>Die zugeteilten Aufträge (leer, wenn die Wartezeit ohne Arbeit verstrich).</summary>
public sealed record ArbeitZugeteilt(IReadOnlyList<AuftragAnbieten> Auftraege) : IWireMessage;

/// <summary>Lebenszeichen eines laufenden Auftrags — verlängert seine Lease (lange Läufe, z. B. Training).</summary>
public sealed record ArbeitLebt(Guid Vorgang, string Arbeiter) : IWireMessage;

/// <summary>Der Auftrag ist erledigt (sein Ergebnis liegt im Log) — der Vermittler vergisst ihn.</summary>
public sealed record ArbeitErledigt(Guid Vorgang) : IWireMessage;

/// <summary>
/// Der reine Kern eines Vermittlers (je Funktion): Warteschlange, Leases, Lebenszeichen, kürzlich Erledigtes. Ohne Uhr und
/// ohne Transport — die Zeit kommt als Argument; im Prüfstand vollständig beweisbar.
/// </summary>
public sealed class FunktionsVermittlung
{
    private readonly TimeSpan _lease;
    private readonly int _erinnerung;
    private readonly LinkedList<Guid> _warteschlange = new();
    private readonly Dictionary<Guid, AuftragAnbieten> _offen = new();
    private readonly Dictionary<Guid, (AuftragAnbieten Auftrag, string Arbeiter, DateTimeOffset Bis)> _vergeben = new();
    private readonly HashSet<Guid> _erledigt = new();
    private readonly Queue<Guid> _erledigtReihenfolge = new();

    /// <param name="lease">Wie lange ein geholter Auftrag einem Ausführer gehört, ohne Lebenszeichen.</param>
    /// <param name="erinnerung">Wie viele erledigte Aufträge der Vermittler sich merkt (gegen erneute Angebote aus alten Weckungen).</param>
    public FunktionsVermittlung(TimeSpan lease, int erinnerung = 10_000)
    {
        _lease = lease;
        _erinnerung = erinnerung;
    }

    public int Wartend => _offen.Count;
    public int Vergeben => _vergeben.Count;

    /// <summary>Nimmt ein Angebot auf. Bekannt (wartend, vergeben, gerade erledigt) → ignoriert. Liefert, ob es neu war.</summary>
    public bool Biete(AuftragAnbieten a)
    {
        if (_offen.ContainsKey(a.Vorgang) || _vergeben.ContainsKey(a.Vorgang) || _erledigt.Contains(a.Vorgang)) return false;
        _offen[a.Vorgang] = a;
        _warteschlange.AddLast(a.Vorgang);
        return true;
    }

    /// <summary>Teilt bis zu <paramref name="max"/> Aufträge zu (älteste zuerst); abgelaufene Leases kommen vorher zurück in die Schlange.</summary>
    public IReadOnlyList<AuftragAnbieten> Hole(string arbeiter, int max, DateTimeOffset jetzt)
    {
        GibAbgelaufeneZurück(jetzt);
        var los = new List<AuftragAnbieten>();
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
