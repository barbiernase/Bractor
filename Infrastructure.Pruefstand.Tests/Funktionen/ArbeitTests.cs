using System;
using System.Collections.Generic;
using System.Linq;
using Abstractions;
using FluentAssertions;
using Infrastructure.Akteure;
using Infrastructure.Funktionen;
using Xunit;

namespace Infrastructure.Pruefstand.Funktionen;

/// <summary>
/// Arbeit für Akteure (docs/konzept-akteure.md §5, Entscheidung 13): hört ein Akteur einen Command ohne Ziel, macht die Laufzeit daraus eine Instanz
/// unter seinem Namen mit genau einem Knoten, der draußen rechnet — Köpfe = die gehörten Commands, Ausgaben = die Commands seines IDarf.
/// Store-frei, an einem synthetischen Akteur.
/// </summary>
public class ArbeitTests
{
    private sealed record Pruefer : IAkteur;
    private sealed record Leser : IAkteur;
    private sealed record Pruefe(Guid Id) : ICommand;
    private sealed record Schaetze(Guid Id) : ICommand;
    private sealed record Befund(Guid Id) : ICommand;
    private sealed record Abgelehnt(Guid Id) : ICommand;
    private sealed record Frage : IQuery;
    private sealed record Gesehen : IEvent;

    private static readonly IReadOnlySet<Type> Leer = new HashSet<Type>();

    private static AkteurRechte A(string name, Type typ, Type[] commands, Type[] arbeit, Type[]? hoert = null) =>
        new(name, typ, commands.ToHashSet(), new HashSet<Type> { typeof(Frage) }, Leer, (hoert ?? []).ToHashSet(), arbeit.ToHashSet());

    private static AkteurRechte PrueferRechte =>
        A("Pruefer", typeof(Pruefer), [typeof(Befund), typeof(Abgelehnt)], [typeof(Schaetze), typeof(Pruefe)], [typeof(Gesehen)]);

    [Fact]
    public void Der_Baustein_hat_je_gehoertem_Command_einen_Eingang_und_rechnet_draussen()
    {
        var b = Arbeit.Baustein(PrueferRechte);
        b.Name.Should().Be("Pruefer");
        b.Typ.Should().Be(typeof(Pruefer));
        b.Draussen.Should().BeTrue("ohne Fabrik und Rechnung — eine Sitzung, die den Akteur verkörpert, gibt die Antwort");
        b.Eingaenge.Select(e => e.Typ).Should().Equal(typeof(Pruefe), typeof(Schaetze));
        b.Eingaenge.Should().OnlyContain(e => e.Strom, "eine Antwort darf in Stücken kommen");
        b.Eingaenge.Should().OnlyContain(e => e.Ausgaben.SequenceEqual(new[] { typeof(Abgelehnt), typeof(Befund) }), "die Antwort sind die Commands seines IDarf");
        b.Eingang(typeof(Gesehen)).Should().BeNull("Kenntnis ist keine Arbeit");
        b.Liest.Should().BeFalse();
    }

    [Fact]
    public void Der_Plan_hat_einen_Knoten_seine_Koepfe_sind_die_gehoerten_Commands_hinaus_gehen_seine_Commands()
    {
        var p = Arbeit.Plan(PrueferRechte);
        p.Name.Should().Be("Pruefer");
        p.Typ.Should().Be(typeof(Pruefer));
        p.Quelle.Should().BeNull();
        var k = p.Knoten.Should().ContainSingle().Subject;
        k.Name.Should().Be("Pruefer");
        k.Funktion.Should().Be("Pruefer", "der Baustein heißt wie der Akteur");
        k.Operationen.Select(o => o.Auf).Should().Equal(0, 1);
        k.Operationen.Select(o => o.Ports.Single().Typen.Single()).Should().Equal(typeof(Pruefe), typeof(Schaetze));
        p.Koepfe.Keys.Should().BeEquivalentTo(new[] { typeof(Pruefe), typeof(Schaetze) });
        p.Koepfe.Values.Should().OnlyContain(v => v.SequenceEqual(new[] { "Pruefer" }));
        p.Ausgaenge.Should().BeEquivalentTo(new[] { typeof(Abgelehnt), typeof(Befund) });
    }

    [Fact]
    public void Je_Akteur_mit_Arbeit_eine_Instanz_unter_seinem_Namen_die_er_hineingibt()
    {
        var akteure = new Dictionary<string, AkteurRechte>
        {
            ["Pruefer"] = PrueferRechte,
            ["Leser"] = A("Leser", typeof(Leser), [], [], [typeof(Gesehen)]),
        };
        var i = Arbeit.Instanzen(akteure).Should().ContainSingle("ein Akteur ohne Arbeit bekommt keine Instanz").Subject;
        i.Name.Should().Be("Pruefer");
        i.Akteur.Should().Be("Pruefer", "was die Instanz antwortet, gibt der Akteur hinein");
        i.Graph.Name.Should().Be("Pruefer");
        i.KopfFuer(typeof(Pruefe)).Should().Equal("Pruefer");
        i.Ort("Pruefer").Should().Be("Pruefer/Pruefer");
    }

    [Fact]
    public void JeGraphEine_nimmt_mit_Akteuren_ihre_Arbeit_dazu()
    {
        var akteure = new Dictionary<string, AkteurRechte> { ["Pruefer"] = PrueferRechte };
        GraphInstanzen.JeGraphEine([], akteure: akteure).Alle.Select(x => x.Name).Should().Equal("Pruefer");
        GraphInstanzen.JeGraphEine([]).Alle.Should().BeEmpty("ohne Akteure keine Arbeit");
        GraphInstanzen.JeGraphEine([], akteure: akteure).Koepfe().Hoerer(typeof(Schaetze)).Should().Equal(["Pruefer"],
            "ein gehörter Command hat die Instanz des Akteurs als Empfänger am Kopf");
        GraphInstanzen.JeGraphEine([], akteure: akteure).Koepfe().Hoerer(typeof(Gesehen)).Should().BeEmpty();
    }
}
