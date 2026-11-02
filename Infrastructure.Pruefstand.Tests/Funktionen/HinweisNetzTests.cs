using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Abstractions;
using FluentAssertions;
using Infrastructure.Funktionen;
using Xunit;

namespace Infrastructure.Pruefstand.Funktionen;

/// <summary>
/// Das Hinweis-Netz (docs/04 §4.4, docs/10 E7, A7), store-frei: je Funktion, die den Hinweis-Typ hört (die Verkettung), ein Eingang über die Wege —
/// verlierbar, mit der Kennung aus Hinweis und Funktion (abgeleitet: derselbe Hinweis ergibt dieselbe Kennung, der Parent erkennt ihn doppelt).
/// </summary>
[Trait("Kategorie", "Garantie")]
public class HinweisNetzTests
{
    private sealed record Merkmal(double Wert) : ITransientEvent;
    private sealed record Leise(double Wert) : ITransientEvent;

    private sealed class Wege : IFunktionsWege
    {
        public readonly ConcurrentQueue<EingangZustellen> Gemeldet = new();
        public Task<EingangQuittung?> StelleZuAsync(EingangZustellen eingang, TimeSpan frist, CancellationToken ct) => throw new NotSupportedException();
        public void Melde(EingangZustellen hinweis) => Gemeldet.Enqueue(hinweis);
    }

    private static HinweisZustellen Hinweis(IEvent h, Guid kennung) =>
        new(kennung, h, Guid.NewGuid(), 3, DateTimeOffset.UtcNow, Guid.NewGuid().ToString(), "Akteur");

    [Fact]
    public void Ein_Hinweis_geht_an_jede_hoerende_Funktion_mit_abgeleiteter_Kennung_und_der_Herkunft_seines_Eingangs()
    {
        var wege = new Wege();
        var netz = new HinweisNetz(new Verkettung(new Dictionary<Type, string[]> { [typeof(Merkmal)] = new[] { "Pruefung", "Schaetzer" } }), () => wege);
        var kennung = Guid.NewGuid();
        var h = Hinweis(new Merkmal(5), kennung);

        netz.Verteile(h).Should().Be(2);

        wege.Gemeldet.Select(e => e.Instanz).Should().Equal("Pruefung", "Schaetzer");
        wege.Gemeldet.Should().OnlyContain(e => e.Art == Eingangsart.Hinweis && e.Nachricht.Equals(h.Hinweis) && e.Stream == h.Stream && e.Version == 3
                                                && e.Akteur == "Akteur" && e.Korrelation == h.Korrelation);
        wege.Gemeldet.Select(e => e.Kennung).Should().Equal(Eingangsvertrag.Eingang(kennung, "Pruefung"), Eingangsvertrag.Eingang(kennung, "Schaetzer"));

        netz.Verteile(h);
        wege.Gemeldet.Skip(2).Select(e => e.Kennung).Should().Equal(wege.Gemeldet.Take(2).Select(e => e.Kennung), "derselbe Hinweis, dieselben Kennungen");
    }

    [Fact]
    public void Einen_Hinweis_den_niemand_hoert_meldet_das_Netz_nicht_er_ist_nur_fuer_Clients()
    {
        var wege = new Wege();
        var netz = new HinweisNetz(new Verkettung(new Dictionary<Type, string[]> { [typeof(Merkmal)] = new[] { "Pruefung" } }), () => wege);
        netz.Hoert(typeof(Leise)).Should().BeFalse();
        netz.Verteile(Hinweis(new Leise(1), Guid.NewGuid())).Should().Be(0);
        wege.Gemeldet.Should().BeEmpty();
    }
}
