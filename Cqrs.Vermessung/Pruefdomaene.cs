using System.Collections.Concurrent;
using System.Text.Json.Nodes;
using Abstractions;

namespace Cqrs.Vermessung;

/// <summary>
/// Verteilt (Prüfdomäne): Vorgänge über den JSON-Eingang, im festen Muster je zehn — acht Aufträge, die gelingen (Diamant: Posten reservieren und
/// Kontingent belegen, Bestätigung am Join), ein Auftrag gegen ein volles Kontingent (Ablehnung → Kompensation der Reservierung) und eine
/// Verteilung (Fan-out auf vier Ziele, Count-Join). Jede Reservierung unter dem Mindestbestand löst die Reaktion
/// <c>NachschubBeiMindestbestand</c> aus (Emittent auf den langlebigen Posten-Streams). Sichtbar ist ein Vorgang mit seinem
/// <c>ProzessBeendet</c> im Log (Log-Zeit, Uhr-Versatz umgerechnet). Die Prüfdomäne hat keine Akteure — das Tor ist offen.
/// </summary>
internal sealed class Pruefdomaene : ILast
{
    private const int Posten = 50, Ziele = 20;
    private readonly Eingang _eingang;
    private readonly TimeSpan _uhrversatz;
    private readonly Guid[] _posten = Enumerable.Range(0, Posten).Select(_ => Guid.NewGuid()).ToArray();
    private readonly Guid[] _kontingente = Enumerable.Range(0, Posten).Select(_ => Guid.NewGuid()).ToArray();
    private readonly Guid[] _ziele = Enumerable.Range(0, Ziele).Select(_ => Guid.NewGuid()).ToArray();
    private readonly Guid _voll = Guid.NewGuid();
    private readonly ConcurrentDictionary<Guid, Vorgang> _offen = new();   // Korrelation des Vorgangs → Vorgang
    private readonly ConcurrentQueue<(Guid Korrelation, DateTimeOffset Zeit)> _frueh = new();
    private readonly ConcurrentDictionary<string, DateTimeOffset> _nachschubAusloeser = new();   // "<Posten>:<Version>" → Log-Zeit der Reservierung
    private readonly ConcurrentBag<double> _reaktionMs = new();
    private long _n;

    public enum Art { AuftragOk, AuftragAbgelehnt, Verteilung }
    public ConcurrentBag<(Guid Korrelation, Guid Id, Art Art, Guid[] Ziele)> Vorgaenge { get; } = new();

    public Pruefdomaene(Eingang eingang, LogStrom log, TimeSpan uhrversatz)
    {
        _eingang = eingang;
        _uhrversatz = uhrversatz;
        log.Beobachte(Sieh);
    }

    public IReadOnlyCollection<double> ReaktionMs => _reaktionMs;
    public void LeereReaktion() => _reaktionMs.Clear();

    /// <summary>Störungslauf: gescheiterte Sendungen mit derselben CommandId wiederholen.</summary>
    public bool Wiederholen { get; init; }

    private void Sieh(LogZeile z)
    {
        switch (z.Typ)
        {
            case "prozess_beendet":
                var zeit = z.Zeit + _uhrversatz;
                if (_offen.TryRemove(z.Stream, out var v)) v.Sichtbar = zeit;
                else _frueh.Enqueue((z.Stream, zeit));
                break;
            case "posten_reserviert" when z.Daten != null && ((int?)JsonNode.Parse(z.Daten)!["Fehlmenge"] ?? 0) > 0:
                _nachschubAusloeser[$"{z.Stream}:{z.Version}"] = z.Zeit;
                break;
            case "posten_zugebucht" when z.Ueber == "reaktion:NachschubBeiMindestbestand" && z.Ursache != null:
                if (_nachschubAusloeser.TryRemove(z.Ursache, out var ausgeloest)) _reaktionMs.Add((z.Zeit - ausgeloest).TotalMilliseconds);
                break;
        }
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
        Guid korrelation;
        if (art == Art.Verteilung)
        {
            ziele = Enumerable.Range(0, 4).Select(j => _ziele[(int)((i / 10 * 4 + j) % Ziele)]).ToArray();
            typ = "StarteVerteilung";
            daten = new JsonObject
            {
                ["AggregateId"] = id.ToString(), ["QuelleId"] = posten.ToString(),
                ["Ziele"] = new JsonArray(ziele.Select(z => (JsonNode)z.ToString()).ToArray()), ["MengeJeZiel"] = 1,
            };
            korrelation = ProzessId.Für("VerteilProzess", id, 1);
        }
        else
        {
            typ = "StarteAuftrag";
            daten = new JsonObject
            {
                ["AggregateId"] = id.ToString(), ["PostenId"] = posten.ToString(),
                ["KontingentId"] = (art == Art.AuftragAbgelehnt ? _voll : _kontingente[i % Posten]).ToString(), ["Menge"] = 1 + (int)(i % 3),
            };
            korrelation = ProzessId.Für("AuftragsProzess", id, 1);
        }
        Vorgaenge.Add((korrelation, id, art, ziele));
        v.Start = DateTimeOffset.UtcNow;
        _offen[korrelation] = v;
        // Ein ProzessBeendet, das schneller im Log war als die Anmeldung hier: nachtragen.
        for (var n = _frueh.Count; n > 0 && _frueh.TryDequeue(out var f); n--)
            if (_offen.TryRemove(f.Korrelation, out var w)) w.Sichtbar = f.Zeit; else _frueh.Enqueue(f);
        // Im Störungslauf ist der Host zeitweise weg: dann mit DERSELBEN CommandId wiederholen (Client-Garantie, docs/11 §11.3).
        var r = Wiederholen
            ? await _eingang.CommandMitWiederholungAsync(typ, daten, null, TimeSpan.FromMinutes(3), ct)
            : await _eingang.CommandAsync(typ, daten, null, ct: ct);
        if (!r.Ok) throw new InvalidOperationException($"{typ}: {r.Status} {r.Fehler}");
    }

    /// <summary>
    /// Exactly-once (§0.2): je Vorgang genau ein ProzessBeendet mit dem erwarteten Ausgang, jede Kompensation genau einmal, je Verteilung jedes
    /// Ziel genau einmal, je Reaktions-Command genau eine Wirkung (Ziel-Events je Ursache = 1) und keine Reservierung unter dem Mindestbestand
    /// ohne Nachschub.
    /// </summary>
    public async Task<List<Pruefung>> PruefeAsync(Datenbank db, bool vollstaendig = true)
    {
        var s = db.Schema;
        Pruefung P(string name, long soll, long ist) => Pruefung.Erwartet(name, soll, ist, vollstaendig);
        var alle = Vorgaenge.ToArray();
        var korr = alle.Select(v => v.Korrelation).ToArray();
        var p = new List<Pruefung>
        {
            P("je Vorgang genau ein ProzessBeendet", alle.Length,
                await db.ZahlAsync($"select count(*) from {s}.mt_events where type = 'prozess_beendet' and stream_id = any(@k)", ("k", korr))),
            Pruefung.Gleich("kein Vorgang mit mehr als einem ProzessBeendet", 0, await db.ZahlAsync(
                $"select count(*) from (select stream_id from {s}.mt_events where type = 'prozess_beendet' and stream_id = any(@k) group by 1 having count(*) > 1) x",
                ("k", korr))),
        };
        var ok = alle.Where(v => v.Art != Art.AuftragAbgelehnt).Select(v => v.Korrelation).ToArray();
        var abgelehnt = alle.Where(v => v.Art == Art.AuftragAbgelehnt).Select(v => v.Korrelation).ToArray();
        p.Add(P("gelingende Vorgänge enden mit Erfolg", ok.Length, await db.ZahlAsync(
            $"select count(*) from {s}.mt_events where type = 'prozess_beendet' and stream_id = any(@k) and (data->>'Erfolg')::boolean", ("k", ok))));
        p.Add(P("abgelehnte Aufträge enden ohne Erfolg", abgelehnt.Length, await db.ZahlAsync(
            $"select count(*) from {s}.mt_events where type = 'prozess_beendet' and stream_id = any(@k) and not (data->>'Erfolg')::boolean", ("k", abgelehnt))));
        p.Add(P("jede Kompensation genau einmal (PostenFreigegeben je abgelehntem Auftrag)", abgelehnt.Length, await db.ZahlAsync(
            $"select count(*) from {s}.mt_events where type = 'posten_freigegeben' and correlation_id = any(@k)", ("k", abgelehnt.Select(k => k.ToString()).ToArray()))));
        var auftraegeOk = alle.Where(v => v.Art == Art.AuftragOk).Select(v => v.Id).ToArray();
        p.Add(P("je gelingendem Auftrag genau ein AuftragBestaetigt", auftraegeOk.Length, await db.ZahlAsync(
            $"select count(*) from {s}.mt_events where type = 'auftrag_bestaetigt' and stream_id = any(@a)", ("a", auftraegeOk))));
        var verteilungen = alle.Where(v => v.Art == Art.Verteilung).ToArray();
        p.Add(P("je Verteilung jedes Ziel genau einmal zugebucht", verteilungen.Sum(v => v.Ziele.Length), await db.ZahlAsync(
            $"select count(*) from {s}.mt_events where type = 'posten_zugebucht' and correlation_id = any(@k) and headers->>'ueber' like 'prozess:VerteilProzess%'",
            ("k", verteilungen.Select(v => v.Korrelation.ToString()).ToArray()))));
        p.Add(P("je Verteilung genau ein VerteilungAbgeschlossen", verteilungen.Length, await db.ZahlAsync(
            $"select count(*) from {s}.mt_events where type = 'verteilung_abgeschlossen' and stream_id = any(@v)", ("v", verteilungen.Select(v => v.Id).ToArray()))));
        // Soll und Ist in EINER Abfrage (ein Schnappschuss): getrennt gezählt, lief der Host dazwischen weiter (früher VM-1).
        var (ausloeser, nachschub) = (await db.LiesAsync($"""
            select (select count(*) from {s}.mt_events where type = 'posten_reserviert' and (data->>'Fehlmenge')::int > 0),
                   (select count(distinct headers->>'ursache') from {s}.mt_events
                     where type = 'posten_zugebucht' and headers->>'ueber' = 'reaktion:NachschubBeiMindestbestand')
            """, r => (r.GetInt64(0), r.GetInt64(1)))).Single();
        p.Add(P("Reaktion: je Reservierung unter dem Mindestbestand genau ein Nachschub", ausloeser, nachschub));
        p.Add(Pruefung.Gleich("Reaktion: je Reaktions-Command genau eine Wirkung (Ziel-Events je Ursache = 1)", 0, await db.ZahlAsync($"""
            select count(*) from (select headers->>'ursache' from {s}.mt_events
              where type = 'posten_zugebucht' and headers->>'ueber' = 'reaktion:NachschubBeiMindestbestand' group by 1 having count(*) > 1) x
            """)));
        return p;
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
