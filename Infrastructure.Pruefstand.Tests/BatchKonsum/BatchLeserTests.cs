using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Infrastructure.Projections;

namespace Infrastructure.Pruefstand.BatchKonsum;

/// <summary>Kosten des Batch-Lesers ohne Garantie-Bezug: was er bei leeren Seiten schreibt und welche Typen er filtert.</summary>
public class BatchLeserTests
{
    private static readonly CancellationToken Ct = CancellationToken.None;
    private static readonly Konsum Zwei = new(Partitionen: 2, BatchGroesse: 10);

    [Fact]
    public async Task Fremde_Events_ruecken_die_Lese_Position_vor_durabel_nur_einmal_je_Poll_Intervall()
    {
        var log = new PartitionsLog();
        var db = new SpeicherKonsum();
        var uhr = DateTimeOffset.UnixEpoch;
        var tracker = new SpeicherTracker(db);
        var leser = new BatchLeser(log, tracker, "p", 0, Zwei, null,
            (e, _) => { tracker.Wende(e.AggregateId, e.AggregateVersion); return Task.CompletedTask; }, jetzt: () => uhr);

        for (var i = 0; i < 5; i++)
        {
            log.Haenge(PartitionsLog.StreamIn(1, 2), 1);                    // nur die andere Partition bekommt Events
            await leser.LaufAsync(Ct);
        }
        db.Partitionen["p:0"].Version.Should().Be(1, "fünf Weckungen ohne eigenes Event: ein Commit (der erste), nicht fünf");

        uhr += Konsum.StandardTakt;
        log.Haenge(PartitionsLog.StreamIn(1, 2), 1);
        await leser.LaufAsync(Ct);
        db.Partitionen["p:0"].Version.Should().Be(2);
        db.Partitionen["p:0"].Position.Should().Be(6, "nach dem Poll-Intervall wird die Lese-Position durabel");
    }

    [Fact]
    public async Task Steht_der_Horizont_bleibt_die_Liste_der_Erledigten_begrenzt_und_nichts_geht_verloren()
    {
        // Eine lange offene Transaktion hält die gesetzte Sequenz an: alles danach ist sichtbar, aber nicht gesetzt. Der Leser wendet Jüngeres
        //   trotzdem an und merkt es sich — ohne Grenze wüchse die Liste mit jedem Event und ginge mit jeder Seite an die Datenbank.
        var log = new PartitionsLog();
        var db = new SpeicherKonsum();
        var tracker = new SpeicherTracker(db);
        var konsum = new Konsum(Partitionen: 1, BatchGroesse: 100);
        var leser = new BatchLeser(log, tracker, "p", 0, konsum, null,
            (e, _) => { tracker.Wende(e.AggregateId, e.AggregateVersion); return Task.CompletedTask; });
        const int n = 3 * BatchLeser.ErledigtHoechstens;
        var eintraege = new List<PartitionsLog.Eintrag>();
        for (var i = 1; i <= n; i++) eintraege.Add(log.Haenge(Guid.NewGuid(), 1, gesetzt: false));
        for (var w = 0; w < 5; w++) await leser.LaufAsync(Ct);   // fünf Weckungen, der Horizont steht

        log.GroessteAusschlussliste.Should().BeLessThanOrEqualTo(BatchLeser.ErledigtHoechstens + konsum.BatchGroesse,
            "die Liste der Erledigten ist gedeckelt — eine Seite kostet nicht mehr, je länger der Horizont steht");
        db.Angewandt.Count.Should().BeLessThan(n, "jenseits der Grenze wartet der Leser, bis die Position nachrückt");

        foreach (var e in eintraege) e.Gesetzt = true;   // die Transaktion endet
        for (var w = 0; w < 3; w++) await leser.LaufAsync(Ct);
        db.Angewandt.Should().HaveCount(n, "jedes Event genau einmal").And.OnlyHaveUniqueItems();
        db.Partitionen["p:0"].Position.Should().Be(n);
    }

    [Fact]
    public void Der_Typfilter_nimmt_die_Upcasting_Herkunft_und_faellt_ohne_verlaessliche_Liste_weg()
    {
        var herkunft = new System.Collections.Generic.Dictionary<Type, string[]>
        {
            [typeof(int)] = ["a", "a_v2"],
            [typeof(string)] = ["b"],
        };
        BatchLeser.TypenAus([typeof(int), typeof(string)], herkunft).Should().BeEquivalentTo("a", "a_v2", "b");
        BatchLeser.TypenAus([typeof(int), typeof(double)], herkunft).Should().BeNull("ein unbekannter Typ: ohne Typfilter lesen (richtig, nur langsamer)");
    }
}
