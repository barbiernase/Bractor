using System.Diagnostics;
using Abstractions;
using Domain.Verkauf;
using FluentAssertions;
using Infrastructure;   // generierte AggregateHandlerFactory

namespace Infrastructure.Pruefstand.Ddd;

/// <summary>
/// DDD-Muster durch die ECHTE Framework-Pipeline: das Aggregat <see cref="Verkaufsauftrag"/>
/// (Aggregate Root + Entity + Value Object <see cref="Geldwert"/> + Domain Events +
/// Invarianten) wird über die GENERIERTE <see cref="AggregateHandlerFactory"/> (Decider +
/// Applier) getrieben — genau wie das Konto-Aggregat, ohne Cluster/Store. Die Commands/Events
/// laufen dabei real über den generierten DTO-/Wire-Pfad (Proto neu erzeugt).
/// </summary>
public class VerkaufAggregatTests
{
    private static Geldwert Eur(decimal betrag) => Geldwert.Euro(betrag);

    private sealed class Fixture
    {
        public IAggregateHandler Handler { get; }
        public Verkaufsauftrag Auftrag { get; }
        public List<IEvent> LetzteAblehnungen { get; } = new();

        public Fixture(decimal kreditlimit)
        {
            Auftrag = new Verkaufsauftrag();
            Handler = new AggregateHandlerFactory().CreateHandler(Auftrag);
            Wende(new EroeffneVerkaufsauftrag(Guid.NewGuid(), Guid.NewGuid(), Geldwert.Euro(kreditlimit)));
        }

        public IReadOnlyList<IEvent> Wende(ICommand cmd)
        {
            LetzteAblehnungen.Clear();
            var events = Handler.HandleCommand(cmd).ToList();
            foreach (var e in events)
            {
                if (e is ITransientEvent) { LetzteAblehnungen.Add(e); continue; }
                Handler.ApplyEvent(e);
                Auftrag.Version++;   // das macht im Betrieb die AggregateActorBase
            }
            return events;
        }
    }

    [Fact]
    public void Eroeffnen_dann_Position_hinzufuegen_summiert_ueber_das_Value_Object()
    {
        var f = new Fixture(kreditlimit: 1000);
        var id = f.Auftrag.Id;

        f.Wende(new FuegePositionHinzu(id, "A-1", "Widget", 2, Eur(100)));
        f.Wende(new FuegePositionHinzu(id, "A-2", "Gadget", 1, Eur(250)));

        f.Auftrag.Positionen.Should().HaveCount(2);
        f.Auftrag.Gesamtsumme.Should().Be(Eur(450));
    }

    [Fact]
    public void Gleiche_Artikelnummer_verschmilzt_die_Menge()
    {
        var f = new Fixture(kreditlimit: 1000);
        var id = f.Auftrag.Id;

        f.Wende(new FuegePositionHinzu(id, "A-1", "Widget", 2, Eur(100)));
        f.Wende(new FuegePositionHinzu(id, "A-1", "Widget", 3, Eur(100)));

        f.Auftrag.Positionen.Should().ContainSingle();
        f.Auftrag.Positionen.Single().Menge.Should().Be(5);
        f.Auftrag.Gesamtsumme.Should().Be(Eur(500));
    }

    [Fact]
    public void Kreditlimit_wird_als_Ablehnung_erzwungen_ohne_Wirkung()
    {
        var f = new Fixture(kreditlimit: 250);
        var id = f.Auftrag.Id;

        f.Wende(new FuegePositionHinzu(id, "A-1", "Widget", 2, Eur(100))); // 200 ok
        f.Wende(new FuegePositionHinzu(id, "A-2", "Gadget", 1, Eur(100))); // 300 > 250

        f.LetzteAblehnungen.Should().ContainSingle().Which.Should().BeOfType<KreditlimitUeberschritten>();
        f.Auftrag.Gesamtsumme.Should().Be(Eur(200), "die abgelehnte Position wirkt nicht");
    }

    [Fact]
    public void Fremde_Waehrung_wird_abgelehnt()
    {
        var f = new Fixture(kreditlimit: 1000);
        f.Wende(new FuegePositionHinzu(f.Auftrag.Id, "A-1", "Widget", 1, Geldwert.Von(100, Waehrung.USD)));

        f.LetzteAblehnungen.Should().ContainSingle().Which.Should().BeOfType<WaehrungPasstNicht>();
        f.Auftrag.Positionen.Should().BeEmpty();
    }

    [Fact]
    public void Aufgeben_sperrt_weitere_Positionen()
    {
        var f = new Fixture(kreditlimit: 1000);
        var id = f.Auftrag.Id;
        f.Wende(new FuegePositionHinzu(id, "A-1", "Widget", 1, Eur(100)));
        f.Wende(new GibVerkaufsauftragAuf(id));

        f.Auftrag.Status.Should().Be(Auftragsstatus.Aufgegeben);

        f.Wende(new FuegePositionHinzu(id, "A-2", "Gadget", 1, Eur(50)));
        f.LetzteAblehnungen.Should().ContainSingle().Which.Should().BeOfType<AuftragNichtOffen>();
    }

    [Fact]
    public void Stornieren_ist_idempotent()
    {
        var f = new Fixture(kreditlimit: 1000);
        var id = f.Auftrag.Id;
        f.Wende(new StorniereVerkaufsauftrag(id, "Kundenwunsch"));
        f.Auftrag.Status.Should().Be(Auftragsstatus.Storniert);

        var events = f.Wende(new StorniereVerkaufsauftrag(id, "nochmal"));
        events.Should().BeEmpty("zweites Stornieren ist ein Noop");
    }

    [Fact]
    public void Aggregat_durch_die_generierte_Pipeline_ist_performant()
    {
        var f = new Fixture(kreditlimit: 1_000_000_000);
        var id = f.Auftrag.Id;
        const int N = 10_000;

        var sw = Stopwatch.StartNew();
        for (var i = 0; i < N; i++)
            f.Wende(new FuegePositionHinzu(id, $"A-{i}", "Artikel", 1, Eur(1)));
        sw.Stop();

        f.Auftrag.Positionen.Should().HaveCount(N);
        f.Auftrag.Gesamtsumme.Should().Be(Eur(N));
        sw.Elapsed.Should().BeLessThan(TimeSpan.FromSeconds(10),
            $"{N:N0} Commands durch Decider+Applier waren {sw.Elapsed.TotalMilliseconds:N0} ms");
    }
}
