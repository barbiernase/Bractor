using FluentAssertions;
using Infrastructure.Aggregate;

namespace Infrastructure.Pruefstand.Phase3;

/// <summary>
/// Phase 3 — deterministische Reaktions-Id (Spec 9.3): gleiche Auslöse-Position +
/// gleicher Command-Typ ergeben dieselbe Id, sonst eine andere. Fundament dafür,
/// dass ein wiederholter Reaktions-Command beim Empfänger als Duplikat erkennbar ist.
/// </summary>
public class ReaktionsIdTests
{
    [Fact]
    public void Gleiche_Ausloeser_ergeben_dieselbe_Id()
    {
        var stream = Guid.NewGuid();

        var a = ReaktionsId.For(stream, 7, "WirkeReaktion");
        var b = ReaktionsId.For(stream, 7, "WirkeReaktion");

        a.Should().Be(b);
        a.Should().NotBe(Guid.Empty);
    }

    [Fact]
    public void Andere_Version_ergibt_andere_Id()
    {
        var stream = Guid.NewGuid();
        ReaktionsId.For(stream, 7, "WirkeReaktion")
            .Should().NotBe(ReaktionsId.For(stream, 8, "WirkeReaktion"));
    }

    [Fact]
    public void Anderer_Stream_ergibt_andere_Id()
    {
        ReaktionsId.For(Guid.NewGuid(), 7, "WirkeReaktion")
            .Should().NotBe(ReaktionsId.For(Guid.NewGuid(), 7, "WirkeReaktion"));
    }

    [Fact]
    public void Anderer_Diskriminator_ergibt_andere_Id()
    {
        var stream = Guid.NewGuid();
        ReaktionsId.For(stream, 7, "WirkeReaktion")
            .Should().NotBe(ReaktionsId.For(stream, 7, "AndererCommand"));
    }
}
