using System;
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

        uhr += Konsum.PollIntervall;
        log.Haenge(PartitionsLog.StreamIn(1, 2), 1);
        await leser.LaufAsync(Ct);
        db.Partitionen["p:0"].Version.Should().Be(2);
        db.Partitionen["p:0"].Position.Should().Be(6, "nach dem Poll-Intervall wird die Lese-Position durabel");
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
