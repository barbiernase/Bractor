using Abstractions;
using Cqrs.Testing;
using Domain.Konto;
using FluentAssertions;
using Infrastructure; // AggregateHandlerFactory

namespace Infrastructure.Pruefstand.Dsl;

/// <summary>
/// Beweist die Rückrichtung: ein gefahrenes Szenario → DSL-Quelltext. Das ist die Grundlage für
/// „GUI/Board erzeugt die DSL" (Board-Session als Regressionstest exportieren).
/// </summary>
public class DslSchreiberTests
{
    private static readonly IAggregateHandlerFactory Fabrik = new AggregateHandlerFactory();

    [Fact]
    public void Erzeugt_aus_einer_Trace_gueltigen_DSL_Quelltext()
    {
        var id = Guid.NewGuid();

        var trace = Szenario.Für<Konto>(Fabrik, id)
            .Gegeben(new KontoEroeffnet(100, Gesperrt: false))
            .Vorab(new ReserviereBetrag(id, 30))
            .Wenn(new ReserviereBetrag(id, 200))
            .DannAbgelehnt<DeckungReichtNicht>()
            .Trace;

        var code = DslSchreiber.AusSzenario(trace);

        code.Should().Contain("var id = Guid.NewGuid();")
            .And.Contain("Szenario.Für<Konto>(new AggregateHandlerFactory(), id)")
            .And.Contain(".Gegeben(new KontoEroeffnet(100m, false))")
            .And.Contain(".Vorab(new ReserviereBetrag(id, 30m))")
            .And.Contain(".Wenn(new ReserviereBetrag(id, 200m))")
            .And.Contain(".DannAbgelehnt<DeckungReichtNicht>();");
    }

    [Fact]
    public void Der_persistente_Ausgang_wird_zu_einem_Dann()
    {
        var id = Guid.NewGuid();

        var trace = Szenario.Für<Konto>(Fabrik, id)
            .Gegeben(new KontoEroeffnet(100, Gesperrt: false))
            .Wenn(new ReserviereBetrag(id, 30))
            .Dann<BetragReserviert>()
            .Trace;

        DslSchreiber.AusSzenario(trace).Should().Contain(".Dann<BetragReserviert>();");
    }
}
