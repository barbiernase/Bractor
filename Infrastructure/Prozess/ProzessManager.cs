using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Abstractions;

namespace Infrastructure.Prozess;

/// <summary>
/// Der GENERISCHE Prozess-Manager (docs/04 §4.5) — ein Petri-Netz-Interpreter, EINMAL geschrieben, für JEDEN
/// Prozess-Typ gültig. Verschmilzt das frühere Prozess-Aggregat + den Treiber: er hält ein eigenes Log
/// (Entscheidungen) UND treibt die Ziel-Commands. Kern-Invariante: <b>Struktur aus Code (die Regeln),
/// Marking aus dem Log</b> — bei jeder Weckung frisch gefaltet, nie in einem Feld gehalten.
///
/// Eine Weckung = EIN Schritt (docs/04 §4.5, sequenziell zuerst): das Marking falten, die aktivierten,
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
    // Korrelation → Akteur des Prozesses (aus dem Manager-Log gefaltet, je Weckung aufgefrischt).
    private readonly System.Collections.Concurrent.ConcurrentDictionary<Guid, string?> _akteurVon = new();
    private readonly Func<Guid, ICommand, Guid, CancellationToken, Task> _dispatch;
    // Funktions-Aufruf (Rufe<F>): Auftrag an den Ausführer übergeben — fire-and-forget, OHNE Selbst-Weckung (das Ergebnis
    //   kann Stunden dauern; der Ausführer weckt den Manager, sobald es im Log liegt). Null = keine Funktionen verdrahtet.
    private readonly Func<Guid, IAuftrag, Guid, string?, CancellationToken, Task>? _rufe;
    // Die Uhr für Zeitlimits — live die DB-Uhr (die Event-Zeitstempel sind DB-generiert, also kein Skew).
    private readonly Func<CancellationToken, Task<DateTimeOffset>>? _jetzt;
    private readonly IProzessOffenIndex? _offenIndex;
    private readonly IDeadLetterSink? _deadLetters;   // ★ #12: KlärungNötig beobachtbar machen (optional, best-effort)

    // ── P5b: der nicht-autoritative Marking-Cursor (docs/04-konsum-und-prozess-maschine.md §4.5) ──
    // Aktiv genau dann, wenn ein Store injiziert ist. Der HOT-Cache hält das gefaltete Marking über die
    // Weckungen EINER Manager-Instanz (der Actor lebt je Korrelation) — so faltet der Warm-Pfad nur den Tail,
    // ohne den durablen Store bei jeder Weckung zu treffen. Der Store ist die durable Kopie für den Kaltstart
    // (Passivierung/Neustart); fehlt/stale → Voll-Fold ab 0 (Fallback). Best-effort: nie Korrektheit, nur Tempo.
    private readonly IProzessMarkingStore? _markingStore;
    // Frisch = in DIESER Aktivierung gestartet: jedes Ergebnis kam nach einem Feuern dieser Aktivierung (dirty) — ein nie gesehener Ziel-Stream
    //   ist dann sicher leer und wird nicht gelesen.
    private readonly Dictionary<Guid, (string RegelHash, MarkingKompakt Marking, bool Frisch)> _hotMarking = new();
    // Korrelationen, deren Marking durabel im Store liegen könnte (geschrieben oder von dort geladen) — nur deren Stand löscht das Ende.
    private readonly HashSet<Guid> _markingDurabel = new();
    // Der Kopf-Zustand des eigenen Logs je Korrelation: der Manager ist dessen einziger Schreiber (Single-Activation, jede Änderung mit
    //   erwarteter Version) — er schreibt ihn mit seinen Appends fort, statt das Log bei jeder Weckung zu lesen. Ein gescheiterter Append, ein
    //   umgeleiteter Schritt (seine Log-Zeit zählt) und die Backstop-Weckung lesen frisch.
    private readonly Dictionary<Guid, ManagerStatus> _status = new();
    // Wieviele Weckungen seit dem letzten DURABLEN Marking-Write je Korrelation (Drossel, s.u.).
    private readonly Dictionary<Guid, int> _seitSchreib = new();
    // ★ P5b (feuer-gerichtete Reads): je Korrelation die Ziel-Streams, in die seit dem letzten Fold GEFEUERT
    //   wurde — nur DEREN Ergebnis kann sich geändert haben (der Manager ist der einzige Erzeuger SEINER
    //   Vorgänge auf dem Ziel). Auf dem WARM-Pfad faltet er nur diese (+ nie-gesehene neue Ziele) nach, statt
    //   alle Kandidaten-Streams neu zu lesen → DB-Roundtrips von O(N²) auf ~O(1) pro Weckung. Kaltstart
    //   (Hot-Miss) liest voll (die Wahrheit rekonstruieren) — Invariante 1 gewahrt.
    private readonly Dictionary<Guid, HashSet<Guid>> _dirty = new();
    // Je Korrelation und Ziel-Stream die Vorgänge, die DIESE Aktivierung dorthin gefeuert hat. Liest der Fold einen Ziel-Stream ab einem
    //   Hinweis (der Version, an der ein Send seinen Ausgang geschrieben hat), überspringt er die Lücke davor; den Cursor darf er darüber nur
    //   vorrücken, wenn dort nichts Eigenes mehr aussteht — in einem frischen Vorgang genau dann, wenn alle hierher gefeuerten aufgelöst sind.
    private readonly Dictionary<Guid, Dictionary<Guid, HashSet<Guid>>> _gefeuert = new();
    // ★ P5b (docs/04 §4.5, ProzessMarkingThreshold): das Marking wird NICHT bei jeder Weckung durabel geschrieben.
    //   Der HOT-Cache trägt die Korrektheit über die Weckungen EINER Aktivierung; der durable Write ist nur der
    //   Kaltstart-Beschleuniger (Passivierung/Crash). Jede Weckung zu schreiben tauschte O(N²) Event-Reads gegen
    //   O(N²) Marking-Writes (die §4-Falle). Alle K Weckungen zu schreiben amortisiert das auf ~O(N²/K) — ein
    //   Crash verliert höchstens die letzten <K Weckungen Fortschritt, die der Tail-Fold ohnehin folgenlos
    //   nachholt (Voll-Fold-Fallback / re-fire verpufft). K=1 = jede Weckung (altes Verhalten).
    private readonly int _markingSchreibIntervall;
    private bool CursorAktiv => _markingStore is not null;

    // PF-4: die zuletzt in den Offen-Index geschriebene Frist je Korrelation (Drossel, siehe PflegeFristAsync).
    private readonly Dictionary<Guid, DateTimeOffset?> _frist = new();
    /// <summary>Wie oft ein offener Warte-Knoten seinen Strom nachliest (über die Frist des Backstops).</summary>
    public static readonly TimeSpan WarteTakt = TimeSpan.FromSeconds(15);

    public ProzessManager(
        IEventStoreRepository store,
        IReadOnlyDictionary<string, ProzessRegeln> registry,
        Func<Guid, ICommand, Guid, CancellationToken, Task> dispatch,
        IProzessOffenIndex? offenIndex = null,
        IDeadLetterSink? deadLetters = null,
        IProzessMarkingStore? markingStore = null,
        int markingSchreibIntervall = 32,
        Func<Guid, IAuftrag, Guid, string?, CancellationToken, Task>? rufe = null,
        Func<CancellationToken, Task<DateTimeOffset>>? jetzt = null)
    {
        _store = store;
        _registry = registry;
        _dispatch = dispatch;
        _rufe = rufe;
        _jetzt = jetzt;
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
        var mz = await StatusAsync(korrelation, frisch: false, ct);
        if (!mz.Gestartet)
        {
            // Kausalkette (docs/konzept-akteure.md §5.1): der Prozess handelt im Auftrag des Akteurs, dessen Event ihn auslöste —
            //   einmal beim Start gelesen und mit ProzessGestartet ins Manager-Log gestempelt (Header), danach aus dem Log gefaltet.
            var auslöser = await _store.ReadEventAsync(auslöserStream, auslöserVersion, ct);
            // Der Offen-Index-Eintrag reist als Beilage im Append des ProzessGestartet — eine Transaktion: es gibt keinen gestarteten Vorgang
            //   ohne Eintrag (so im Störungslauf gesehen: 51 Vorgänge, als der Eintrag danach kam) und keinen Eintrag ohne Vorgang.
            await AppendAsync(korrelation, mz.Version, new ProzessGestartet(prozessName, auslöserStream, auslöserVersion), ct, auslöser?.UserId,
                _offenIndex is not null ? [new VorgangOffen(korrelation, prozessName)] : null);
            // Frischer Vorgang: sein Marking beginnt hier, leer bis auf den Auslöser (schon gelesen) — kein Laden aus dem Store, kein Lesen der
            //   Ziel-Streams, in die noch nie gefeuert wurde.
            if (CursorAktiv && _registry.TryGetValue(prozessName, out var regeln))
                _hotMarking[korrelation] = (ProzessRegelHash.Berechne(regeln),
                    new MarkingKompakt { AuslöserPayload = auslöser?.Payload, AuslöserZeit = auslöser?.CreatedAtUtc ?? default }, Frisch: true);
        }
        await WakeAsync(korrelation, ct);
    }

    /// <summary>
    /// Ein Treib-Schritt: Marking falten → aktivierte Transition feuern ODER kompensieren ODER terminal setzen.
    /// <paramref name="vomBackstop"/>: der Backstop weckt (fällige Frist oder Heilung nach Stille, <see cref="PflegeFristAsync"/>) — dann liest
    /// der Manager alles frisch (Log und Ziel-Streams), die Heilung verlässt sich auf keine Buchführung. Sonst liest er nur, was sich seit dem
    /// letzten Fold geändert haben kann: weckt der Router wegen eines Events (<paramref name="quelle"/>/<paramref name="quellVersion"/>), dessen
    /// Stream, und nichts, wenn der Fold ihn schon so weit kennt; nach einem Send oder Funktions-Ergebnis (<paramref name="vorgang"/>) den
    /// Ziel-Stream, und nichts, wenn der Ausgang des Vorgangs schon gefaltet ist; ohne Quelle die Streams, in die er gefeuert hat und deren
    /// Ergebnis noch aussteht.
    /// </summary>
    public async Task WakeAsync(Guid korrelation, CancellationToken ct = default, bool vomBackstop = false, Guid quelle = default, int quellVersion = 0,
        Guid vorgang = default)
    {
        var mz = await StatusAsync(korrelation, frisch: vomBackstop, ct);
        if (!mz.Gestartet || mz.Beendet || !_registry.TryGetValue(mz.ProzessName, out var regeln))
        {
            // Der Backstop weckt einen Vorgang, der nichts mehr tut (beendet, das Löschen aus dem Index ging verloren; oder sein Prozess ist
            //   aus dem Code verschwunden): den Eintrag jetzt aufräumen — sonst weckte ihn eine liegengebliebene Frist jede Sekunde.
            if (vomBackstop && _offenIndex is not null)
            {
                try
                {
                    if (mz.Gestartet && !mz.Beendet) await _offenIndex.SetzeFristAsync(korrelation, null, ct);
                    else await _offenIndex.MarkiereBeendetAsync(korrelation, ct);
                }
                catch (Exception ex) { Console.WriteLine($"[Prozess-Frist] Aufräumen fehlgeschlagen ({korrelation}): {ex.Message}"); }
            }
            return;
        }
        _akteurVon[korrelation] = mz.Akteur;

        var (kandidaten, ein) = await FaltMarkingMitCursorAsync(korrelation, mz, regeln, vomBackstop, quelle, quellVersion, vorgang, ct);
        await PflegeFristAsync(korrelation, mz, kandidaten, vomBackstop, ct);

        // ── Treiber-Fold (EM-1, §4/§7.3): einen im Fold gesehenen Fehlschlag DURABEL machen. Eine
        //   KommandoAbgelehnt-Marke auf dem Ziel-Stream (AbgelehntDa) wird zu SchrittGescheitert im Manager-Log —
        //   die Quelle der Fehlschlag-Erkennung wandert von der Ziel-Quittung in den Fold. Das MUSS vor dem
        //   Vorwärts/Kompensations-Split passieren: sonst läse der Vorwärtszweig den Marker als „aufgelöst"
        //   (ErgebnisDa) und schriebe fälschlich ProzessBeendet(true) (der stille Falsch-Erfolg aus §4).
        //   Idempotent gegen die (in Scheibe A noch aktive) Quittung: nur Vorgänge, die NICHT schon in
        //   mz.Gescheitert stehen, werden gestempelt; ein Doppel-Stempel unterbleibt.
        var neuAbgelehnt = kandidaten
            .Where(k => k.AbgelehntDa && !mz.Gescheitert.ContainsKey(k.Vorgang) && !mz.Umgeleitet.ContainsKey(k.Vorgang))
            .GroupBy(k => k.Vorgang)
            .Select(g => (Kandidat: g.First(), Art: "abgelehnt", Grund: g.First().AbgelehntGrund))
            .ToList();
        // ── Zeitlimit: ein offener Aufruf (kein Ergebnis), dessen Limit seit der Aktivierung abgelaufen ist, scheitert
        //   wie eine Ablehnung. Die Aktivierung ist das jüngste gematchte Event (DB-Zeit, aus dem Log) — kein eigener
        //   Timer-Zustand, der §3-Backstop weckt offene Prozesse ohnehin periodisch. Nur wenn ein Limit offen ist, wird
        //   die Uhr gelesen.
        if (mz.Gescheitert.Count == 0 && kandidaten.Any(k => !k.ErgebnisDa && k.Regel.Zeitlimit is not null))
        {
            var jetzt = _jetzt is null ? DateTimeOffset.UtcNow : await _jetzt(ct);
            foreach (var k in kandidaten.Where(k => !k.ErgebnisDa && k.Regel.Zeitlimit is not null && k.Bereit != default))
                if (jetzt >= k.Bereit + k.Regel.Zeitlimit!.Value && !neuAbgelehnt.Any(n => n.Kandidat.Vorgang == k.Vorgang))
                    neuAbgelehnt.Add((k, "zeitlimit", $"Zeitlimit ({k.Regel.Zeitlimit.Value}) für {k.Ziel}"));
        }
        if (neuAbgelehnt.Count > 0)
        {
            var v = mz.Version;
            foreach (var (k, art, grundA) in neuAbgelehnt)
            {
                // Pipeline-Fluss (§14): ist der passende Fehler-Port des Knotens verdrahtet, wird der Fehlschlag zum Token
                //   (eigener Weg) statt den Vorgang scheitern zu lassen.
                var umleiten = k.Regel.Knoten is int knoten &&
                    (art == "zeitlimit" ? regeln.UmleitenZeitlimit : regeln.UmleitenAbgelehnt).Contains(knoten);
                // Spur: der Fehlschlag gehört dem Knoten bzw. der Regel, deren Aufruf scheiterte (⏳/✕ an dieser Karte).
                using (Spur.Von(SpurVon(k, regeln).Ursache, regeln.Wirker(k.RegelIndex)))
                    await AppendAsync(korrelation, v,
                        umleiten ? new SchrittUmgeleitet(k.Vorgang, art, grundA) : new SchrittGescheitert(k.Vorgang, grundA), ct);
                v++;
            }
            // Frisch falten: mz.Gescheitert (bzw. die Umleitung) trägt den Fehlschlag jetzt.
            await WakeAsync(korrelation, ct, vomBackstop, quelle, quellVersion, vorgang);
            return;
        }

        if (mz.Gescheitert.Count == 0)
        {
            var offen = kandidaten.Where(k => !k.ErgebnisDa).ToList();

            // ── Funktionen: ALLE offenen Aufträge beauftragen (parallel). Ein schon laufender oder erledigter Auftrag
            //   verpufft beim Ausführer (Dedupe über den Vorgang = Ausführungs-Id) — deshalb ist das bei jeder Weckung
            //   gefahrlos und heilt zugleich einen Auftrag, dessen Ausführer verloren ging.
            foreach (var k in offen.Where(k => k.Auftrag is not null))
                await BeauftrageAsync(korrelation, k.Auftrag!, k.Vorgang, SpurVon(k, regeln), ct);

            // ── Commands: die erste noch nicht erledigte Transition feuern (sequenziell) ──
            var pending = offen.FirstOrDefault(k => k.Cmd is not null);
            if (pending != null)
            {
                await FeuereAsync(korrelation, pending.Cmd!, pending.Vorgang, SpurVon(pending, regeln), ct);
                return;
            }
            // Nur noch laufende Funktionen → warten; der Ausführer weckt mit dem Ergebnis (Backstop als Netz).
            if (offen.Count > 0) return;
            // Keine offene Transition, kein Fehler → Erfolg terminal.
            await AppendAsync(korrelation, mz.Version, new ProzessBeendet(true, ""), ct);
            return;
        }

        // ── Kompensation: die Erfolgs-Transitionen mit Gegenzug rückwärts (reverse Regel-Reihenfolge) ausgleichen ──
        var (komp, unvollziehbar) = await NächsteKompensationAsync(korrelation, kandidaten, mz.Gescheitert, ein, ct);
        if (CursorAktiv && _dirty.TryGetValue(korrelation, out var dirtyK)) dirtyK.ExceptWith(ein.MitErgebnis);
        if (komp is not null)
        {
            await FeuereAsync(korrelation, komp.Cmd, komp.Vorgang, SpurVon(komp.Von, regeln), ct);
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
    /// PF-4: die Frist im Offen-Index — bis wann der Backstop diesen Vorgang wecken muss, auch wenn kein Event kommt: das früheste
    /// Zeitlimit eines offenen Aufrufs bzw. Warte-Knotens, für einen offenen Warte-Knoten spätestens <see cref="WarteTakt"/> (er liest seinen
    /// Strom nur bei einer Weckung). Geschrieben wird sie erst, wenn der Vorgang still wird: bei einer Weckung durch den Backstop
    /// (<paramref name="vomBackstop"/>: fällige Frist oder Heilung nach <see cref="Heilplan.Anfang"/> Stille) — ein Vorgang, der vorher fertig
    /// wird, schreibt nie eine. Sofort geschrieben wird nur, was kürzer ist als diese Stille (ein kurzes Zeitlimit, ein Warte-Knoten), sonst
    /// litte die Genauigkeit. Je Aktivierung wird nur ein geänderter Wert geschrieben (auch <c>null</c>: keine abgelaufene Frist bleibt stehen).
    /// Best-effort wie der Index.
    /// </summary>
    private async Task PflegeFristAsync(
        Guid korrelation, ManagerStatus mz, List<Kandidat> kandidaten, bool vomBackstop, CancellationToken ct)
    {
        if (_offenIndex is null) return;
        var offen = mz.Gescheitert.Count == 0 ? kandidaten.Where(k => !k.ErgebnisDa && k.Bereit != default).ToList() : new List<Kandidat>();
        var kurz = offen.Any(k => k.Regel.WartetAuf.Count > 0 || k.Regel.Zeitlimit < Heilplan.Anfang);
        if (!vomBackstop && !kurz) return;

        DateTimeOffset? frist = null;
        foreach (var k in offen.Where(k => k.Regel.Zeitlimit is not null))
            frist = Früher(frist, k.Bereit + k.Regel.Zeitlimit!.Value);
        if (offen.Any(k => k.Regel.WartetAuf.Count > 0))
            frist = Früher(frist, (_jetzt is null ? DateTimeOffset.UtcNow : await _jetzt(ct)) + WarteTakt);
        if (_frist.TryGetValue(korrelation, out var alt) && alt == frist) return;
        try
        {
            await _offenIndex.SetzeFristAsync(korrelation, frist, ct);
            _frist[korrelation] = frist;
        }
        catch (Exception ex) { Console.WriteLine($"[Prozess-Frist] Schreiben fehlgeschlagen ({korrelation}): {ex.Message}"); }

        static DateTimeOffset? Früher(DateTimeOffset? a, DateTimeOffset b) => a is null || b < a ? b : a;
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
                AggregateId = unvollziehbar.Cmd.Ziel,
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
    private Task FeuereAsync(Guid korrelation, ICommand cmd, Guid vorgang, Spur.Kopf spur, CancellationToken ct)
    {
        // Spur: der Command trägt Ursache (jüngster echter Match-Token) und Wirker (die Regel bzw. der Fluss-Knoten) — der Emit liest sie
        //   synchron beim Bauen des Envelopes, das Aggregat stempelt die Ursache an seine Events.
        using var spurScope = Spur.Von(spur.Ursache, spur.Wirker);
        // Im Auftrag des Auslöser-Akteurs (der Emit liest ImAuftrag synchron beim Bauen des Envelopes).
        using var imAuftrag = ImAuftrag.IstAkteur(_akteurVon.GetValueOrDefault(korrelation)) ? ImAuftrag.Von(_akteurVon[korrelation]!) : null;
        // ★ P5b: das befeuerte Ziel ist ab jetzt „dirty" — die nächste Weckung MUSS genau diesen Stream nachfalten
        //   (dort erscheint das Ergebnis der Transition). Alles andere trägt der HOT-Cache.
        if (CursorAktiv)
        {
            if (!_dirty.TryGetValue(korrelation, out var set)) { set = new HashSet<Guid>(); _dirty[korrelation] = set; }
            set.Add(cmd.Ziel);
            if (!_gefeuert.TryGetValue(korrelation, out var jeStream)) _gefeuert[korrelation] = jeStream = new Dictionary<Guid, HashSet<Guid>>();
            if (!jeStream.TryGetValue(cmd.Ziel, out var vorgaenge)) jeStream[cmd.Ziel] = vorgaenge = new HashSet<Guid>();
            vorgaenge.Add(vorgang);
        }
        return _dispatch(korrelation, cmd, vorgang, ct);
    }

    // ── Beauftragen: Funktions-Auftrag an den Ausführer (fire-and-forget, ohne Selbst-Weckung) ──
    // Der Ausführungs-Stream IST der Vorgang: dort schreibt der Ausführer genau ein Ergebnis (OCC auf Version 0) mit
    // CausationId == Vorgang — der Fold liest es wie das Event eines Aggregats.
    private Task BeauftrageAsync(Guid korrelation, IAuftrag auftrag, Guid vorgang, Spur.Kopf spur, CancellationToken ct)
    {
        // Spur: der Auftrag nimmt die Ursache mit; das Ergebnis schreibt die Funktion (Wirker funktion:F, FunktionsAusfuehrer).
        using var spurScope = Spur.Von(spur.Ursache, spur.Wirker);
        if (_rufe is null)
            throw new InvalidOperationException(
                $"Der Prozess ruft eine Funktion ({auftrag.GetType().Name}), aber es ist kein Funktions-Ausführer verdrahtet (AddFunktionen).");
        if (CursorAktiv)
        {
            if (!_dirty.TryGetValue(korrelation, out var set)) { set = new HashSet<Guid>(); _dirty[korrelation] = set; }
            set.Add(vorgang);
        }
        var akteur = _akteurVon.GetValueOrDefault(korrelation);
        return _rufe(korrelation, auftrag, vorgang, ImAuftrag.IstAkteur(akteur) ? akteur : null, ct);
    }

    // ── Marking falten (Fixpunkt über die Ziel-Streams) ──

    /// <summary>Ein Token = ein Event-Payload plus seine Herkunft (Stream/Version), für Vorgang-Ableitung + Join.</summary>
    private sealed record Token(IEvent Payload, Guid Stream, int Version, DateTimeOffset Zeit, int Herkunft, JeTeile Teile) : IFlussToken;

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
    /// Genau eines von <paramref name="Cmd"/> (Aggregat) / <paramref name="Auftrag"/> (Funktion) ist gesetzt.
    /// <paramref name="Bereit"/> = Aktivierung (jüngstes gematchtes Event, DB-Zeit) — die Basis des Zeitlimits.
    /// </summary>
    private sealed record Kandidat(
        Regel Regel, int RegelIndex, IReadOnlyList<Token> Match, ICommand? Cmd, IAuftrag? Auftrag, Guid Vorgang,
        bool ErgebnisDa, bool WirkungDa, bool AbgelehntDa, string AbgelehntGrund, DateTimeOffset Bereit, JeTeile Teile)
    {
        public string Ziel => Cmd?.GetType().Name ?? Auftrag?.GetType().Name ?? "Warten auf " + string.Join("|", Regel.WartetAuf.Select(t => t.Name));
    }

    /// <summary>
    /// P5b-Einstieg: entscheidet Voll-Fold vs. inkrementellen Cursor-Fold und pflegt den Marking-Cache. Ist der
    /// Cursor inaktiv (kein Store), ist das exakt der frühere Voll-Fold (frisches, leeres Marking je Weckung → jeder
    /// Ziel-Stream ab 0). Ist er aktiv, faltet der Manager auf einem fortgeschriebenen Marking weiter (Tail-Read) und
    /// schreibt es best-effort fort. Beide Wege liefern per Konstruktion DIESELBEN Kandidaten (der Fold ist
    /// identisch; nur die Startbedingung — leeres vs. fortgeschriebenes Marking + Cursor — unterscheidet sie).
    /// </summary>
    private async Task<(List<Kandidat> Kandidaten, Einarbeitung Einarbeitung)> FaltMarkingMitCursorAsync(
        Guid korrelation, ManagerStatus mz, ProzessRegeln regeln, bool vomBackstop, Guid quelle, int quellVersion, Guid vorgang,
        CancellationToken ct)
    {
        if (!CursorAktiv)
        {
            // Voll-Fold: frisches, leeres Marking → jeder Ziel-Stream ohne Cursor (ab dem Beginn des Vorgangs).
            var (_, kandidatenVoll, einVoll) = await FalteAsync(korrelation, mz, regeln, new MarkingKompakt(), nurDirty: null, nieGeseheneLeer: false,
                hinweis: null, ct);
            return (kandidatenVoll, einVoll);
        }

        var regelHash = ProzessRegelHash.Berechne(regeln);
        var (marking, warm, frisch) = await HoleMarkingAsync(korrelation, regelHash, ct);

        // WARM (Hot-Cache): nachfalten, was sich seit dem letzten Fold geändert haben kann —
        //   • nach einem Send/Funktions-Ergebnis den Ziel-Stream (nichts, wenn der Ausgang des Vorgangs schon gefaltet ist);
        //   • weckt der Router wegen eines Events, dessen Stream (nichts, wenn der Cursor schon so weit ist);
        //   • ohne Quelle die Streams, in die gefeuert wurde und deren Ergebnis aussteht (dirty), und nie gesehene (in einem frischen Vorgang
        //     sind die sicher leer).
        //   Nennt die Weckung nach einem Send die Version, an der der Send den Ausgang geschrieben hat (quellVersion > 0), liest der Fold den
        //   Ziel-Stream erst ab dort (Hinweis) — nicht ab seinem Cursor, der in einem viel beschriebenen Stream weit hinten stehen kann.
        // KALT (Store-Load/neu) und Backstop: voll lesen — die Wahrheit rekonstruieren (nurDirty = null).
        var dirty = _dirty.TryGetValue(korrelation, out var d) ? d : (_dirty[korrelation] = new HashSet<Guid>());
        var nichts = new HashSet<Guid>();
        HashSet<Guid>? nurDirty = !warm || vomBackstop ? null
            : quelle == Guid.Empty ? dirty
            : vorgang != Guid.Empty ? (marking.Vorgänge.ContainsKey(vorgang.ToString()) ? nichts : new HashSet<Guid> { quelle })
            : marking.StreamCursor.TryGetValue(quelle, out var c) && c >= quellVersion ? nichts
            : new HashSet<Guid> { quelle };
        (Guid Stream, int Ab)? hinweis = nurDirty is not null && vorgang != Guid.Empty && quellVersion > 0 && quelle != Guid.Empty
            ? (quelle, quellVersion) : null;
        var (_, kandidaten, ein) = await FalteAsync(korrelation, mz, regeln, marking, nurDirty, nieGeseheneLeer: frisch, hinweis, ct);
        // Aus „dirty“ fällt nur, wessen Ergebnis jetzt gefaltet ist: ein zu früh gelesener Ziel-Stream (die Weckung eines anderen Vorgangs kam
        //   vor dem Ergebnis) bleibt, bis es da ist — sonst fände ein Noop oder eine Ablehnung (kein Signal) nur noch der Backstop.
        dirty.ExceptWith(ein.MitErgebnis);

        // Vom Backstop geweckt heißt: der Vorgang ist still geworden — sein Stand geht jetzt durabel, damit eine spätere kalte Weckung
        //   (Passivierung, Neustart) nicht wieder vom Beginn liest.
        await SchreibeMarkingAsync(korrelation, regelHash, mz.Version, marking, frisch, sofort: vomBackstop, ct);
        return (kandidaten, ein);
    }

    /// <summary>
    /// Der EINE Fold — Voll wie inkrementell. Er liest je Ziel-Stream ab <c>StreamCursor[s]+1</c> (fehlt der Cursor → ab dem Beginn des
    /// Vorgangs, <see cref="Einarbeitung"/>) und arbeitet die gelesenen Events in das <paramref name="marking"/> ein (mutierend): je Kausalität
    /// (Vorgang) die drei Achsen aufgelöst/Wirkung/abgelehnt + der von einer Wirkung erzeugte Downstream-Token.
    /// Der Fixpunkt zieht dann — rein in-memory, ohne weitere I/O — aus dem Marking die Kandidaten und die Tokens.
    ///
    /// Äquivalenz: Ziel-Streams sind append-only, Cursor rücken nur vor. Ein auf einem Präfix gefaltetes Marking +
    /// der Tail darauf ergibt dieselben akkumulierten Achsen wie ein Fold aller Events ab 0 (Monotonie) → dieselben
    /// Kandidaten, dieselbe Feuer-Entscheidung. Ein leeres <paramref name="marking"/> ist der Voll-Fold ab 0.
    ///
    /// <paramref name="hinweis"/>: in diesem Stream steht der Ausgang eines Vorgangs ab dieser Version (der Send hat ihn dort geschrieben). Der
    /// Fold liest ihn ab dort; die Events eines Befehls stehen zusammenhängend, also ist das erste gelesene eigene Event auch seine erste Wirkung.
    /// Die übersprungene Lücke kann Ausgänge anderer, noch offener Vorgänge derselben Korrelation enthalten — den Cursor rückt der Fold deshalb
    /// nur darüber, wenn dort nichts mehr aussteht; sonst bleibt er stehen, und ein späterer Read ohne Hinweis liest die Lücke (Wiederholung
    /// verpufft: die Marken sind idempotent).
    /// </summary>
    private async Task<(List<Token> Tokens, List<Kandidat> Kandidaten, Einarbeitung Einarbeitung)> FalteAsync(
        Guid korrelation, ManagerStatus mz, ProzessRegeln regeln, MarkingKompakt marking,
        HashSet<Guid>? nurDirty, bool nieGeseheneLeer, (Guid Stream, int Ab)? hinweis, CancellationToken ct)
    {
        var ein = new Einarbeitung(this, korrelation, marking, nurDirty, nieGeseheneLeer, hinweis);

        // Wurzel-Token: das Auslöse-Event. Einmalig gelesen und im Marking zwischengehalten (unveränderlich).
        if (marking.AuslöserPayload is null)
        {
            var auslöserEnv = await _store.ReadEventAsync(mz.AuslöserStream, mz.AuslöserVersion, ct);
            marking.AuslöserPayload = auslöserEnv?.Payload;
            marking.AuslöserZeit = auslöserEnv?.CreatedAtUtc ?? default;
        }

        var tokens = new List<Token>();
        if (marking.AuslöserPayload is not null)
        {
            tokens.Add(new Token(marking.AuslöserPayload, mz.AuslöserStream, mz.AuslöserVersion, marking.AuslöserZeit,
                regeln.QuellKnoten ?? -1, JeTeile.Leer));
            // Pipeline-Fluss (§14): der zweite Ausgang der Quelle — ihr Strom (quelle.Strom()). Version −2 hält den Token-Schlüssel
            //   (Stream, Version) vom Auslöser-Token getrennt; die echte Version trägt der Payload.
            if (regeln.QuellKnoten is int qk)
                tokens.Add(new Token(new QuellStrom(mz.AuslöserStream, mz.AuslöserVersion), mz.AuslöserStream, -2, marking.AuslöserZeit,
                    qk, JeTeile.Leer));
        }

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
                foreach (var match in FlussBelegung.Belegungen(regel, schnappschuss))
                {
                    var payloads = match.Select(t => (IEvent)t.Payload).ToList();
                    var bereit = match.Any(t => t.Zeit == default) ? default : match.Max(t => t.Zeit);
                    // Pipeline-Fluss (§14): ein WARTEN ruft nichts — es liest den Strom der Quelle nach dem ersten passenden Event.
                    if (regel.WartetAuf.Count > 0)
                    {
                        var (k, neu) = await WarteAsync(korrelation, regel, ri, match, bereit, mz, marking, ct);
                        kandidaten.Add(k);
                        if (neu is not null && !tokens.Any(t => t.Stream == neu.Stream && t.Version == neu.Version && t.Herkunft == neu.Herkunft))
                        {
                            tokens.Add(neu);
                            geändert = true;
                        }
                        continue;
                    }
                    // Ein Aufruf je Ausgang: Command an ein Aggregat (Sende) ODER Auftrag an eine Funktion (Ruft).
                    var aufrufe = regel.Sende is not null
                        ? regel.Sende(payloads).Select(c => ((object)c, c.GetType().Name)).ToList()
                        : regel.Ruft!(payloads).Select(a => ((object)a, a.GetType().Name)).ToList();
                    foreach (var ((ausgang, typName), ci) in aufrufe.Select((c, i) => (c, i)))
                    {
                        var cmd = ausgang as ICommand;
                        var auftrag = ausgang as IAuftrag;
                        var primär = match[0];
                        // ★ Befund 7/8: RegelIndex (ri) + Instanz-Index (ci) in den Diskriminator → zwei Regeln
                        //   mit gleichem Auslöser/Command/Ziel kollidieren nicht (8); Fan-out an DASSELBE Ziel
                        //   bekommt distinkte Vorgänge (7). Deterministisch (Sende ist rein, Ordnung stabil).
                        //   Ein Funktions-Auftrag hat kein Ziel-Aggregat: sein Diskriminator ist der Regel-/Instanz-Index.
                        var vorgang = ProzessId.FürTransition(
                            korrelation, primär.Stream, primär.Version, typName,
                            cmd is not null ? $"{ri}:{ci}:{cmd.Ziel:N}" : $"{ri}:{ci}:auftrag");
                        // Ziel-Stream: das Aggregat des Commands bzw. der Ausführungs-Stream (= Vorgang) der Funktion.
                        var zielStream = cmd?.Ziel ?? vorgang;

                        // Den Ziel-Stream bis Head einarbeiten (Tail-Read bei aktivem Cursor, ab 0 beim Voll-Fold),
                        // DANN die Achsen aus dem Marking lesen — statt den Stream bei jeder Weckung neu zu scannen.
                        // ★ Feuer-gerichtet (warm): NUR befeuerte (dirty) oder nie-gesehene Streams lesen; für alle
                        //   anderen trägt das gecachte Marking die Wahrheit (ihr Ergebnis kann sich nicht geändert
                        //   haben). nurDirty == null (Kaltstart/Voll-Fold) → jeden Stream lesen (Fallback).
                        if (ein.MussLesen(zielStream))
                            await ein.IntegriereAsync(zielStream, ct);
                        var marke = marking.Vorgänge.GetValueOrDefault(vorgang.ToString());
                        var aufgeloest = marke is not null;                 // irgendein Ziel-Event mit dieser Kausalität
                        var wirkung = marke?.Wirkung ?? false;               // ein Domänen-Event → kompensierbar + Join
                        var abgelehnt = marke?.Abgelehnt ?? false;           // KommandoAbgelehnt-Marke → SchrittGescheitert
                        var abgelehntGrund = marke?.Grund ?? "abgelehnt";
                        var teile = FlussBelegung.TeileDesAufrufs(regel, match, ci);
                        var herkunft = regel.Knoten ?? -1;

                        // Pipeline-Fluss (§14): ein umgeleiteter Fehlschlag (verdrahteter ⏳/✕-Port) ist aufgelöst; er liefert
                        //   statt der (evtl. später doch eintreffenden) Wirkung genau ein Fehler-Token an seinem Port.
                        if (mz.Umgeleitet.TryGetValue(vorgang, out var umleitung))
                        {
                            aufgeloest = true; wirkung = false; abgelehnt = false;
                            if (!tokens.Any(t => t.Stream == vorgang && t.Version == -1))
                            {
                                IEvent fehler = umleitung.Art == "zeitlimit"
                                    ? new ZeitlimitAbgelaufen(umleitung.Grund)
                                    : new SchrittAbgelehnt(umleitung.Grund);
                                tokens.Add(new Token(fehler, vorgang, -1, umleitung.Zeit, herkunft, teile));
                                geändert = true;
                            }
                        }

                        kandidaten.Add(new Kandidat(
                            regel, ri, match, cmd, auftrag, vorgang,
                            aufgeloest, wirkung, abgelehnt, abgelehntGrund, bereit, teile));

                        // Nur eine WIRKUNG bringt ein neues Token in den Fold (aktiviert Downstream-Joins). Eine
                        // reine Marke (Noop/Ablehnung) ist inert — sie darf keinen Join scharf schalten.
                        if (wirkung && marke!.TokenPayload is not null &&
                            !tokens.Any(t => t.Stream == marke.TokenStream && t.Version == marke.TokenVersion))
                        {
                            tokens.Add(new Token(marke.TokenPayload, marke.TokenStream, marke.TokenVersion, marke.TokenZeit, herkunft, teile));
                            geändert = true;
                        }
                    }
                }
            }
        }
        return (tokens, kandidaten, ein);
    }

    /// <summary>
    /// Das Einarbeiten der Ziel-Streams EINER Weckung in das Marking — geteilt vom Fold und von der Kompensation (dieselben Regeln, derselbe
    /// Cursor). Je Stream höchstens ein Read je Weckung; er liest nur die eigenen Events (Korrelation) ab dem Cursor, ab einem Hinweis oder —
    /// ohne Cursor — ab dem Beginn des Vorgangs (der Store grenzt das ab, <see cref="IEventStoreRepository.ReadKorreliertAsync"/>).
    /// </summary>
    private sealed class Einarbeitung(
        ProzessManager m, Guid korrelation, MarkingKompakt marking, HashSet<Guid>? nurDirty, bool nieGeseheneLeer, (Guid Stream, int Ab)? hinweis)
    {
        private readonly HashSet<Guid> _integriert = new();
        private readonly string _eigeneKorrelation = korrelation.ToString();

        public MarkingKompakt Marking => marking;
        /// <summary>Streams, in denen diese Weckung ein eigenes Event fand.</summary>
        public HashSet<Guid> MitErgebnis { get; } = new();

        /// <summary>
        /// Muss der Stream in dieser Weckung gelesen werden? Kalt bzw. vom Backstop jeder; warm nur befeuerte (dirty) oder — außer in einem
        /// frischen Vorgang, wo sie sicher leer sind — nie gesehene; für alle anderen trägt das Marking die Wahrheit (ihr Ergebnis kann sich ohne
        /// Feuern dorthin nicht geändert haben).
        /// </summary>
        public bool MussLesen(Guid s)
            => nurDirty is null || nurDirty.Contains(s) || (!nieGeseheneLeer && !marking.StreamCursor.ContainsKey(s));

        public async Task IntegriereAsync(Guid s, CancellationToken ct)
        {
            if (!_integriert.Add(s)) return;
            var abCursor = marking.StreamCursor.TryGetValue(s, out var c) ? c + 1 : 0;
            var von = hinweis is { } h && h.Stream == s && h.Ab > abCursor ? h.Ab : abCursor;
            // Nur die eigenen Events: jede Wirkung, Noop- und Ablehnungs-Marke eines Befehls dieses Vorgangs trägt seine Korrelation (der
            //   Manager setzt sie beim Feuern, der Ausführer bei Funktionsergebnissen). In einem viel beschriebenen Ziel-Stream (ein Posten,
            //   auf den Hunderte Vorgänge reservieren) waren die meisten gelesenen Events fremd — gelesen, ausgepackt, verworfen.
            var gelesen = await m._store.ReadKorreliertAsync(s, von, _eigeneKorrelation, korrelation, ct);
            var maxV = marking.StreamCursor.TryGetValue(s, out var alt) ? alt : -1;
            if (gelesen.Events.Count > 0) MitErgebnis.Add(s);
            foreach (var e in gelesen.Events)   // aufsteigend (Vertrag: geordnet) → „erste Wirkung" = niedrigste Version
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
                    vm.TokenZeit = e.CreatedAtUtc;
                }
                // sonst (KommandoVerarbeitet-Noop u. a. IProzessIntern): nur „aufgelöst" (Schlüssel-Präsenz).
            }
            // Der Stream ist jetzt bis zu seinem Kopf gesehen: alles Eigene bis dorthin war im Read (ein sichtbares Event hat keine unsichtbaren
            //   davor), fremde Events dahinter liest die nächste Weckung nicht noch einmal. Nach einem Read ab Hinweis nur, wenn in der Lücke davor
            //   nichts Eigenes mehr aussteht.
            if (von == abCursor || LueckeErledigt(s))
                marking.StreamCursor[s] = Math.Max(maxV, Math.Max(gelesen.Kopf, Math.Max(von - 1, 0)));
        }

        // In einem frischen Vorgang kam jedes eigene Event eines Ziel-Streams aus einem Feuern dieser Aktivierung dorthin: sind alle diese
        //   Vorgänge aufgelöst, steht in der Lücke nichts mehr, was der Fold nicht schon kennt.
        private bool LueckeErledigt(Guid s)
            => nieGeseheneLeer && m._gefeuert.TryGetValue(korrelation, out var jeStream) && jeStream.TryGetValue(s, out var vorgaenge)
               && vorgaenge.All(v => marking.Vorgänge.ContainsKey(v.ToString()));
    }

    /// <summary>
    /// Ein WARTE-Knoten (§14): das erste der erwarteten Events im Strom der Quelle NACH ihrer Version. Der Strom gehört nicht dem Vorgang (fremde
    /// Events, keine Korrelation): der Knoten merkt sich im Marking, bis wohin er ihn geprüft hat (<see cref="VorgangMarke.Gelesen"/>), und
    /// liest bei der nächsten Weckung nur dahinter — ein langes Warten auf einem viel beschriebenen Strom kostet so je Weckung nur das Neue, nicht
    /// alles seit dem Auslöser. Den Treffer hält die Marke wie eine Wirkung fest. Ein Event, das erst NACH dem Zeitlimit geschrieben wurde,
    /// zählt nicht: das Rennen gewinnt, wer zuerst da war (DB-Zeit); geprüfte Events zählen auch später nicht, der Zeiger darf über sie.
    /// Ein umgeleiteter Ablauf (⏳ verdrahtet) wird zum Token <see cref="ZeitlimitAbgelaufen"/>.
    /// </summary>
    private async Task<(Kandidat Kandidat, Token? Neu)> WarteAsync(
        Guid korrelation, Regel regel, int ri, IReadOnlyList<Token> match, DateTimeOffset bereit, ManagerStatus mz, MarkingKompakt marking,
        CancellationToken ct)
    {
        var primär = match[0];
        var strom = (QuellStrom)primär.Payload;
        var vorgang = ProzessId.FürTransition(korrelation, primär.Stream, primär.Version, "Warte", $"{ri}:0:{strom.Id:N}");
        var teile = FlussBelegung.TeileDesAufrufs(regel, match, 0);
        var herkunft = regel.Knoten ?? -1;

        if (mz.Umgeleitet.TryGetValue(vorgang, out var umleitung))
            return (new Kandidat(regel, ri, match, null, null, vorgang, true, false, false, "", bereit, teile),
                new Token(new ZeitlimitAbgelaufen(umleitung.Grund), vorgang, -1, umleitung.Zeit, herkunft, teile));

        var schluessel = vorgang.ToString();
        var marke = marking.Vorgänge.GetValueOrDefault(schluessel);
        if (marke is not { Wirkung: true })
        {
            var frist = regel.Zeitlimit is { } z && bereit != default ? bereit + z : (DateTimeOffset?)null;
            var ab = Math.Max(strom.Version, marke?.Gelesen ?? 0) + 1;
            var gelesen = await _store.ReadStreamAsync(strom.Id, ab, ct);
            var treffer = gelesen.FirstOrDefault(e => e.AggregateVersion >= ab
                && regel.WartetAuf.Any(t => t.IsInstanceOfType(e.Payload))
                && (frist is null || e.CreatedAtUtc <= frist));
            if (gelesen.Count > 0)
            {
                if (marke is null) marking.Vorgänge[schluessel] = marke = new VorgangMarke();
                marke.Gelesen = Math.Max(marke.Gelesen, gelesen.Max(e => e.AggregateVersion));
                if (treffer is not null)
                {
                    marke.Wirkung = true; marke.TokenStream = strom.Id; marke.TokenVersion = treffer.AggregateVersion;
                    marke.TokenPayload = treffer.Payload; marke.TokenZeit = treffer.CreatedAtUtc;
                }
            }
        }
        var da = marke is { Wirkung: true };
        var kandidat = new Kandidat(regel, ri, match, null, null, vorgang, da, da, false, "", bereit, teile);
        return (kandidat, da ? new Token(marke!.TokenPayload!, strom.Id, marke.TokenVersion, marke.TokenZeit, herkunft, teile) : null);
    }

    // ── P5b: Marking-Cache laden/schreiben (best-effort; ein Fehler kostet nur Tempo, nie Korrektheit) ──

    /// <summary>
    /// Holt das gefaltete Marking für die Weckung: erst der HOT-Cache dieser Instanz (Warm-Pfad, kein I/O), sonst
    /// der durable Store (Kaltstart). Passt der <paramref name="regelHash"/> nicht (Regeländerung) oder fehlt der
    /// Eintrag, startet ein LEERES Marking → Voll-Fold ab 0 (Fallback, Invariante 1).
    /// </summary>
    private async Task<(MarkingKompakt Marking, bool Warm, bool Frisch)> HoleMarkingAsync(Guid korrelation, string regelHash, CancellationToken ct)
    {
        // WARM: der HOT-Cache dieser Instanz hat das Marking in dieser Aktivierung schon gefaltet (oder der Vorgang begann in ihr) → es ist
        //   aktuell bis auf die seither befeuerten (dirty) Streams → feuer-gerichteter Read genügt.
        if (_hotMarking.TryGetValue(korrelation, out var hot) && hot.RegelHash == regelHash)
            return (hot.Marking, true, hot.Frisch);

        // KALT: aus dem durablen Store geladen ODER neu → könnte Tails verpasst haben (Passivierung) →
        //   diese Weckung VOLL falten (nurDirty = null), erst danach ist es warm.
        try
        {
            var doc = await _markingStore!.LadeAsync(korrelation, ct);
            if (doc is not null)
            {
                _markingDurabel.Add(korrelation);
                if (doc.RegelHash == regelHash) return (doc.Marking, false, false);
            }
        }
        catch (Exception ex)
        {
            _markingDurabel.Add(korrelation);   // unbekannt — das Ende räumt vorsichtshalber ab
            Console.WriteLine($"[Prozess-Marking] Laden fehlgeschlagen ({korrelation}): {ex.Message}");
        }

        return (new MarkingKompakt(), false, false);
    }

    /// <summary>
    /// Schreibt das Marking in den HOT-Cache (IMMER — er trägt die Korrektheit über die Weckungen) und
    /// gedrosselt (alle <see cref="_markingSchreibIntervall"/> Weckungen) durabel in den Store (best-effort).
    /// So bleibt der durable Write O(N²/K) statt O(N²) — ohne die Warm-Korrektheit anzutasten.
    /// </summary>
    private async Task SchreibeMarkingAsync(Guid korrelation, string regelHash, int logVersion, MarkingKompakt marking, bool frisch, bool sofort,
        CancellationToken ct)
    {
        _hotMarking[korrelation] = (regelHash, marking, frisch);

        var seit = _seitSchreib.GetValueOrDefault(korrelation) + 1;
        if (seit < _markingSchreibIntervall && !sofort) { _seitSchreib[korrelation] = seit; return; }
        _seitSchreib[korrelation] = 0;
        await SchreibeDurabelAsync(korrelation, regelHash, logVersion, marking, ct);
    }

    /// <summary>
    /// Vor dem Passivieren (der Actor stoppt): was diese Aktivierung seit dem letzten durablen Write gefaltet hat, durabel ablegen — sonst
    /// begänne die nächste Aktivierung eines noch offenen Vorgangs beim letzten Write (oder ohne einen beim Beginn des Vorgangs). Best-effort.
    /// </summary>
    public async Task SichereMarkingAsync(Guid korrelation, CancellationToken ct = default)
    {
        if (!CursorAktiv || _seitSchreib.GetValueOrDefault(korrelation) == 0 || !_hotMarking.TryGetValue(korrelation, out var hot)) return;
        _seitSchreib[korrelation] = 0;
        await SchreibeDurabelAsync(korrelation, hot.RegelHash, _status.GetValueOrDefault(korrelation)?.Version ?? 0, hot.Marking, ct);
    }

    private async Task SchreibeDurabelAsync(Guid korrelation, string regelHash, int logVersion, MarkingKompakt marking, CancellationToken ct)
    {
        _markingDurabel.Add(korrelation);
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

    /// <summary>
    /// Verwirft den Marking-Cache einer terminalen Korrelation (HOT + Store) — sie braucht ihn nie wieder. Im Store gelöscht wird nur, was dort
    /// liegen könnte: die meisten Vorgänge enden, bevor ihr Marking je durabel geschrieben wurde.
    /// </summary>
    private async Task VerwirfMarkingAsync(Guid korrelation, CancellationToken ct)
    {
        _hotMarking.Remove(korrelation);
        _seitSchreib.Remove(korrelation);
        _dirty.Remove(korrelation);
        _gefeuert.Remove(korrelation);
        if (_markingStore is null || !_markingDurabel.Remove(korrelation)) return;
        try { await _markingStore.LöscheAsync(korrelation, ct); }
        catch (Exception ex) { Console.WriteLine($"[Prozess-Marking] Löschen fehlgeschlagen ({korrelation}): {ex.Message}"); }
    }

    // Belegungen (Herkunft, Je-Teile, Sammeln) liegen im reinen Kern Abstractions.FlussBelegung — geteilt mit dem Prüfstand-Laufwerk (FlussLaufwerk, Cqrs.Testing).

    // ── Kompensation: reverse Regel-Reihenfolge über Erfolgs-Transitionen mit Gegenzug ──
    private sealed record Kompensation(ICommand Cmd, Guid Vorgang, Kandidat Von);

    /// <summary>
    /// Die Spur eines Aufrufs: Ursache = der jüngste ECHTE gematchte Log-Eintrag (synthetische Tokens wie Strom −2 oder Fehler-Port −1
    /// stehen nicht im Log und zählen nicht), Wirker = die Regel (<c>prozess:P#A→C</c>) bzw. der Fluss-Knoten (<c>fluss:P#knoten</c>).
    /// </summary>
    private static Spur.Kopf SpurVon(Kandidat k, ProzessRegeln regeln)
    {
        var t = k.Match.Where(t => t.Version > 0).OrderByDescending(t => t.Zeit).ThenByDescending(t => t.Version).FirstOrDefault();
        return new Spur.Kopf(t is null ? null : Spur.Ursache(t.Stream, t.Version), regeln.Wirker(k.RegelIndex));
    }

    /// <summary>
    /// Liefert den nächsten noch offenen Gegenzug ODER — wenn keiner mehr feuerbar ist — einen etwaigen
    /// <c>Unvollziehbar</c>-Gegenzug (ein Gegenzug, der SELBST abgelehnt wurde, sein Vorgang steht in
    /// <paramref name="gescheitert"/>). Der Aufrufer feuert <c>Naechste</c>, solange es einen gibt; sonst
    /// entscheidet <c>Unvollziehbar != null</c> zwischen sauberem Fehlschlag-Terminal und KlärungNötig (#12).
    /// </summary>
    private async Task<(Kompensation? Naechste, Kompensation? Unvollziehbar)> NächsteKompensationAsync(
        Guid korrelation, List<Kandidat> kandidaten, IReadOnlyDictionary<Guid, string> gescheitert, Einarbeitung ein, CancellationToken ct)
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
                    $"{k.RegelIndex}:{ci}:{cmd.Ziel:N}");
                // Schon ausgeglichen? Der Gegenzug ist erledigt, wenn sein Ergebnis auf dem Ziel-Stream liegt —
                // ABER nur ein Ergebnis, das KEINE Ablehnung ist (eine Wirkung ODER die KommandoVerarbeitet-Noop-
                // Marke). Eine KommandoAbgelehnt-Marke zählt NICHT als erledigt (der Gegenzug wurde abgelehnt).
                // Das Ergebnis trägt die Korrelation des Vorgangs wie jedes andere: es steht im Marking, sobald der Ziel-Stream eingearbeitet
                //   ist — nach denselben Regeln wie im Fold (Cursor, Hinweis, dirty), nie mehr der ganze Ziel-Stream ab 0.
                if (ein.MussLesen(cmd.Ziel)) await ein.IntegriereAsync(cmd.Ziel, ct);
                var marke = ein.Marking.Vorgänge.GetValueOrDefault(vorgang.ToString());
                var erledigt = marke is not null && (marke.Wirkung || !marke.Abgelehnt);
                if (erledigt) continue;
                // ★ Audit-Fix #12 + Treiber-Fold: Der Gegenzug wurde SELBST abgelehnt — als durable
                //   KommandoAbgelehnt-Marke auf dem Ziel-Stream (der Fold ersetzt die frühere Quittung; nach
                //   Entfall des Quittungs-Pfads ist der Marker die Wahrheit). NICHT neu feuern (sonst enger
                //   Kompensations-Livelock: „erledigt" wird er nie) — als unvollziehbar merken und weitersuchen.
                //   (gescheitert.ContainsKey bleibt als zweite Quelle stehen: harmlos, deckt Alt-/Boot-Zustände.)
                var abgelehnt = marke?.Abgelehnt ?? false;
                if (abgelehnt || gescheitert.ContainsKey(vorgang)) { unvollziehbar ??= new Kompensation(cmd, vorgang, k); continue; }
                return (new Kompensation(cmd, vorgang, k), unvollziehbar);
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
        /// <summary>Pipeline-Fluss: umgeleitete Fehlschläge je Vorgang (Art „zeitlimit“/„abgelehnt“, Grund, Log-Zeit).</summary>
        public IReadOnlyDictionary<Guid, (string Art, string Grund, DateTimeOffset Zeit)> Umgeleitet { get; init; }
            = new Dictionary<Guid, (string, string, DateTimeOffset)>();
        public bool Beendet { get; init; }
        public bool Erfolg { get; init; }
        /// <summary>Der Akteur, in dessen Auftrag der Prozess handelt (Auslöser-Event; null = keiner).</summary>
        public string? Akteur { get; init; }
    }

    /// <summary>Der Kopf-Zustand für eine Weckung: aus dem Cache, wenn er da ist und nicht <paramref name="frisch"/> gelesen werden soll.</summary>
    private async Task<ManagerStatus> StatusAsync(Guid korrelation, bool frisch, CancellationToken ct)
    {
        if (!frisch && _status.TryGetValue(korrelation, out var mz)) return mz;
        mz = await LadeStatusAsync(korrelation, ct);
        _status[korrelation] = mz;
        return mz;
    }

    /// <summary>Der Kopf-Zustand nach einem eigenen Append — dieselbe Faltung wie <see cref="LadeStatusAsync"/>, um ein Event weiter.</summary>
    private static ManagerStatus Fortgeschrieben(ManagerStatus mz, IEvent ereignis, string? akteur) => ereignis switch
    {
        ProzessGestartet g => new ManagerStatus
        {
            Gestartet = true, ProzessName = g.ProzessName, AuslöserStream = g.AuslöserStream, AuslöserVersion = g.AuslöserVersion,
            Version = mz.Version + 1, Gescheitert = mz.Gescheitert, Umgeleitet = mz.Umgeleitet, Beendet = mz.Beendet, Erfolg = mz.Erfolg,
            Akteur = ImAuftrag.IstAkteur(akteur) ? akteur : null,
        },
        SchrittGescheitert f => new ManagerStatus
        {
            Gestartet = mz.Gestartet, ProzessName = mz.ProzessName, AuslöserStream = mz.AuslöserStream, AuslöserVersion = mz.AuslöserVersion,
            Version = mz.Version + 1, Gescheitert = new Dictionary<Guid, string>(mz.Gescheitert) { [f.Vorgang] = f.Grund },
            Umgeleitet = mz.Umgeleitet, Beendet = mz.Beendet, Erfolg = mz.Erfolg, Akteur = mz.Akteur,
        },
        ProzessBeendet b => new ManagerStatus
        {
            Gestartet = mz.Gestartet, ProzessName = mz.ProzessName, AuslöserStream = mz.AuslöserStream, AuslöserVersion = mz.AuslöserVersion,
            Version = mz.Version + 1, Gescheitert = mz.Gescheitert, Umgeleitet = mz.Umgeleitet, Beendet = true, Erfolg = b.Erfolg, Akteur = mz.Akteur,
        },
        _ => throw new InvalidOperationException($"{ereignis.GetType().Name} wird nicht fortgeschrieben"),
    };

    public async Task<ManagerStatus> LadeStatusAsync(Guid korrelation, CancellationToken ct = default)
    {
        var log = await _store.ReadStreamAsync(korrelation, 0, ct);
        bool gestartet = false, beendet = false, erfolg = false;
        string name = "", grund = "";
        Guid auslöserStream = default;
        int auslöserVersion = 0;
        string? akteur = null;
        var gescheitert = new Dictionary<Guid, string>();
        var umgeleitet = new Dictionary<Guid, (string, string, DateTimeOffset)>();

        foreach (var env in log)
        {
            switch (env.Payload)
            {
                case ProzessGestartet g:
                    gestartet = true; name = g.ProzessName; auslöserStream = g.AuslöserStream; auslöserVersion = g.AuslöserVersion;
                    akteur = ImAuftrag.IstAkteur(env.UserId) ? env.UserId : null;
                    break;
                case SchrittGescheitert f:
                    gescheitert[f.Vorgang] = f.Grund;
                    break;
                case SchrittUmgeleitet u:
                    umgeleitet[u.Vorgang] = (u.Art, u.Grund, env.CreatedAtUtc);
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
            Version = log.Count, Gescheitert = gescheitert, Umgeleitet = umgeleitet, Beendet = beendet, Erfolg = erfolg, Akteur = akteur,
        };
    }

    private async Task AppendAsync(Guid korrelation, int erwarteteVersion, IEvent ereignis, CancellationToken ct, string? akteur = null,
        IReadOnlyList<Beilage>? beilagen = null)
    {
        // Das Ende nimmt den Eintrag des Offen-Index in derselben Transaktion mit (der Eintrag entsteht mit dem ProzessGestartet).
        if (_offenIndex is not null && ereignis is ProzessBeendet) beilagen = [new VorgangBeendet(korrelation)];
        var alt = _status.GetValueOrDefault(korrelation);
        _status.Remove(korrelation);   // bis der Append sicher durch ist; ein gescheiterter liest frisch
        await _store.AppendEventsAsync(korrelation, erwarteteVersion, new[] { ereignis }, aggregateType: "ProzessManager", akteur: akteur,
            beilagen: beilagen);
        if (alt is not null && alt.Version == erwarteteVersion && ereignis is not SchrittUmgeleitet)
            _status[korrelation] = Fortgeschrieben(alt, ereignis, akteur);

        // ★ P5b: der Marking-Cursor wird mit dem Terminal überflüssig — der Prozess wird nie wieder geweckt
        //   (die nächste Weckung faltet Beendet und kehrt sofort zurück). HOT + Store aufräumen (best-effort).
        if (CursorAktiv && ereignis is ProzessBeendet)
            await VerwirfMarkingAsync(korrelation, ct);
        if (ereignis is ProzessBeendet) _akteurVon.TryRemove(korrelation, out _);
    }
}
