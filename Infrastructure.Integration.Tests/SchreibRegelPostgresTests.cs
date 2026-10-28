using Abstractions;
using Domain.ImagePair;
using Core;
using FluentAssertions;
using Infrastructure.Persistence;
using Infrastructure.Projections;
using Marten;

namespace Infrastructure.Integration.Tests;

/// <summary>
/// Die Schreib-Regel von <see cref="MartenStore"/> gegen echtes Postgres, mit einer Sonde, die den
/// Konflikt ERZWINGT: zwei Batches zweier Partitionen laden dasselbe geteilte Dokument, treffen sich an einer Barriere (beide haben jetzt
/// denselben Stand gelesen) und schreiben dann. Ohne Versionsprüfung gewönne still der letzte Commit.
/// </summary>
[Collection(GeteiltCollection.Name)]
[Trait("Kategorie", "Garantie")]
public class SchreibRegelPostgresTests
{
    private readonly GeteiltFixture _fx;
    public SchreibRegelPostgresTests(GeteiltFixture fx) => _fx = fx;

    [Fact]
    public async Task Geteilt_gleichzeitig_angelegt_genau_ein_Stapel_gewinnt_der_andere_faellt_ganz_zurueck()
        => await ZweiStapelGleichzeitig(vorhandenerStand: null, erwarteterStand: 1);

    [Fact]
    public async Task Geteilt_gleichzeitig_geaendert_genau_ein_Stapel_gewinnt_der_andere_faellt_ganz_zurueck()
        => await ZweiStapelGleichzeitig(vorhandenerStand: 5, erwarteterStand: 6);

    [Fact]
    public async Task Zwei_Partitions_Leser_nach_Konflikt_beide_Beitraege_bleiben_erhalten()
    {
        var zaehler = "z-" + Guid.NewGuid().ToString("N");
        var proj = "it-sonde-" + Guid.NewGuid().ToString("N");
        var konsum = new Konsum(2, 500);
        await BatchHilfe.StarteAmKopfAsync(_fx.Store, proj, konsum);
        var (sa, sb) = (BatchHilfe.StreamIn(0, 2), BatchHilfe.StreamIn(1, 2));
        var es = _fx.EventStore();
        await es.AppendEventsAsync(sa, 0, new IEvent[] { new ImagePairInspiziert() });
        await es.AppendEventsAsync(sb, 0, new IEvent[] { new ImagePairInspiziert() });

        using var treffpunkt = new Barrier(2);
        var a = new SondenStore(_fx.Store, treffpunkt);
        var b = new SondenStore(_fx.Store, treffpunkt);
        var leserA = new BatchLeser(es, new Projektionsnaht(a, null), proj, 0, konsum, null, (_, _) => a.Erhoehe(zaehler));
        var leserB = new BatchLeser(es, new Projektionsnaht(b, null), proj, 1, konsum, null, (_, _) => b.Erhoehe(zaehler));

        // Der erste Versuch kollidiert (Barriere), der Verlierer wiederholt seinen Stream einzeln — keine Ausnahme nach außen.
        await Task.WhenAll(Task.Run(() => leserA.LaufAsync()), Task.Run(() => leserB.LaufAsync()));

        (await Lies(zaehler))!.Stand.Should().Be(2, "beide Beiträge, keiner überschrieben");
        (await BatchHilfe.GuardAsync(_fx.Store, proj, sa)).Should().Be(1);
        (await BatchHilfe.GuardAsync(_fx.Store, proj, sb)).Should().Be(1);
    }

    [Fact]
    public async Task Fremdes_Dokument_ueber_den_Eigen_Weg_wirft_sofort()
    {
        var store = new SondenStore(_fx.Store);
        var stream = Guid.NewGuid();
        store.Beginne(Generationen.Erste);
        store.Stream(stream);

        await store.Invoking(s => s.AendereEigen(stream)).Should().NotThrowAsync();
        await store.Invoking(s => s.AendereEigen(Guid.NewGuid()))
            .Should().ThrowAsync<InvalidOperationException>().WithMessage("*gehört nicht dem Stream des Events*");
        await store.Invoking(s => s.SchreibeZeile(Guid.NewGuid()))
            .Should().ThrowAsync<InvalidOperationException>().WithMessage("*gehört nicht dem Stream des Events*");
    }

    [Fact]
    public async Task Schreiben_ausserhalb_eines_Stapels_wirft()
        => await new SondenStore(_fx.Store).Invoking(s => s.AendereEigen(Guid.NewGuid()))
            .Should().ThrowAsync<InvalidOperationException>().WithMessage("*außerhalb eines Batches*");

    // ─── Helfer ───

    private async Task ZweiStapelGleichzeitig(int? vorhandenerStand, int erwarteterStand)
    {
        var zaehler = "z-" + Guid.NewGuid().ToString("N");
        var proj = "it-sonde-" + Guid.NewGuid().ToString("N");
        if (vorhandenerStand is { } stand)
        {
            await using var s = _fx.Store.LightweightSession();
            s.Store(new SondenZaehler { Id = zaehler, Stand = stand });
            await s.SaveChangesAsync();
        }

        using var treffpunkt = new Barrier(2);
        var (sa, sb) = (Guid.NewGuid(), Guid.NewGuid());
        var a = new SondenStore(_fx.Store, treffpunkt);
        var b = new SondenStore(_fx.Store, treffpunkt);
        a.Beginne(Generationen.Erste); a.Stream(sa); await a.Erhoehe(zaehler);
        b.Beginne(Generationen.Erste); b.Stream(sb); await b.Erhoehe(zaehler);

        // Zwei Partitionen derselben Projektion, je ein Batch — sie teilen nur das geteilte Dokument.
        var ergebnisse = await Task.WhenAll(
            Task.Run(() => Commit(a, PartitionsCheckpoint.Neu(proj, 0), sa)),
            Task.Run(() => Commit(b, PartitionsCheckpoint.Neu(proj, 1), sb)));

        ergebnisse.Count(e => e is null).Should().Be(1, "genau ein Batch committet");
        ergebnisse.Count(e => e is SchreibKonfliktException).Should().Be(1, "der andere meldet den Konflikt");
        (await Lies(zaehler))!.Stand.Should().Be(erwarteterStand, "nur der Gewinner hat geschrieben");

        // Der Verlierer hat auch Guard und Partition NICHT gesetzt — sein Batch ist ganz zurückgefallen und wiederholbar.
        var guards = new[] { await BatchHilfe.GuardAsync(_fx.Store, proj, sa), await BatchHilfe.GuardAsync(_fx.Store, proj, sb) };
        guards.Should().BeEquivalentTo(new[] { 1, -1 });
        var partitionen = new[] { await BatchHilfe.PartitionAsync(_fx.Store, proj, 0), await BatchHilfe.PartitionAsync(_fx.Store, proj, 1) };
        partitionen.Count(p => p is not null).Should().Be(1);
    }

    private static async Task<Exception?> Commit(SondenStore store, PartitionsCheckpoint stand, Guid stream)
    {
        try { await store.SchreibeMitFortschrittAsync(stand, new Dictionary<Guid, int> { [stream] = 1 }, default); return null; }
        catch (Exception ex) { return ex; }
    }

    private async Task<SondenZaehler?> Lies(string id)
    {
        await using var s = _fx.Store.QuerySession();
        return await s.LoadAsync<SondenZaehler>(id);
    }
}

/// <summary>Geteiltes Test-Dokument (mit Optimistic Concurrency registriert, siehe <see cref="GeteiltFixture"/>).</summary>
public record SondenZaehler : IGeteiltesReadModel
{
    public string Id { get; init; } = "";
    public int Stand { get; init; }
}

/// <summary>Eigenes Test-Dokument (Schlüssel = Stream).</summary>
public record SondenEigen : IReadModel
{
    public Guid Id { get; init; }
}

/// <summary>
/// Sonde: erhöht einen geteilten Zähler. Mit Barriere wartet der ERSTE Versuch je Instanz nach dem Laden auf den
/// anderen Stapel — so haben beide sicher denselben Stand gelesen, bevor einer schreibt.
/// </summary>
internal sealed class SondenStore : MartenStore
{
    private readonly Barrier? _treffpunkt;
    private bool _getroffen;

    public SondenStore(IDocumentStore store, Barrier? treffpunkt = null) : base(store, typeof(SondenZaehler), typeof(SondenEigen)) => _treffpunkt = treffpunkt;

    public Task Erhoehe(string id) => EnqueueGeteilt<SondenZaehler>(id, alt =>
    {
        Treffen();
        return new SondenZaehler { Id = id, Stand = (alt?.Stand ?? 0) + 1 };
    });

    public Task AendereEigen(Guid id) => EnqueueTransform<SondenEigen>(id, d => d);

    public Task SchreibeZeile(Guid besitzer) => EnqueueZeile(besitzer, new SondenEigen { Id = Guid.NewGuid() });

    private void Treffen()
    {
        if (_treffpunkt is null || _getroffen) return;
        _getroffen = true;
        if (!_treffpunkt.SignalAndWait(TimeSpan.FromSeconds(10)))
            throw new TimeoutException("Barriere: der zweite Stapel kam nicht.");
    }
}
