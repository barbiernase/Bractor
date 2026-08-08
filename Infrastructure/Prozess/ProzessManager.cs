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
/// — kein <c>await</c> auf die Quittung im Turn, die (A)-Hang-Klasse ist strukturell weg). Die Bestätigung
/// kommt später als korreliertes Ziel-Event, das neu weckt; eine Ablehnung kommt über
/// <see cref="NotiereFehlschlagAsync"/> zurück (der Transport-Seam meldet die negative Quittung).
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

    public ProzessManager(
        IEventStoreRepository store,
        IReadOnlyDictionary<string, ProzessRegeln> registry,
        Func<Guid, ICommand, Guid, CancellationToken, Task> dispatch,
        IProzessOffenIndex? offenIndex = null,
        IDeadLetterSink? deadLetters = null)
    {
        _store = store;
        _registry = registry;
        _dispatch = dispatch;
        _offenIndex = offenIndex;
        _deadLetters = deadLetters;
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

        var (_, kandidaten) = await FaltMarkingAsync(korrelation, mz, regeln, ct);

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

    /// <summary>
    /// Der Transport-Seam meldet eine negative Ziel-Quittung: der Manager macht sie DURABEL (Spec §5.2) und
    /// treibt weiter (→ Kompensation). Idempotent — ein doppelt gemeldeter Fehlschlag verpufft.
    /// </summary>
    public async Task NotiereFehlschlagAsync(Guid korrelation, Guid vorgang, string grund, CancellationToken ct = default)
    {
        var mz = await LadeStatusAsync(korrelation, ct);
        if (mz.Beendet || mz.Gescheitert.ContainsKey(vorgang)) return;
        await AppendAsync(korrelation, mz.Version, new SchrittGescheitert(vorgang, grund), ct);
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
    /// </summary>
    private sealed record Kandidat(
        Regel Regel, int RegelIndex, IReadOnlyList<Token> Match, ICommand Cmd, Guid Vorgang, bool ErgebnisDa, bool WirkungDa);

    private async Task<(List<Token> Tokens, List<Kandidat> Kandidaten)> FaltMarkingAsync(
        Guid korrelation, ManagerStatus mz, ProzessRegeln regeln, CancellationToken ct)
    {
        var cache = new Dictionary<Guid, IReadOnlyList<EventEnvelope>>();
        async Task<IReadOnlyList<EventEnvelope>> Lies(Guid s)
        {
            if (!cache.TryGetValue(s, out var evs)) { evs = await _store.ReadStreamAsync(s, 0, ct); cache[s] = evs; }
            return evs;
        }

        // Wurzel-Token: das Auslöse-Event (aus seinen im Log gemerkten Koordinaten).
        var tokens = new List<Token>();
        var auslöserEvents = await Lies(mz.AuslöserStream);
        var auslöser = auslöserEvents.FirstOrDefault(e => e.AggregateVersion == mz.AuslöserVersion);
        if (auslöser is not null)
            tokens.Add(new Token(auslöser.Payload, mz.AuslöserStream, mz.AuslöserVersion));

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
                    foreach (var cmd in cmds)
                    {
                        var primär = match[0];
                        var vorgang = ProzessId.FürTransition(
                            korrelation, primär.Stream, primär.Version, cmd.GetType().Name, cmd.AggregateId.ToString("N"));

                        var zielEvents = await Lies(cmd.AggregateId);
                        // Ergebnis ↔ Transition per KAUSALITÄT: der Actor stempelt CausationId = CommandId = vorgang.
                        // „Aufgelöst": irgendein Ergebnis (auch die Inbox-Marke bei Noop/Ablehnung) → nicht neu feuern.
                        var aufgeloest = zielEvents.Any(e => e.CausationId == vorgang.ToString());
                        // „Wirkung": nur ein DOMÄNEN-Event (kein IProzessIntern) — kompensierbar + aktiviert Joins.
                        var wirkung = zielEvents.FirstOrDefault(e => e.CausationId == vorgang.ToString() && e.Payload is not IProzessIntern);
                        kandidaten.Add(new Kandidat(regel, ri, match, cmd, vorgang, aufgeloest, wirkung is not null));

                        // Nur eine WIRKUNG bringt ein neues Token in den Fold (aktiviert Downstream-Joins). Eine
                        // reine Marke (Noop/Ablehnung) ist inert — sie darf keinen Join scharf schalten.
                        if (wirkung is not null &&
                            !tokens.Any(t => t.Stream == cmd.AggregateId && t.Version == wirkung.AggregateVersion))
                        {
                            tokens.Add(new Token(wirkung.Payload, cmd.AggregateId, wirkung.AggregateVersion));
                            geändert = true;
                        }
                    }
                }
            }
        }
        return (tokens, kandidaten);
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
            foreach (var cmd in gegen)
            {
                var primär = k.Match[0];
                var vorgang = ProzessId.FürKompensation(
                    korrelation, primär.Stream, primär.Version, cmd.GetType().Name, cmd.AggregateId.ToString("N"));
                // Schon ausgeglichen? Der Gegenzug ist erledigt, wenn sein Ergebnis (Event mit
                // CausationId == diesem Kompensations-Vorgang) auf dem Ziel-Stream liegt.
                var zielEvents = await _store.ReadStreamAsync(cmd.AggregateId, 0, ct);
                var erledigt = zielEvents.Any(e => e.CausationId == vorgang.ToString());
                if (erledigt) continue;
                // ★ Audit-Fix #12: Der Gegenzug wurde SELBST abgelehnt (sein Vorgang steht in Gescheitert) →
                //   NICHT neu feuern (sonst enger Kompensations-Livelock: er wird nie „erledigt", weil eine
                //   Ablehnung weder Event noch Marke hinterlässt) — als unvollziehbar merken und weitersuchen.
                if (gescheitert.ContainsKey(vorgang)) { unvollziehbar ??= new Kompensation(cmd, vorgang); continue; }
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
    }
}
