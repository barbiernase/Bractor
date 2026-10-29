using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Abstractions;
using FluentAssertions;
using Infrastructure.Verteilen;
using Proto;
using Xunit;

namespace Infrastructure.Pruefstand.Verteilen;

/// <summary>
/// Der Verteiler eines Knotens (docs/konzept-akteure.md §5): ein Commit geht an jede Sitzung, die einen seiner Typen hört — je
/// Sitzung eine Zustellung, in Log-Reihenfolge; eine gezielte Antwort geht an genau ihre Sitzung, auch wenn sie den Typ nicht hört; eine beendete
/// Sitzung ist ausgetragen; liest eine Sitzung nicht nach, verwirft er über der Grenze ihres Rückstaus. Lokales <see cref="ActorSystem"/>, Proben als
/// Sitzungen.
/// </summary>
[Trait("Kategorie", "Garantie")]
public class VerteilerTests
{
    private sealed record Gebucht(int Betrag) : IEvent;
    private sealed record Storniert : IEvent;
    private sealed record Abgelehnt : ITransientEvent;

    private static readonly TimeSpan Geduld = TimeSpan.FromSeconds(5);

    /// <summary>Eine Sitzung, die nur mitschreibt, was ihr zugestellt wird (und den Rückstau nicht abbaut, wenn sie hängt).</summary>
    private sealed class Probe(ConcurrentQueue<Zustellung> empfangen, Rueckstau rueckstau, bool haengt = false) : IActor
    {
        public Task ReceiveAsync(IContext context)
        {
            if (context.Message is Zustellung z)
            {
                empfangen.Enqueue(z);
                if (!haengt) rueckstau.Erledigt();
            }
            return Task.CompletedTask;
        }
    }

    private sealed class Sitzung(string id)
    {
        public string Id { get; } = id;
        public PID Pid { get; set; } = null!;
        public ConcurrentQueue<Zustellung> Empfangen { get; } = new();
        public Rueckstau Rueckstau { get; } = new();
        public List<EventEnvelope> Alles => Empfangen.SelectMany(z => z.Envelopes).ToList();
    }

    private static Sitzung Starte(ActorSystem system, PID verteiler, string id, bool haengt, params Type[] hoert)
    {
        var s = new Sitzung(id);
        s.Pid = system.Root.Spawn(Props.FromProducer(() => new Probe(s.Empfangen, s.Rueckstau, haengt)));
        system.Root.Send(verteiler, new SitzungMeldetAn(id, s.Pid, hoert.ToHashSet(), s.Rueckstau));
        return s;
    }

    private static EventEnvelope E(IEvent payload, int version, string? ziel = null) =>
        new() { AggregateId = Guid.Empty, AggregateVersion = version, Payload = payload, AggregateType = "Konto", ZielSitzung = ziel };

    private static async Task BisAsync(Func<bool> bedingung)
    {
        var bis = DateTime.UtcNow + Geduld;
        while (!bedingung())
        {
            if (DateTime.UtcNow > bis) throw new TimeoutException("Bedingung nicht erreicht");
            await Task.Delay(10);
        }
    }

    private static (ActorSystem System, PID Verteiler) Neu()
    {
        var system = new ActorSystem();
        var verteiler = system.Root.SpawnNamed(Props.FromProducer(() => new VerteilerActor()), VerteilerActor.Name);
        return (system, verteiler);
    }

    [Fact]
    public async Task Ein_Commit_geht_je_Sitzung_einmal_an_die_die_seine_Typen_hoeren_in_Log_Reihenfolge()
    {
        var (system, verteiler) = Neu();
        await using var _ = system;
        var beide = Starte(system, verteiler, "k#0001", false, typeof(Gebucht), typeof(Storniert));
        var gebucht = Starte(system, verteiler, "k#0002", false, typeof(Gebucht));
        var keine = Starte(system, verteiler, "k#0003", false);

        system.Root.Send(verteiler, new Verteile([E(new Gebucht(1), 1), E(new Storniert(), 2), E(new Gebucht(2), 3)]));

        await BisAsync(() => beide.Empfangen.Count == 1 && gebucht.Empfangen.Count == 1);
        beide.Alles.Select(e => e.AggregateVersion).Should().Equal(1, 2, 3);
        gebucht.Alles.Select(e => e.AggregateVersion).Should().Equal(1, 3);
        await Task.Delay(100);
        keine.Empfangen.Should().BeEmpty("sie hört keinen der Typen");
    }

    [Fact]
    public async Task Eine_gezielte_Antwort_geht_nur_an_ihre_Sitzung_auch_ohne_dass_sie_den_Typ_hoert()
    {
        var (system, verteiler) = Neu();
        await using var _ = system;
        var ziel = Starte(system, verteiler, "k#0001", false);
        var andere = Starte(system, verteiler, "k#0002", false, typeof(Abgelehnt));

        system.Root.Send(verteiler, new Verteile([E(new Abgelehnt(), 4, ziel: "k#0001")]));

        await BisAsync(() => ziel.Empfangen.Count == 1);
        ziel.Alles.Single().Payload.Should().BeOfType<Abgelehnt>();
        await Task.Delay(100);
        andere.Empfangen.Should().BeEmpty("die Antwort gilt nur der Sitzung, die den Command geschickt hat");
    }

    [Fact]
    public async Task Eine_beendete_Sitzung_ist_ausgetragen_und_eine_neue_Anmeldung_ersetzt_das_Gehoerte()
    {
        var (system, verteiler) = Neu();
        await using var _ = system;
        var weg = Starte(system, verteiler, "k#0001", false, typeof(Gebucht));
        var bleibt = Starte(system, verteiler, "k#0002", false, typeof(Gebucht));
        await system.Root.StopAsync(weg.Pid);
        system.Root.Send(verteiler, new SitzungMeldetAn(bleibt.Id, bleibt.Pid, new HashSet<Type> { typeof(Storniert) }, bleibt.Rueckstau));

        system.Root.Send(verteiler, new Verteile([E(new Gebucht(1), 1), E(new Storniert(), 2)]));

        await BisAsync(() => bleibt.Empfangen.Count == 1);
        bleibt.Alles.Select(e => e.Payload).Should().ContainSingle().Which.Should().BeOfType<Storniert>("die zweite Anmeldung ersetzt die erste");
    }

    [Fact]
    public async Task Ueber_der_Grenze_des_Rueckstaus_verwirft_der_Verteiler_statt_die_Mailbox_wachsen_zu_lassen()
    {
        var (system, verteiler) = Neu();
        await using var _ = system;
        var haengt = Starte(system, verteiler, "k#0001", true, typeof(Gebucht));

        for (var i = 0; i < Rueckstau.Grenze + 50; i++)
            system.Root.Send(verteiler, new Verteile([E(new Gebucht(i), i + 1)]));

        await BisAsync(() => haengt.Rueckstau.Verworfen == 50);
        await Task.Delay(100);
        haengt.Empfangen.Count.Should().Be(Rueckstau.Grenze);
    }

    [Fact]
    public void Die_Sitzungs_Id_nennt_ihren_Knoten()
    {
        var id = Verteilung.SitzungsId("10.0.0.7:4020", 12);
        id.Should().Be("10.0.0.7:4020#0012");
        Verteilung.Knoten(id).Should().Be("10.0.0.7:4020");
        Verteilung.Knoten("session-0001").Should().BeNull("eine Id ohne Knoten findet keinen Verteiler");
    }
}
