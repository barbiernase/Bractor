using Abstractions;
using Domain.Konto;
using Domain.Sammelauftrag;
using FluentAssertions;
using Infrastructure.Persistence;
using JasperFx;
using Marten;

namespace Infrastructure.Integration.Tests;

/// <summary>
/// M3-Live-Tor (P5(b)): der <see cref="MartenProzessMarkingStore"/> gegen ECHTES Postgres. Beweist, dass das
/// verdichtete Marking inkl. der gecachten Event-PAYLOADS (getaggtes JSON, reflection-freie Rehydrierung über
/// <c>GeneratedTypeRegistry</c>) verlustfrei zurückkommt — auch ein Payload mit <c>IReadOnlyList&lt;Guid&gt;</c>
/// (der Fan-out-Auslöser) und der Guid-Schlüssel-Cursor. Ein unbekannter/mismatchter Cache → null → Voll-Fold.
/// Braucht laufendes Postgres auf localhost:5432.
/// </summary>
public class ProzessMarkingStorePostgresTests : IClassFixture<ProzessMarkingPostgresFixture>
{
    private readonly ProzessMarkingPostgresFixture _fx;
    public ProzessMarkingStorePostgresTests(ProzessMarkingPostgresFixture fx) => _fx = fx;

    [Fact]
    public async Task Schreibe_dann_Lade_liefert_Cursor_und_Payloads_verlustfrei()
    {
        var store = new MartenProzessMarkingStore(_fx.Store);
        var korr = Guid.NewGuid();
        var quelle = Guid.NewGuid();
        var ziel1 = Guid.NewGuid();
        var ziel2 = Guid.NewGuid();
        var vorgangA = Guid.NewGuid();
        var vorgangB = Guid.NewGuid();

        var marking = new ProzessMarking
        {
            Id = korr,
            RegelHash = "hash-xyz",
            LogVersion = 4,
            StreamCursor = new Dictionary<Guid, int> { [quelle] = 3, [ziel1] = 2 },
            Marking = new MarkingKompakt
            {
                // Auslöser-Payload mit IReadOnlyList<Guid> — der harte Serialisierungs-Fall.
                Auslöser = new MarkingToken
                {
                    Payload = new SammelUeberweisungBeauftragt(quelle, new[] { ziel1, ziel2 }, 20),
                    Stream = korr, Version = 1,
                },
                Ergebnisse = new List<VorgangErgebnis>
                {
                    new() { Vorgang = vorgangA, ErgebnisDa = true, WirkungDa = true,
                            Wirkung = new MarkingToken { Payload = new BetragReserviert(60), Stream = quelle, Version = 2 } },
                    new() { Vorgang = vorgangB, ErgebnisDa = true, AbgelehntDa = true, AbgelehntGrund = "KontoGesperrt" },
                },
            },
            UpdatedAt = DateTimeOffset.UtcNow,
        };

        await store.SchreibeAsync(marking, default);
        var geladen = await store.LadeAsync(korr, default);

        geladen.Should().NotBeNull();
        geladen!.RegelHash.Should().Be("hash-xyz");
        geladen.LogVersion.Should().Be(4);
        geladen.StreamCursor.Should().BeEquivalentTo(new Dictionary<Guid, int> { [quelle] = 3, [ziel1] = 2 });

        var aus = geladen.Marking.Auslöser!;
        aus.Stream.Should().Be(korr);
        aus.Version.Should().Be(1);
        var ausPayload = aus.Payload.Should().BeOfType<SammelUeberweisungBeauftragt>().Subject;
        ausPayload.Quelle.Should().Be(quelle);
        ausPayload.Ziele.Should().Equal(ziel1, ziel2);
        ausPayload.BetragProZiel.Should().Be(20);

        geladen.Marking.Ergebnisse.Should().HaveCount(2);
        var a = geladen.Marking.Ergebnisse.Single(v => v.Vorgang == vorgangA);
        a.WirkungDa.Should().BeTrue();
        a.Wirkung!.Payload.Should().BeOfType<BetragReserviert>().Which.Betrag.Should().Be(60);
        a.Wirkung.Version.Should().Be(2);
        var b = geladen.Marking.Ergebnisse.Single(v => v.Vorgang == vorgangB);
        b.AbgelehntDa.Should().BeTrue();
        b.AbgelehntGrund.Should().Be("KontoGesperrt");
    }

    [Fact]
    public async Task Fehlendes_Marking_liefert_null_Voll_Fold()
        => (await new MartenProzessMarkingStore(_fx.Store).LadeAsync(Guid.NewGuid(), default)).Should().BeNull();

    [Fact]
    public async Task Schreibe_ueberschreibt_je_Korrelation()
    {
        var store = new MartenProzessMarkingStore(_fx.Store);
        var korr = Guid.NewGuid();

        await store.SchreibeAsync(new ProzessMarking { Id = korr, RegelHash = "v1", LogVersion = 1 }, default);
        await store.SchreibeAsync(new ProzessMarking { Id = korr, RegelHash = "v2", LogVersion = 2 }, default);

        var geladen = await store.LadeAsync(korr, default);
        geladen!.RegelHash.Should().Be("v2");   // ein aktuelles Dokument je Korrelation (überschreibend)
        geladen.LogVersion.Should().Be(2);
    }
}

public sealed class ProzessMarkingPostgresFixture : IDisposable
{
    public IDocumentStore Store { get; }

    public ProzessMarkingPostgresFixture()
    {
        Store = DocumentStore.For(opts =>
        {
            opts.Connection(
                "Host=localhost;Port=5432;Database=cqrs_events;Username=postgres;Password=postgres");
            opts.DatabaseSchemaName = "prozess_marking_it";
            opts.AutoCreateSchemaObjects = AutoCreate.All;
            opts.Schema.For<ProzessMarkingDoc>().Identity(x => x.Id);
        });
    }

    public void Dispose() => Store.Dispose();
}
