using Abstractions;
using Domain.Datensatz;
using Domain.ImagePair;   // Klassifikation
using FluentAssertions;
using Infrastructure;     // generierte AggregateHandlerFactory
using DatensatzAgg = Domain.Datensatz.Datensatz;

namespace Infrastructure.Pruefstand.Ddd;

/// <summary>
/// Das Aggregat <see cref="DatensatzAgg"/> (Meilenstein 1 des Trainings-/Datensatz-Kontexts)
/// durch die ECHTE generierte <see cref="AggregateHandlerFactory"/> (Decider + Applier),
/// store-frei — genau wie <see cref="VerkaufAggregatTests"/>. Deckt die Invarianten aus dem
/// Konzept §3.4 ab: Range-Union/Dedup, „keine Änderung nach Einfrieren", „Einfrieren ohne
/// Mitglieder", deterministischer Split.
/// </summary>
public class DatensatzAggregatTests
{
    private sealed class Fixture
    {
        public IAggregateHandler Handler { get; }
        public DatensatzAgg Datensatz { get; }
        public Guid Id { get; }
        public List<IEvent> LetzteAblehnungen { get; } = new();

        public Fixture(bool erstellen = true)
        {
            Datensatz = new DatensatzAgg();
            Handler = new AggregateHandlerFactory().CreateHandler(Datensatz);
            Id = Guid.NewGuid();
            if (erstellen)
                Wende(new ErstelleDatensatz(Id, "Charge-Juni"));
        }

        public IReadOnlyList<IEvent> Wende(ICommand cmd)
        {
            LetzteAblehnungen.Clear();
            var events = Handler.HandleCommand(cmd).ToList();
            foreach (var e in events)
            {
                if (e is ITransientEvent) { LetzteAblehnungen.Add(e); continue; }
                Handler.ApplyEvent(e);
                Datensatz.Version++;   // das macht im Betrieb die AggregateActorBase
            }
            return events;
        }

        private static RangeHerkunft Herkunft(int anzahl) =>
            new(new RangeKriterien(KiKlassifikation: Klassifikation.Anomalie), anzahl);

        public void NimmRange(params Guid[] ids) =>
            Wende(new NimmRangeAuf(Id, ids, Herkunft(ids.Length)));
    }

    // ═══════════════════════════════════════════════════
    // LIFECYCLE
    // ═══════════════════════════════════════════════════

    [Fact]
    public void Erstellen_setzt_Name_und_Entwurf()
    {
        var f = new Fixture();
        f.Datensatz.Name.Should().Be("Charge-Juni");
        f.Datensatz.Status.Should().Be(DatensatzStatus.Entwurf);
        f.Datensatz.IstEingefroren.Should().BeFalse();
    }

    [Fact]
    public void Zweimal_Erstellen_wird_abgelehnt()
    {
        var f = new Fixture();
        f.Wende(new ErstelleDatensatz(f.Id, "nochmal"));
        f.LetzteAblehnungen.Should().ContainSingle()
            .Which.Should().BeOfType<DatensatzExistiertBereits>();
    }

    // ═══════════════════════════════════════════════════
    // RANGE — Union & Dedup (Konzept §3.2)
    // ═══════════════════════════════════════════════════

    [Fact]
    public void Mehrere_Ranges_werden_vereinigt_und_dedupliziert()
    {
        var a = Guid.NewGuid();
        var b = Guid.NewGuid();
        var c = Guid.NewGuid();

        var f = new Fixture();
        f.NimmRange(a, b);        // Range 1
        f.NimmRange(b, c);        // Range 2 — b überlappt

        f.Datensatz.DraftMitglieder.Should().BeEquivalentTo(new[] { a, b, c },
            "die Vereinigung dedupliziert das überlappende Paar b");
        f.Datensatz.AnzahlDraftMitglieder.Should().Be(3);
        f.Datensatz.Ranges.Should().HaveCount(2, "beide Ranges bleiben als Provenienz erhalten");
    }

    [Fact]
    public void Leere_Range_wird_abgelehnt()
    {
        var f = new Fixture();
        f.Wende(new NimmRangeAuf(f.Id, Array.Empty<Guid>(), new RangeHerkunft(new RangeKriterien(), 0)));
        f.LetzteAblehnungen.Should().ContainSingle().Which.Should().BeOfType<RangeLeer>();
    }

    [Fact]
    public void Manuelles_Aufnehmen_ist_idempotent()
    {
        var p = Guid.NewGuid();
        var f = new Fixture();
        f.Wende(new NimmPaarAuf(f.Id, p));
        var wieder = f.Wende(new NimmPaarAuf(f.Id, p));

        wieder.Should().BeEmpty("ein bereits enthaltenes Paar erzeugt kein zweites Event");
        f.Datensatz.AnzahlDraftMitglieder.Should().Be(1);
    }

    [Fact]
    public void Entfernen_eines_nicht_enthaltenen_Paares_ist_Noop()
    {
        var f = new Fixture();
        var events = f.Wende(new EntfernePaar(f.Id, Guid.NewGuid()));
        events.Should().BeEmpty();
    }

    // ═══════════════════════════════════════════════════
    // SPLIT-OVERRIDE
    // ═══════════════════════════════════════════════════

    [Fact]
    public void Split_Override_wird_uebernommen()
    {
        var f = new Fixture();
        f.Wende(new SetzeSplit(f.Id, 80, 10, 10, 7));
        f.Datensatz.Split.Should().Be(new SplitKonfig(80, 10, 10, 7));
    }

    [Fact]
    public void Ungueltiger_Split_wird_abgelehnt()
    {
        var f = new Fixture();
        f.Wende(new SetzeSplit(f.Id, 80, 10, 5, 7));   // Summe 95 ≠ 100
        f.LetzteAblehnungen.Should().ContainSingle().Which.Should().BeOfType<SplitUngueltig>();
        f.Datensatz.Split.Should().Be(SplitKonfig.Default, "der ungültige Split wirkt nicht");
    }

    // ═══════════════════════════════════════════════════
    // EINFRIEREN (Konzept §3.1)
    // ═══════════════════════════════════════════════════

    [Fact]
    public void Einfrieren_ohne_Mitglieder_wird_abgelehnt()
    {
        var f = new Fixture();
        f.Wende(new FriereEin(f.Id));
        f.LetzteAblehnungen.Should().ContainSingle().Which.Should().BeOfType<DatensatzLeer>();
    }

    [Fact]
    public void Einfrieren_mit_Mitgliedern_fordert_das_Einfrieren_an()
    {
        var f = new Fixture();
        f.NimmRange(Guid.NewGuid(), Guid.NewGuid());
        var events = f.Wende(new FriereEin(f.Id));

        events.Should().ContainSingle().Which.Should().BeOfType<EinfrierenAngefordert>();
        f.Datensatz.IstEingefroren.Should().BeFalse("erst SchliesseEinfrierenAb friert wirklich ein");
    }

    [Fact]
    public void Einfrieren_abschliessen_schreibt_den_Snapshot_fest()
    {
        var f = new Fixture();
        var p1 = Guid.NewGuid();
        var p2 = Guid.NewGuid();
        f.NimmRange(p1, p2);
        f.Wende(new FriereEin(f.Id));

        var mitglieder = new[]
        {
            new DatensatzMitglied(p1, Klassifikation.Anomalie, Split.Train, "dc0/1.png", "dc2/1.png"),
            new DatensatzMitglied(p2, Klassifikation.KeineAnomalie, Split.Test, "dc0/2.png", "dc2/2.png"),
        };
        var events = f.Wende(new SchliesseEinfrierenAb(f.Id, mitglieder));

        events.Should().ContainSingle().Which.Should().BeOfType<DatensatzEingefroren>();
        f.Datensatz.Status.Should().Be(DatensatzStatus.Eingefroren);
        f.Datensatz.IstEingefroren.Should().BeTrue();
        f.Datensatz.EingefroreneVersion.Should().Be(1);
        f.Datensatz.EingefroreneMitglieder.Should().BeEquivalentTo(mitglieder);
    }

    // ═══════════════════════════════════════════════════
    // IMMUTABILITÄT — keine Änderung nach dem Einfrieren
    // ═══════════════════════════════════════════════════

    private static Fixture Eingefroren()
    {
        var f = new Fixture();
        var p = Guid.NewGuid();
        f.NimmRange(p);
        f.Wende(new FriereEin(f.Id));
        f.Wende(new SchliesseEinfrierenAb(f.Id,
            new[] { new DatensatzMitglied(p, Klassifikation.Anomalie, Split.Train, "a", "b") }));
        return f;
    }

    [Theory]
    [MemberData(nameof(AenderungsCommands))]
    public void Jede_Aenderung_nach_dem_Einfrieren_wird_abgelehnt(ICommand cmd)
    {
        var f = Eingefroren();
        f.Wende(cmd);
        f.LetzteAblehnungen.Should().ContainSingle()
            .Which.Should().BeOfType<DatensatzBereitsEingefroren>();
    }

    public static IEnumerable<object[]> AenderungsCommands()
    {
        var id = Guid.Empty; // wird im Test nicht gegen die reale Id geprüft — der Guard greift vor der Id
        yield return new object[] { new FuegeRangeHinzu(id, new RangeKriterien()) };
        yield return new object[] { new NimmRangeAuf(id, new[] { Guid.NewGuid() }, new RangeHerkunft(new RangeKriterien(), 1)) };
        yield return new object[] { new NimmPaarAuf(id, Guid.NewGuid()) };
        yield return new object[] { new EntfernePaar(id, Guid.NewGuid()) };
        yield return new object[] { new SetzeSplit(id, 70, 15, 15, 1) };
        yield return new object[] { new FriereEin(id) };
    }

    [Fact]
    public void Zweites_Abschliessen_nach_dem_Einfrieren_wird_abgelehnt()
    {
        var f = Eingefroren();
        f.Wende(new SchliesseEinfrierenAb(f.Id,
            new[] { new DatensatzMitglied(Guid.NewGuid(), Klassifikation.Anomalie, Split.Val, "a", "b") }));
        f.LetzteAblehnungen.Should().ContainSingle()
            .Which.Should().BeOfType<DatensatzBereitsEingefroren>();
    }

    // ═══════════════════════════════════════════════════
    // SPLIT-ZUTEILER — deterministisch & stratifiziert (Konzept §3.3)
    // ═══════════════════════════════════════════════════

    [Fact]
    public void SplitZuteiler_ist_deterministisch()
    {
        var mitglieder = Enumerable.Range(0, 200)
            .Select(i => (Id: Guid.NewGuid(), Label: i % 2 == 0 ? Klassifikation.Anomalie : Klassifikation.KeineAnomalie))
            .ToList();

        var a = SplitZuteiler.Zuteilen(mitglieder, SplitKonfig.Default);
        var b = SplitZuteiler.Zuteilen(mitglieder, SplitKonfig.Default);

        a.Should().BeEquivalentTo(b, "gleiche Mitglieder + gleicher Seed → bit-genau gleicher Split");
    }

    [Fact]
    public void SplitZuteiler_stratifiziert_je_Klasse_im_Verhaeltnis()
    {
        // 100 pro Klasse → 70/15/15 exakt je Klasse.
        var anomalie = Enumerable.Range(0, 100).Select(_ => (Guid.NewGuid(), Klassifikation.Anomalie));
        var ok = Enumerable.Range(0, 100).Select(_ => (Guid.NewGuid(), Klassifikation.KeineAnomalie));
        var mitglieder = anomalie.Concat(ok).ToList();

        var zuteilung = SplitZuteiler.Zuteilen(mitglieder, SplitKonfig.Default);

        zuteilung.Should().HaveCount(200, "jedes Mitglied bekommt genau einen Split");

        foreach (var label in new[] { Klassifikation.Anomalie, Klassifikation.KeineAnomalie })
        {
            var derKlasse = mitglieder.Where(m => m.Item2 == label).Select(m => m.Item1).ToList();
            var splits = derKlasse.Select(id => zuteilung[id]).ToList();

            splits.Count(s => s == Split.Train).Should().Be(70);
            splits.Count(s => s == Split.Val).Should().Be(15);
            splits.Count(s => s == Split.Test).Should().Be(15);
        }
    }

    [Fact]
    public void SplitZuteiler_teilt_jedes_Mitglied_genau_einmal_zu_auch_bei_krummer_Zahl()
    {
        var mitglieder = Enumerable.Range(0, 7)
            .Select(_ => (Guid.NewGuid(), Klassifikation.Anomalie))
            .ToList();

        var zuteilung = SplitZuteiler.Zuteilen(mitglieder, SplitKonfig.Default);

        zuteilung.Should().HaveCount(7, "Rundung darf kein Mitglied verlieren oder doppelt vergeben");
    }
}
