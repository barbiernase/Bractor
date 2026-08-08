using Abstractions;
using Domain.Konto;
using FluentAssertions;
using Infrastructure.Persistence;
using JasperFx;
using Marten;

namespace Infrastructure.Integration.Tests;

/// <summary>
/// S4-Live-Tor (docs/snapshot-konzept.md): der <see cref="MartenSnapshotStore"/> gegen ECHTES Postgres.
/// Beweist, dass die GENERIERTE Dokumenttyp-Registrierung (<see cref="RegisteredSnapshotTypes"/>) die
/// jsonb-Tabelle <c>mt_doc_snapshot_konto</c> anlegt und ein Snapshot inkl. nested State und Inbox-Menge
/// verlustfrei zurückkommt. Braucht laufendes Postgres auf localhost:5432.
/// </summary>
public class SnapshotStorePostgresTests : IClassFixture<SnapshotPostgresFixture>
{
    private readonly SnapshotPostgresFixture _fx;
    public SnapshotStorePostgresTests(SnapshotPostgresFixture fx) => _fx = fx;

    [Fact]
    public async Task Save_dann_Load_liefert_State_Inbox_und_Version_verlustfrei()
    {
        var store = new MartenSnapshotStore(_fx.Store);
        var id = Guid.NewGuid();
        var cmdId = Guid.NewGuid();

        await store.SaveAsync(new Snapshot<Konto>
        {
            Id = id,
            Version = 5,
            SchemaVersion = "hash-abc",
            State = new Konto { Id = id, Version = 5, Saldo = 100, Reserviert = 30, Gesperrt = false },
            ProcessedCommandIds = new[] { cmdId },
            UpdatedAt = DateTimeOffset.UtcNow
        }, default);

        var loaded = await store.TryLoadAsync<Konto>(id, default);

        loaded.Should().NotBeNull();
        loaded!.Version.Should().Be(5);
        loaded.SchemaVersion.Should().Be("hash-abc");
        loaded.State.Saldo.Should().Be(100);
        loaded.State.Reserviert.Should().Be(30);
        loaded.State.Verfuegbar.Should().Be(70);          // aus State neu berechnet
        loaded.ProcessedCommandIds.Should().ContainSingle().Which.Should().Be(cmdId);
    }

    [Fact]
    public async Task Save_ueberschreibt_und_Delete_entfernt()
    {
        var store = new MartenSnapshotStore(_fx.Store);
        var id = Guid.NewGuid();

        await store.SaveAsync(new Snapshot<Konto>
            { Id = id, Version = 1, State = new Konto { Id = id, Version = 1, Saldo = 10 } }, default);
        await store.SaveAsync(new Snapshot<Konto>
            { Id = id, Version = 2, State = new Konto { Id = id, Version = 2, Saldo = 20 } }, default);

        (await store.TryLoadAsync<Konto>(id, default))!.State.Saldo.Should().Be(20);  // ein aktueller je Aggregat

        await store.DeleteAsync<Konto>(id, default);
        (await store.TryLoadAsync<Konto>(id, default)).Should().BeNull();             // Replay-Kohärenz
    }
}

public sealed class SnapshotPostgresFixture : IDisposable
{
    public IDocumentStore Store { get; }

    public SnapshotPostgresFixture()
    {
        Store = DocumentStore.For(opts =>
        {
            opts.Connection(
                "Host=localhost;Port=5432;Database=cqrs_events;Username=postgres;Password=postgres");
            opts.DatabaseSchemaName = "snapshot_it";
            opts.AutoCreateSchemaObjects = AutoCreate.All;
            RegisteredSnapshotTypes.Register(opts);   // die GENERIERTE Registrierung
        });
    }

    public void Dispose() => Store.Dispose();
}
