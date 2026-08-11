using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Abstractions;

namespace Infrastructure.Prozess;

/// <summary>
/// Der GENERISCHE Prozess-Manager (Spec §4) — ein Petri-Netz-Interpreter, EINMAL geschrieben, für JEDEN
/// Prozess-Typ gültig. Verschmilzt das frühere Prozess-Aggregat + den Treiber: er hält ein eigenes Log
/// (Entscheidungen) UND treibt die Ziel-Commands. Kern-Invariante: <b>Struktur aus Code (die Regeln),
/// Marking aus dem Log</b> — bei jeder Weckung frisch gefaltet, nie in einem Feld gehalten.
///
/// Eine Weckung = EIN Schritt (Spec §8, sequenziell zuerst): das Marking falten, die aktivierten,
/// noch-nicht-erledigten Transitionen rechnen, die erste feuern (FIRE-AND-FORGET über <see cref="_dispatch"/>
/// — kein <c>await</c> auf eine Quittung im Turn, die (A)-Hang-Klasse ist strukturell weg). Der Ausgang
/// kommt DURABEL vom Ziel-Stream und wird bei der nächsten Weckung gefaltet (Treiber-Fold/EM-1): eine
/// Wirkung (Domänen-Event), eine <c>KommandoVerarbeitet</c>-Noop-Marke oder eine <c>KommandoAbgelehnt</c>-
/// Ablehnungs-Marke (Achse <c>AbgelehntDa</c> → <c>SchrittGescheitert</c>). Keine out-of-turn-Quittung mehr.
///
/// Der <see cref="_dispatch"/>-Seam IST die einzige Transport-Berührung: live ein detached, bounded
/// Cluster-Send + Fehlschlag-Continuation, im Prüfstand ein Fake — so ist die ganze Petri-Logik in-memory
/// beweisbar, ohne den verteilten Hang zu raten.
/// </summary>
public sealed class ProzessManager
{
    private readonly IEventStoreRepository _store;
    private readonly IReadOnlyDictionary<string, ProzessRegeln> _registry;
    private readonly Func<Guid, ICommand, Guid, CancellationToken, Task> _dispatch;
    private readonly IProzessOffenIndex? _offenIndex;
    private readonly IDeadLetterSink? _deadLetters;   // ★ #12: KlärungNötig beobachtbar machen (optional, best-effort)

    // ── P5b: der nicht-autoritative Marking-Cursor (docs/prozess-marking-cursor-konzept.md) ──
    // Aktiv genau dann, wenn ein Store injiziert ist. Der HOT-Cache hält das gefaltete Marking über die
    // Weckungen EINER Manager-Instanz (der Actor lebt je Korrelation) — so faltet der Warm-Pfad nur den Tail,
    // ohne den durablen Store bei jeder Weckung zu treffen. Der Store ist die durable Kopie für den Kaltstart
    // (Passivierung/Neustart); fehlt/stale → Voll-Fold ab 0 (Fallback). Best-effort: nie Korrektheit, nur Tempo.
    private readonly IProzessMarkingStore? _markingStore;
    private readonly Dictionary<Guid, (string RegelHash, MarkingKompakt Marking)> _hotMarking = new();
    // Wieviele Weckungen seit dem letzten DURABLEN Marking-Write je Korrelation (Drossel, s.u.).
    private readonly Dictionary<Guid, int> _seitSchreib = new();
    // ★ P5b (Konzept §5, ProzessMarkingThreshold): das Marking wird NICHT bei jeder Weckung durabel geschrieben.
    //   Der HOT-Cache trägt die Korrektheit über die Weckungen EINER Aktivierung; der durable Write ist nur der
    //   Kaltstart-Beschleuniger (Passivierung/Crash). Jede Weckung zu schreiben tauschte O(N²) Event-Reads gegen
    //   O(N²) Marking-Writes (die §4-Falle). Alle K Weckungen zu schreiben amortisiert das auf ~O(N²/K) — ein
    //   Crash verliert höchstens die letzten <K Weckungen Fortschritt, die der Tail-Fold ohnehin folgenlos
    //   nachholt (Voll-Fold-Fallback / re-fire verpufft). K=1 = jede Weckung (altes Verhalten).
    private readonly int _markingSchreibIntervall;
    private bool CursorAktiv => _markingStore is not null;

    public ProzessManager(
        IEventStoreRepository store,
        IReadOnlyDictionary<string, ProzessRegeln> registry,
        Func<Guid, ICommand, Guid, CancellationToken, Task> dispatch,
        IProzessOffenIndex? offenIndex = null,
        IDeadLetterSink? deadLetters = null,
        IProzessMarkingStore? markingStore = null,
        int markingSchreibIntervall = 32)
    {
        _store = store;
        _registry = registry;
        _dispatch = dispatch;
        _offenIndex = offenIndex;
        _deadLetters = deadLetters;
        _markingStore = markingStore;
        _markingSchreibIntervall = Math.Max(1, markingSchreibIntervall);
    }

    // ── Öffentliche Eingänge (Actor/Fake rufen sie) ──

    /// <summary>Erste Weckung aus dem Auslöse-Event: startet den Prozess idempotent, dann treibt <see cref="WakeAsync"/>.</summary>
    public async Task StarteAsync(
        Guid korrelation, string prozessName, Guid auslöserStream, int auslöserVersion, CancellationToken ct = default)
    {
        var mz = await LadeStatusAsync(korrelation, ct);
        if (!mz.Gestartet)
        {
            await AppendAsync(korrelation, mz.Version, new ProzessGestartet(prozessName, auslöserStream, auslöserVersion), ct);
        }
        await WakeAsync(korrelation, ct);
    }

    /// <summary>Ein Treib-Schritt: Marking falten → aktivierte Transition feuern ODER kompensieren ODER terminal setzen.</summary>
    public async Task WakeAsync(Guid korrelation, CancellationToken ct = default)
    {
        var mz = await LadeStatusAsync(korrelation, ct);
        if (!mz.Gestartet || mz.Beendet) return;
        if (!_registry.TryGetValue(mz.ProzessName, out var regeln)) return;

        var kandidaten = await FaltMarkingMitCursorAsync(korrelation, mz, regeln, ct);

        // ── Treiber-Fold (EM-1, §4/§7.3): einen im Fold gesehenen Fehlschlag DURABEL machen. Eine
        //   KommandoAbgelehnt-Marke auf dem Ziel-Stream (AbgelehntDa) wird zu SchrittGescheitert im Manager-Log —
        //   die Quelle der Fehlschlag-Erkennung wandert von der Ziel-Quittung in den Fold. Das MUSS vor dem
        //   Vorwärts/Kompensations-Split passieren: sonst läse der Vorwärtszweig den Marker als „aufgelöst"
        //   (ErgebnisDa) und schriebe fälschlich ProzessBeendet(true) (der stille Falsch-Erfolg aus §4).
        //   Idempotent gegen die (in Scheibe A noch aktive) Quittung: nur Vorgänge, die NICHT schon in
        //   mz.Gescheitert stehen, werden gestempelt; ein Doppel-Stempel unterbleibt.
        var neuAbgelehnt = kandidaten
            .Where(k => k.AbgelehntDa && !mz.Gescheitert.ContainsKey(k.Vorgang))
            .GroupBy(k => k.Vorgang)
            .Select(g => g.First())
            .ToList();
        if (neuAbgelehnt.Count > 0)
        {
            var v = mz.Version;
            foreach (var k in neuAbgelehnt)
            {
                await AppendAsync(korrelation, v, new SchrittGescheitert(k.Vorgang, k.AbgelehntGrund), ct);
                v++;
            }
            // Frisch falten: mz.Gescheitert trägt den Fehlschlag jetzt → Kompensationszweig.
            await WakeAsync(korrelation, ct);
            return;
        }

        if (mz.Gescheitert.Count == 0)
        {
            // ── Vorwärts: die erste noch nicht erledigte Transition feuern (sequenziell, Spec §8) ──
            var pending = kandidaten.FirstOrDefault(k => !k.ErgebnisDa);
            if (pending != null)
            {
                await FeuereAsync(korrelation, pending.Cmd, pending.Vorgang, ct);
                return;
            }
            // Keine offene Transition, kein Fehler → Erfolg terminal.
            await AppendAsync(korrelation, mz.Version, new ProzessBeendet(true, ""), ct);
            return;
        }

        // ── Kompensation: die Erfolgs-Transitionen mit Gegenzug rückwärts (reverse Regel-Reihenfolge) ausgleichen ──
        var (komp, unvollziehbar) = await NächsteKompensationAsync(korrelation, kandidaten, mz.Gescheitert, ct);
        if (komp is not null)
        {
            await FeuereAsync(korrelation, komp.Cmd, komp.Vorgang, ct);
            return;
        }

        // ★ Audit-Fix #12 (Kompensations-Livelock): Ließ sich ein Gegenzug NICHT vollziehen (er wurde selbst
        //   abgelehnt → sein Vorgang steht in Gescheitert), steckt der Prozess halb-kompensiert fest — es gibt
        //   keinen sauberen Rollback. Den Gegenzug NICHT endlos neu feuern (das war der enge Livelock: erledigt
        //   wird er nie, weil eine Ablehnung weder Event noch Marke hinterlässt), sondern terminal als
        //   KlärungNötig halten. Das ProzessBeendet ist die durable Wahrheit und stoppt jedes weitere Feuern
        //   (die nächste Weckung faltet Beendet und kehrt sofort zurück); die DLQ ist nur der best-effort
        //   Ops-Blick auf den steckengebliebenen Gegenzug (ein Mensch/anderer Prozess muss auflösen).
        if (unvollziehbar is not null)
        {
            var grundK = mz.Gescheitert.TryGetValue(unvollziehbar.Vorgang, out var g) ? g : "abgelehnt";
            await AppendAsync(korrelation, mz.Version,
                new ProzessBeendet(false,
                    $"KlärungNötig: Kompensation '{unvollziehbar.Cmd.GetType().Name}' abgelehnt ({grundK})",
                    KlärungNötig: true),
                ct);
            await SchreibeKlärungsDeadLetterAsync(korrelation, mz.ProzessName, unvollziehbar, grundK, ct);
            return;
        }

        // Nichts mehr auszugleichen (alle Gegenzüge sauber erledigt) → fehlgeschlagen terminal.
        var grund = mz.Gescheitert.Values.FirstOrDefault() ?? "abgelehnt";
        await AppendAsync(korrelation, mz.Version, new ProzessBeendet(false, grund), ct);
    }

    /// <summary>
    /// Schreibt einen best-effort DLQ-Eintrag für einen Prozess, der in KlärungNötig terminal ist (Gegenzug
    /// selbst abgelehnt, #12). Reine Beobachtbarkeit — die Wahrheit ist das <see cref="ProzessBeendet"/> im
    /// Manager-Log; ein verlorener Eintrag kostet nur die Ops-Sicht, nie Korrektheit.
    /// </summary>
    private async Task SchreibeKlärungsDeadLetterAsync(
        Guid korrelation, string prozessName, Kompensation unvollziehbar, string grund, CancellationToken ct)
    {
        if (_deadLetters is null) return;
        try
        {
            await _deadLetters.WriteAsync(new DeadLetter
            {
                Id = Guid.NewGuid(),
                Quelle = $"prozess-manager/{prozessName}",
                CommandType = unvollziehbar.Cmd.GetType().Name,
                AggregateId = unvollziehbar.Cmd.AggregateId,
                CorrelationId = korrelation.ToString(),
                Grund = $"Kompensation abgelehnt — Prozess in KlärungNötig ({grund})",
                Versuche = 1,
                ErfasstUtc = DateTimeOffset.UtcNow,
            }, ct);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[Prozess-KlärungNötig] DLQ-Write fehlgeschlagen ({korrelation}): {ex.Message}");
        }
    }

    // ── Feuern: Vorgang → deterministische CommandId (Framework-Inbox), fire-and-forget dispatchen ──
    // Der Regel-Command bleibt REIN (keine Vorgang-Injektion); die Idempotenz sichert die CommandId.
    private Task FeuereAsync(Guid korrelation, ICommand cmd, Guid vorgang, CancellationToken ct)
        => _dispatch(korrelation, cmd, vorgang, ct);

    // ── Marking falten (Fixpunkt über die Ziel-Streams) ──

    /// <summary>Ein Token = ein Event-Payload plus seine Herkunft (Stream/Version), für Vorgang-Ableitung + Join.</summary>
    private sealed record Token(IEvent Payload, Guid Stream, int Version);

    /// <summary>
    /// Eine mögliche Transition (Regel × gematchte Tokens) samt deterministischem Vorgang und ZWEI getrennten
    /// Ergebnis-Achsen (Audit-Fix K2):
    ///   • <paramref name="ErgebnisDa"/> = „aufgelöst": IRGENDEIN Ziel-Event mit dieser Kausalität liegt vor —
    ///     auch die interne Inbox-Marke (Noop/Ablehnung hinterlässt nur sie). Steuert „nicht mehr feuern"
    ///     (verhindert den Livelock) und die Terminal-Erkennung.
    ///   • <paramref name="WirkungDa"/> = „wirksam": ein DOMÄNEN-Event (kein <see cref="IProzessIntern"/>) liegt
    ///     vor. Nur eine Wirkung ist kompensierbar und aktiviert Downstream-Joins. Eine Ablehnung/ein Noop ist
    ///     aufgelöst, aber NICHT wirksam → sie wird nie kompensiert (keine Wirkung zum Zurücknehmen).
    ///   • <paramref name="AbgelehntDa"/> = „fachlich abgelehnt": eine durable <c>KommandoAbgelehnt</c>-Marke mit
    ///     dieser Kausalität liegt vor (Treiber-Fold/EM-1). Das ist die dritte Achse, die die frühere
    ///     Quittungs-Fehlschlag-Erkennung ersetzt: <see cref="WakeAsync"/> stempelt daraus ein durables
    ///     <c>SchrittGescheitert</c>. OHNE diese Achse läse der Vorwärtszweig den Marker nur als
    ///     <paramref name="ErgebnisDa"/> und schriebe fälschlich <c>ProzessBeendet(true)</c> (§4-Kopplung).
    ///     <paramref name="AbgelehntGrund"/> trägt den getippten Ablehnungs-Grund in den Fehlschlag.
    /// </summary>
    private sealed record Kandidat(
        Regel Regel, int RegelIndex, IReadOnlyList<Token> Match, ICommand Cmd, Guid Vorgang,
        bool ErgebnisDa, bool WirkungDa, bool AbgelehntDa, string AbgelehntGrund);

    /// <summary>
    /// P5b-Einstieg: entscheidet Voll-Fold vs. inkrementellen Cursor-Fold und pflegt den Marking-Cache. Ist der
    /// Cursor inaktiv (kein Store), ist das exakt der frühere Voll-Fold (frisches, leeres Marking je Weckung → jeder
    /// Ziel-Stream ab 0). Ist er aktiv, faltet der Manager auf einem fortgeschriebenen Marking weiter (Tail-Read) und
    /// schreibt es best-effort fort. Beide Wege liefern per Konstruktion DIESELBEN Kandidaten (der Fold ist
    /// identisch; nur die Startbedingung — leeres vs. fortgeschriebenes Marking + Cursor — unterscheidet sie).
    /// </summary>
    private async Task<List<Kandidat>> FaltMarkingMitCursorAsync(
        Guid korrelation, ManagerStatus mz, ProzessRegeln regeln, CancellationToken ct)
    {
        if (!CursorAktiv)
        {
            // Voll-Fold: frisches, leeres Marking → jeder Ziel-Stream ab 0 (das frühere Verhalten, unverändert).
            var (_, kandidatenVoll) = await FalteAsync(korrelation, mz, regeln, new MarkingKompakt(), ct);
            return kandidatenVoll;
        }

        var regelHash = ProzessRegelHash.Berechne(regeln);
        var marking = await HoleMarkingAsync(korrelation, regelHash, ct);
        var (_, kandidaten) = await FalteAsync(korrelation, mz, regeln, marking, ct);
        await SchreibeMarkingAsync(korrelation, regelHash, mz.Version, marking, ct);
        return kandidaten;
    }

    /// <summary>
    /// Der EINE Fold — Voll wie inkrementell. Er liest je Ziel-Stream ab <c>StreamCursor[s]+1</c> (fehlt der Cursor
    /// → ab 0) und arbeitet die gelesenen Events in das <paramref name="marking"/> ein (mutierend): je Kausalität
    /// (Vorgang) die drei Achsen aufgelöst/Wirkung/abgelehnt + der von einer Wirkung erzeugte Downstream-Token.
    /// Der Fixpunkt zieht dann — rein in-memory, ohne weitere I/O — aus dem Marking die Kandidaten und die Tokens.
    ///
    /// Äquivalenz: Ziel-Streams sind append-only, Cursor rücken nur vor. Ein auf einem Präfix gefaltetes Marking +
    /// der Tail darauf ergibt dieselben akkumulierten Achsen wie ein Fold aller Events ab 0 (Monotonie) → dieselben
    /// Kandidaten, dieselbe Feuer-Entscheidung. Ein leeres <paramref name="marking"/> ist der Voll-Fold ab 0.
    /// </summary>
    private async Task<(List<Token> Tokens, List<Kandidat> Kandidaten)> FalteAsync(
        Guid korrelation, ManagerStatus mz, ProzessRegeln regeln, MarkingKompakt marking, CancellationToken ct)
    {
        // In DIESER Weckung schon bis Head integrierte Streams (ein Read je Stream pro Weckung, wie das alte Lies).
        var integriert = new HashSet<Guid>();
        async Task Integriere(Guid s)
        {
            if (!integriert.Add(s)) return;
            var von = marking.StreamCursor.TryGetValue(s, out var c) ? c + 1 : 0;
            var evs = await _store.ReadStreamAsync(s, von, ct);
            var maxV = marking.StreamCursor.TryGetValue(s, out var alt) ? alt : -1;
            foreach (var e in evs)   // aufsteigend (Vertrag: geordnet) → „erste Wirkung" = niedrigste Version
            {
                if (e.AggregateVersion > maxV) maxV = e.AggregateVersion;
                var cid = e.CausationId ?? "";
                if (!marking.Vorgänge.TryGetValue(cid, out var vm)) { vm = new VorgangMarke(); marking.Vorgänge[cid] = vm; }
                // „Abgelehnt": die durable KommandoAbgelehnt-Marke (Treiber-Fold/EM-1).
                if (e.Payload is Infrastructure.Aggregate.KommandoAbgelehnt ka) { vm.Abgelehnt = true; vm.Grund = ka.Grund; }
                // „Wirkung": ein DOMÄNEN-Event (kein IProzessIntern), NUR das erste je Vorgang (wie das alte FirstOrDefault).
                else if (e.Payload is not IProzessIntern && !vm.Wirkung)
                {
                    vm.Wirkung = true; vm.TokenStream = s; vm.TokenVersion = e.AggregateVersion; vm.TokenPayload = e.Payload;
                }
                // sonst (KommandoVerarbeitet-Noop u. a. IProzessIntern): nur „aufgelöst" (Schlüssel-Präsenz).
            }
            if (maxV >= 0) marking.StreamCursor[s] = maxV;
        }

        // Wurzel-Token: das Auslöse-Event. Einmalig gelesen und im Marking zwischengehalten (unveränderlich).
        if (marking.AuslöserPayload is null)
        {
            var auslöserEvents = await _store.ReadStreamAsync(mz.AuslöserStream, 0, ct);
            marking.AuslöserPayload = auslöserEvents.FirstOrDefault(e => e.AggregateVersion == mz.AuslöserVersion)?.Payload;
        }

        var tokens = new List<Token>();
        if (marking.AuslöserPayload is not null)
            tokens.Add(new Token(marking.AuslöserPayload, mz.AuslöserStream, mz.AuslöserVersion));

        var kandidaten = new List<Kandidat>();
        bool geändert = true;
        while (geändert)
        {
            geändert = false;
            var schnappschuss = tokens.ToList();
            kandidaten = new List<Kandidat>();

            for (int ri = 0; ri < regeln.Regeln.Count; ri++)
            {
                var regel = regeln.Regeln[ri];
                foreach (var match in Belegungen(regel, schnappschuss))
                {
                    var cmds = regel.Sende(match.Select(t => (IEvent)t.Payload).ToList());
                    foreach (var (cmd, ci) in cmds.Select((c, i) => (c, i)))
                    {
                        var primär = match[0];
                        // ★ Befund 7/8: RegelIndex (ri) + Instanz-Index (ci) in den Diskriminator → zwei Regeln
                        //   mit gleichem Auslöser/Command/Ziel kollidieren nicht (8); Fan-out an DASSELBE Ziel
                        //   bekommt distinkte Vorgänge (7). Deterministisch (Sende ist rein, Ordnung stabil).
                        var vorgang = ProzessId.FürTransition(
                            korrelation, primär.Stream, primär.Version, cmd.GetType().Name,
                            $"{ri}:{ci}:{cmd.AggregateId:N}");

                        // Den Ziel-Stream bis Head einarbeiten (Tail-Read bei aktivem Cursor, ab 0 beim Voll-Fold),
                        // DANN die Achsen aus dem Marking lesen — statt den Stream bei jeder Weckung neu zu scannen.
                        await Integriere(cmd.AggregateId);
                        var marke = marking.Vorgänge.GetValueOrDefault(vorgang.ToString());
                        var aufgeloest = marke is not null;                 // irgendein Ziel-Event mit dieser Kausalität
                        var wirkung = marke?.Wirkung ?? false;               // ein Domänen-Event → kompensierbar + Join
                        var abgelehnt = marke?.Abgelehnt ?? false;           // KommandoAbgelehnt-Marke → SchrittGescheitert
                        var abgelehntGrund = marke?.Grund ?? "abgelehnt";
                        kandidaten.Add(new Kandidat(
                            regel, ri, match, cmd, vorgang,
                            aufgeloest, wirkung, abgelehnt, abgelehntGrund));

                        // Nur eine WIRKUNG bringt ein neues Token in den Fold (aktiviert Downstream-Joins). Eine
                        // reine Marke (Noop/Ablehnung) ist inert — sie darf keinen Join scharf schalten.
                        if (wirkung && marke!.TokenPayload is not null &&
                            !tokens.Any(t => t.Stream == marke.TokenStream && t.Version == marke.TokenVersion))
                        {
                            tokens.Add(new Token(marke.TokenPayload, marke.TokenStream, marke.TokenVersion));
                            geändert = true;
                        }
                    }
                }
            }
        }
        return (tokens, kandidaten);
    }

    // ── P5b: Marking-Cache laden/schreiben (best-effort; ein Fehler kostet nur Tempo, nie Korrektheit) ──

    /// <summary>
    /// Holt das gefaltete Marking für die Weckung: erst der HOT-Cache dieser Instanz (Warm-Pfad, kein I/O), sonst
    /// der durable Store (Kaltstart). Passt der <paramref name="regelHash"/> nicht (Regeländerung) oder fehlt der
    /// Eintrag, startet ein LEERES Marking → Voll-Fold ab 0 (Fallback, Invariante 1).
    /// </summary>
    private async Task<MarkingKompakt> HoleMarkingAsync(Guid korrelation, string regelHash, CancellationToken ct)
    {
        if (_hotMarking.TryGetValue(korrelation, out var hot) && hot.RegelHash == regelHash)
            return hot.Marking;

        try
        {
            var doc = await _markingStore!.LadeAsync(korrelation, ct);
            if (doc is not null && doc.RegelHash == regelHash)
                return doc.Marking;
        }
        catch (Exception ex) { Console.WriteLine($"[Prozess-Marking] Laden fehlgeschlagen ({korrelation}): {ex.Message}"); }

        return new MarkingKompakt();
    }

    /// <summary>
    /// Schreibt das Marking in den HOT-Cache (IMMER — er trägt die Korrektheit über die Weckungen) und
    /// gedrosselt (alle <see cref="_markingSchreibIntervall"/> Weckungen) durabel in den Store (best-effort).
    /// So bleibt der durable Write O(N²/K) statt O(N²) — ohne die Warm-Korrektheit anzutasten.
    /// </summary>
    private async Task SchreibeMarkingAsync(Guid korrelation, string regelHash, int logVersion, MarkingKompakt marking, CancellationToken ct)
    {
        _hotMarking[korrelation] = (regelHash, marking);

        var seit = _seitSchreib.GetValueOrDefault(korrelation) + 1;
        if (seit < _markingSchreibIntervall) { _seitSchreib[korrelation] = seit; return; }
        _seitSchreib[korrelation] = 0;

        try
        {
            await _markingStore!.SchreibeAsync(new ProzessMarking
            {
                Id = korrelation,
                RegelHash = regelHash,
                LogVersion = logVersion,
                Marking = marking,
                UpdatedAt = DateTimeOffset.UtcNow,
            }, ct);
        }
        catch (Exception ex) { Console.WriteLine($"[Prozess-Marking] Schreiben fehlgeschlagen ({korrelation}): {ex.Message}"); }
    }

    /// <summary>Verwirft den Marking-Cache einer terminalen Korrelation (HOT + Store) — sie braucht ihn nie wieder.</summary>
    private async Task VerwirfMarkingAsync(Guid korrelation, CancellationToken ct)
    {
        _hotMarking.Remove(korrelation);
        _seitSchreib.Remove(korrelation);
        if (_markingStore is null) return;
        try { await _markingStore.LöscheAsync(korrelation, ct); }
        catch (Exception ex) { Console.WriteLine($"[Prozess-Marking] Löschen fehlgeschlagen ({korrelation}): {ex.Message}"); }
    }

    /// <summary>
    /// Alle Belegungen einer Regel: für normale Regeln die kartesischen Konjunktions-Matches; für einen
    /// COUNT-JOIN die Bedingungs-Matches, an die ALLE Sammel-Tokens angehängt werden — aber nur, wenn ihre
    /// Zahl die aus dem Auslöser abgeleitete Breite erreicht (buche erst nach allen N).
    /// </summary>
    private static IEnumerable<IReadOnlyList<Token>> Belegungen(Regel regel, List<Token> tokens)
    {
        if (regel.Sammel is null)
        {
            foreach (var m in Matches(regel, tokens)) yield return m;
            yield break;
        }

        var sammel = tokens.Where(t => regel.Sammel.Typ.IsInstanceOfType(t.Payload)).ToList();
        foreach (var trig in Matches(regel, tokens))   // Matches nutzt regel.Bedingung (nur der/die Auslöser)
        {
            var erwartet = regel.Sammel.Anzahl((IEvent)trig[0].Payload);
            if (erwartet > 0 && sammel.Count >= erwartet)
                yield return trig.Concat(sammel).ToList();
        }
    }

    /// <summary>Kartesische Konjunktions-Matches über <see cref="Regel.Bedingung"/> (pro Typ die passenden Tokens).</summary>
    private static IEnumerable<IReadOnlyList<Token>> Matches(Regel regel, List<Token> tokens)
    {
        var perTyp = regel.Bedingung
            .Select(t => tokens.Where(tok => t.IsInstanceOfType(tok.Payload)).ToList())
            .ToList();
        if (perTyp.Any(l => l.Count == 0)) yield break;

        var indizes = new int[perTyp.Count];
        while (true)
        {
            yield return Enumerable.Range(0, perTyp.Count).Select(i => perTyp[i][indizes[i]]).ToList();

            int k = perTyp.Count - 1;
            while (k >= 0 && ++indizes[k] >= perTyp[k].Count) { indizes[k] = 0; k--; }
            if (k < 0) yield break;
        }
    }

    // ── Kompensation: reverse Regel-Reihenfolge über Erfolgs-Transitionen mit Gegenzug ──
    private sealed record Kompensation(ICommand Cmd, Guid Vorgang);

    /// <summary>
    /// Liefert den nächsten noch offenen Gegenzug ODER — wenn keiner mehr feuerbar ist — einen etwaigen
    /// <c>Unvollziehbar</c>-Gegenzug (ein Gegenzug, der SELBST abgelehnt wurde, sein Vorgang steht in
    /// <paramref name="gescheitert"/>). Der Aufrufer feuert <c>Naechste</c>, solange es einen gibt; sonst
    /// entscheidet <c>Unvollziehbar != null</c> zwischen sauberem Fehlschlag-Terminal und KlärungNötig (#12).
    /// </summary>
    private async Task<(Kompensation? Naechste, Kompensation? Unvollziehbar)> NächsteKompensationAsync(
        Guid korrelation, List<Kandidat> kandidaten, IReadOnlyDictionary<Guid, string> gescheitert, CancellationToken ct)
    {
        Kompensation? unvollziehbar = null;
        // Erfolgreiche Vorwärts-Transitionen, die einen Gegenzug tragen — rückwärts durch den DAG
        // (reverse Regel-Reihenfolge ist bei sequenzieller Fahrt eine gültige transponierte Kausalität, §7).
        foreach (var k in kandidaten.Where(k => k.WirkungDa && k.Regel.RückgängigDurch is not null)
                                    .OrderByDescending(k => k.RegelIndex))
        {
            var gegen = k.Regel.RückgängigDurch!(k.Match.Select(t => (IEvent)t.Payload).ToList());
            foreach (var (cmd, ci) in gegen.Select((c, i) => (c, i)))
            {
                var primär = k.Match[0];
                // ★ Befund 7/8: RegelIndex (k.RegelIndex) + Instanz-Index (ci) — analog zur Vorwärts-Transition.
                var vorgang = ProzessId.FürKompensation(
                    korrelation, primär.Stream, primär.Version, cmd.GetType().Name,
                    $"{k.RegelIndex}:{ci}:{cmd.AggregateId:N}");
                // Schon ausgeglichen? Der Gegenzug ist erledigt, wenn sein Ergebnis auf dem Ziel-Stream liegt —
                // ABER nur ein Ergebnis, das KEINE Ablehnung ist (eine Wirkung ODER die KommandoVerarbeitet-Noop-
                // Marke). Eine KommandoAbgelehnt-Marke zählt NICHT als erledigt (der Gegenzug wurde abgelehnt).
                var zielEvents = await _store.ReadStreamAsync(cmd.AggregateId, 0, ct);
                var erledigt = zielEvents.Any(e =>
                    e.CausationId == vorgang.ToString() && e.Payload is not Infrastructure.Aggregate.KommandoAbgelehnt);
                if (erledigt) continue;
                // ★ Audit-Fix #12 + Treiber-Fold: Der Gegenzug wurde SELBST abgelehnt — als durable
                //   KommandoAbgelehnt-Marke auf dem Ziel-Stream (der Fold ersetzt die frühere Quittung; nach
                //   Entfall des Quittungs-Pfads ist der Marker die Wahrheit). NICHT neu feuern (sonst enger
                //   Kompensations-Livelock: „erledigt" wird er nie) — als unvollziehbar merken und weitersuchen.
                //   (gescheitert.ContainsKey bleibt als zweite Quelle stehen: harmlos, deckt Alt-/Boot-Zustände.)
                var abgelehnt = zielEvents.Any(e =>
                    e.CausationId == vorgang.ToString() && e.Payload is Infrastructure.Aggregate.KommandoAbgelehnt);
                if (abgelehnt || gescheitert.ContainsKey(vorgang)) { unvollziehbar ??= new Kompensation(cmd, vorgang); continue; }
                return (new Kompensation(cmd, vorgang), unvollziehbar);
            }
        }
        return (null, unvollziehbar);
    }

    // ── Manager-Log falten ──

    /// <summary>Der aus dem Manager-Log gefaltete Kopf-Zustand (Start, Fehlschläge, Terminal, Log-Version für OCC).</summary>
    public sealed class ManagerStatus
    {
        public bool Gestartet { get; init; }
        public string ProzessName { get; init; } = "";
        public Guid AuslöserStream { get; init; }
        public int AuslöserVersion { get; init; }
        public int Version { get; init; }
        public IReadOnlyDictionary<Guid, string> Gescheitert { get; init; } = new Dictionary<Guid, string>();
        public bool Beendet { get; init; }
        public bool Erfolg { get; init; }
    }

    public async Task<ManagerStatus> LadeStatusAsync(Guid korrelation, CancellationToken ct = default)
    {
        var log = await _store.ReadStreamAsync(korrelation, 0, ct);
        bool gestartet = false, beendet = false, erfolg = false;
        string name = "", grund = "";
        Guid auslöserStream = default;
        int auslöserVersion = 0;
        var gescheitert = new Dictionary<Guid, string>();

        foreach (var env in log)
        {
            switch (env.Payload)
            {
                case ProzessGestartet g:
                    gestartet = true; name = g.ProzessName; auslöserStream = g.AuslöserStream; auslöserVersion = g.AuslöserVersion;
                    break;
                case SchrittGescheitert f:
                    gescheitert[f.Vorgang] = f.Grund;
                    break;
                case ProzessBeendet b:
                    beendet = true; erfolg = b.Erfolg; grund = b.Grund;
                    break;
            }
        }

        return new ManagerStatus
        {
            Gestartet = gestartet, ProzessName = name,
            AuslöserStream = auslöserStream, AuslöserVersion = auslöserVersion,
            Version = log.Count, Gescheitert = gescheitert, Beendet = beendet, Erfolg = erfolg,
        };
    }

    private async Task AppendAsync(Guid korrelation, int erwarteteVersion, IEvent ereignis, CancellationToken ct)
    {
        await _store.AppendEventsAsync(korrelation, erwarteteVersion, new[] { ereignis }, aggregateType: "ProzessManager");

        // Offen-Index NACH dem durablen Log-Append pflegen — das Log ist die Wahrheit, der Index nur ein
        // best-effort-Hinweis für den §3-Backstop. Ein Fehler hier ist folgenlos (siehe IProzessOffenIndex):
        // ein fehlender Eintrag fällt auf den Signal-/Selbst-Weckungs-Pfad zurück, ein stale Eintrag weckt
        // einen bereits terminalen Prozess (dessen WakeAsync sofort folgenlos zurückkehrt).
        if (_offenIndex is not null)
        {
            try
            {
                if (ereignis is ProzessGestartet g) await _offenIndex.MarkiereOffenAsync(korrelation, g.ProzessName, ct);
                else if (ereignis is ProzessBeendet) await _offenIndex.MarkiereBeendetAsync(korrelation, ct);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[Prozess-Offen-Index] Pflege fehlgeschlagen ({ereignis.GetType().Name}): {ex.Message}");
            }
        }

        // ★ P5b: der Marking-Cursor wird mit dem Terminal überflüssig — der Prozess wird nie wieder geweckt
        //   (die nächste Weckung faltet Beendet und kehrt sofort zurück). HOT + Store aufräumen (best-effort).
        if (CursorAktiv && ereignis is ProzessBeendet)
            await VerwirfMarkingAsync(korrelation, ct);
    }
}
