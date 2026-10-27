using Abstractions;
using FluentAssertions;
using Infrastructure.Funktionen;
using Infrastructure.Lebenszyklus;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Proto;
using Proto.Cluster;
using Pruefdomaene.Abrechnung;
using Pruefdomaene.Pipeline.Abrechnung;
using static Pruefdomaene.Integration.Tests.Pruefstand;

namespace Pruefdomaene.Integration.Tests.Garantien;

/// <summary>
/// Die Übergabe „Funktion → Ausgaben“ unter Störung, gegen echtes Marten (Plan Pub/Sub-Neustrukturierung §4.2, §4.4): Abbruch nach dem Festhalten vor
/// den Commands, Abbruch nach den Commands vor der Position, zwei Aktivierungen derselben Partition, zwei Worker mit verschiedenen Antworten. Geprüft
/// wird die Wirkung im Log — je Eingang genau eine, auch wenn die Außenwelt (der Versand) bei jedem Aufruf anders antwortet.
/// </summary>
[Trait("Kategorie", "Garantie")]
public class FunktionsGarantieTests
{
    private static readonly TimeSpan Frist = TimeSpan.FromSeconds(90);

    /// <summary>Der Versand draußen: jede Antwort anders; optional wartet jeder Aufruf, bis <see cref="Zusammen"/> Aufrufe da sind.</summary>
    private sealed class Versand(int zusammen = 1) : IMahnversand
    {
        private readonly TaskCompletionSource _alle = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private int _n;
        public int Aufrufe => Volatile.Read(ref _n);
        public async Task<string> VersendeAsync(Id<Kundenkonto> konto, Id<Rechnung> rechnung, int stufe)
        {
            var n = Interlocked.Increment(ref _n);
            if (n >= zusammen) _alle.TrySetResult();
            await _alle.Task.WaitAsync(TimeSpan.FromSeconds(60));
            return $"brief-{n}-{Guid.NewGuid():N}";
        }
    }

    private static async Task<(Id<Rechnung> Rechnung, Id<Kundenkonto> Konto)> UeberfaelligAsync(IHost host)
    {
        var rechnung = Id<Rechnung>.Neu();
        var konto = Id<Kundenkonto>.Neu();
        (await FrageAsync(host, new StelleRechnung(rechnung, konto, 99m), Guid.NewGuid()))!.Success.Should().BeTrue();
        (await FrageAsync(host, new PruefeZahlung(rechnung), Guid.NewGuid()))!.Success.Should().BeTrue();
        return (rechnung, konto);
    }

    private static async Task<IReadOnlyList<EventEnvelope>> Log(IHost host, Guid stream) =>
        await host.Services.GetRequiredService<IEventStoreRepository>().ReadStreamAsync(stream, 0, default);

    private static async Task<IReadOnlyList<EventEnvelope>> Festgehalten(IHost host, Id<Rechnung> rechnung)
    {
        var ueberfaellig = (await Log(host, rechnung.Wert)).Single(e => e.Payload is ZahlungUeberfaellig);
        return await Log(host, FunktionsIds.Festhalten(nameof(Mahnwesen), rechnung.Wert, ueberfaellig.AggregateVersion));
    }

    /// <summary>Hält fest — und „stürzt“ danach einmal ab, bevor gesendet wird.</summary>
    private sealed class AbsturzNachDemFesthalten(IAusgabeAblage innen) : IAusgabeAblage
    {
        public int Abstuerze;
        public Task<Festgehalten> LiesAsync(FunktionsEingabe e, CancellationToken ct) => innen.LiesAsync(e, ct);
        public async Task<bool> HalteFestAsync(FunktionsEingabe e, int ab, IReadOnlyList<IAusgabe> a, bool abgeschlossen, CancellationToken ct)
        {
            var ok = await innen.HalteFestAsync(e, ab, a, abgeschlossen, ct);
            if (ok && abgeschlossen && e.Funktion == nameof(Mahnwesen) && Interlocked.Increment(ref Abstuerze) == 1)
                throw new InvalidOperationException("Absturz nach dem Festhalten, vor den Commands");
            return ok;
        }
    }

    [Fact]
    public async Task Abbruch_nach_dem_Festhalten_vor_den_Commands_wirkt_beim_naechsten_Versuch_genau_einmal()
    {
        var versand = new Versand();
        AbsturzNachDemFesthalten? ablage = null;
        using var host = Cluster("garantie-funktion-festhalten", dienste: s =>
        {
            s.AddSingleton<IMahnversand>(versand);
            s.AddSingleton(new BausteinBindung(nameof(Mahnwesen), Wiederholungen: 3));
            Dekoriere<IAusgabeAblage>(s, (_, innen) => ablage = new AbsturzNachDemFesthalten(innen));
        });
        await host.StartAsync();
        try
        {
            var (rechnung, konto) = await UeberfaelligAsync(host);
            await Warte(async () => (await Zustand<Kundenkonto>(host.Services, konto.Wert)).Mahnungen == 1, Frist, "die Mahnung kommt nicht an");
            await Task.Delay(TimeSpan.FromSeconds(2));
            ablage!.Abstuerze.Should().BeGreaterThanOrEqualTo(1);
            (await Log(host, konto.Wert)).Count(e => e.Payload is MahnungVermerkt).Should().Be(1);
            versand.Aufrufe.Should().Be(1, "der zweite Versuch findet den festgehaltenen Eintrag und rechnet nicht neu");
        }
        finally { await host.StopAsync(); }
    }

    /// <summary>Ein Stand, der nie geschrieben wird: nach einem Neustart beginnt die Funktion wieder vor dem schon bearbeiteten Eingang.</summary>
    private sealed class StandOhneSchreiben(IFunktionsStand innen) : IFunktionsStand
    {
        public Task<Abstractions.PartitionsCheckpoint> LadeAsync(string id, int partition, CancellationToken ct) => innen.LadeAsync(id, partition, ct);
        public Task<int> SchreibeAsync(Abstractions.PartitionsCheckpoint stand, CancellationToken ct) =>
            throw new InvalidOperationException("Absturz nach den Commands, vor der Position");
        public Task<FunktionsZustandsDokument?> LadeZustandAsync(string id, CancellationToken ct) => innen.LadeZustandAsync(id, ct);
        public Task<(int? Stand, int Zustand)> SichereAsync(Abstractions.PartitionsCheckpoint? stand, FunktionsZustandsDokument zustand, CancellationToken ct) =>
            throw new InvalidOperationException("Absturz nach den Commands, vor der Position");
    }

    [Fact]
    public async Task Abbruch_nach_den_Commands_vor_der_Position_sendet_nach_dem_Neustart_dieselben_Ids()
    {
        var versand = new Versand();
        Id<Kundenkonto> konto;
        Id<Rechnung> rechnung;
        using (var erster = Cluster("garantie-funktion-position-1", dienste: s =>
               {
                   s.AddSingleton<IMahnversand>(versand);
                   Dekoriere<IFunktionsStand>(s, (_, innen) => new StandOhneSchreiben(innen));
               }))
        {
            await erster.StartAsync();
            try
            {
                (rechnung, konto) = await UeberfaelligAsync(erster);
                await Warte(async () => (await Zustand<Kundenkonto>(erster.Services, konto.Wert)).Mahnungen == 1, Frist, "die Mahnung kommt nicht an");
            }
            finally { await erster.StopAsync(); }
        }

        // Der Neustart liest die Partition ab dem alten Stand: derselbe Eingang, der festgehaltene Eintrag, dieselben Command-Ids.
        using var zweiter = Cluster("garantie-funktion-position-2", dienste: s => s.AddSingleton<IMahnversand>(versand));
        await zweiter.StartAsync();
        try
        {
            var ueberfaellig = (await Log(zweiter, rechnung.Wert)).Single(e => e.Payload is ZahlungUeberfaellig);
            var p = Partitionierung.Von(rechnung.Wert, Infrastructure.Projections.Konsum.Standard.Partitionen);
            var staende = zweiter.Services.GetRequiredService<IFunktionsStand>();
            var seq = await Sequenz(ueberfaellig);
            await Warte(async () => (await staende.LadeAsync(Funktionsvertrag.StandId(nameof(Mahnwesen)), p, default)).Position >= seq, Frist,
                "die Funktion holt den Eingang nach dem Neustart nicht nach");
            (await Log(zweiter, konto.Wert)).Count(e => e.Payload is MahnungVermerkt).Should().Be(1, "dieselbe Stelle, dieselbe Command-Id — die Inbox verwirft es");
            versand.Aufrufe.Should().Be(1, "festgehalten wird einmal; der zweite Lauf sendet aus dem Eintrag");
        }
        finally { await zweiter.StopAsync(); }
    }

    private static async Task<long> Sequenz(EventEnvelope e)
    {
        await using var conn = new Npgsql.NpgsqlConnection(Verbindung);
        await conn.OpenAsync();
        await using var cmd = new Npgsql.NpgsqlCommand("select seq_id from pruef_e2e.mt_events where stream_id = $1 and version = $2", conn);
        cmd.Parameters.Add(new Npgsql.NpgsqlParameter { Value = e.AggregateId });
        cmd.Parameters.Add(new Npgsql.NpgsqlParameter { Value = (long)e.AggregateVersion });
        return (long)(await cmd.ExecuteScalarAsync())!;
    }

    [Fact]
    public async Task Zwei_Aktivierungen_derselben_Partition_wirken_einmal_es_gilt_der_zuerst_festgehaltene_Lauf()
    {
        // Zwei Cluster auf demselben Log: je eine Aktivierung derselben Funktions-Partition. Der Versand wartet, bis beide gerechnet haben —
        //   dann hält einer fest, der andere findet den Eintrag und sendet aus ihm.
        var versand = new Versand(zusammen: 2);
        using var a = Cluster("garantie-funktion-zwei-a", dienste: s => s.AddSingleton<IMahnversand>(versand));
        using var b = Cluster("garantie-funktion-zwei-b", dienste: s => s.AddSingleton<IMahnversand>(versand));
        await a.StartAsync();
        await b.StartAsync();
        try
        {
            var (rechnung, konto) = await UeberfaelligAsync(a);
            await Warte(async () => (await Zustand<Kundenkonto>(a.Services, konto.Wert)).Mahnungen >= 1, Frist, "die Mahnung kommt nicht an");
            await Warte(() => Task.FromResult(versand.Aufrufe >= 2), Frist, "die zweite Aktivierung rechnet nicht");
            await Task.Delay(TimeSpan.FromSeconds(3));

            var fest = await Festgehalten(a, rechnung);
            fest.Select(e => e.Payload.GetType().Name).Should().Equal(new[] { nameof(BefehlFestgehalten), nameof(AusgabeFestgehalten) }.AsEnumerable(),
                "genau ein Lauf ist festgehalten");
            var vermerkt = (await Log(a, konto.Wert)).Where(e => e.Payload is MahnungVermerkt).ToList();
            vermerkt.Should().ContainSingle("zwei Rechnungen, eine Wirkung");
            ((BefehlFestgehalten)fest[0].Payload).Daten.Should().Contain(((MahnungVermerkt)vermerkt[0].Payload).Versand);
        }
        finally
        {
            await b.StopAsync();
            await a.StopAsync();
        }
    }

    [Fact]
    public async Task Zwei_Worker_mit_verschiedenen_Antworten_es_wirkt_die_erste()
    {
        using var host = Cluster("garantie-funktion-worker", dienste: s =>
        {
            s.AddSingleton(new BausteinBindung(nameof(IFreigabe), FunktionsLaufort.Hand));
            s.AddHandAusfuehrer();
        });
        await host.StartAsync();
        try
        {
            var rechnung = Id<Rechnung>.Neu();
            (await FrageAsync(host, new StelleRechnung(rechnung, Id<Kundenkonto>.Neu(), 500m), Guid.NewGuid()))!.Success.Should().BeTrue();
            var hand = host.Services.GetRequiredService<HandAusfuehrer>();
            HandEingang? offen = null;
            await Warte(() => Task.FromResult((offen = hand.OffeneEingaenge.FirstOrDefault(e => e.Eingang.Stream == rechnung.Wert)) is not null),
                Frist, "der Eingang wartet nicht von Hand");

            // Ein zweiter Worker hat denselben Eingang (etwa nach Ablauf der Lease des ersten) — er antwortet anders und schneller.
            var system = host.Services.GetRequiredService<ActorSystem>();
            var bote = new Bote(system.Cluster());
            var baustein = Infrastructure.Komposition.Anwendung.Funktionen.Bausteine.Single(b => b.Name == nameof(IFreigabe));
            await using var zweiter = new BausteinSitzung("zweiter-worker", n => n == baustein.Name ? baustein : null,
                (_, _, _) => Task.FromResult<EingaengeZugeteilt?>(new EingaengeZugeteilt(new[] { offen!.Eingang })),
                (b, nachricht) => { bote.Melde(FunktionsVermittlerActor.Identitaet(b.Typ), nachricht); return Task.CompletedTask; },
                (_, _, _) => Task.CompletedTask, host.Services.GetRequiredService<IAusgabeAblage>(),
                (b, e, fehler, teil, ausgaben) =>
                {
                    bote.Melde(FunktionsActor.IdentitaetFuer(b, e.Stream, Infrastructure.Projections.Konsum.Standard),
                        EingangBeantwortet.Aus(e.Vorgang, fehler, teil, ausgaben));
                    return Task.CompletedTask;
                });
            zweiter.Biete(new[] { (baustein.Name, 1) });
            await Warte(() => Task.FromResult(zweiter.Laufende.Count == 1), Frist, "der zweite Worker bekommt den Eingang nicht");

            (await zweiter.AntwortAsync(offen!.Eingang.Vorgang, new IAusgabe[] { new GibRechnungFrei(rechnung) }, null, default))
                .Should().Be(ErgebnisAusgang.Geschrieben);
            (await hand.AntworteAsync(offen.Eingang.Vorgang, new IAusgabe[] { new SperreRechnung(rechnung, "zu spät") }, null, default))
                .Should().Be(ErgebnisAusgang.Ueberholt, "die erste Antwort ist festgehalten — die zweite verfällt");

            await Warte(async () => (await Zustand<Rechnung>(host.Services, rechnung.Wert)).Freigegeben, Frist, "die erste Antwort wirkt nicht");
            await Task.Delay(TimeSpan.FromSeconds(2));
            (await Zustand<Rechnung>(host.Services, rechnung.Wert)).Gesperrt.Should().BeFalse();
        }
        finally { await host.StopAsync(); }
    }
}
