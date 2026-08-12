using Abstractions;
using Cqrs.Testing;
using Domain.Auftrag;
using Domain.Konto;
using Domain.Ueberweisung;
using FluentAssertions;
using Infrastructure; // AggregateHandlerFactory

namespace Infrastructure.Pruefstand.Dsl;

/// <summary>
/// Die drei Debugger-Bausteine: Guard-Bindung mit Werten (das „70 &lt; 200"), Coverage-Erfassung
/// (welche Zweige berührt) und Saga-Trace → DSL.
/// </summary>
public class DebuggerBausteineTests
{
    private static readonly IAggregateHandlerFactory Fabrik = new AggregateHandlerFactory();

    [Fact]
    public void GuardBinder_macht_aus_dem_Ausdruck_das_konkrete_Warum()
    {
        var gebunden = GuardBinder.Binde(
            "State.Verfuegbar < cmd.Betrag",
            new Dictionary<string, object?> { ["Verfuegbar"] = 70m },
            new Dictionary<string, object?> { ["Betrag"] = 200m });

        gebunden.Should().Be("70 < 200");
    }

    [Fact]
    public void Abdeckung_erfasst_die_beruehrten_Zweige()
    {
        var id = Guid.NewGuid();
        var trace = Szenario.Für<Konto>(Fabrik, id)
            .Gegeben(new KontoEroeffnet(100, Gesperrt: false))
            .Wenn(new ReserviereBetrag(id, 200))
            .DannAbgelehnt<DeckungReichtNicht>()
            .Trace;

        var abd = new Abdeckung().Erfasse(trace);

        abd.Ids.Should().Contain("cmd:ReserviereBetrag")
            .And.Contain("evt:DeckungReichtNicht")
            .And.Contain("produces:ReserviereBetrag->DeckungReichtNicht");
    }

    [Fact]
    public void AusSaga_erzeugt_ein_Szenario_Skelett_mit_Feuer_Reihenfolge_und_offenem_Join()
    {
        var quelle = Guid.NewGuid();
        var ziel = Guid.NewGuid();

        var trace = SagaSzenario.Mit(Fabrik, new UeberweisungsProzess())
            .Gegeben<Konto>(quelle, new KontoEroeffnet(20, Gesperrt: false)) // deckt nicht
            .Gegeben<Konto>(ziel, new KontoEroeffnet(0, Gesperrt: false))
            .Wenn(new BeauftrageUeberweisung(Guid.NewGuid(), quelle, ziel, 50))
            .Trace;

        var code = DslSchreiber.AusSaga(trace);

        code.Should().Contain("SagaSzenario.Mit(new AggregateHandlerFactory(), new UeberweisungsProzess())")
            .And.Contain(".Wenn(new BeauftrageUeberweisung(")
            .And.Contain(".FeuertInReihenfolge(typeof(ReserviereBetrag)")
            .And.Contain(".SagaWartetAuf<BetragReserviert>();");
    }
}
