using System;
using Abstractions;
using Domain.ImagePair;
using FluentAssertions;
using Infrastructure.Serialization;

namespace Infrastructure.Pruefstand.Phase1;

/// <summary>
/// Deckt den Cross-Node-Wire-Round-Trip eines <see cref="SignalEnvelope"/> ab — die eine Stelle,
/// die der Signal-Umbau berührt: <c>GeneratedWirePoly.WriteSignal/ReadSignal</c> serialisieren das
/// Signal jetzt UNIFORM (StreamId, Version) selbst, ohne per-Typ-STJ-<c>JsonTypeInfo</c> und ohne
/// <c>[JsonSerializable]</c>-Eintrag. Hier wird bewiesen, dass Diskriminator + Nutzlast über den
/// echten Wire-Serializer verlustfrei zurückkommen (store-frei, kein Host nötig).
/// </summary>
public class SignalWireRoundTripTests
{
    [Fact]
    public void SignalEnvelope_round_trippt_ueber_den_Wire_ohne_STJ_TypeInfo()
    {
        var streamId = Guid.NewGuid();
        var env = new SignalEnvelope
        {
            Signal = new StateChangeViaImagePairInspiziert(streamId, 7)
        };

        var typeName = GeneratedWire.TypeName(env);
        var bytes = GeneratedWire.Serialize(env);
        var back = (SignalEnvelope)GeneratedWire.Deserialize(bytes, typeName);

        back.Signal.Should().BeOfType<StateChangeViaImagePairInspiziert>();
        back.Signal.StreamId.Should().Be(streamId);
        back.Signal.Version.Should().Be(7);
    }

    [Fact]
    public void Verschiedene_Signal_Typen_behalten_ihren_Diskriminator()
    {
        var streamId = Guid.NewGuid();
        var env = new SignalEnvelope
        {
            Signal = new StateChangeViaImagePairKomplett(streamId, 42)
        };

        var bytes = GeneratedWire.Serialize(env);
        var back = (SignalEnvelope)GeneratedWire.Deserialize(bytes, GeneratedWire.TypeName(env));

        back.Signal.Should().BeOfType<StateChangeViaImagePairKomplett>();
        back.Signal.StreamId.Should().Be(streamId);
        back.Signal.Version.Should().Be(42);
    }
}
