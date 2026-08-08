using Abstractions;
using Domain.Flug;
using Domain.Hotel;
using Domain.Reise;
using Domain.Reiseauftrag;
using FluentAssertions;
using Infrastructure.Extensions;
using Infrastructure.Pipeline;

namespace Infrastructure.Pruefstand.Phase5;

/// <summary>
/// Beweist: der Einstiegs-Dispatch braucht KEINEN Magic String mehr. <c>AggregateType</c> wird aus der
/// generierten Map <see cref="GeneratedPipelines.CommandAggregateTypes"/> abgeleitet (dieselbe, die der
/// Prozess-Manager nutzt), <c>AggregateId</c> aus dem Command. In-memory, ohne Cluster — ein Fake-Dispatcher
/// fängt nur den gebauten Envelope.
/// </summary>
public class SprechenderDispatchTests
{
    private sealed class FakeDispatcher : IAggregateDispatcher
    {
        public CommandEnvelope? Letzter { get; private set; }
        public void Dispatch(CommandEnvelope envelope) => Letzter = envelope;
    }

    [Theory]
    [MemberData(nameof(Faelle))]
    public void Dispatch_leitet_AggregateType_aus_der_generierten_Map_ab(ICommand cmd, string erwartetesAggregat)
    {
        var fake = new FakeDispatcher();

        fake.Dispatch(cmd);   // die neue, sprechende Überladung — nur der Command

        fake.Letzter.Should().NotBeNull();
        fake.Letzter!.AggregateType.Should().Be(erwartetesAggregat, "die Namespace-Konvention bestimmt das Ziel");
        fake.Letzter.AggregateId.Should().Be(cmd.AggregateId, "die Instanz-Id trägt der Command selbst");
        fake.Letzter.Payload.Should().BeSameAs(cmd);
    }

    public static IEnumerable<object[]> Faelle()
    {
        var g = Guid.NewGuid();
        // Einstiegs- UND Prozess-Schritt-Commands quer durch die Reise-Domäne:
        yield return new object[] { new RichteFlugEin(g, 10), "Flugkontingent" };
        yield return new object[] { new ReserviereFlug(g, 3), "Flugkontingent" };
        yield return new object[] { new GebeFlugFrei(g, 3), "Flugkontingent" };
        yield return new object[] { new RichteHotelEin(g, 10), "Hotelkontingent" };
        yield return new object[] { new ReserviereHotel(g, 2), "Hotelkontingent" };
        yield return new object[] { new BestaetigeReise(g, Guid.NewGuid()), "Reise" };
        yield return new object[] { new BucheReise(g, Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), 3, 2), "Reiseauftrag" };
    }
}
