using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Abstractions;
using FluentAssertions;
using Infrastructure.Pruefstand.Funktionen;
using Infrastructure.Prozess;

namespace Infrastructure.Pruefstand.Garantien;

/// <summary>
/// Der Prozess-Poll (PF-4, G-8) über einen Lese-Seam: er routet die geladenen Events seitenweise, ohne Streams nachzulesen, startet keinen
/// beendeten Vorgang neu und rückt seine Marke nie über eine unbestätigte Weckung — ein Vorgang mit verlorenem Start-Signal und verlorener
/// Poll-Weckung wird beim nächsten Durchlauf erneut geweckt. Die Marten-Semantik der Seite (Ordnung, Karenz) prüft die Integration.
/// </summary>
[Trait("Kategorie", "Garantie")]
public class ProzessPollGarantieTests
{
    private sealed record Auftrag : IEvent;
    private sealed record Ergebnis : IEvent;

    private static readonly IReadOnlyDictionary<Type, string> Auslöser = new Dictionary<Type, string> { [typeof(Auftrag)] = "P" };

    /// <summary>Ein Log mit globaler Sequenz; <c>gesetzt</c> = älter als die Karenz. Streams nachzulesen ist verboten (PF-4).</summary>
    private sealed class Log : IEventStoreRepository
    {
        public readonly List<(long Seq, EventEnvelope Env, bool Gesetzt)> Eintraege = new();
        public readonly HashSet<Guid> Streams = new();

        public EventEnvelope Haenge(IEvent e, string korrelation = "", bool gesetzt = true)
        {
            var env = new EventEnvelope { AggregateId = Guid.NewGuid(), AggregateVersion = 1, Payload = e, CorrelationId = korrelation };
            Eintraege.Add((Eintraege.Count + 1, env, gesetzt));
            return env;
        }

        public Task<PartitionsSeite> ReadPartitionAsync(long nach, int partition, int partitionen, IReadOnlyCollection<string>? typen,
            IReadOnlyCollection<long> ausser, int limit, CancellationToken ct)
        {
            var danach = Eintraege.Where(e => e.Seq > nach).ToList();
            var gesetzt = danach.Where(e => e.Gesetzt).Select(e => e.Seq).DefaultIfEmpty(nach).Max();
            var seite = danach.Take(limit).Select(e => new PartitionsEvent(e.Seq, e.Env)).ToList();
            return Task.FromResult(new PartitionsSeite(seite, gesetzt, seite.Count >= limit));
        }

        public Task<IReadOnlyCollection<Guid>> StreamsVorhandenAsync(IReadOnlyCollection<Guid> s, CancellationToken ct)
            => Task.FromResult<IReadOnlyCollection<Guid>>(s.Where(Streams.Contains).ToList());

        public Task<IReadOnlyList<EventEnvelope>> ReadStreamAsync(Guid s, int v, CancellationToken ct)
            => throw new InvalidOperationException("PF-4: der Poll liest keine Streams nach");
        public Task AppendEventsAsync(Guid a, int v, IReadOnlyList<IEvent> e, string? c = null, string? ca = null, string? at = null, string? ak = null)
            => throw new NotSupportedException();
        public Task<StreamChanges> ReadChangedStreamsAsync(long after, IReadOnlyCollection<string>? typen, int limit, CancellationToken ct) => throw new NotSupportedException();
        public Task<TState?> LoadStateAsync<TState>(Guid id) where TState : class, IState, new() => throw new NotSupportedException();
    }

    private sealed class Lauf
    {
        public readonly Log Log = new();
        public readonly OffenIndexImSpeicher Offen = new();
        public readonly ConcurrentQueue<ProzessPollWeckung> Geweckt = new();
        public readonly List<long> Gemerkt = new();
        public Func<ProzessPollWeckung, bool> Bestaetigt = _ => true;
        public ProzessPoll Poll { get; }

        public Lauf(long hochwasser = 0)
            => Poll = new ProzessPoll(Log, Offen, Auslöser,
                (w, _) => { Geweckt.Enqueue(w); return Task.FromResult(Bestaetigt(w)); },
                (hwm, _) => { lock (Gemerkt) Gemerkt.Add(hwm); return Task.CompletedTask; }, hochwasser);
    }

    [Fact]
    public async Task Der_Poll_routet_die_geladenen_Events_ohne_Streams_nachzulesen()
    {
        var l = new Lauf();
        var a = l.Log.Haenge(new Auftrag());
        var offen = Guid.NewGuid();
        await l.Offen.MarkiereOffenAsync(offen, "P", default);
        l.Log.Haenge(new Ergebnis(), offen.ToString());
        l.Log.Haenge(new Ergebnis(), Guid.NewGuid().ToString());   // fremd

        await l.Poll.DurchlaufAsync();

        l.Geweckt.Select(w => w.Korrelation).Should().BeEquivalentTo(new[] { ProzessId.Für("P", a.AggregateId, 1), offen });
        l.Poll.Hochwasser.Should().Be(3);
    }

    [Fact]
    public async Task Eine_unbestaetigte_Start_Weckung_haelt_die_Marke_und_der_naechste_Durchlauf_weckt_erneut()
    {
        var l = new Lauf();
        l.Log.Haenge(new Auftrag());
        var verloren = l.Log.Haenge(new Auftrag());
        l.Log.Haenge(new Auftrag());
        var korr = ProzessId.Für("P", verloren.AggregateId, 1);
        l.Bestaetigt = w => w.Korrelation != korr;   // Cluster nach Neustart noch nicht bereit, Zeitüberschreitung

        await l.Poll.DurchlaufAsync();
        l.Poll.Hochwasser.Should().Be(1, "die Marke bleibt vor dem Event, dessen Weckung unbestätigt blieb (G-8)");

        l.Bestaetigt = _ => true;
        while (l.Geweckt.TryDequeue(out _)) { }
        await l.Poll.DurchlaufAsync();

        l.Geweckt.Select(w => w.Korrelation).Should().Contain(korr, "der nächste Durchlauf startet den Vorgang");
        l.Poll.Hochwasser.Should().Be(3);
    }

    [Fact]
    public async Task Ein_beendeter_Vorgang_wird_nicht_erneut_gestartet_ein_offener_schon()
    {
        var l = new Lauf();
        var beendet = l.Log.Haenge(new Auftrag());
        var offen = l.Log.Haenge(new Auftrag());
        var neu = l.Log.Haenge(new Auftrag());
        var (kb, ko, kn) = (ProzessId.Für("P", beendet.AggregateId, 1), ProzessId.Für("P", offen.AggregateId, 1), ProzessId.Für("P", neu.AggregateId, 1));
        l.Log.Streams.UnionWith(new[] { kb, ko });   // beide gestartet, nur einer noch offen
        await l.Offen.MarkiereOffenAsync(ko, "P", default);

        await l.Poll.DurchlaufAsync();

        l.Geweckt.Select(w => w.Korrelation).Should().BeEquivalentTo(new[] { ko, kn });
        l.Poll.Hochwasser.Should().Be(3, "ein nicht nötiger Start hält die Marke nicht");
    }

    [Fact]
    public async Task Volle_Seiten_werden_alle_gelesen_je_Korrelation_einmal_geweckt_und_die_Marke_je_Seite_gemerkt()
    {
        var l = new Lauf();
        var korr = Guid.NewGuid();
        await l.Offen.MarkiereOffenAsync(korr, "P", default);
        for (var i = 0; i < ProzessPoll.SeitenGroesse * 2 + 100; i++) l.Log.Haenge(new Ergebnis(), korr.ToString());

        await l.Poll.DurchlaufAsync();

        l.Geweckt.Should().HaveCount(3, "eine Weckung je Korrelation und Seite");
        l.Gemerkt.Should().Equal(ProzessPoll.SeitenGroesse, ProzessPoll.SeitenGroesse * 2, ProzessPoll.SeitenGroesse * 2 + 100);
    }

    [Fact]
    public async Task Junge_Events_werden_geweckt_die_Marke_bleibt_aber_bei_der_gesetzten_Sequenz()
    {
        var l = new Lauf();
        l.Log.Haenge(new Auftrag());
        var jung = l.Log.Haenge(new Auftrag(), gesetzt: false);   // jünger als die Karenz: ein Nachzügler davor ist noch möglich

        await l.Poll.DurchlaufAsync();

        l.Geweckt.Select(w => w.Korrelation).Should().Contain(ProzessId.Für("P", jung.AggregateId, 1), "Latenz: jung wird trotzdem geweckt");
        l.Poll.Hochwasser.Should().Be(1);
    }
}
