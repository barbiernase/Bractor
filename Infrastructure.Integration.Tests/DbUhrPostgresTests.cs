using Abstractions;
using FluentAssertions;
using Infrastructure.Persistence;
using Marten;

namespace Infrastructure.Integration.Tests;

/// <summary>
/// Ebene 2 — die DB-Uhr gegen ECHTES Postgres (kein Cluster): <see cref="MartenDbClock"/> liefert die Postgres-Zeit — dieselbe Quelle, aus der
/// die Uhr Fristen fällig rechnet und die Datenbank die Log-Zeit eines Events setzt (G-10). Braucht Postgres (localhost:5432); schnell.
/// </summary>
[Trait("Kategorie", "Garantie")]
public sealed class DbUhrPostgresTests : IDisposable
{
    private readonly IDocumentStore _store;

    public DbUhrPostgresTests()
    {
        _store = DocumentStore.For(opts =>
        {
            opts.Connection("Host=localhost;Port=5432;Database=cqrs_events;Username=postgres;Password=postgres");
            opts.DatabaseSchemaName = "dbuhr_it";
        });
    }

    [Fact]
    public async Task DB_Uhr_liefert_eine_plausible_Postgres_Zeit()
    {
        var jetzt = await new MartenDbClock(_store).JetztAsync();
        jetzt.Should().BeCloseTo(DateTimeOffset.UtcNow, TimeSpan.FromMinutes(5),
            "die DB-Uhr und die Node-Uhr laufen hier auf derselben Maschine grob synchron");
    }

    public void Dispose() => _store.Dispose();
}
