using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Abstractions;
using Domain.Bildaufbereitung;
using FluentAssertions;
using Google.Protobuf;
using Infrastructure.Aggregate;   // KommandoAbgelehnt
using Infrastructure.Funktionen;
using Infrastructure.Serialization;
using Xunit;

namespace Infrastructure.Pruefstand.Funktionen;

/// <summary>
/// Ebene 1 — Katalog-Funktionen EXTERN anbieten (docs/konzept-editor-pipelines.md §14.5): ein Worker (Python über gRPC) meldet
/// „ich biete F mit n Slots“; die Sitzung holt für ihn Arbeit beim Vermittler (Pull, höchstens so viel wie Slots frei sind),
/// reicht sie weiter, verlängert Leases bei Lebenszeichen und schreibt sein Ergebnis genau einmal — Ergebnisse außerhalb des
/// Vertrags und gemeldete Fehler werden zur Fehlschlag-Marke. Gegen den echten Vermittlungs-Kern und den echten Ausführer
/// (Log im Speicher mit OCC wie Marten); der Transport (Cluster, gRPC-Stream) ist durch Delegaten ersetzt.
/// </summary>
public class FunktionsAnbieterSitzungTests
{
    /// <summary>Ein Vermittler im Speicher (echter Kern) + Mitschrift aller Wege der Sitzung.</summary>
    private sealed class Welt
    {
        public readonly FunktionsVermittlung Vermittlung = new(TimeSpan.FromSeconds(30));
        public readonly object Lock = new();
        public DateTimeOffset Jetzt = new(2026, 10, 7, 12, 0, 0, TimeSpan.Zero);
        public readonly ConcurrentQueue<HoleArbeit> Anfragen = new();
        public readonly ConcurrentQueue<object> Meldungen = new();
        public readonly ConcurrentQueue<AuftragAnbieten> Gesendet = new();
        public readonly LogImSpeicher Log = new();
        public readonly ConcurrentQueue<Guid> Weckungen = new();
        public readonly FunktionsAusfuehrer Ausfuehrer;
        public bool SchreibenScheitert;

        public Welt()
        {
            Ausfuehrer = new FunktionsAusfuehrer(Log,
                (_, _) => throw new InvalidOperationException("rechnet extern"),
                (k, _) => { Weckungen.Enqueue(k); return Task.CompletedTask; },
                _ => null);
        }

        public AuftragAnbieten Biete(string pfad, Guid? korrelation = null)
        {
            var a = new AuftragAnbieten(Guid.NewGuid(), korrelation ?? Guid.NewGuid(), "KameraSystem", new VerkleinereBild(pfad, 512));
            lock (Lock) Vermittlung.Biete(a);
            return a;
        }

        public FunktionsAnbieterSitzung Sitzung(string name = "s1") => new(
            name,
            n => FunktionsAnbieterSitzung.LoeseImKatalog(n, GeneratedFunktionen.Ergebnisse.Keys),
            f => GeneratedFunktionen.Ergebnisse[f],
            async (f, h, ct) =>
            {
                Anfragen.Enqueue(h);
                IReadOnlyList<AuftragAnbieten> los;
                lock (Lock) los = Vermittlung.Hole(h.Arbeiter, h.Max, Jetzt);
                if (los.Count == 0) await Task.Delay(20, ct);   // simulierter Long-Poll ohne Arbeit
                return new ArbeitZugeteilt(los);
            },
            (f, m) =>
            {
                Meldungen.Enqueue(m);
                lock (Lock)
                {
                    if (m is ArbeitErledigt e) Vermittlung.Erledigt(e.Vorgang);
                    if (m is ArbeitLebt l) Vermittlung.Lebenszeichen(l.Vorgang, l.Arbeiter, Jetzt);
                }
                return Task.CompletedTask;
            },
            (a, f) => { Gesendet.Enqueue(a); return Task.CompletedTask; },
            async (k, v, e, akteur, name, ct) =>
                !SchreibenScheitert && await Ausfuehrer.SchreibeErgebnisAsync(k, v, e, akteur, name, ct),
            warteMs: 50,
            fehlerPause: TimeSpan.FromMilliseconds(10));
    }

    private static async Task Bis(Func<bool> bedingung, string warum)
    {
        for (var i = 0; i < 200 && !bedingung(); i++) await Task.Delay(10);
        bedingung().Should().BeTrue(warum);
    }

    [Fact]
    public async Task Unbekannte_Funktionen_werden_genannt_bekannte_holen_hoechstens_so_viel_wie_Slots()
    {
        var w = new Welt();
        await using var s = w.Sitzung();
        var a = w.Biete("a"); var b = w.Biete("b"); var c = w.Biete("c");

        var unbekannt = s.Biete(new[] { ("IBildVerkleinerung", 2), ("IGibtEsNicht", 1) });

        unbekannt.Should().Equal("IGibtEsNicht");
        s.Angeboten.Should().Equal(typeof(IBildVerkleinerung));
        await Bis(() => w.Gesendet.Count == 2, "zwei Slots → zwei Aufträge beim Worker");
        w.Gesendet.Select(x => x.Vorgang).Should().Equal(a.Vorgang, b.Vorgang);
        w.Anfragen.First().Should().Be(new HoleArbeit("s1/IBildVerkleinerung", 2, 50));

        await Task.Delay(100);
        w.Gesendet.Should().HaveCount(2, "alle Slots belegt — die Sitzung holt nicht mehr, bis ein Ergebnis kommt");
        lock (w.Lock) w.Vermittlung.Wartend.Should().Be(1);
        _ = c;
    }

    [Fact]
    public async Task Ergebnis_wird_genau_einmal_geschrieben_erledigt_gemeldet_und_der_Slot_ist_frei()
    {
        var w = new Welt();
        await using var s = w.Sitzung();
        var korrelation = Guid.NewGuid();
        var a = w.Biete("a", korrelation); var b = w.Biete("b");
        s.Biete(new[] { ("IBildVerkleinerung", 1) });
        await Bis(() => w.Gesendet.Count == 1, "ein Slot → ein Auftrag");

        var ausgang = await s.ErgebnisAsync(a.Vorgang, new BildVerkleinert("/v/a.png", 640, 512), null, CancellationToken.None);

        ausgang.Should().Be(ErgebnisAusgang.Geschrieben);
        var stream = w.Log.Stream(a.Vorgang);
        stream.Should().ContainSingle();
        stream[0].Payload.Should().Be(new BildVerkleinert("/v/a.png", 640, 512));
        stream[0].CorrelationId.Should().Be(korrelation.ToString());
        stream[0].CausationId.Should().Be(a.Vorgang.ToString());
        stream[0].AggregateType.Should().Be("Funktion:IBildVerkleinerung");
        stream[0].UserId.Should().Be("KameraSystem", "der Akteur der Kette reist mit");
        w.Weckungen.Should().Contain(korrelation);
        w.Meldungen.OfType<ArbeitErledigt>().Should().ContainSingle().Which.Vorgang.Should().Be(a.Vorgang);

        await Bis(() => w.Gesendet.Count == 2, "der freie Slot holt den nächsten Auftrag");
        w.Gesendet.Last().Vorgang.Should().Be(b.Vorgang);
        lock (w.Lock) w.Vermittlung.Vergeben.Should().Be(1, "a ist erledigt und vergessen; b ist jetzt vergeben");

        (await s.ErgebnisAsync(a.Vorgang, new BildVerkleinert("/v/a.png", 1, 1), null, CancellationToken.None))
            .Should().Be(ErgebnisAusgang.Unbekannt, "ein zweites Ergebnis desselben Vorgangs wirkt nicht");
        w.Log.Stream(a.Vorgang).Should().ContainSingle();
    }

    [Fact]
    public async Task Fehler_und_Ergebnis_ausserhalb_des_Vertrags_werden_zur_Fehlschlag_Marke()
    {
        var w = new Welt();
        await using var s = w.Sitzung();
        var a = w.Biete("a"); var b = w.Biete("b");
        s.Biete(new[] { ("Domain.Bildaufbereitung.IBildVerkleinerung", 2) });   // auch der volle Name gilt
        await Bis(() => w.Gesendet.Count == 2, "zwei Slots");

        (await s.ErgebnisAsync(a.Vorgang, null, "Datei gesperrt", CancellationToken.None)).Should().Be(ErgebnisAusgang.Abgelehnt);
        (await s.ErgebnisAsync(b.Vorgang, new HistogrammAusgeglichen("/x.png", 1, 1, DateTimeOffset.UnixEpoch), null, CancellationToken.None))
            .Should().Be(ErgebnisAusgang.Abgelehnt);

        w.Log.Stream(a.Vorgang).Single().Payload.Should().BeOfType<KommandoAbgelehnt>()
            .Which.Grund.Should().Contain("Datei gesperrt");
        w.Log.Stream(b.Vorgang).Single().Payload.Should().BeOfType<KommandoAbgelehnt>()
            .Which.Grund.Should().Contain("außerhalb des Vertrags").And.Contain("BildVerkleinert");
        w.Meldungen.OfType<ArbeitErledigt>().Should().HaveCount(2, "auch ein Fehlschlag ist ein Abschluss");
    }

    [Fact]
    public async Task Lebenszeichen_verlaengert_die_Lease_nur_fuer_laufende_Auftraege()
    {
        var w = new Welt();
        await using var s = w.Sitzung();
        var a = w.Biete("a");
        s.Biete(new[] { ("IBildVerkleinerung", 1) });
        await Bis(() => w.Gesendet.Count == 1, "Auftrag beim Worker");

        w.Jetzt = w.Jetzt.AddSeconds(25);
        await s.LebtAsync(a.Vorgang);
        await s.LebtAsync(Guid.NewGuid());   // fremder Vorgang: nichts

        w.Meldungen.OfType<ArbeitLebt>().Should().ContainSingle().Which.Should().Be(new ArbeitLebt(a.Vorgang, "s1/IBildVerkleinerung"));
        lock (w.Lock) w.Vermittlung.Hole("anderer", 1, w.Jetzt.AddSeconds(10)).Should().BeEmpty("die Lease lebt dank Lebenszeichen");
    }

    [Fact]
    public async Task Sitzungsende_meldet_nichts_erledigt_die_Lease_laeuft_ab_und_ein_anderer_Worker_bekommt_den_Auftrag()
    {
        var w = new Welt();
        var s = w.Sitzung();
        var a = w.Biete("a");
        s.Biete(new[] { ("IBildVerkleinerung", 1) });
        await Bis(() => w.Gesendet.Count == 1, "Auftrag beim Worker");

        await s.DisposeAsync();

        w.Meldungen.OfType<ArbeitErledigt>().Should().BeEmpty();
        s.Laufende.Should().Be(0);
        var vorher = w.Anfragen.Count;
        await Task.Delay(100);
        w.Anfragen.Count.Should().Be(vorher, "nach dem Ende holt die Sitzung nichts mehr");

        w.Jetzt = w.Jetzt.AddSeconds(31);
        await using var neu = w.Sitzung("s2");
        neu.Biete(new[] { ("IBildVerkleinerung", 1) });
        await Bis(() => w.Gesendet.Count == 2, "die abgelaufene Lease geht an die neue Sitzung");
        w.Gesendet.Last().Vorgang.Should().Be(a.Vorgang);
        (await neu.ErgebnisAsync(a.Vorgang, new BildNichtLesbar("a", "kaputt"), null, CancellationToken.None))
            .Should().Be(ErgebnisAusgang.Geschrieben);
    }

    [Fact]
    public async Task Nicht_schreibbar_meldet_nicht_erledigt_und_ein_schnellerer_Ausfuehrer_gewinnt()
    {
        var w = new Welt();
        await using var s = w.Sitzung();
        var a = w.Biete("a"); var b = w.Biete("b");
        s.Biete(new[] { ("IBildVerkleinerung", 2) });
        await Bis(() => w.Gesendet.Count == 2, "zwei Slots");

        w.SchreibenScheitert = true;
        (await s.ErgebnisAsync(a.Vorgang, new BildVerkleinert("a", 1, 1), null, CancellationToken.None))
            .Should().Be(ErgebnisAusgang.NichtGeschrieben);
        w.Meldungen.OfType<ArbeitErledigt>().Should().BeEmpty("nicht im Log → nicht erledigt; die Lease heilt");

        // b hat ein anderer (C#-)Ausführer schon abgeschlossen: StartStream → der Erste gewinnt, unser Ergebnis verpufft
        w.SchreibenScheitert = false;
        await w.Ausfuehrer.SchreibeErgebnisAsync(b.Korrelation, b.Vorgang, new BildNichtLesbar("b", "zuerst"), null, "IBildVerkleinerung", default);
        (await s.ErgebnisAsync(b.Vorgang, new BildVerkleinert("b", 1, 1), null, CancellationToken.None))
            .Should().Be(ErgebnisAusgang.Geschrieben, "es liegt ein Ergebnis im Stream — das ist erledigt");
        w.Log.Stream(b.Vorgang).Should().ContainSingle().Which.Payload.Should().Be(new BildNichtLesbar("b", "zuerst"));
    }

    [Fact]
    public void Auftrag_und_Ergebnis_reisen_ueber_Proto_ohne_Reflexion()
    {
        var mapper = new ProtoMessageMapper();
        var auftrag = new VerkleinereBild("/roh/a.tiff", 512);

        var dto = mapper.MapToDto(auftrag);
        var bytes = dto.ToByteArray();
        var zurueck = mapper.MapToDomain(ProtoRepo.AuftragPayloadDto.Parser.ParseFrom(bytes));
        zurueck.Should().Be(auftrag);

        // Das Ergebnis kommt als EventEnvelopeDto — nur die Nutzlast zählt (keine Ids nötig, der Server stempelt selbst).
        var ergebnis = new ProtoRepo.EventEnvelopeDto { BildVerkleinert = new ProtoRepo.BildVerkleinertDto { Pfad = "/v/a.png", BreitePixel = 640, HoehePixel = 512 } };
        mapper.MapErgebnis(ProtoRepo.EventEnvelopeDto.Parser.ParseFrom(ergebnis.ToByteArray()))
            .Should().Be(new BildVerkleinert("/v/a.png", 640, 512));
    }
}
