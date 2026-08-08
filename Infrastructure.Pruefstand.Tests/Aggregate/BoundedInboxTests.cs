using System;
using System.Linq;
using FluentAssertions;
using Infrastructure.Aggregate;
using Xunit;

namespace Infrastructure.Pruefstand.Aggregate;

/// <summary>
/// Audit-Fix #4/H3: die Framework-Inbox ist jetzt HART gedeckelt (letzte K statt monoton wachsend). Diese
/// store-freie Logik wird hier direkt bewiesen: unter der Grenze verlustfrei, über der Grenze FIFO-Verdrängung
/// (ältestes zuerst), Einfüge-Reihenfolge erhalten (Snapshot-Roundtrip), Doppel-Add folgenlos.
/// </summary>
public class BoundedInboxTests
{
    [Fact]
    public void Unter_der_Grenze_bleibt_alles_erhalten()
    {
        var ids = Enumerable.Range(0, 5).Select(_ => Guid.NewGuid()).ToArray();
        var inbox = new BoundedInbox(cap: 10);

        foreach (var id in ids) inbox.Add(id);

        inbox.Count.Should().Be(5);
        ids.Should().OnlyContain(id => inbox.Contains(id));
        inbox.ToArray().Should().Equal(ids);   // Einfüge-Reihenfolge erhalten
    }

    [Fact]
    public void Ueber_der_Grenze_bleiben_nur_die_letzten_K()
    {
        var ids = Enumerable.Range(0, 100).Select(_ => Guid.NewGuid()).ToArray();
        var inbox = new BoundedInbox(cap: 10);

        foreach (var id in ids) inbox.Add(id);

        inbox.Count.Should().Be(10);
        // Die ältesten 90 sind verdrängt → Dedup greift für sie nicht mehr (bewusster Tradeoff, Weg A).
        ids.Take(90).Should().OnlyContain(id => !inbox.Contains(id));
        // Die jüngsten 10 sind noch da, in Reihenfolge.
        ids.Skip(90).Should().OnlyContain(id => inbox.Contains(id));
        inbox.ToArray().Should().Equal(ids.Skip(90));
    }

    [Fact]
    public void Doppel_Add_ist_folgenlos_fuer_Count_und_Ordnung()
    {
        var a = Guid.NewGuid();
        var b = Guid.NewGuid();
        var inbox = new BoundedInbox(cap: 10);

        inbox.Add(a);
        inbox.Add(b);
        inbox.Add(a);   // erneut — darf weder Count erhöhen noch die Ordnung/Verdrängung stören

        inbox.Count.Should().Be(2);
        inbox.ToArray().Should().Equal(a, b);
    }

    [Fact]
    public void FromOrdered_behaelt_die_letzten_K_und_ist_roundtrip_stabil()
    {
        var ids = Enumerable.Range(0, 50).Select(_ => Guid.NewGuid()).ToArray();

        // Snapshot-Roundtrip: eine große Folge einkürzen, das Ergebnis serialisieren (ToArray) und zurückladen.
        var erst = BoundedInbox.FromOrdered(ids, cap: 10);
        erst.ToArray().Should().Equal(ids.Skip(40));

        var wieder = BoundedInbox.FromOrdered(erst.ToArray(), cap: 10);
        wieder.ToArray().Should().Equal(erst.ToArray());   // idempotenter Seed → dieselbe Menge/Ordnung
    }
}
