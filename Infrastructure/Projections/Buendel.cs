using Abstractions;
using Infrastructure.Komposition;

namespace Infrastructure.Projections;

/// <summary>Woher ein Event eines Bündels seinen Schlüssel nimmt (docs/04 §4.3.1): aus seinem Stream, aus einem Feld <c>Id&lt;T&gt;</c> oder keinen (global).</summary>
public enum Schluesselart { Stream, Feld, Global }

/// <summary>
/// Ein Event eines Bündels, wie der Generator es beschreibt (<c>GeneratedProjektionen.&lt;P&gt;_Buendel</c>): sein Typ, woher sein Schlüssel kommt
/// und — bei einem Feld — dessen Name und der Zugriff darauf (ohne Reflection).
/// </summary>
public sealed record Buendelglied(Type Typ, Schluesselart Art, string? Feld = null, Func<IEvent, Guid>? Schluessel = null);

/// <summary>Ein Bündel einer Projektion: seine Stelle im generierten Dispatch und seine Events in der Reihenfolge der Typargumente.</summary>
public sealed record Buendelart(int Nummer, IReadOnlyList<Buendelglied> Glieder);

/// <summary>
/// Ein Eintrag des Verzeichnisses der Schlüssel, wie der Append ihn schreibt (<c>GeneratedProjektionen.Rueckblick</c>, über die Anwendung): das Feld
/// (leer = global) und der Zugriff auf den Schlüssel.
/// </summary>
public sealed record Rueckblickeintrag(string Feld, Func<IEvent, Guid> Schluessel);

/// <summary>
/// Der BLICK EINES BÜNDELS ins Log (docs/04 §4.3.1) — die Laufzeit hinter <c>Auf(Buendel&lt;A, B, …&gt; b, …)</c>, ohne Zustand: zu jedem Event eines
/// Bündel-Typs an der Sequenz <c>s</c> fragt er das Log nach dem neuesten Event jedes übrigen Typs zum selben Schlüssel vor <c>s</c>. Stehen alle,
/// ist das Bündel vollständig und das <c>Auf</c> wird gerufen. Weil die Antwort nur am Log bis <c>s</c> hängt, ist sie gleich bei jedem Neuaufbau,
/// jeder Partitionierung und jeder Reihenfolge der Ankunft — vorausgesetzt, jede Sequenz vor <c>s</c> ist endgültig: der Leser wertet ein Bündel
/// darum nur bis zur gesetzten Sequenz aus (<see cref="BatchLeser"/>). Gefragt wird je Seite in EINER Runde (<see cref="LadeAsync"/>); der
/// generierte Dispatch holt das Ergebnis je Event (<see cref="Gebuendelt"/>).
/// </summary>
public sealed class Buendelblick
{
    private readonly IEventStoreRepository _store;
    private readonly IReadOnlyList<Buendelart> _arten;
    private readonly Dictionary<Type, List<(Buendelart Art, int Stelle)>> _stellen = new();
    private readonly Dictionary<Type, (string Typ, string[] Typen)> _diskriminatoren = new();
    private readonly Dictionary<(int Art, Guid Stream, int Version, int Sub), IEvent[]?> _treffer = new();
    private bool _nachgetragen;

    public Buendelblick(IEventStoreRepository store, IReadOnlyList<Buendelart> arten)
    {
        _store = store;
        _arten = arten;
        foreach (var art in arten)
            for (var i = 0; i < art.Glieder.Count; i++)
            {
                var typ = art.Glieder[i].Typ;
                if (!_stellen.TryGetValue(typ, out var liste)) _stellen[typ] = liste = new();
                liste.Add((art, i));
            }
    }

    /// <summary>Die Bündel dieser Projektion.</summary>
    public IReadOnlyList<Buendelart> Arten => _arten;

    /// <summary>Ist dieses Event Teil eines Bündels (dann wird es erst bis zur gesetzten Sequenz ausgewertet)?</summary>
    public bool Hoert(IEvent e) => _stellen.ContainsKey(e.GetType());

    /// <summary>
    /// Fragt das Log für alle Bündel-Events einer Seite in einer Runde: je Event und Bündel, das es enthält, nach dem neuesten Event jedes übrigen
    /// Typs zum Schlüssel vor seiner Sequenz. Gleiche Fragen gehen einmal hinaus.
    /// </summary>
    public async Task LadeAsync(IEnumerable<(long Sequenz, EventEnvelope Event)> events, CancellationToken ct)
    {
        _treffer.Clear();
        if (!_nachgetragen)
        {
            var felder = _arten.SelectMany(a => a.Glieder).Where(g => g.Art != Schluesselart.Stream)
                .Select(g => (Diskriminatoren(g.Typ), Feld: g.Feld ?? ""))
                .Distinct()
                .Select(x => new Rueckblickfeld(x.Item1.Typ, x.Item1.Typen, x.Feld)).ToList();
            if (felder.Count > 0) await _store.SichereRueckblickAsync(felder, ct);
            _nachgetragen = true;
        }

        var fragen = new List<Rueckblick>();
        var nummer = new Dictionary<Rueckblick, int>(new RueckblickGleich());
        var plan = new List<((int, Guid, int, int) Schluessel, Buendelart Art, int Stelle, int[] Fragen, IEvent Payload)>();
        foreach (var (sequenz, e) in events)
        {
            if (!_stellen.TryGetValue(e.Payload.GetType(), out var stellen)) continue;
            foreach (var (art, stelle) in stellen)
            {
                var eigenes = art.Glieder[stelle];
                var schluessel = eigenes.Art switch
                {
                    Schluesselart.Stream => e.AggregateId,
                    Schluesselart.Feld => eigenes.Schluessel!(e.Payload),
                    _ => Guid.Empty,
                };
                var idx = new int[art.Glieder.Count];
                for (var j = 0; j < art.Glieder.Count; j++)
                {
                    if (j == stelle) { idx[j] = -1; continue; }
                    var f = Frage(art.Glieder[j], schluessel, sequenz);
                    if (!nummer.TryGetValue(f, out var n)) { n = fragen.Count; fragen.Add(f); nummer[f] = n; }
                    idx[j] = n;
                }
                plan.Add(((art.Nummer, e.AggregateId, e.AggregateVersion, e.SubIndex), art, stelle, idx, e.Payload));
            }
        }
        if (plan.Count == 0) return;

        var antworten = await _store.RueckblickAsync(fragen, ct);
        foreach (var (schluessel, art, stelle, idx, payload) in plan)
        {
            var glieder = new IEvent[art.Glieder.Count];
            var voll = true;
            for (var j = 0; j < glieder.Length && voll; j++)
            {
                if (j == stelle) { glieder[j] = payload; continue; }
                var a = antworten[idx[j]];
                if (a is null || a.Payload.GetType() != art.Glieder[j].Typ) voll = false;
                else glieder[j] = a.Payload;
            }
            _treffer[schluessel] = voll ? glieder : null;
        }
    }

    /// <summary>Das vollständige Bündel <paramref name="art"/> zu diesem Event (seine Events in Reihenfolge der Typargumente), sonst null.</summary>
    public IEvent[]? Gebuendelt(int art, EventEnvelope e) =>
        _treffer.TryGetValue((art, e.AggregateId, e.AggregateVersion, e.SubIndex), out var g) ? g : null;

    private Rueckblick Frage(Buendelglied g, Guid schluessel, long vor)
    {
        var (typ, typen) = Diskriminatoren(g.Typ);
        return g.Art switch
        {
            Schluesselart.Stream => new Rueckblick(typ, typen, schluessel, "", schluessel, vor),
            Schluesselart.Feld => new Rueckblick(typ, typen, null, g.Feld!, schluessel, vor),
            _ => new Rueckblick(typ, typen, null, "", Guid.Empty, vor),
        };
    }

    /// <summary>Der aktuelle Speicher-Diskriminator eines Event-Typs und alle, die beim Lesen zu ihm werden (Upcasting).</summary>
    private (string Typ, string[] Typen) Diskriminatoren(Type t)
    {
        if (_diskriminatoren.TryGetValue(t, out var d)) return d;
        var typ = Anwendung.Upcasting.Registrierungen.First(r => r.Typ == t).Diskriminator;
        var typen = Anwendung.Upcasting.Herkunft.TryGetValue(t, out var h) ? h : new[] { typ };
        return _diskriminatoren[t] = (typ, typen);
    }

    /// <summary>Zwei Fragen sind gleich bei gleichem Typ, Stream, Feld, Schlüssel und Sequenz.</summary>
    private sealed class RueckblickGleich : IEqualityComparer<Rueckblick>
    {
        public bool Equals(Rueckblick? a, Rueckblick? b) =>
            a is not null && b is not null && a.Typ == b.Typ && a.Stream == b.Stream && a.Feld == b.Feld && a.Schluessel == b.Schluessel && a.Vor == b.Vor;

        public int GetHashCode(Rueckblick r) => HashCode.Combine(r.Typ, r.Stream, r.Feld, r.Schluessel, r.Vor);
    }
}
