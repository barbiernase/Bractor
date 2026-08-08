using Abstractions;
using Domain.ImagePair;
using FluentAssertions;
using Infrastructure.Mapping;

namespace Infrastructure.Pruefstand.Phase1;

/// <summary>
/// Phase 1 (1c) — Emit-Wiring: die generierte Event→Signal-Factory und die
/// Transport-Hülle. (Das echte „Signal erscheint auf dem PubSub" ist Integration
/// und braucht den laufenden Host; hier wird die reflection-freie Zuordnung geprüft.)
/// </summary>
public class SignalEmitTests
{
    [Fact]
    public void Factory_erzeugt_das_passende_Signal_mit_StreamId_und_Version()
    {
        var streamId = Guid.NewGuid();

        var signal = GeneratedSignalFactory.Create(new ImagePairInspiziert(), streamId, 5);

        signal.Should().BeOfType<StateChangeViaImagePairInspiziert>();
        signal!.StreamId.Should().Be(streamId);
        signal.Version.Should().Be(5);
    }

    [Fact]
    public void Signal_traegt_sein_Event_im_generischen_Marker_TG1()
    {
        // ★ P1b (TG-1): die Event→Signal-Kante steckt jetzt im TYP-Argument des Markers
        //   IStateChangeSignal<TEvent>, nicht im Namens-Präfix. Der Factory-Generator liest sie daraus.
        typeof(StateChangeViaImagePairInspiziert)
            .Should().Implement<IStateChangeSignal<ImagePairInspiziert>>();
    }

    [Fact]
    public void Factory_liefert_null_fuer_transiente_Events()
    {
        // CommandFailed ist ITransientEvent → kein Signal.
        var signal = GeneratedSignalFactory.Create(
            new CommandFailed("IrgendEinCommand", "grund", Guid.NewGuid().ToString()),
            Guid.NewGuid(), 1);

        signal.Should().BeNull();
    }

    [Fact]
    public void SignalEnvelope_exponiert_das_Signal_als_Payload()
    {
        var signal = new StateChangeViaImagePairInspiziert(Guid.NewGuid(), 3);

        IMessageEnvelope envelope = new SignalEnvelope { Signal = signal };

        // Der BrokerPublisher shardet über Payload.GetType() → muss der Signal-Typ sein.
        envelope.Payload.Should().BeSameAs(signal);
        envelope.Payload.GetType().Should().Be<StateChangeViaImagePairInspiziert>();
    }
}
