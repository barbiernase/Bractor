using Abstractions;
using FluentAssertions;
using Infrastructure.Persistence;
using Marten;

namespace Infrastructure.Integration.Tests;

/// <summary>
/// Ebene 2 — die DB-Uhr und der durable Fristplan gegen ECHTES Postgres (kein Cluster). Beweist die zwei
/// tragenden Teile des Deadline-Primitivs: <see cref="MartenDbClock"/> liefert die Postgres-Zeit, und
/// <see cref="MartenFristplan"/> legt Fristen an, gibt genau die gegen die DB-Uhr fälligen zurück und entfernt
/// sie wieder. Braucht Postgres (localhost:5432); schnell, kein Cold-Boot.
/// </summary>
public sealed class DeadlineStorePostgresTests : IDisposable
{
    private readonly IDocumentStore _store;

    public DeadlineStorePostgresTests()
    {
        _store = DocumentStore.For(opts =>
        {
            opts.Connection("Host=localhost;Port=5432;Database=cqrs_events;Username=postgres;Password=postgres");
            opts.DatabaseSchemaName = "deadline_it";
            opts.AutoCreateSchemaObjects = JasperFx.AutoCreate.All;
            opts.Schema.For<Frist>().Identity(x => x.Id);
        });
    }

    [Fact]
    public async Task DB_Uhr_liefert_eine_plausible_Postgres_Zeit()
    {
        var jetzt = await new MartenDbClock(_store).JetztAsync();
        jetzt.Should().BeCloseTo(DateTimeOffset.UtcNow, TimeSpan.FromMinutes(5),
            "die DB-Uhr und die Node-Uhr laufen hier auf derselben Maschine grob synchron");
    }

    [Fact]
    public async Task Fristplan_gibt_nur_die_gegen_die_DB_Uhr_faelligen_zurueck_und_entfernt_sie()
    {
        var plan = new MartenFristplan(_store);
        var clock = new MartenDbClock(_store);
        var jetzt = await clock.JetztAsync();

        var überfällig = new Frist(Guid.NewGuid(), jetzt.AddMinutes(-1), Guid.NewGuid(), "faellig");
        var künftig = new Frist(Guid.NewGuid(), jetzt.AddHours(1), Guid.NewGuid(), "spaeter");
        await plan.PlaneAsync(überfällig);
        await plan.PlaneAsync(künftig);

        var fällige = await plan.FälligeAsync(await clock.JetztAsync());
        fällige.Select(f => f.Id).Should().Contain(überfällig.Id).And.NotContain(künftig.Id);

        await plan.EntferneAsync(überfällig.Id);
        (await plan.FälligeAsync((await clock.JetztAsync()).AddHours(2))).Select(f => f.Id)
            .Should().NotContain(überfällig.Id, "entfernt")
            .And.Contain(künftig.Id, "noch offen (jetzt auch fällig)");

        await plan.EntferneAsync(künftig.Id);   // aufräumen
    }

    public void Dispose() => _store.Dispose();
}
