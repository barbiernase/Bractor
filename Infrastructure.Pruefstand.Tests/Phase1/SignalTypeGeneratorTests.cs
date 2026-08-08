using Abstractions;
using Domain.ImagePair;
using FluentAssertions;
using Infrastructure.Mapping;

namespace Infrastructure.Pruefstand.Phase1;

/// <summary>
/// Phase 1 (1b) — Signal-Typ-Generator: pro persistiertem Event ein
/// `StateChangeVia{Event}`-Weckruf (Spec 4.3 / 5.1), im Type-Registry.
/// </summary>
public class SignalTypeGeneratorTests
{
    [Fact]
    public void Signal_traegt_nur_StreamId_und_Version_und_ist_IStateChangeSignal()
    {
        var streamId = Guid.NewGuid();

        var signal = new StateChangeViaImagePairInspiziert(streamId, 7);

        signal.Should().BeAssignableTo<IStateChangeSignal>();
        signal.StreamId.Should().Be(streamId);
        signal.Version.Should().Be(7);
    }

    [Fact]
    public void Signale_stehen_im_TypeRegistry()
    {
        GeneratedTypeRegistry.Signals.Should().ContainKey(nameof(StateChangeViaImagePairInspiziert));
        GeneratedTypeRegistry.Signals[nameof(StateChangeViaImagePairInspiziert)]
            .Should().Be<StateChangeViaImagePairInspiziert>();
    }

    [Fact]
    public void Pro_persistiertem_Event_genau_ein_Signal()
    {
        // 1:1 — kein persistierbares Event ohne Signal, kein Signal zu viel.
        GeneratedTypeRegistry.Signals.Count
            .Should().Be(GeneratedTypeRegistry.PersistableEvents.Count);
    }

    [Fact]
    public void Transiente_Events_bekommen_KEIN_Signal()
    {
        // CommandFailed ist ITransientEvent — steht nicht im Log, also kein Weckruf.
        GeneratedTypeRegistry.Signals.Keys.Should().NotContain("StateChangeViaCommandFailed");
    }
}
