using Abstractions;
using Domain.ImagePair;
using FluentAssertions;
using Infrastructure.Aggregate;

namespace Infrastructure.Pruefstand.Phase1;

/// <summary>
/// Phase 1 (1a-i) — Version PRO Event im Publish-Pfad (Spec 4.2).
///
/// Belegt den Fix der Phase-1-Lücke: früher trug jedes Event eines Batches den
/// Batch-Endwert (<c>_state.Version</c>); jetzt trägt jedes Event seine eigene,
/// monotone, lückenlose Position.
/// </summary>
public class EventVersioningTests
{
    private static IReadOnlyList<IEvent> DreiEvents() => new IEvent[]
    {
        new ImagePairInspiziert(),
        new ImagePairInspiziert(),
        new ImagePairInspiziert()
    };

    [Fact]
    public void NeuerStream_beginnt_bei_1_und_zaehlt_pro_Event_hoch()
    {
        var id = Guid.NewGuid();

        var envelopes = EventEnvelopeFactory.BuildPerEvent(
            DreiEvents(), id, "ImagePair",
            baseVersion: 0, correlationId: "c", causationId: "cause", userId: "u");

        envelopes.Select(e => e.AggregateVersion).Should().Equal(1, 2, 3);
    }

    [Fact]
    public void BestehenderStream_zaehlt_ab_ExpectedVersion_weiter()
    {
        var id = Guid.NewGuid();

        var envelopes = EventEnvelopeFactory.BuildPerEvent(
            DreiEvents(), id, "ImagePair",
            baseVersion: 5, correlationId: "c", causationId: "cause", userId: "u");

        // Der Stream stand auf 5 → die drei neuen Events liegen auf 6, 7, 8.
        envelopes.Select(e => e.AggregateVersion).Should().Equal(6, 7, 8);
    }

    [Fact]
    public void Metadaten_und_Payload_landen_unveraendert_auf_jedem_Envelope()
    {
        var id = Guid.NewGuid();
        var events = DreiEvents();

        var envelopes = EventEnvelopeFactory.BuildPerEvent(
            events, id, "ImagePair",
            baseVersion: 0, correlationId: "korr-1", causationId: "cmd-1", userId: "tobi");

        envelopes.Should().OnlyContain(e =>
            e.AggregateId == id &&
            e.AggregateType == "ImagePair" &&
            e.CorrelationId == "korr-1" &&
            e.CausationId == "cmd-1" &&
            e.UserId == "tobi" &&
            e.TargetSubscriberId == null);

        envelopes.Select(e => e.Payload).Should().Equal(events);
    }

    [Fact]
    public void KeineEvents_ergibt_leere_Liste()
    {
        var envelopes = EventEnvelopeFactory.BuildPerEvent(
            new List<IEvent>(), Guid.NewGuid(), "ImagePair",
            baseVersion: 0, correlationId: "c", causationId: "cause", userId: "u");

        envelopes.Should().BeEmpty();
    }
}
