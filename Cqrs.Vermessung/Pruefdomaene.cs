using System.Collections.Concurrent;
using System.Text.Json.Nodes;
using Abstractions;

namespace Cqrs.Vermessung;

/// <summary>
/// S5 Prüfdomäne: Vorgänge über den JSON-Eingang, im festen Muster je zehn — acht Aufträge, die gelingen (Diamant: die Auftragsabwicklung
/// reserviert den Posten und belegt das Kontingent, das Auftrags-Aggregat bestätigt am Join), ein Auftrag gegen ein volles Kontingent (Absage →
/// das Aggregat ordnet die Freigabe der Reservierung an, die Funktion sendet sie) und eine Verteilung (Fan-out auf vier Ziele, Zählung im
/// Verteilungs-Aggregat). Jede Reservierung unter dem Mindestbestand löst die Funktion <c>Nachschub</c> aus (auf den langlebigen Posten-Streams).
/// Sichtbar ist ein Vorgang mit seinem letzten Event im Log (Log-Zeit, Uhr-Versatz umgerechnet): <c>AuftragBestaetigt</c>, beim abgelehnten
/// Auftrag das <c>PostenFreigegeben</c> der Kompensation (seine Ursache liegt im Auftrags-Stream), bei der Verteilung <c>VerteilungAbgeschlossen</c>.
/// Die Prüfdomäne hat keine Akteure — das Tor ist offen.
/// </summary>
internal sealed class Pruefdomaene : ILast
{
    private const int Posten = 50, Ziele = 20;

    /// <summary>Der Wirker der Funktion Nachschub (Spur-Header <c>ueber</c> ihrer Zubuchungen).</summary>
    private const string NachschubWirker = "funktion:Nachschub";

    private readonly Eingang _eingang;
    private readonly TimeSpan _uhrversatz;
    private readonly Guid[] _posten = Enumerable.Range(0, Posten).Select(_ => Guid.NewGuid()).ToArray();
    private readonly Guid[] _kontingente = Enumerable.Range(0, Posten).Select(_ => Guid.NewGuid()).ToArray();
    private readonly Guid[] _ziele = Enumerable.Range(0, Ziele).Select(_ => Guid.NewGuid()).ToArray();
    private readonly Guid _voll = Guid.NewGuid();
    private readonly ConcurrentDictionary<Guid, Vorgang> _offen = new();   // Id des Vorgangs-Aggregats → Vorgang
    private readonly ConcurrentQueue<(Guid Id, DateTimeOffset Zeit)> _frueh = new();
    private readonly ConcurrentDictionary<string, DateTimeOffset> _nachschubAusloeser = new();   // "<Posten>:<Version>" → Log-Zeit der Reservierung
    private readonly ConcurrentBag<double> _nachschubMs = new();
    private long _n;

    public enum Art { AuftragOk, AuftragAbgelehnt, Verteilung }
    public ConcurrentBag<(Guid Id, Art Art, Guid[] Ziele)> Vorgaenge { get; } = new();

    public Pruefdomaene(Eingang eingang, LogStrom log, TimeSpan uhrversatz)
    {
        _eingang = eingang;
        _uhrversatz = uhrversatz;
        log.Beobachte(Sieh);
    }

    /// <summary>Von der auslösenden Reservierung bis zur Zubuchung der Funktion Nachschub (Log-Zeiten).</summary>
    public IReadOnlyCollection<double> NachschubMs => _nachschubMs;

    /// <summary>Störungslauf: gescheiterte Sendungen mit derselben CommandId wiederholen.</summary>
    public bool Wiederholen { get; init; }

    private void Sieh(LogZeile z)
    {
        switch (z.Typ)
        {
            case "auftrag_bestaetigt" or "verteilung_abgeschlossen":
                Sichtbar(z.Stream, z.Zeit + _uhrversatz);
                break;
            case "posten_freigegeben" when Abstractions.Spur.ZerlegeUrsache(z.Ursache, out var auftrag, out _):
                Sichtbar(auftrag, z.Zeit + _uhrversatz);
                break;
            case "posten_reserviert" when z.Daten != null && ((int?)JsonNode.Parse(z.Daten)!["Fehlmenge"] ?? 0) > 0:
                _nachschubAusloeser[$"{z.Stream}:{z.Version}"] = z.Zeit;
                break;
            case "posten_zugebucht" when z.Ueber == NachschubWirker && z.Ursache != null:
                if (_nachschubAusloeser.TryRemove(z.Ursache, out var ausgeloest)) _nachschubMs.Add((z.Zeit - ausgeloest).TotalMilliseconds);
                break;
        }
    }

    private void Sichtbar(Guid id, DateTimeOffset zeit)
    {
        if (_offen.TryRemove(id, out var v)) v.Sichtbar = zeit;
        else _frueh.Enqueue((id, zeit));
    }

    /// <summary>Posten (großer Bestand, Mindestbestand knapp darunter: fast jede Reservierung bestellt nach), Kontingente, Ziele.</summary>
    public async Task EinrichtenAsync()
    {
        var auftraege = new List<(string Typ, JsonObject Daten)>();
        foreach (var p in _posten) auftraege.Add(("EroeffnePosten", new JsonObject { ["AggregateId"] = p.ToString(), ["Bestand"] = 1_000_000, ["Mindestbestand"] = 999_990 }));
        foreach (var k in _kontingente) auftraege.Add(("RichteKontingentEin", new JsonObject { ["AggregateId"] = k.ToString(), ["Plaetze"] = 1_000_000_000 }));
        foreach (var z in _ziele) auftraege.Add(("EroeffnePosten", new JsonObject { ["AggregateId"] = z.ToString(), ["Bestand"] = 0 }));
        auftraege.Add(("RichteKontingentEin", new JsonObject { ["AggregateId"] = _voll.ToString(), ["Plaetze"] = 0 }));
        await Parallel.ForEachAsync(auftraege, new ParallelOptions { MaxDegreeOfParallelism = 16 }, async (a, ct) =>
        {
            var r = await _eingang.CommandAsync(a.Typ, a.Daten, null, ct: ct);
            if (!r.Ok) throw new InvalidOperationException($"S5 einrichten: {a.Typ} {r.Status} {r.Fehler}");
        });
    }

    public async Task StarteAsync(Vorgang v, CancellationToken ct)
    {
        var i = Interlocked.Increment(ref _n);
        var id = Guid.NewGuid();
        var art = (i % 10) switch { 8 => Art.AuftragAbgelehnt, 9 => Art.Verteilung, _ => Art.AuftragOk };
        var posten = _posten[i % Posten];
        string typ;
        JsonObject daten;
        Guid[] ziele = [];
        if (art == Art.Verteilung)
        {
            ziele = Enumerable.Range(0, 4).Select(j => _ziele[(int)((i / 10 * 4 + j) % Ziele)]).ToArray();
            typ = "StarteVerteilung";
            daten = new JsonObject
            {
                ["AggregateId"] = id.ToString(), ["QuelleId"] = posten.ToString(),
                ["Ziele"] = new JsonArray(ziele.Select(z => (JsonNode)z.ToString()).ToArray()), ["MengeJeZiel"] = 1,
            };
        }
        else
        {
            typ = "StarteAuftrag";
            daten = new JsonObject
            {
                ["AggregateId"] = id.ToString(), ["PostenId"] = posten.ToString(),
                ["KontingentId"] = (art == Art.AuftragAbgelehnt ? _voll : _kontingente[i % Posten]).ToString(), ["Menge"] = 1 + (int)(i % 3),
            };
        }
        Vorgaenge.Add((id, art, ziele));
        v.Start = DateTimeOffset.UtcNow;
        _offen[id] = v;
        // Ein Ende, das schneller im Log war als die Anmeldung hier: nachtragen.
        for (var n = _frueh.Count; n > 0 && _frueh.TryDequeue(out var f); n--)
            if (_offen.TryRemove(f.Id, out var w)) w.Sichtbar = f.Zeit; else _frueh.Enqueue(f);
        // Im Störungslauf ist der Host zeitweise weg: dann mit DERSELBEN CommandId wiederholen (Client-Garantie, docs/11 §11.3).
        var r = Wiederholen
            ? await _eingang.CommandMitWiederholungAsync(typ, daten, null, TimeSpan.FromMinutes(3), ct)
            : await _eingang.CommandAsync(typ, daten, null, ct: ct);
        if (!r.Ok) throw new InvalidOperationException($"{typ}: {r.Status} {r.Fehler}");
    }

    /// <summary>
    /// Exactly-once (§0.2), gezählt im Log, nicht am Mechanismus: je Auftrag genau ein Ausgang (bestätigt oder gescheitert, wie erwartet), jede
    /// Kompensation genau einmal (über den Beleg des Auftrags), je Verteilung jedes Ziel genau einmal und genau ein Abschluss, je Reservierung unter
    /// dem Mindestbestand genau ein Nachschub. Soll und Ist, die beide aus dem Log kommen, stehen in EINER Abfrage (ein Schnappschuss, VM-1).
    /// </summary>
    public async Task<List<Pruefung>> PruefeAsync(Datenbank db, bool vollstaendig = true)
    {
        var s = db.Schema;
        Pruefung P(string name, long soll, long ist) => Pruefung.Erwartet(name, soll, ist, vollstaendig);
        Guid[] Ids(Func<Art, bool> f) => Vorgaenge.Where(v => f(v.Art)).Select(v => v.Id).ToArray();
        var ok = Ids(a => a == Art.AuftragOk);
        var abgelehnt = Ids(a => a == Art.AuftragAbgelehnt);
        var auftraege = Ids(a => a != Art.Verteilung);
        var verteilungen = Vorgaenge.Where(v => v.Art == Art.Verteilung).ToArray();
        var vIds = verteilungen.Select(v => v.Id).ToArray();
        var beleg = "(data->'Beleg'->>'Nummer')::uuid";

        var p = new List<Pruefung>
        {
            P("je gelingendem Auftrag genau ein AuftragBestaetigt", ok.Length,
                await db.ZahlAsync($"select count(*) from {s}.mt_events where type = 'auftrag_bestaetigt' and stream_id = any(@a)", ("a", ok))),
            P("je abgelehntem Auftrag genau ein AuftragGescheitert", abgelehnt.Length,
                await db.ZahlAsync($"select count(*) from {s}.mt_events where type = 'auftrag_gescheitert' and stream_id = any(@a)", ("a", abgelehnt))),
            Pruefung.Gleich("kein Auftrag mit zwei Ausgängen", 0, await db.ZahlAsync($"""
                select count(*) from (select stream_id from {s}.mt_events where type in ('auftrag_bestaetigt', 'auftrag_gescheitert')
                  and stream_id = any(@a) group by 1 having count(*) > 1) x
                """, ("a", auftraege))),
            Pruefung.Gleich("kein gelingender Auftrag gescheitert", 0,
                await db.ZahlAsync($"select count(*) from {s}.mt_events where type = 'auftrag_gescheitert' and stream_id = any(@a)", ("a", ok))),
            P("jede Kompensation genau einmal (PostenFreigegeben je abgelehntem Auftrag)", abgelehnt.Length,
                await db.ZahlAsync($"select count(*) from {s}.mt_events where type = 'posten_freigegeben' and {beleg} = any(@a)", ("a", abgelehnt))),
            Pruefung.Gleich("keine Freigabe zweimal (je Auftrag höchstens ein PostenFreigegeben)", 0, await db.ZahlAsync($"""
                select count(*) from (select {beleg} from {s}.mt_events where type = 'posten_freigegeben' and {beleg} = any(@a)
                  group by 1 having count(*) > 1) x
                """, ("a", auftraege))),
            P("je Verteilung jedes Ziel genau einmal zugebucht", verteilungen.Sum(v => v.Ziele.Length),
                await db.ZahlAsync($"select count(*) from {s}.mt_events where type = 'posten_zugebucht' and {beleg} = any(@v)", ("v", vIds))),
            Pruefung.Gleich("kein Ziel einer Verteilung zweimal zugebucht", 0, await db.ZahlAsync($"""
                select count(*) from (select stream_id, {beleg} from {s}.mt_events where type = 'posten_zugebucht' and {beleg} = any(@v)
                  group by 1, 2 having count(*) > 1) x
                """, ("v", vIds))),
            P("je Verteilung genau ein VerteilungAbgeschlossen", verteilungen.Length,
                await db.ZahlAsync($"select count(*) from {s}.mt_events where type = 'verteilung_abgeschlossen' and stream_id = any(@v)", ("v", vIds))),
        };
        // Soll (Reservierungen unter dem Mindestbestand) und Ist (Nachschübe) aus demselben Schnappschuss (VM-1).
        var (ausloeser, nachschub) = (await db.LiesAsync($"""
            select (select count(*) from {s}.mt_events where type = 'posten_reserviert' and (data->>'Fehlmenge')::int > 0),
                   (select count(distinct headers->>'ursache') from {s}.mt_events where type = 'posten_zugebucht' and headers->>'ueber' = @w)
            """, r => (r.GetInt64(0), r.GetInt64(1)), ("w", NachschubWirker))).Single();
        p.Add(P("Funktion: je Reservierung unter dem Mindestbestand genau ein Nachschub", ausloeser, nachschub));
        p.Add(Pruefung.Gleich("Funktion: je Eingang genau eine Wirkung (Zubuchungen je Ursache = 1)", 0, await db.ZahlAsync($"""
            select count(*) from (select headers->>'ursache' from {s}.mt_events
              where type = 'posten_zugebucht' and headers->>'ueber' = @w group by 1 having count(*) > 1) x
            """, ("w", NachschubWirker))));
        return p;
    }

    /// <summary>Die Funktion läuft den Vorgängen nach (eigener Konsument): warten, bis jede auslösende Reservierung ihren Nachschub hat.</summary>
    public static async Task WarteAufNachschubAsync(Anwendungslauf lauf)
    {
        var s = lauf.Db.Schema;
        for (var i = 0; i < 120; i++)
        {
            var offen = await lauf.Db.ZahlAsync($"""
                select (select count(*) from {s}.mt_events where type = 'posten_reserviert' and (data->>'Fehlmenge')::int > 0)
                     - (select count(distinct headers->>'ursache') from {s}.mt_events where type = 'posten_zugebucht' and headers->>'ueber' = @w)
                """, ("w", NachschubWirker));
            if (offen <= 0) return;
            await Task.Delay(1000);
        }
    }
}

/// <summary>
/// Prüfungen, die für jede Anwendung gelten: keine CommandId hat zweimal gewirkt. Unabhängig vom Mechanismus (Inbox-Schlüssel) gemessen: die
/// Events eines Commands (causation_id = CommandId, jede CommandId der Inbox) stehen alle in EINER Transaktion (xmin) — ein zweites Wirken
/// stünde in einer zweiten. Dazu Noop- und Ablehnungs-Marken je CommandId höchstens einmal.
/// </summary>
internal static class GlobalePruefung
{
    public static async Task<List<Pruefung>> PruefeAsync(Datenbank db, string titel)
    {
        var s = db.Schema;
        return new List<Pruefung>
        {
            Pruefung.Gleich($"{titel}: keine CommandId zweimal gewirkt (Events je CommandId in genau einer Transaktion)", 0, await db.ZahlAsync($"""
                select count(*) from (select e.causation_id from {s}.mt_events e join {s}.cqrs_inbox i on i.command_id::text = e.causation_id
                  group by 1 having count(distinct e.xmin::text) > 1) x
                """)),
            Pruefung.Gleich($"{titel}: keine CommandId zweimal noop-markiert", 0, await db.ZahlAsync(
                $"select count(*) from (select data->>'CommandId' from {s}.mt_events where type = 'kommando_verarbeitet' group by 1 having count(*) > 1) x")),
            Pruefung.Gleich($"{titel}: keine CommandId zweimal abgelehnt-markiert", 0, await db.ZahlAsync(
                $"select count(*) from (select data->>'CommandId' from {s}.mt_events where type = 'kommando_abgelehnt' group by 1 having count(*) > 1) x")),
            Pruefung.Gleich($"{titel}: kein Stream mit doppelter Version", 0, await db.ZahlAsync(
                $"select count(*) from (select stream_id, version from {s}.mt_events group by 1, 2 having count(*) > 1) x")),
        };
    }
}
