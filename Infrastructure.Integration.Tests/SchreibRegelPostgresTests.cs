using Abstractions;
using Domain.ImagePair;
using Domain.Infrastructure;
using FluentAssertions;
using Infrastructure.Persistence;
using Infrastructure.Projections;
using Marten;

namespace Infrastructure.Integration.Tests;

/// <summary>
/// Die Schreib-Regel von <see cref="MartenCoCommitStoreBase"/> gegen echtes Postgres, mit einer Sonde, die den
/// Konflikt ERZWINGT: zwei Stapel laden dasselbe geteilte Dokument, treffen sich an einer Barriere (beide haben jetzt
/// denselben Stand gelesen) und schreiben dann. Ohne Versionsprüfung gewönne still der letzte Commit.
/// </summary>
[Collection(GeteiltCollection.Name)]
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
    public async Task Adapter_wiederholt_nach_Konflikt_beide_Beitraege_bleiben_erhalten()
    {
        var zaehler = "z-" + Guid.NewGuid().ToString("N");
        var proj = "it-sonde-" + Guid.NewGuid().ToString("N");
        var (sa, sb) = (Guid.NewGuid(), Guid.NewGuid());
        var es = _fx.EventStore();
        await es.AppendEventsAsync(sa, 0, new IEvent[] { new ImagePairInspiziert() });
        await es.AppendEventsAsync(sb, 0, new IEvent[] { new ImagePairInspiziert() });

        using var treffpunkt = new Barrier(2);
        var a = new SondenStore(_fx.Store, treffpunkt);
        var b = new SondenStore(_fx.Store, treffpunkt);
        var adapterA = new ProjectionAdapter(es, a, null, proj, (_, _) => a.Erhoehe(zaehler));
        var adapterB = new ProjectionAdapter(es, b, null, proj, (_, _) => b.Erhoehe(zaehler));

        // Der erste Versuch kollidiert (Barriere), der Verlierer wiederholt ohne Barriere — keine Ausnahme nach außen.
        await Task.WhenAll(Task.Run(() => adapterA.WakeAsync(sa)), Task.Run(() => adapterB.WakeAsync(sb)));

        (await Lies(zaehler))!.Stand.Should().Be(2, "beide Beiträge, keiner überschrieben");
        (await a.LastProcessedVersionAsync(proj, sa, default)).Should().Be(1);
        (await b.LastProcessedVersionAsync(proj, sb, default)).Should().Be(1);
    }

    [Fact]
    public async Task Fremdes_Dokument_ueber_den_Eigen_Weg_wirft_sofort()
    {
        var store = new SondenStore(_fx.Store);
        var stream = Guid.NewGuid();
        await store.LastProcessedVersionAsync("it-sonde-fremd", stream, default);

        await store.Invoking(s => s.AendereEigen(stream)).Should().NotThrowAsync();
        await store.Invoking(s => s.AendereEigen(Guid.NewGuid()))
            .Should().ThrowAsync<InvalidOperationException>().WithMessage("*gehört nicht dem Stream des Stapels*");
        await store.Invoking(s => s.SchreibeZeile(Guid.NewGuid()))
            .Should().ThrowAsync<InvalidOperationException>().WithMessage("*gehört nicht dem Stream des Stapels*");
    }

    [Fact]
    public async Task Schreiben_ausserhalb_eines_Stapels_wirft()
        => await new SondenStore(_fx.Store).Invoking(s => s.AendereEigen(Guid.NewGuid()))
            .Should().ThrowAsync<InvalidOperationException>().WithMessage("*außerhalb eines Stapels*");

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
        await a.LastProcessedVersionAsync(proj, sa, default);
        await b.LastProcessedVersionAsync(proj, sb, default);
        await a.Erhoehe(zaehler);
        await b.Erhoehe(zaehler);

        var ergebnisse = await Task.WhenAll(
            Task.Run(() => Commit(a, proj, sa)),
            Task.Run(() => Commit(b, proj, sb)));

        ergebnisse.Count(e => e is null).Should().Be(1, "genau ein Stapel committet");
        ergebnisse.Count(e => e is SchreibKonfliktException).Should().Be(1, "der andere meldet den Konflikt");
        (await Lies(zaehler))!.Stand.Should().Be(erwarteterStand, "nur der Gewinner hat geschrieben");

        // Der Verlierer hat auch seine Marke NICHT gesetzt — sein Stapel ist ganz zurückgefallen und wiederholbar.
        var marken = new[] { await a.LastProcessedVersionAsync(proj, sa, default), await b.LastProcessedVersionAsync(proj, sb, default) };
        marken.Should().BeEquivalentTo(new[] { 1, -1 });
    }

    private static async Task<Exception?> Commit(SondenStore store, string proj, Guid stream)
    {
        try { await store.MarkProcessedAsync(proj, stream, 1, default); return null; }
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
internal sealed class SondenStore : MartenCoCommitStoreBase
{
    private readonly Barrier? _treffpunkt;
    private bool _getroffen;

    public SondenStore(IDocumentStore store, Barrier? treffpunkt = null) : base(store) => _treffpunkt = treffpunkt;

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
