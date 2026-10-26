using Abstractions;
using FluentAssertions;
using Infrastructure.Lebenszyklus;
using Microsoft.Extensions.DependencyInjection;
using Pruefdomaene.Posten;
using static Pruefdomaene.Integration.Tests.Pruefstand;
using PostenState = Pruefdomaene.Posten.Posten;

namespace Pruefdomaene.Integration.Tests.Garantien;

/// <summary>
/// Passivierung (PF-5, docs/11 §11.2/§11.6) im echten Cluster: ein Aggregat- und ein Funktions-Actor, die eine Weile nichts hören, stoppen sich; die
/// nächste Nachricht aktiviert sie frisch — derselbe Stand, dieselbe Inbox (eine alte CommandId wirkt nicht erneut), dieselbe Position der Funktion
/// Nachschub (nichts doppelt, nichts verloren). Beweis der Passivierung: der Aggregat-Actor schreibt beim Stoppen einen Snapshot.
/// </summary>
[Trait("Kategorie", "Garantie")]
public class LebenszyklusGarantieTests
{
    private static readonly TimeSpan Frist = TimeSpan.FromSeconds(60);

    [Fact]
    public async Task Passivierung_und_Reaktivierung_ergeben_denselben_Stand()
    {
        var kurz = new Passivierung(TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(1));
        using var host = Cluster("lebenszyklus", snapshotSchwelle: 10_000,
            dienste: s => s.AddSingleton(kurz));   // die letzte Registrierung gewinnt
        await host.StartAsync();
        try
        {
            var store = host.Services.GetRequiredService<IEventStoreRepository>();
            var snapshots = host.Services.GetRequiredService<ISnapshotStore>();
            var p = Guid.NewGuid();
            var erste = Guid.NewGuid();
            (await FrageAsync(host, new EroeffnePosten(new(p), 100, Mindestbestand: 98), Guid.NewGuid()))!.Success.Should().BeTrue();
            (await FrageAsync(host, new ReservierePosten(new(p), 5, Beleg.Ohne), erste))!.Success.Should().BeTrue();   // Fehlmenge 3 → Nachschub

            async Task<int> Nachschuebe() => (await store.ReadStreamAsync(p, 0, default)).Count(e => e.Payload is PostenZugebucht);
            await Warte(async () => await Nachschuebe() == 1, Frist, "Die Funktion Nachschub hat nicht nachgebucht.");

            // Der Leerlauf passiviert den Aggregat-Actor: beim Stoppen schreibt er seinen Snapshot (die Schwelle allein löst keinen aus).
            await Warte(async () => await snapshots.TryLoadAsync<PostenState>(p, default) is not null, Frist, "Der Actor ist nicht passiviert.");
            await Task.Delay(TimeSpan.FromSeconds(2));   // auch der Funktions-Actor des Nachschubs ist jetzt passiviert

            // Frisch aktiviert: die alte CommandId erkennt die Inbox (aus dem Snapshot), eine neue Reservierung wirkt auf dem fortgeschriebenen Stand.
            (await FrageAsync(host, new ReservierePosten(new(p), 5, Beleg.Ohne), erste))!.Success.Should().BeTrue();
            (await FrageAsync(host, new ReservierePosten(new(p), 5, Beleg.Ohne), Guid.NewGuid()))!.Success.Should().BeTrue();
            await Warte(async () => await Nachschuebe() == 2, Frist, "Der reaktivierte Nachschub hat nicht nachgebucht.");
            await Task.Delay(TimeSpan.FromSeconds(3));

            (await store.ReadStreamAsync(p, 0, default)).Count(e => e.Payload is PostenReserviert).Should().Be(2, "die alte CommandId wirkt nicht erneut");
            (await Nachschuebe()).Should().Be(2, "die reaktivierte Funktion liest ab ihrer Position — nichts doppelt, nichts verloren");
            (await Zustand<PostenState>(host.Services, p)).Bestand.Should().Be(100 + 3 + 5);
        }
        finally { await host.StopAsync(); }
    }
}
