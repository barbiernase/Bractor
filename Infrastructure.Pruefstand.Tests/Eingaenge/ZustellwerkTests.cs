using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Abstractions;
using FluentAssertions;
using Infrastructure.Eingaenge;
using Xunit;

namespace Infrastructure.Pruefstand.Eingaenge;

/// <summary>
/// Der Zusteller der offenen Eingänge (docs/04 §4.4), store-frei: fertig verlässt die Tabelle, angenommen ist nach der Frist wieder fällig (kommt bis
/// dahin kein „fertig“, geht der Eingang erneut hinaus), später und gescheitert sind bald wieder fällig. Dazu die Kennungen und die Beilagen der Folgen.
/// </summary>
[Trait("Kategorie", "Garantie")]
public class ZustellwerkTests
{
    private sealed class Tabelle : IOffeneEingaenge
    {
        public readonly List<OffenerEingang> Zeilen = new();
        public readonly List<Guid> Erledigt = new();
        public readonly List<(Guid Kennung, TimeSpan Nach)> Verschoben = new();
        public Task<IReadOnlyList<OffenerEingang>> FaelligeAsync(int hoechstens, CancellationToken ct) =>
            Task.FromResult<IReadOnlyList<OffenerEingang>>(Zeilen.Where(z => !Erledigt.Contains(z.Kennung) && Verschoben.All(v => v.Kennung != z.Kennung))
                .Take(hoechstens).ToList());
        public Task ErledigtAsync(IReadOnlyCollection<Guid> kennungen, CancellationToken ct) { Erledigt.AddRange(kennungen); return Task.CompletedTask; }
        public Task VerschiebeAsync(IReadOnlyCollection<Guid> kennungen, TimeSpan nach, CancellationToken ct)
        {
            Verschoben.AddRange(kennungen.Select(k => (k, nach)));
            return Task.CompletedTask;
        }
        public Task ParkeAsync(IReadOnlyCollection<(Guid Kennung, string Grund)> geparkt, CancellationToken ct) => Task.CompletedTask;
        public Task LegeAsync(IReadOnlyList<EingangOffen> eingaenge, Guid stream, CancellationToken ct) => Task.CompletedTask;
    }

    private sealed class Zustellung(Func<OffenerEingang, Zustellausgang> ausgang) : IEingangsZustellung
    {
        public readonly List<Guid> Gesehen = new();
        public Task<Zustellausgang> StelleZuAsync(OffenerEingang e, CancellationToken ct)
        {
            Gesehen.Add(e.Kennung);
            return Task.FromResult(ausgang(e));
        }
    }

    private static OffenerEingang Zeile(string funktion) =>
        new(Guid.NewGuid(), funktion, "X", "{}", Guid.NewGuid(), 1, DateTimeOffset.UtcNow, null, null, 0);

    [Fact]
    public async Task Fertig_verlaesst_die_Tabelle_angenommen_kommt_nach_der_Frist_wieder_spaeter_und_gescheitert_bald()
    {
        var t = new Tabelle();
        var (fertig, angenommen, spaeter, wirft) = (Zeile("fertig"), Zeile("angenommen"), Zeile("spaeter"), Zeile("wirft"));
        t.Zeilen.AddRange(new[] { fertig, angenommen, spaeter, wirft });
        var z = new Zustellung(e => e.Instanz switch
        {
            "fertig" => Zustellausgang.Fertig,
            "angenommen" => Zustellausgang.Angenommen,
            "spaeter" => Zustellausgang.Spaeter,
            _ => throw new InvalidOperationException("nicht erreichbar"),
        });

        (await new Zustellwerk(t, z).LaufAsync(default)).Should().Be(2, "fertig und angenommen sind zugestellt");

        z.Gesehen.Should().BeEquivalentTo(new[] { fertig.Kennung, angenommen.Kennung, spaeter.Kennung, wirft.Kennung });
        t.Erledigt.Should().Equal(fertig.Kennung);
        t.Verschoben.Should().BeEquivalentTo(new[]
        {
            (angenommen.Kennung, Zustellwerk.Frist), (spaeter.Kennung, Zustellwerk.Spaeter), (wirft.Kennung, Zustellwerk.Spaeter),
        });
    }

    [Fact]
    public void Kennungen_sind_abgeleitet_derselbe_Command_ergibt_dieselben()
    {
        var cmd = Guid.NewGuid();
        Eingangsvertrag.Eingang(cmd, "A").Should().Be(Eingangsvertrag.Eingang(cmd, "A"));
        Eingangsvertrag.Eingang(cmd, "A").Should().NotBe(Eingangsvertrag.Eingang(cmd, "B"), "je Kopf ein eigener Eingang");
    }

    [Fact]
    public void Ein_Decide_gibt_nur_Events_aus_ein_Command_ist_ein_Fehler()
    {
        Entscheidungsausgabe.NurEvents(new Melde(), [new Gemeldet()]).Should().ContainSingle().Which.Should().BeOfType<Gemeldet>();
        FluentActions.Invoking(() => Entscheidungsausgabe.NurEvents(new Melde(), [new Gemeldet(), new Melde()]))
            .Should().Throw<InvalidOperationException>().WithMessage("*gab Melde aus*nur Events, Hinweise und Fristen*CQRS114*");
        FluentActions.Invoking(() => Entscheidungsausgabe.NurEvents(new Melde(), [new AnAggregat()])).Should().Throw<InvalidOperationException>();
    }

    private sealed record Melde : ICommand;
    private sealed record AnAggregat : IAggregatCommand { public Guid Ziel => Guid.Empty; }
    private sealed record Gemeldet : IEvent;
}
