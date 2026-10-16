using Abstractions;
using FluentAssertions;
using Infrastructure.Aggregate;

namespace Infrastructure.Pruefstand.Konsumenten;

/// <summary>
/// Die deterministische CommandId eines Emits (<see cref="EmitId"/>, docs/04 §4.4): dieselbe Ursache an dasselbe Ziel ergibt dieselbe Id — so
/// erkennt die Inbox des Ziels einen wiederholten Emit —, jede andere Ursache, jeder andere Command und jedes andere Ziel eine andere.
/// </summary>
public class EmitIdTests
{
    private static readonly Guid Korrelation = Guid.NewGuid(), Ursache = Guid.NewGuid(), Ziel = Guid.NewGuid();
    private static Guid Id(Guid? korrelation = null, Guid? ursache = null, string diskriminator = "WirkeReaktion", Guid? ziel = null)
        => EmitId.Ableiten(new EmitKausalität(korrelation ?? Korrelation, ursache ?? Ursache, diskriminator), ziel ?? Ziel);

    [Fact]
    public void Dieselbe_Ursache_an_dasselbe_Ziel_ergibt_dieselbe_Id() => Id().Should().Be(Id());

    [Fact]
    public void Andere_Ursache_anderer_Command_anderes_Ziel_oder_andere_Korrelation_ergeben_andere_Ids()
        => new[] { Id(), Id(ursache: Guid.NewGuid()), Id(diskriminator: "AndererCommand"), Id(ziel: Guid.NewGuid()), Id(korrelation: Guid.NewGuid()) }
            .Should().OnlyHaveUniqueItems("ein Fan-out an verschiedene Ziele oder zwei Ursachen dürfen nicht kollidieren");
}
