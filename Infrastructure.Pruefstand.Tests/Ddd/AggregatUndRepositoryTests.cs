using System.Diagnostics;
using Domain.DddPatterns.Bestellung;
using Domain.DddPatterns.Gemeinsam;
using Domain.DddPatterns.Repository;
using FluentAssertions;

namespace Infrastructure.Pruefstand.Ddd;

/// <summary>
/// DDD-Bausteine AGGREGATE ROOT + ENTITY + FACTORY + DOMAIN EVENT + REPOSITORY, gemeinsam
/// belegt: die Wurzel erzwingt ihre Invarianten, verschmilzt Positionen, gibt Ereignisse
/// aus, wird aus der Historie exakt rekonstituiert und über ein event-sourced Repository
/// geladen/gespeichert — inklusive Nebenläufigkeitsschutz. Plus ein Skalierungstest.
/// </summary>
public class AggregatUndRepositoryTests
{
    private static Geld Eur(decimal betrag) => Geld.Von(betrag, Waehrung.EUR);

    private static Bestellung NeueBestellung(decimal kreditlimit = 1000m) =>
        Bestellung.Eroeffne(Guid.NewGuid(), Guid.NewGuid(), Eur(kreditlimit));

    // ── Aggregat + Entity + Invarianten ──

    [Fact]
    public void Positionen_summieren_sich_und_verschmelzen_bei_gleicher_Artikelnummer()
    {
        var b = NeueBestellung();
        b.FuegePositionHinzu("A-1", "Widget", 2, Eur(100));
        b.FuegePositionHinzu("A-1", "Widget", 3, Eur(100)); // verschmilzt → Menge 5

        b.Positionen.Should().ContainSingle();
        b.Positionen.Single().Menge.Should().Be(5);
        b.Gesamtsumme.Should().Be(Eur(500));
    }

    [Fact]
    public void Kreditlimit_ist_eine_harte_Invariante()
    {
        var b = NeueBestellung(kreditlimit: 250m);
        b.FuegePositionHinzu("A-1", "Widget", 2, Eur(100)); // 200 ok

        var ueberzug = () => b.FuegePositionHinzu("A-2", "Gadget", 1, Eur(100)); // 300 > 250
        ueberzug.Should().Throw<DomaeneException>().WithMessage("*Kreditlimit*");
        b.Gesamtsumme.Should().Be(Eur(200), "die abgelehnte Position wirkt nicht");
    }

    [Fact]
    public void Aufgegebene_Bestellung_ist_gegen_Positionsaenderung_gesperrt()
    {
        var b = NeueBestellung();
        b.FuegePositionHinzu("A-1", "Widget", 1, Eur(100));
        b.GibAuf();

        b.Status.Should().Be(Bestellstatus.Aufgegeben);
        var aendern = () => b.FuegePositionHinzu("A-2", "Gadget", 1, Eur(50));
        aendern.Should().Throw<DomaeneException>();
    }

    [Fact]
    public void Leere_Bestellung_kann_nicht_aufgegeben_werden()
    {
        var b = NeueBestellung();
        var aufgeben = () => b.GibAuf();
        aufgeben.Should().Throw<DomaeneException>().WithMessage("*leere*");
    }

    [Fact]
    public void Entity_hat_Identitaet_nicht_Wertgleichheit()
    {
        var b = NeueBestellung();
        b.FuegePositionHinzu("A-1", "Widget", 1, Eur(10));
        b.AendereMenge("A-1", 9);
        // Dieselbe Position (ArtikelNr A-1), obwohl sich die Menge geändert hat.
        b.Positionen.Single().ArtikelNr.Should().Be("A-1");
        b.Positionen.Single().Menge.Should().Be(9);
    }

    // ── Factory / Rekonstitution ──

    [Fact]
    public void Rekonstitution_aus_der_Historie_ergibt_denselben_Zustand()
    {
        var original = NeueBestellung(kreditlimit: 5000m);
        original.FuegePositionHinzu("A-1", "Widget", 4, Eur(100));
        original.FuegePositionHinzu("A-2", "Gadget", 1, Eur(250));
        original.AendereMenge("A-1", 6);

        var historie = original.NeueEreignisse.ToList();
        var rekonstruiert = Bestellung.AusHistorie(historie);

        rekonstruiert.Id.Should().Be(original.Id);
        rekonstruiert.Version.Should().Be(original.Version);
        rekonstruiert.Gesamtsumme.Should().Be(original.Gesamtsumme);
        rekonstruiert.Positionen.Should().HaveCount(2);
        rekonstruiert.NeueEreignisse.Should().BeEmpty("Rekonstitution erzeugt keine neuen Ereignisse");
    }

    // ── Repository (event-sourced, optimistische Nebenläufigkeit) ──

    private static EventSourcedRepository<Bestellung, IDomaenenEreignis> NeuesRepo() =>
        new(Bestellung.AusHistorie);

    [Fact]
    public async Task Repository_speichert_und_laedt_ueber_Replay()
    {
        var repo = NeuesRepo();
        var b = NeueBestellung(kreditlimit: 5000m);
        b.FuegePositionHinzu("A-1", "Widget", 3, Eur(100));
        await repo.SpeichereAsync(b);
        b.NeueEreignisse.Should().BeEmpty("nach dem Speichern sind die Ereignisse übernommen");

        var geladen = await repo.LadeAsync(b.Id);
        geladen!.Gesamtsumme.Should().Be(Eur(300));

        geladen.FuegePositionHinzu("A-2", "Gadget", 1, Eur(200));
        await repo.SpeichereAsync(geladen);

        var wiederGeladen = await repo.LadeAsync(b.Id);
        wiederGeladen!.Gesamtsumme.Should().Be(Eur(500));
        wiederGeladen.Version.Should().Be(geladen.Version);
    }

    [Fact]
    public async Task Repository_erkennt_Nebenlaeufigkeitskonflikt()
    {
        var repo = NeuesRepo();
        var b = NeueBestellung(kreditlimit: 5000m);
        b.FuegePositionHinzu("A-1", "Widget", 1, Eur(100));
        await repo.SpeichereAsync(b);

        // Zwei Leser derselben Version.
        var leserA = await repo.LadeAsync(b.Id);
        var leserB = await repo.LadeAsync(b.Id);

        leserA!.FuegePositionHinzu("A-2", "Gadget", 1, Eur(100));
        await repo.SpeichereAsync(leserA); // gewinnt

        leserB!.FuegePositionHinzu("A-3", "Gizmo", 1, Eur(100));
        var konflikt = async () => await repo.SpeichereAsync(leserB); // verliert
        await konflikt.Should().ThrowAsync<NebenlaeufigkeitsKonflikt>();
    }

    // ── Performance ──

    [Fact]
    public void Aggregat_haelt_grosse_Historien_und_Replay_aus()
    {
        const int Positionen = 20_000;
        var b = Bestellung.Eroeffne(Guid.NewGuid(), Guid.NewGuid(), Eur(1_000_000_000m));

        var sw = Stopwatch.StartNew();
        for (var i = 0; i < Positionen; i++)
            b.FuegePositionHinzu($"A-{i}", "Artikel", 1, Eur(1));
        var schreibZeit = sw.Elapsed;

        b.Positionen.Should().HaveCount(Positionen);
        b.Gesamtsumme.Should().Be(Eur(Positionen));

        var historie = b.NeueEreignisse.ToList();
        sw.Restart();
        var rekonstruiert = Bestellung.AusHistorie(historie);
        var replayZeit = sw.Elapsed;

        rekonstruiert.Gesamtsumme.Should().Be(b.Gesamtsumme);
        // Großzügige Budgets: der laufende Saldo hält das Hinzufügen O(1) statt O(N²) — ein
        // versehentliches O(N²) (20k² = 400M Ops) würde diese Budgets sofort sprengen.
        schreibZeit.Should().BeLessThan(TimeSpan.FromSeconds(10), $"20k Invarianten-geprüfte Positionen (waren {schreibZeit.TotalMilliseconds:N0} ms)");
        replayZeit.Should().BeLessThan(TimeSpan.FromSeconds(5), $"Replay von {historie.Count:N0} Ereignissen (waren {replayZeit.TotalMilliseconds:N0} ms)");
    }
}
