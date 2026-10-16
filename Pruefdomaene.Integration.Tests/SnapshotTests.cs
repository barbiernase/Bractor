using Abstractions;
using FluentAssertions;
using Infrastructure.Aggregate;
using Infrastructure.Extensions;
using Infrastructure.Persistence;
using Marten;
using Microsoft.Extensions.DependencyInjection;
using Pruefdomaene.Posten;
using static Pruefdomaene.Integration.Tests.Pruefstand;
using PostenState = Pruefdomaene.Posten.Posten;

namespace Pruefdomaene.Integration.Tests;

/// <summary>
/// Snapshots gegen ECHTES Postgres (docs/13 T-1): der <see cref="MartenSnapshotStore"/> über die GENERIERTE Registrierung der Komposition
/// (Roundtrip samt Inboxen, Überschreiben, Löschen) — und die volle Schleife im Cluster: ein Actor schreibt über der Schwelle einen
/// Snapshot, eine spätere Rehydration seedet NACHWEISLICH daraus (Snapshot + Tail == Voll-Fold).
/// </summary>
public class SnapshotTests : IClassFixture<SnapshotTests.Fixture>
{
    private readonly Fixture _fx;
    public SnapshotTests(Fixture fx) => _fx = fx;

    [Fact]
    public async Task Save_dann_Load_liefert_State_und_Version_verlustfrei()
    {
        var store = new MartenSnapshotStore(_fx.Store);
        var id = Guid.NewGuid();

        await store.SaveAsync(new Snapshot<PostenState>
        {
            Id = id, Version = 5, SchemaVersion = GeneratedSnapshotSchema.VersionOf<PostenState>(),
            State = new PostenState { Id = id, Version = 5, Bestand = 100, Reserviert = 30 },
            UpdatedAt = DateTimeOffset.UtcNow,
        }, default);

        var geladen = await store.TryLoadAsync<PostenState>(id, default);

        geladen.Should().NotBeNull();
        geladen!.Version.Should().Be(5);
        geladen.SchemaVersion.Should().Be(GeneratedSnapshotSchema.VersionOf<PostenState>());
        geladen.State.Bestand.Should().Be(100);
        geladen.State.Verfuegbar.Should().Be(70, "aus dem State neu berechnet");
    }

    [Fact]
    public async Task Save_ueberschreibt_und_Delete_entfernt()
    {
        var store = new MartenSnapshotStore(_fx.Store);
        var id = Guid.NewGuid();

        await store.SaveAsync(new Snapshot<PostenState> { Id = id, Version = 1, State = new PostenState { Id = id, Version = 1, Bestand = 10 } }, default);
        await store.SaveAsync(new Snapshot<PostenState> { Id = id, Version = 2, State = new PostenState { Id = id, Version = 2, Bestand = 20 } }, default);
        (await store.TryLoadAsync<PostenState>(id, default))!.State.Bestand.Should().Be(20, "ein aktueller Snapshot je Aggregat");

        await store.DeleteAsync<PostenState>(id, default);
        (await store.TryLoadAsync<PostenState>(id, default)).Should().BeNull();
    }

    [Fact]
    public async Task Actor_schreibt_Snapshot_und_spaetere_Rehydration_seedet_daraus()
    {
        using var host = Cluster("pruef-snapshot", snapshotSchwelle: 3);
        await host.StartAsync();
        try
        {
            var d = host.Services.GetRequiredService<IAggregateDispatcher>();
            var es = host.Services.GetRequiredService<IEventStoreRepository>();
            var snapshots = host.Services.GetRequiredService<ISnapshotStore>();
            var fabrik = host.Services.GetRequiredService<IAggregateHandlerFactory>();
            var id = Guid.NewGuid();

            // Vier Commands nacheinander, je mit der OCC-Erwartung der vorigen Version (Client-Modus) — Bestand 10 → 11 → 13 → 16.
            d.Dispatch(new EroeffnePosten(id, 10));
            var soll = 10;
            foreach (var (menge, i) in new[] { 1, 2, 3 }.Select((m, i) => (m, i)))
            {
                await Warte(async () => (await Zustand<PostenState>(host.Services, id)).Bestand == soll, TimeSpan.FromSeconds(60),
                    $"Schritt {i}: Bestand {soll} nicht erreicht (Cluster-Boot?).");
                d.Dispatch(new BuchePostenZu(id, menge), expectedVersion: (await Zustand<PostenState>(host.Services, id)).Version);
                soll += menge;
            }
            await Warte(async () => (await Zustand<PostenState>(host.Services, id)).Bestand == 16, TimeSpan.FromSeconds(30), "Bestand 16 nicht erreicht.");

            // Der (best-effort, out-of-band) Snapshot liegt in Postgres — ein TEIL-Stand vor dem letzten Command.
            await Warte(async () => await snapshots.TryLoadAsync<PostenState>(id, default) is { Version: > 0 }, TimeSpan.FromSeconds(15),
                "Der Actor hat keinen Snapshot nach Postgres geschrieben.");
            var snap = (await snapshots.TryLoadAsync<PostenState>(id, default))!;
            snap.SchemaVersion.Should().Be(GeneratedSnapshotSchema.VersionOf<PostenState>());

            // Rehydration MIT Snapshot seedet aus ihm und vervollständigt über den Tail; ohne Snapshot dasselbe Ergebnis (Voll-Fold).
            var mit = await AggregateRehydrator.LoadAsync<PostenState>(id, snapshots, es, fabrik, GeneratedSnapshotSchema.VersionOf<PostenState>(), default);
            var ohne = await AggregateRehydrator.LoadAsync<PostenState>(id, null, es, fabrik, null, default);
            mit.SnapshotVersion.Should().Be(snap.Version, "nicht ab 0 gefaltet");
            mit.State.Bestand.Should().Be(16);
            mit.State.Version.Should().Be(ohne.State.Version);
            mit.State.Bestand.Should().Be(ohne.State.Bestand, "Snapshot + Tail == Voll-Fold");

            // Ein Snapshot mit fremder Schema-Version wird ignoriert (Voll-Replay, Invariante 1).
            var stale = await AggregateRehydrator.LoadAsync<PostenState>(id, snapshots, es, fabrik, "andere-version", default);
            stale.SnapshotVersion.Should().Be(0);
            stale.State.Bestand.Should().Be(16);
        }
        finally { await host.StopAsync(); }
    }

    public sealed class Fixture : IDisposable
    {
        public IDocumentStore Store { get; } = Pruefstand.Store("pruef_snapshot");
        public void Dispose() => Store.Dispose();
    }
}
