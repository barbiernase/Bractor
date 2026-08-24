using System;
using Abstractions;
using Domain.ImagePair;
using Domain.Projections;
using FluentAssertions;
using Google.Protobuf;
using Infrastructure.Serialization;
using ProtoRepo;
using Xunit;

namespace Infrastructure.Pruefstand.Phase1;

/// <summary>
/// Store-freier Byte-Level-Round-trip des Proto-Query-Mappers (<see cref="ProtoMessageMapper"/> +
/// generierte <c>ProtoRepo</c>-DTOs) für den nullable-Skalar-lastigen Query
/// <c>SucheImagePairs(ImagePairFilter)</c>. Deckt die vom DtoMapper-Umbau berührte
/// C#↔Proto-Skalarabbildung ab — inkl. der (in Phase A noch verlustbehafteten) Nullable-Encodings.
/// Kein Docker/Marten/gRPC nötig: der Round-trip läuft über echte Protobuf-Bytes
/// (<c>ToByteArray</c> + <c>Parser.ParseFrom</c>), also mit der realen Presence-Semantik des Drahts.
/// </summary>
public class DtoMapperRoundTripTests
{
    private static ImagePairFilter RoundTrip(ImagePairFilter filter)
    {
        var mapper = new ProtoMessageMapper();
        var dto = mapper.MapToDto((IQuery)new SucheImagePairs(filter));
        var wire = QueryPayloadDto.Parser.ParseFrom(dto.ToByteArray());
        return ((SucheImagePairs)mapper.MapToDomain(wire)).Filter;
    }

    [Fact]
    public void Typische_Werte_round_trippen_verlustfrei()
    {
        var filter = new ImagePairFilter(
            Von: new DateTimeOffset(2024, 3, 1, 12, 0, 0, TimeSpan.Zero),
            Bis: new DateTimeOffset(2024, 3, 31, 12, 0, 0, TimeSpan.Zero),
            KiKlassifikation: Klassifikation.Anomalie,
            MenschLabel: Klassifikation.KeineAnomalie,
            NurKomplette: true,
            HatMenschLabel: false,
            Seite: 3, SeitenGroesse: 25);

        RoundTrip(filter).Should().BeEquivalentTo(filter);
    }

    /// <summary>
    /// Der Kern der Phase-B-Reparatur: ein nullable <see cref="DateTimeOffset"/> GENAU auf der
    /// Unix-Epoche (0 ms) überlebt jetzt den Round-trip. Unter der alten Sentinel-Kodierung (0 = null)
    /// wäre die Epoche nicht von <c>null</c> zu unterscheiden gewesen; mit proto3 <c>optional</c>
    /// (native Presence) sind gesetzter Grenzwert und <c>null</c> unterscheidbar.
    /// </summary>
    [Fact]
    public void Nullable_DateTimeOffset_auf_der_Epoche_ueberlebt_den_RoundTrip()
    {
        var epoche = DateTimeOffset.FromUnixTimeMilliseconds(0);

        RoundTrip(new ImagePairFilter(Von: epoche)).Von.Should().Be(epoche);
        // Und null bleibt null (nicht in die Epoche verwechselt):
        RoundTrip(new ImagePairFilter(Von: null)).Von.Should().BeNull();
    }
}
