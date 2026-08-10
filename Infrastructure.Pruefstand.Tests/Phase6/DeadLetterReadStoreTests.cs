using System;
using System.Threading.Tasks;
using Abstractions;
using FluentAssertions;
using Infrastructure.Testing;
using Xunit;

namespace Infrastructure.Pruefstand.Phase6;

/// <summary>
/// Feature: der DLQ-Ops-/Read-Pfad. Bewiesen store-frei (schreibende + lesende Achse auf einer
/// In-Memory-Ablage): schreiben → auflisten (neueste zuerst), je Korrelation filtern, per Id holen,
/// zählen (Monitoring) und nach manueller Klärung auflösen (entfernen).
/// </summary>
public class DeadLetterReadStoreTests
{
    private static DeadLetter Toter(string quelle, string corr, DateTimeOffset erfasst) => new()
    {
        Id = Guid.NewGuid(),
        Quelle = quelle,
        CommandType = "MacheWas",
        AggregateId = Guid.NewGuid(),
        AggregateType = "Ziel",
        CorrelationId = corr,
        Grund = "abgelehnt",
        Versuche = 1,
        ErfasstUtc = erfasst,
    };

    [Fact]
    public async Task Schreiben_dann_Auflisten_neueste_zuerst_und_zaehlen()
    {
        var store = new InMemoryDeadLetterStore();
        var t0 = DateTimeOffset.UtcNow;
        var alt = Toter("pipeline", "c1", t0.AddMinutes(-5));
        var neu = Toter("prozess", "c2", t0);
        await store.WriteAsync(alt);
        await store.WriteAsync(neu);

        (await store.CountAsync()).Should().Be(2);
        var liste = await store.ListAsync(10);
        liste.Should().HaveCount(2);
        liste[0].Id.Should().Be(neu.Id);   // neueste zuerst
        liste[1].Id.Should().Be(alt.Id);
    }

    [Fact]
    public async Task Nach_Korrelation_filtern_und_per_Id_holen()
    {
        var store = new InMemoryDeadLetterStore();
        var a = Toter("prozess", "korr-A", DateTimeOffset.UtcNow);
        var b = Toter("prozess", "korr-B", DateTimeOffset.UtcNow);
        await store.WriteAsync(a);
        await store.WriteAsync(b);

        (await store.ListByCorrelationAsync("korr-A")).Should().ContainSingle().Which.Id.Should().Be(a.Id);
        (await store.GetAsync(b.Id))!.CorrelationId.Should().Be("korr-B");
        (await store.GetAsync(Guid.NewGuid())).Should().BeNull();
    }

    [Fact]
    public async Task Aufloesen_entfernt_den_Eintrag()
    {
        var store = new InMemoryDeadLetterStore();
        var t = Toter("pipeline", "c", DateTimeOffset.UtcNow);
        await store.WriteAsync(t);

        (await store.ResolveAsync(t.Id)).Should().BeTrue();
        (await store.CountAsync()).Should().Be(0);
        (await store.ResolveAsync(t.Id)).Should().BeFalse();   // schon weg
    }
}
