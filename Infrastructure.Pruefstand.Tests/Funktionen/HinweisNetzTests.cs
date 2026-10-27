using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Abstractions;
using FluentAssertions;
using Infrastructure.Funktionen;
using Infrastructure.Lebenszyklus;
using Infrastructure.Projections;
using Proto.Cluster;
using Xunit;

namespace Infrastructure.Pruefstand.Funktionen;

/// <summary>
/// Das Hinweis-Netz (Plan Pub/Sub-Neustrukturierung §2.9, E7, E8, A6, A7), store-frei: die Routing-Tabelle schickt einen Hinweis direkt an die Adresse
/// jeder hörenden Funktion (Partition des Herkunfts-Streams bzw. die eine Adresse); ein Hinweis-Eingang rechnet einmal, Commands tragen eine Id aus
/// Funktion, Hinweis-Kennung, Stelle, Typ und Ziel — ein Wiederholen nach „unbekannt“ sendet dieselben Ids, ohne neu zu rechnen; Kennungen sind
/// abgeleitet (dasselbe Nachrechnen ergibt dieselbe Kennung).
/// </summary>
[Trait("Kategorie", "Garantie")]
public class HinweisNetzTests
{
    private sealed class Ziel : IState { public int Version { get; set; } public Guid Id { get; set; } }
    private sealed record Gemessen(double Wert) : IEvent;
    private sealed record Merkmal(double Wert) : ITransientEvent;
    private sealed record Auffaellig(double Wert) : ITransientEvent;
    private sealed record Melde(Id<Ziel> AggregateId, double Wert) : ICommand<Ziel>;

    private static FunktionsBaustein Funktion(string name, Type eingang, Func<IEvent, Herkunft, IAsyncEnumerable<IAusgabe>> auf, bool eineAdresse = false) =>
        new(typeof(HinweisNetzTests), name, false, false,
            new[] { new FunktionsEingang(eingang, typeof(ITransientEvent).IsAssignableFrom(eingang), false, false,
                new[] { typeof(Melde), typeof(Auffaellig) }, Type.EmptyTypes) },
            Array.Empty<FunktionsUmsetzung>(), Type.EmptyTypes,
            (f, e, h, b, ct) => auf(e, h),
            (f, s, b, ct) => throw new NotSupportedException(), EineAdresse: eineAdresse);

    private static async IAsyncEnumerable<IAusgabe> Aus(params IAusgabe[] a)
    {
        await Task.Yield();
        foreach (var x in a) yield return x;
    }

    private static HinweisZustellen Hinweis(IEvent h, Guid? kennung = null, Guid? stream = null) =>
        new(kennung ?? Guid.NewGuid(), h, stream ?? Guid.NewGuid(), 3, DateTimeOffset.UtcNow, Guid.NewGuid().ToString(), null);

    private sealed class Gesendet
    {
        public readonly ConcurrentQueue<(Guid Id, ICommand Befehl)> Alle = new();
        public EmitAusgang Ausgang = EmitAusgang.Zugestellt;
        public Task<EmitAusgang> SendeAsync(ICommand c, Guid id, Guid korrelation, CancellationToken ct)
        {
            Alle.Enqueue((id, c));
            return Task.FromResult(Ausgang);
        }
    }

    [Fact]
    public async Task Ein_Hinweis_Eingang_sendet_Commands_mit_Ids_aus_der_Kennung_und_wiederholt_ohne_neu_zu_rechnen()
    {
        var ziel = Id<Ziel>.Neu();
        var gerechnet = 0;
        var b = Funktion("Schaetzer", typeof(Merkmal), (e, h) =>
        {
            Interlocked.Increment(ref gerechnet);
            return Aus(new Melde(ziel, ((Merkmal)e).Wert), new Auffaellig(1), new Melde(ziel, 2));
        });
        var weiter = new List<HinweisZustellen>();
        var sende = new Gesendet { Ausgang = EmitAusgang.Unbekannt };
        var arbeit = new FunktionsArbeit(b, () => new object(), new SpeicherAblage(), sende.SendeAsync, weiter.Add);
        var h = Hinweis(new Merkmal(7));
        var rechnung = new EingangsRechnung();

        (await arbeit.HinweisAsync(h, rechnung, default)).Should().Be(EingangsAusgang.Spaeter);
        sende.Ausgang = EmitAusgang.Zugestellt;
        (await arbeit.HinweisAsync(h, rechnung, default)).Should().Be(EingangsAusgang.Fertig);

        gerechnet.Should().Be(1, "das Wiederholen sendet die Ausgabe des einen Laufs");
        sende.Alle.Should().HaveCount(4);
        sende.Alle.Select(x => x.Id).Distinct().Should().BeEquivalentTo(new[]
        {
            FunktionsIds.BefehlAusHinweis("Schaetzer", h.Kennung, 0, new Melde(ziel, 7)),
            FunktionsIds.BefehlAusHinweis("Schaetzer", h.Kennung, 1, new Melde(ziel, 2)),
        }, "Funktion, Hinweis-Kennung, Stelle, Typ und Ziel — die Inbox des Ziels verwirft das Wiederholen");
        weiter.Should().ContainSingle("der Hinweis geht einmal weiter, auch beim Wiederholen")
            .Which.Should().Match<HinweisZustellen>(w => w.Kennung == FunktionsIds.Hinweis("Schaetzer", h.Kennung, 0) && w.Stream == h.Stream,
                "abgeleitete Kennung, derselbe Herkunfts-Stream (dieselbe Partition)");
    }

    [Fact]
    public async Task Hinweise_aus_einem_Log_Eingang_tragen_abgeleitete_Kennungen_und_den_Stream_des_Eingangs()
    {
        var b = Funktion("Merkmalsbildung", typeof(Gemessen), (e, h) => Aus(new Merkmal(((Gemessen)e).Wert), new Merkmal(0)));
        var weiter = new List<HinweisZustellen>();
        var stream = Guid.NewGuid();
        var e = new EventEnvelope { AggregateId = stream, AggregateVersion = 5, Payload = new Gemessen(4), CorrelationId = Guid.NewGuid().ToString() };
        var arbeit = new FunktionsArbeit(b, () => new object(), new SpeicherAblage(), new Gesendet().SendeAsync, weiter.Add);
        await arbeit.VerarbeiteAsync(e, default);
        await new FunktionsArbeit(b, () => new object(), new SpeicherAblage(), new Gesendet().SendeAsync, weiter.Add).VerarbeiteAsync(e, default);

        weiter.Should().HaveCount(4);
        weiter.Select(w => w.Kennung).Distinct().Should().Equal(FunktionsIds.Hinweis("Merkmalsbildung", stream, 5, 0), FunktionsIds.Hinweis("Merkmalsbildung", stream, 5, 1));
        weiter.Should().OnlyContain(w => w.Stream == stream && w.Version == 5);
    }

    [Fact]
    public async Task Neue_Ids_aus_einem_Hinweis_sind_je_Kennung_verschieden_und_beim_Wiederholen_gleich()
    {
        var ids = new List<Guid>();
        var b = Funktion("Anleger", typeof(Merkmal), (e, h) => { ids.Add(h.NeueId<Ziel>().Wert); return Aus(); });
        var arbeit = new FunktionsArbeit(b, () => new object(), new SpeicherAblage(), new Gesendet().SendeAsync);
        var stream = Guid.NewGuid();
        var a = Hinweis(new Merkmal(1), Guid.NewGuid(), stream);
        await arbeit.HinweisAsync(a, new EingangsRechnung(), default);
        await arbeit.HinweisAsync(a, new EingangsRechnung(), default);
        await arbeit.HinweisAsync(Hinweis(new Merkmal(1), Guid.NewGuid(), stream), new EingangsRechnung(), default);
        ids[0].Should().Be(ids[1]).And.NotBe(ids[2]);
    }

    [Fact]
    public void Das_Netz_schickt_direkt_an_die_Partition_des_Herkunfts_Streams_bzw_an_die_eine_Adresse()
    {
        var gemeldet = new ConcurrentQueue<ClusterIdentity>();
        var bote = new Bote((ziel, _, _) => { gemeldet.Enqueue(ziel); return Task.FromResult<object?>(new WakeAck()); });
        var je = Funktion("JePartition", typeof(Merkmal), (e, h) => Aus());
        var eine = Funktion("Eine", typeof(Merkmal), (e, h) => Aus(), eineAdresse: true);
        var andere = Funktion("Andere", typeof(Auffaellig), (e, h) => Aus());
        var konsum = new Konsum(8, 500);
        var netz = new HinweisNetz(new[] { je, eine, andere }, new Dictionary<Type, string[]>
        {
            [typeof(Merkmal)] = new[] { je.KindName, eine.KindName },
            [typeof(Auffaellig)] = new[] { andere.KindName },
        }, konsum, () => bote);

        var stream = Guid.NewGuid();
        netz.Verteile(Hinweis(new Merkmal(1), stream: stream)).Should().Be(2);
        netz.Verteile(Hinweis(new Gemessen(1))).Should().Be(0, "niemand hört ihn — er ist nur für Clients");
        SpinWait.SpinUntil(() => gemeldet.Count == 2, TimeSpan.FromSeconds(5));
        gemeldet.Should().BeEquivalentTo(new[]
        {
            ClusterIdentity.Create(Partitionierung.Von(stream, 8).ToString(), je.KindName),
            ClusterIdentity.Create(Funktionsvertrag.EinzigeIdentitaet, eine.KindName),
        });
    }

    [Fact]
    public void Eine_Adresse_der_Routing_Tabelle_ohne_Funktion_ist_ein_Boot_Fehler()
    {
        var je = Funktion("JePartition", typeof(Merkmal), (e, h) => Aus());
        FluentActions.Invoking(() => new HinweisNetz(new[] { je }, new Dictionary<Type, string[]> { [typeof(Merkmal)] = new[] { "funktion-Fehlt" } },
                new Konsum(8, 500), () => throw new InvalidOperationException()))
            .Should().Throw<Infrastructure.Umgebung.BootFehler>().WithMessage("*funktion-Fehlt*");
    }
}
