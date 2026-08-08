using Domain.Reaktion;
using FluentAssertions;
using Infrastructure.Aggregate;

namespace Infrastructure.Pruefstand.Phase3;

/// <summary>
/// Phase 3 — die Empfänger-seitige Garantie: „exactly-once wirksam durch den Empfänger".
///
/// Beweist den Noop-Decider (Spec 9.3) am ECHTEN Framework-Aggregat: derselbe
/// Reaktions-Command (gleiche deterministische ReaktionId) wirkt genau einmal;
/// ein doppelt gesendeter Command verpufft (keine Events, keine Wirkung).
/// </summary>
public class ReaktionsEmpfaengerDeciderTests
{
    [Fact]
    public void Erste_Reaktion_wirkt__doppelte_verpufft()
    {
        var state = new Reaktionsempfaenger { Id = Guid.NewGuid(), Version = 0 };
        var decider = new Reaktionsempfaenger.Decider(state);
        var applier = new Reaktionsempfaenger.Applier(state);

        // Deterministische Id aus dem (fiktiven) auslösenden Event.
        var reaktId = ReaktionsId.For(Guid.NewGuid(), 3, nameof(WirkeReaktion));

        // Erster Command → genau ein Event.
        var erste = decider.Decide(new WirkeReaktion(state.Id, reaktId, "hallo"))
            .Select(o => o.Value).ToList();
        erste.Should().ContainSingle().Which.Should().BeOfType<ReaktionGewirkt>();

        // Effekt anwenden — der Empfänger merkt sich die Reaktion.
        applier.Apply((ReaktionGewirkt)erste[0]);
        state.Wirkungen.Should().Be(1);

        // Duplikat (gleiche ReaktionId) → Noop.
        var zweite = decider.Decide(new WirkeReaktion(state.Id, reaktId, "hallo")).ToList();
        zweite.Should().BeEmpty();
        state.Wirkungen.Should().Be(1, "das Duplikat ist beim Empfänger verpufft");
    }

    [Fact]
    public void Verschiedene_Reaktionen_wirken_beide()
    {
        var state = new Reaktionsempfaenger { Id = Guid.NewGuid(), Version = 0 };
        var decider = new Reaktionsempfaenger.Decider(state);
        var applier = new Reaktionsempfaenger.Applier(state);

        var r1 = decider.Decide(new WirkeReaktion(state.Id, Guid.NewGuid(), "a")).Select(o => o.Value).ToList();
        applier.Apply((ReaktionGewirkt)r1[0]);

        var r2 = decider.Decide(new WirkeReaktion(state.Id, Guid.NewGuid(), "b")).Select(o => o.Value).ToList();
        applier.Apply((ReaktionGewirkt)r2[0]);

        state.Wirkungen.Should().Be(2);
        state.VerarbeiteteReaktionen.Should().HaveCount(2);
    }
}
