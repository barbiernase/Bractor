using Abstractions;

namespace Infrastructure.Funktionen;

/// <summary>
/// Der Lauf einer Funktion über EINE Partition des Logs (Plan Pub/Sub-Neustrukturierung §2.2) — die reine Buchführung, ohne Ein-/Ausgabe: der
/// Funktions-Actor ruft sie nur aus seiner Mailbox, liest und arbeitet neben ihr.
/// <list type="bullet">
/// <item><b>Slots:</b> höchstens so viele Eingänge zugleich in Arbeit; neue holt der Actor erst, wenn einer frei ist — der Rückstau liegt im
///   Log, nicht im Speicher.</item>
/// <item><b>Je Stream nacheinander:</b> ein Eingang startet erst, wenn kein früherer desselben Streams mehr offen ist; verschiedene Streams
///   laufen parallel.</item>
/// <item><b>Position beim ältesten Unfertigen:</b> sie rückt nur bis vor den ersten Eingang, der noch offen ist oder noch nicht starten konnte,
///   und nie über die gesetzte Sequenz (Horizont der Vollständigkeit) bzw. das Ende einer vollen Seite. Was dahinter fertig ist, merkt sich der
///   Lauf (<see cref="Erledigt"/>) — gedeckelt wie im Batch-Leser: ist die Liste voll, startet nur noch, was bis zur gesetzten Sequenz reicht.</item>
/// <item><b>Parken:</b> ein Eingang, der auch nach den Wiederholungen scheitert, parkt seinen Stream (im Stand, durabel mit dem nächsten
///   Schreiben); dessen spätere Eingänge überspringt der Lauf, bis ein Einzelversuch des Streams durchläuft — die übrigen Streams laufen weiter.</item>
/// </list>
/// </summary>
public sealed class FunktionsLauf
{
    /// <summary>So viele fertige Eingänge jenseits der Position merkt sich ein Lauf höchstens (wie <c>BatchLeser.ErledigtHoechstens</c>).</summary>
    public const int ErledigtHoechstens = 5_000;

    private readonly int _slots;
    private readonly int _erledigtHoechstens;
    private readonly HashSet<long> _erledigt = new();
    private readonly SortedDictionary<long, PartitionsEvent> _inArbeit = new();
    private readonly Dictionary<Guid, long> _belegt = new();
    private readonly HashSet<Guid> _parkVersuch = new();
    private long _grenze;
    private long _geschrieben;

    public FunktionsLauf(int slots, int erledigtHoechstens = ErledigtHoechstens)
    {
        _slots = Math.Max(1, slots);
        _erledigtHoechstens = Math.Max(1, erledigtHoechstens);
    }

    /// <summary>Der geladene Stand (null = noch zu laden, etwa nach einem Konflikt mit einer zweiten Aktivierung).</summary>
    public PartitionsCheckpoint? Stand { get; private set; }

    /// <summary>Bis hierher ist jeder Eingang der Partition erledigt (die Lese-Position; durabel wird sie mit <see cref="ZuSchreiben"/>).</summary>
    public long Position { get; private set; }

    public int FreieSlots => _slots - _inArbeit.Count;
    public int InArbeit => _inArbeit.Count;
    public IReadOnlyCollection<long> Erledigt => _erledigt;

    /// <summary>Was das Lesen ausschließt: Erledigtes und Eingänge in Arbeit.</summary>
    public IReadOnlyCollection<long> Ausser => _erledigt.Concat(_inArbeit.Keys).ToList();

    public IReadOnlyList<GeparkterStream> Geparkt => Stand?.Geparkt ?? (IReadOnlyList<GeparkterStream>)Array.Empty<GeparkterStream>();

    /// <summary>Setzt den geladenen Stand (erster Start oder nach einem Konflikt): Position aus dem Stand, Erledigtes vergessen, Arbeit läuft weiter.</summary>
    public void Setze(PartitionsCheckpoint stand)
    {
        Stand = stand;
        Position = stand.Position;
        _geschrieben = stand.Position;
        _grenze = stand.Position;
        _erledigt.Clear();
    }

    /// <summary>Eine zweite Aktivierung hat den Stand geschrieben: beim nächsten Lesen neu laden (die laufende Arbeit ist wiederholungsfest).</summary>
    public void Verloren() => Stand = null;

    /// <summary>
    /// Nimmt eine Seite auf und liefert die Eingänge, die jetzt starten: je Stream der früheste offene, solange Slots frei sind; Eingänge geparkter
    /// Streams sind erledigt (übersprungen). Die übrigen bleiben im Log und halten die Position vor sich.
    /// </summary>
    public IReadOnlyList<PartitionsEvent> Nimm(PartitionsSeite seite)
    {
        if (Stand is null) throw new InvalidOperationException("Erst den Stand setzen.");
        var events = seite.Events
            .Where(e => e.Sequenz > Position && !_erledigt.Contains(e.Sequenz) && !_inArbeit.ContainsKey(e.Sequenz))
            .GroupBy(e => e.Sequenz).Select(g => g.OrderBy(e => e.Event.SubIndex).First())   // ein Eingang je Sequenz
            .OrderBy(e => e.Sequenz).ToList();
        var geparkt = Stand.Geparkt.Select(g => g.StreamId).ToHashSet();
        var voll = _erledigt.Count >= _erledigtHoechstens;

        var grenze = seite.Gesetzt;
        if (seite.Voll && events.Count > 0) grenze = Math.Min(grenze, events[^1].Sequenz);
        var start = new List<PartitionsEvent>();
        long? ersteWartende = null;
        var gesehen = new HashSet<Guid>();
        foreach (var e in events)
        {
            var stream = e.Event.AggregateId;
            if (geparkt.Contains(stream) && !_parkVersuch.Contains(stream))
            {
                _erledigt.Add(e.Sequenz);   // geparkt: der Einzelversuch des Streams liest ab seinem Stand
                continue;
            }
            var startbar = FreieSlots > start.Count && !_belegt.ContainsKey(stream) && gesehen.Add(stream)
                           && !(voll && e.Sequenz > seite.Gesetzt);
            if (startbar) start.Add(e);
            else
            {
                gesehen.Add(stream);
                ersteWartende ??= e.Sequenz;
            }
        }
        if (ersteWartende is { } w) grenze = Math.Min(grenze, w - 1);
        _grenze = Math.Max(Position, grenze);
        foreach (var e in start)
        {
            _inArbeit[e.Sequenz] = e;
            _belegt[e.Event.AggregateId] = e.Sequenz;
        }
        Rueckevor();
        return start;
    }

    /// <summary>Der Eingang an <paramref name="sequenz"/> ist fertig (gewirkt oder bewusst ohne Wirkung).</summary>
    public void Fertig(long sequenz)
    {
        if (!_inArbeit.Remove(sequenz, out var e)) return;
        if (_belegt.TryGetValue(e.Event.AggregateId, out var s) && s == sequenz) _belegt.Remove(e.Event.AggregateId);
        if (sequenz > Position) _erledigt.Add(sequenz);
        Rueckevor();
    }

    /// <summary>
    /// Der Eingang scheitert dauerhaft: sein Stream wird geparkt ab der Version vor ihm, der Eingang gilt als erledigt (der Einzelversuch des
    /// Streams holt ihn nach). Liefert den neuen Eintrag, oder null, wenn der Stream schon geparkt war.
    /// </summary>
    public GeparkterStream? Parke(long sequenz, string grund, DateTimeOffset jetzt)
    {
        if (!_inArbeit.TryGetValue(sequenz, out var e)) return null;
        GeparkterStream? neu = null;
        if (Stand!.Geparkt.All(g => g.StreamId != e.Event.AggregateId))
        {
            neu = new GeparkterStream { StreamId = e.Event.AggregateId, Version = e.Event.AggregateVersion - 1, Grund = grund, Seit = jetzt };
            Stand.Geparkt = Stand.Geparkt.Append(neu).ToList();
        }
        Fertig(sequenz);
        return neu;
    }

    /// <summary>Beginnt den Einzelversuch eines geparkten Streams: solange er läuft, starten seine Eingänge nicht (sie warten im Log).</summary>
    public bool BeginneParkVersuch(Guid stream)
    {
        if (_belegt.ContainsKey(stream) || !_parkVersuch.Add(stream)) return false;
        _belegt[stream] = long.MaxValue;
        return true;
    }

    /// <summary>Der Einzelversuch ist zu Ende; lief er durch, ist der Stream nicht mehr geparkt (seine weiteren Eingänge laufen wieder normal).</summary>
    public void EndeParkVersuch(Guid stream, bool durch)
    {
        _parkVersuch.Remove(stream);
        if (_belegt.TryGetValue(stream, out var s) && s == long.MaxValue) _belegt.Remove(stream);
        if (durch && Stand is not null) Stand.Geparkt = Stand.Geparkt.Where(g => g.StreamId != stream).ToList();
    }

    /// <summary>Hat sich der Stand seit dem letzten Schreiben geändert (Position oder Geparkt)? Dann der nächste Stand zum Schreiben.</summary>
    public PartitionsCheckpoint? ZuSchreiben(bool auchGeparkt)
    {
        if (Stand is null || (Position <= _geschrieben && !auchGeparkt)) return null;
        return new PartitionsCheckpoint
        {
            Id = Stand.Id, ProjektionsId = Stand.ProjektionsId, Partition = Stand.Partition, Position = Position,
            Version = Stand.Version, Geparkt = Stand.Geparkt.ToList(),
        };
    }

    /// <summary>Der Stand wurde geschrieben (neue Version).</summary>
    public void Geschrieben(PartitionsCheckpoint stand, int version)
    {
        if (Stand is null) return;
        Stand.Version = version;
        _geschrieben = Math.Max(_geschrieben, stand.Position);
    }

    /// <summary>Position bis vor den ältesten offenen Eingang, höchstens bis zur Grenze der letzten Seite; Erledigtes dahinter vergessen.</summary>
    private void Rueckevor()
    {
        var bis = _grenze;
        if (_inArbeit.Count > 0) bis = Math.Min(bis, _inArbeit.Keys.First() - 1);
        if (bis <= Position) return;
        Position = bis;
        _erledigt.RemoveWhere(s => s <= Position);
    }
}
