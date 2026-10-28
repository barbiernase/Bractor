using Abstractions;
using Core;
using Infrastructure.Komposition;
using Microsoft.Extensions.Logging;

namespace Infrastructure.Projections;

/// <summary>
/// Der Batch-Konsum einer replaybaren Projektion für EINE Partition (docs/04 §4.1) — die store-agnostische Schleife ohne Proto-Mantel
/// (<see cref="BatchLeserActor"/>). Sie besitzt die <b>Policy</b>, die Transaktion besitzt der Store (<see cref="IProjectionTracker"/>):
/// <list type="number">
///   <item><b>Stand laden</b>: Position, Version, geparkte Streams der Partition.</item>
///   <item><b>Seite lesen</b> (<see cref="IEventStoreRepository.ReadPartitionAsync"/>): Events jenseits der Position, nur die Partition, nur die
///     abonnierten Typen samt Upcasting-Vorgängern, ohne schon Erledigtes, höchstens <see cref="Konsum.BatchGroesse"/>.</item>
///   <item><b>Guard je Stream</b> in einer Abfrage; Events mit Version ≤ Guard überspringen (Nachzügler-Wiederholung).</item>
///   <item><b>Anwenden</b> je Stream in Versionsreihenfolge über den generierten Dispatch; vor jedem Event setzt der Leser den Stream
///     (<see cref="IProjectionTracker.Stream"/>) für die Schreib-Regel je Dokument.</item>
///   <item><b>Commit</b>: Effekte, Guards und Partitions-Checkpoint mit erwarteter Version in einer Transaktion (G-4).</item>
///   <item><b>Danach</b> der Versions-Index best-effort — nie über Uncommittetes.</item>
///   <item><b>Weiter</b>, solange die Seite voll war.</item>
/// </list>
/// <b>Position und Nachzügler:</b> die Position rückt nur bis zur gesetzten Sequenz vor (bis dorthin ist laut Store jede endgültig) und nie über ein Event, das
/// hängt. Jüngere Events werden trotzdem angewandt (Latenz); welche schon erledigt sind, merkt sich die Aktivierung (<c>_erledigt</c>), damit
/// der nächste Durchlauf sie nicht erneut liest — nach einer Neuaktivierung fängt der Guard sie ab. Gelesen wird ab der Lese-Position im
/// Speicher (<c>_lese</c>); durabel wird sie mit jedem Commit eines Batches, über fremde Events hinweg (leere Seite) aber höchstens einmal je
/// Poll-Intervall — ein veralteter durabler Stand kostet nach einer Neuaktivierung nur ein erneutes Lesen, das der Guard abfängt.
/// <b>Poison und Konflikt halten niemanden auf:</b> wirft der Dispatch, wird der Stream des werfenden Events abgetrennt und allein versucht;
/// scheitert ein Commit, wird der Batch nach Streams halbiert. Ein Stream, der allein wirft, wird <b>geparkt</b> (im Checkpoint, Dead-Letter
/// einmal), seine Events überspringt der Leser, und je Poll-Intervall versucht er ihn einzeln — läuft er durch, ist er nicht mehr geparkt. Ein
/// Konflikt an einem geteilten Dokument wird einzeln bis zu fünfmal gestreut wiederholt, dann bleibt der Stream bis zum nächsten Durchlauf
/// stehen (die Position hält vor ihm).
/// </summary>
public sealed class BatchLeser
{
    private const int KonfliktVersuche = 5;

    /// <summary>
    /// So viele erledigte Sequenzen jenseits der Lese-Position merkt sich eine Aktivierung höchstens. Steht die Position — der Horizont steht
    /// (eine lange offene Transaktion irgendwo im selben Postgres, etwa eine Sicherung) oder ein Stream hängt —, wüchse die Liste sonst mit jedem
    /// Event und ginge mit jeder Seite an die Datenbank, die alle Erledigten erneut übergeht: die Kosten wüchsen quadratisch mit der Dauer. Ist
    /// sie voll, wendet der Leser nur noch an, was bis zur gesetzten Sequenz reicht (das Übrige wartet, bis die Position nachrückt), und parkt
    /// einen hängenden Stream, statt hinter ihm weiter zu sammeln.
    /// </summary>
    public const int ErledigtHoechstens = 5_000;

    private readonly IEventStoreRepository _eventStore;
    private readonly IProjectionTracker _tracker;
    private readonly string _projektionsId;
    private readonly int _partition;
    private readonly Konsum _konsum;
    private readonly IReadOnlyCollection<string>? _typen;
    private readonly Func<EventEnvelope, ProjectionWriter, Task> _dispatch;
    private readonly IDeadLetterSink? _deadLetter;
    private readonly ILogger? _logger;
    private readonly Func<Task>? _absturzVorCommit;
    private readonly Func<DateTimeOffset> _jetzt;

    private PartitionsCheckpoint? _stand;
    /// <summary>Die Lese-Position dieser Aktivierung (≥ der durablen): bis hierher ist jedes Event der Partition erledigt.</summary>
    private long _lese;
    /// <summary>Sequenzen jenseits der Lese-Position, die diese Aktivierung schon erledigt hat (angewandt, übersprungen, geparkt).</summary>
    private readonly HashSet<long> _erledigt = new();
    private DateTimeOffset _letzterCommit = DateTimeOffset.MinValue;
    private DateTimeOffset _naechsterParkVersuch = DateTimeOffset.MinValue;
    private DateTimeOffset _letzteVollWarnung = DateTimeOffset.MinValue;

    /// <param name="typen">Speicher-Diskriminatoren der abonnierten Typen samt Upcasting-Vorgängern (<see cref="TypenAus"/>); null = ohne Typfilter.</param>
    /// <param name="absturzVorCommit">Prüf-Naht: läuft nach dem Dispatch eines Batches, vor seinem Commit (Absturz mitten im Batch, zweite Aktivierung).</param>
    public BatchLeser(
        IEventStoreRepository eventStore,
        IProjectionTracker tracker,
        string projektionsId,
        int partition,
        Konsum konsum,
        IReadOnlyCollection<string>? typen,
        Func<EventEnvelope, ProjectionWriter, Task> dispatch,
        IDeadLetterSink? deadLetter = null,
        ILogger? logger = null,
        Func<Task>? absturzVorCommit = null,
        Func<DateTimeOffset>? jetzt = null)
    {
        if (partition < 0 || partition >= konsum.Partitionen)
            throw new ArgumentOutOfRangeException(nameof(partition), $"{projektionsId}: Partition {partition} außerhalb 0..{konsum.Partitionen - 1}.");
        _eventStore = eventStore;
        _tracker = tracker;
        _projektionsId = projektionsId;
        _partition = partition;
        _konsum = konsum;
        _typen = typen;
        _dispatch = dispatch;
        _deadLetter = deadLetter;
        _logger = logger;
        _absturzVorCommit = absturzVorCommit;
        _jetzt = jetzt ?? (() => DateTimeOffset.UtcNow);
    }

    /// <summary>Die durable Position der Partition, wie diese Aktivierung sie kennt (0 vor dem ersten Durchlauf).</summary>
    public long Position => _stand?.Position ?? 0;

    /// <summary>Die geparkten Streams, wie diese Aktivierung sie kennt.</summary>
    public IReadOnlyList<GeparkterStream> Geparkt => _stand?.Geparkt ?? (IReadOnlyList<GeparkterStream>)Array.Empty<GeparkterStream>();

    /// <summary>
    /// Die Speicher-Diskriminatoren der abonnierten Typen samt ihrer Upcasting-Vorgänger (generierte Tabelle). Kennt die Tabelle einen Typ
    /// nicht (etwa ein Interface als Handle-Parameter), gibt es keine verlässliche Liste — dann ohne Typfilter (richtig, nur langsamer).
    /// </summary>
    public static IReadOnlyCollection<string>? TypenAus(IEnumerable<Type> abonniert, IReadOnlyDictionary<Type, string[]> herkunft)
    {
        var typen = new SortedSet<string>(StringComparer.Ordinal);
        foreach (var t in abonniert)
        {
            if (!herkunft.TryGetValue(t, out var diskriminatoren)) return null;
            typen.UnionWith(diskriminatoren);
        }
        return typen;
    }

    /// <inheritdoc cref="TypenAus(IEnumerable{Type}, IReadOnlyDictionary{Type, string[]})"/>
    public static IReadOnlyCollection<string>? TypenAus(IEnumerable<Type> abonniert) => TypenAus(abonniert, Anwendung.Upcasting.Herkunft);

    /// <summary>
    /// Ein Durchlauf: Seiten lesen und anwenden, bis eine nicht voll war. Hat eine zweite Aktivierung oder ein Neuaufbau die Partition
    /// dazwischen geschrieben (<see cref="PartitionsKonfliktException"/>), lädt er den Stand neu und liest von dort — nichts aus dem verlorenen
    /// Batch ist durabel. Wirft sonst etwas (Datenbank weg), gilt beim nächsten Durchlauf wieder der durable Stand. Liefert die Zahl der
    /// angewandten Events.
    /// </summary>
    public async Task<int> LaufAsync(CancellationToken ct = default)
    {
        for (var versuch = 1; ; versuch++)
        {
            try
            {
                return await LaufEinmalAsync(ct);
            }
            catch (PartitionsKonfliktException ex) when (versuch < KonfliktVersuche)
            {
                _logger?.LogInformation("[Konsum] {Projektion}/{Partition}: Partition dazwischen geschrieben ({Grund}) — Stand neu laden",
                    _projektionsId, _partition, ex.Message);
                _stand = null;
                _erledigt.Clear();
                await Task.Delay(Random.Shared.Next(5, 25 * versuch), ct);
            }
            catch
            {
                _stand = null;          // der nächste Durchlauf beginnt beim durablen Stand (Position, Version, Geparkt)
                throw;
            }
        }
    }

    private async Task<int> LaufEinmalAsync(CancellationToken ct)
    {
        if (_stand is null)
        {
            _stand = await _tracker.LadePartitionAsync(_projektionsId, _partition, ct);
            _lese = _stand.Position;
            _erledigt.Clear();
        }
        var angewandt = 0;

        if (_stand.Geparkt.Count > 0 && _jetzt() >= _naechsterParkVersuch)
        {
            _naechsterParkVersuch = _jetzt() + _konsum.Takt;
            angewandt += await GeparkteVersuchenAsync(ct);
        }

        while (true)
        {
            var seite = await _eventStore.ReadPartitionAsync(
                _lese, _partition, _konsum.Partitionen, _typen, _erledigt, _konsum.BatchGroesse, ct);
            var events = seite.Events
                .Where(e => e.Sequenz > _lese && !_erledigt.Contains(e.Sequenz))
                .DistinctBy(e => (e.Sequenz, e.Event.SubIndex))
                .OrderBy(e => e.Sequenz).ThenBy(e => e.Event.SubIndex)
                .ToList();

            if (events.Count == 0)
            {
                // Nichts Unerledigtes in der Partition: die Lese-Position läuft bis zur gesetzten Sequenz über fremde Events hinweg; durabel
                //   höchstens einmal je Poll-Intervall (sonst committete jede Weckung unter Last nur eine Zahl).
                Rueckevor(seite.Gesetzt);
                if (_lese > _stand.Position && _jetzt() - _letzterCommit >= _konsum.Takt)
                {
                    _tracker.BeginneBatch();
                    await CommitAsync(Neu(_lese), new Dictionary<Guid, int>(), ct);
                }
                return angewandt;
            }

            // Die Liste der Erledigten ist voll: nur noch anwenden, was bis zur gesetzten Sequenz reicht — dorthin darf die Position, dann
            //   schrumpft die Liste. Reicht nichts so weit, wartet die Partition, bis der Horizont nachrückt (eine Seite je Weckung, gedeckelt).
            if (_erledigt.Count >= ErledigtHoechstens)
            {
                var bisGesetzt = events.Where(e => e.Sequenz <= seite.Gesetzt).ToList();
                if (bisGesetzt.Count == 0)
                {
                    Rueckevor(seite.Gesetzt);
                    if (_erledigt.Count < ErledigtHoechstens) continue;
                    if (_jetzt() - _letzteVollWarnung >= _konsum.Takt)
                    {
                        _letzteVollWarnung = _jetzt();
                        _logger?.LogWarning("[Konsum] {Projektion}/{Partition}: {N} Events jenseits der gesetzten Sequenz {Gesetzt} angewandt — die Partition "
                            + "wartet, bis der Horizont nachrückt (eine lange offene Transaktion?)", _projektionsId, _partition, _erledigt.Count, seite.Gesetzt);
                    }
                    return angewandt;
                }
                // Reichte die volle Seite nicht bis zur gesetzten Sequenz, darf die Position nur bis zu ihrem Ende.
                var bis = seite.Voll && bisGesetzt.Count == events.Count ? Math.Min(seite.Gesetzt, events[^1].Sequenz) : seite.Gesetzt;
                angewandt += await VerarbeiteSeiteAsync(bisGesetzt, bis, ct);
                continue;
            }

            // Bis wohin die Position nach dieser Seite darf: nie über die gesetzte Sequenz, bei voller Seite nie über ihr Ende.
            var grenze = seite.Gesetzt;
            if (seite.Voll) grenze = Math.Min(grenze, events[^1].Sequenz);

            angewandt += await VerarbeiteSeiteAsync(events, grenze, ct);
            if (!seite.Voll) return angewandt;
        }
    }

    // ─────────────────────────────────────────────────────────────
    // Eine Seite: schneller Weg (alles in einem Commit), sonst trennen
    // ─────────────────────────────────────────────────────────────

    /// <summary>Ausgang eines Versuchs: angewandt, Konflikt an einem geteilten Dokument, oder ein Fehler (mit dem Stream des werfenden Events).</summary>
    private sealed record Versuch(bool Ok, int Angewandt = 0, bool Konflikt = false, Exception? Fehler = null, Guid? Verdacht = null);

    private async Task<int> VerarbeiteSeiteAsync(List<PartitionsEvent> events, long grenze, CancellationToken ct)
    {
        var streams = events.Select(e => e.Event.AggregateId).Distinct().ToList();
        var guards = new Dictionary<Guid, int>(await _tracker.LadeGuardsAsync(_projektionsId, streams, ct));
        var geparkt = _stand!.Geparkt.Select(g => g.StreamId).ToHashSet();

        // Schneller Weg: die ganze Seite in EINEM Commit samt neuer Position.
        var ganz = await VersucheAsync(events, streams, guards, geparkt, Math.Max(_lese, grenze), ct);
        if (ganz.Ok) return ganz.Angewandt;

        // Getrennt: jede Teilmenge committet für sich (die Position bleibt stehen), bis jeder Stream angewandt, geparkt oder hängend ist.
        var angewandt = 0;
        var haengend = new HashSet<Guid>();
        var offen = new Queue<List<Guid>>();

        async Task FehlschlagAsync(List<Guid> teil, Versuch r)
        {
            if (r.Konflikt)
            {
                if (teil.Count > 1) { Halbiere(teil, offen); return; }
                if (await EinzelnBeiKonfliktAsync(events, teil[0], guards, geparkt, ct) is { } n) angewandt += n;
                else haengend.Add(teil[0]);
                return;
            }
            // Poison: allein → parken; mit Verdacht (der Stream des werfenden Events) → der Rest ohne ihn, er allein; sonst halbieren.
            if (teil.Count == 1) { Parke(teil[0], guards, r.Fehler!, geparkt); return; }
            if (r.Verdacht is { } v && teil.Contains(v))
            {
                offen.Enqueue(teil.Where(s => s != v).ToList());
                offen.Enqueue(new List<Guid> { v });
            }
            else Halbiere(teil, offen);
        }

        await FehlschlagAsync(streams, ganz);
        while (offen.Count > 0)
        {
            var teil = offen.Dequeue();
            var r = await VersucheAsync(events, teil, guards, geparkt, _lese, ct);
            if (r.Ok) angewandt += r.Angewandt;
            else await FehlschlagAsync(teil, r);
        }

        // Hängt ein Stream, während die Liste der Erledigten voll ist, wird er geparkt (je Poll-Intervall einzeln versucht wie ein Poison-Stream),
        //   statt die Position weiter aufzuhalten und hinter ihm zu sammeln.
        if (haengend.Count > 0 && _erledigt.Count + events.Count >= ErledigtHoechstens)
        {
            foreach (var h in haengend)
                Parke(h, guards, new InvalidOperationException(
                    $"hängt an Konflikten am geteilten Dokument, während {ErledigtHoechstens} spätere Events warten"), geparkt);
            haengend.Clear();
        }

        // Abschluss: Position (nie über das erste Event eines hängenden Streams) und Geparkt in einem Commit.
        var neuePosition = grenze;
        var ersteHaengende = events.FirstOrDefault(e => haengend.Contains(e.Event.AggregateId));
        if (ersteHaengende is not null) neuePosition = Math.Min(neuePosition, ersteHaengende.Sequenz - 1);
        _tracker.BeginneBatch();
        await CommitAsync(Neu(Math.Max(_lese, neuePosition)), new Dictionary<Guid, int>(), ct);
        // Erledigt sind auch die übersprungenen Events geparkter Streams; hängende nicht — die liest der nächste Durchlauf erneut.
        foreach (var e in events)
            if (e.Sequenz > _lese && !haengend.Contains(e.Event.AggregateId)) _erledigt.Add(e.Sequenz);
        return angewandt;
    }

    private static void Halbiere(List<Guid> teil, Queue<List<Guid>> offen)
    {
        var mitte = teil.Count / 2;
        offen.Enqueue(teil.Take(mitte).ToList());
        offen.Enqueue(teil.Skip(mitte).ToList());
    }

    /// <summary>Ein einzelner Stream im Konflikt: bis zu fünfmal gestreut wiederholen; danach null (er bleibt bis zum nächsten Durchlauf stehen).</summary>
    private async Task<int?> EinzelnBeiKonfliktAsync(
        List<PartitionsEvent> events, Guid stream, Dictionary<Guid, int> guards, HashSet<Guid> geparkt, CancellationToken ct)
    {
        for (var versuch = 2; versuch <= KonfliktVersuche; versuch++)
        {
            await Task.Delay(Random.Shared.Next(5, 25 * versuch), ct);
            var r = await VersucheAsync(events, new List<Guid> { stream }, guards, geparkt, _lese, ct);
            if (r.Ok) return r.Angewandt;
            if (!r.Konflikt) { Parke(stream, guards, r.Fehler!, geparkt); return 0; }
        }
        _logger?.LogWarning("[Konsum] {Projektion}/{Partition}: Stream {Stream} nach {N} Konflikten am geteilten Dokument zurückgestellt",
            _projektionsId, _partition, stream, KonfliktVersuche);
        return null;
    }

    /// <summary>
    /// Wendet die Events der Streams <paramref name="teil"/> an und committet sie mit der Position <paramref name="position"/>. Ein Konflikt
    /// am Partitions-Checkpoint fliegt durch (der Lauf lädt neu); alles andere wird zum Ergebnis — nichts davon ist dann durabel.
    /// </summary>
    private async Task<Versuch> VersucheAsync(
        List<PartitionsEvent> events, IReadOnlyCollection<Guid> teil, Dictionary<Guid, int> guards, HashSet<Guid> geparkt,
        long position, CancellationToken ct)
    {
        var imTeil = teil.ToHashSet();
        _tracker.BeginneBatch();
        var neueGuards = new Dictionary<Guid, int>();
        var erledigt = new List<long>();
        var angewandt = 0;
        Guid? aktuell = null;
        try
        {
            foreach (var pe in events)
            {
                var e = pe.Event;
                if (!imTeil.Contains(e.AggregateId)) continue;
                erledigt.Add(pe.Sequenz);
                if (geparkt.Contains(e.AggregateId)) continue;                                      // geparkt: übersprungen
                if (e.AggregateVersion <= guards.GetValueOrDefault(e.AggregateId, -1)) continue;    // Guard: schon angewandt
                aktuell = e.AggregateId;
                _tracker.Stream(e.AggregateId);
                var writer = new ProjectionWriter(e.AggregateId, e.AggregateVersion);
                await _dispatch(e, writer);
                neueGuards[e.AggregateId] = e.AggregateVersion;
                angewandt++;
            }
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested) { throw; }
        catch (Exception ex) { return new Versuch(false, Fehler: ex, Verdacht: aktuell); }

        // Prüf-Naht „Absturz mitten im Batch“: was hier fliegt, beendet den Lauf (kein Poison, nichts geparkt, nichts durabel).
        if (_absturzVorCommit is not null) await _absturzVorCommit();

        try
        {
            await CommitAsync(Neu(position), neueGuards, ct);
        }
        catch (PartitionsKonfliktException) { throw; }
        catch (OperationCanceledException) when (ct.IsCancellationRequested) { throw; }
        catch (SchreibKonfliktException ex) { return new Versuch(false, Konflikt: true, Fehler: ex); }
        catch (Exception ex) { return new Versuch(false, Fehler: ex); }

        foreach (var s in erledigt) if (s > _lese) _erledigt.Add(s);
        foreach (var (stream, version) in neueGuards) guards[stream] = version;
        return new Versuch(true, angewandt);
    }

    /// <summary>Parkt einen Stream: ab dem nächsten Commit durabel im Checkpoint, Dead-Letter einmal (best-effort).</summary>
    private void Parke(Guid stream, IReadOnlyDictionary<Guid, int> guards, Exception fehler, HashSet<Guid> geparkt)
    {
        if (!geparkt.Add(stream)) return;
        _naechsterParkVersuch = _jetzt() + _konsum.Takt;   // der Einzelversuch kommt mit dem nächsten Poll
        var g = new GeparkterStream
        {
            StreamId = stream, Version = guards.GetValueOrDefault(stream, -1), Grund = $"{fehler.GetType().Name}: {fehler.Message}", Seit = _jetzt()
        };
        _stand!.Geparkt = _stand.Geparkt.Append(g).ToList();
        _logger?.LogWarning(fehler, "[Konsum] {Projektion}/{Partition}: Stream {Stream} geparkt ab v{Version} — die übrigen laufen weiter",
            _projektionsId, _partition, stream, g.Version + 1);
        _ = _deadLetter?.WriteAsync(new DeadLetter
        {
            Id = Guid.NewGuid(), Quelle = $"projektion:{_projektionsId}", CommandType = "(Projektion)", AggregateId = stream,
            Grund = $"geparkt ab v{g.Version + 1}: {g.Grund}", Versuche = 1, ErfasstUtc = g.Seit
        });
    }

    // ─────────────────────────────────────────────────────────────
    // Geparkte Streams einzeln versuchen
    // ─────────────────────────────────────────────────────────────

    private async Task<int> GeparkteVersuchenAsync(CancellationToken ct)
    {
        var angewandt = 0;
        foreach (var g in _stand!.Geparkt.ToList())
        {
            var guard = (await _tracker.LadeGuardsAsync(_projektionsId, new[] { g.StreamId }, ct))[g.StreamId];
            var events = await _eventStore.ReadStreamAsync(g.StreamId, guard + 1, ct);
            _tracker.BeginneBatch();
            var letzte = guard;
            var n = 0;
            try
            {
                foreach (var e in events)
                {
                    if (e.AggregateVersion <= guard) continue;
                    _tracker.Stream(e.AggregateId);
                    var writer = new ProjectionWriter(e.AggregateId, e.AggregateVersion);
                    await _dispatch(e, writer);
                    letzte = e.AggregateVersion;
                    n++;
                }
                var ohne = Neu(_lese);
                ohne.Geparkt = _stand.Geparkt.Where(x => x.StreamId != g.StreamId).ToList();
                await CommitAsync(ohne, letzte > guard ? new Dictionary<Guid, int> { [g.StreamId] = letzte } : new Dictionary<Guid, int>(), ct);
                angewandt += n;
                _logger?.LogInformation("[Konsum] {Projektion}/{Partition}: geparkter Stream {Stream} läuft wieder (bis v{Version})",
                    _projektionsId, _partition, g.StreamId, letzte);
            }
            catch (PartitionsKonfliktException) { throw; }
            catch (OperationCanceledException) when (ct.IsCancellationRequested) { throw; }
            catch (Exception ex)
            {
                _tracker.BeginneBatch();
                _logger?.LogDebug(ex, "[Konsum] {Projektion}/{Partition}: geparkter Stream {Stream} wirft weiter", _projektionsId, _partition, g.StreamId);
            }
        }
        return angewandt;
    }

    // ─────────────────────────────────────────────────────────────
    // Commit und Stand
    // ─────────────────────────────────────────────────────────────

    /// <summary>Die Lese-Position vorrücken (nie zurück) und Erledigtes dahinter vergessen.</summary>
    private void Rueckevor(long position)
    {
        if (position <= _lese) return;
        _lese = position;
        _erledigt.RemoveWhere(s => s <= position);
    }

    /// <summary>Der nächste Stand: Position, die aktuell geparkten Streams, erwartete Version = die geladene.</summary>
    private PartitionsCheckpoint Neu(long position) => new()
    {
        Id = _stand!.Id, ProjektionsId = _stand.ProjektionsId, Partition = _stand.Partition,
        Position = position, Version = _stand.Version, Geparkt = _stand.Geparkt.ToList()
    };

    private async Task CommitAsync(
        PartitionsCheckpoint neu, IReadOnlyDictionary<Guid, int> guards, CancellationToken ct)
    {
        neu.Version = await _tracker.CommitAsync(neu, guards, ct);
        _stand = neu;
        _letzterCommit = _jetzt();
        Rueckevor(neu.Position);
    }
}
