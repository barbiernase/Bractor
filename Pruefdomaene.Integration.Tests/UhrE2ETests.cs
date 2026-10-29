using Abstractions;
using FluentAssertions;
using Infrastructure.Uhr;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Npgsql;
using Pruefdomaene.Abrechnung;
using Xunit;
using static Pruefdomaene.Integration.Tests.Pruefstand;

namespace Pruefdomaene.Integration.Tests;

/// <summary>
/// Die Uhr im echten Cluster gegen echtes Marten (docs/11): ein Aggregat entscheidet eine Frist; sie steht als
/// <c>FristGesetzt</c> in seinem Stream und — in derselben Transaktion — im Fälligkeits-Verzeichnis; fällig zur Log-Zeit plus Dauer sendet die Uhr den
/// Command mit der Id aus dem Frist-Event. Doppelt gefeuert wirkt er einmal.
/// </summary>
[Trait("Kategorie", "Garantie")]
[Collection(nameof(UhrE2ETests))]
public class UhrE2ETests
{
    private static readonly TimeSpan Frist = TimeSpan.FromSeconds(60);

    private static async Task<IReadOnlyList<EventEnvelope>> Log(IHost host, Guid stream) =>
        await host.Services.GetRequiredService<IEventStoreRepository>().ReadStreamAsync(stream, 0, default);

    private static async Task<long> Zeilen(Guid stream)
    {
        await using var conn = new NpgsqlConnection(Verbindung);
        await conn.OpenAsync();
        await using var cmd = new NpgsqlCommand($"select count(*) from pruef_e2e.{FristVerzeichnis.Tabelle} where stream_id = $1", conn);
        cmd.Parameters.Add(new NpgsqlParameter { Value = stream });
        return (long)(await cmd.ExecuteScalarAsync())!;
    }

    [Fact]
    public async Task Eine_faellige_Frist_kommt_als_Command_mit_der_Id_aus_ihrem_Event_und_verlaesst_das_Verzeichnis()
    {
        using var host = Cluster("pruef-uhr");
        await host.StartAsync();
        try
        {
            var rechnung = Id<Rechnung>.Neu();
            var konto = Id<Kundenkonto>.Neu();
            (await FrageAsync(host, new StelleRechnung(rechnung, konto, 75m, ZahlungszielSekunden: 2), Guid.NewGuid()))!.Success.Should().BeTrue();

            var gesetzt = (await Log(host, rechnung.Wert)).Single(e => e.Payload is FristGesetzt);
            ((FristGesetzt)gesetzt.Payload).Befehl.Should().Be(new PruefeZahlung(rechnung));
            (await Zeilen(rechnung.Wert)).Should().Be(1, "die Frist steht mit ihrem Event im Verzeichnis (eine Transaktion)");

            await Warte(async () => (await Log(host, rechnung.Wert)).Any(e => e.Payload is ZahlungUeberfaellig), Frist, "die Frist feuert nicht");
            var ueberfaellig = (await Log(host, rechnung.Wert)).Single(e => e.Payload is ZahlungUeberfaellig);
            ueberfaellig.CausationId.Should().Be(Uhrwerk.BefehlsId(rechnung.Wert, gesetzt.AggregateVersion).ToString(), "die Command-Id kommt aus dem Frist-Event");
            (ueberfaellig.CreatedAtUtc - gesetzt.CreatedAtUtc).Should().BeGreaterThanOrEqualTo(TimeSpan.FromSeconds(2), "fällig zur Log-Zeit plus Dauer");
            await Warte(async () => await Zeilen(rechnung.Wert) == 0, Frist, "die entschiedene Frist bleibt im Verzeichnis");
            // Weiter durch die Kette: das Mahnwesen (Funktion mit Dienst) vermerkt die Mahnung am Kundenkonto.
            await Warte(async () => (await Zustand<Kundenkonto>(host.Services, konto.Wert)).Mahnungen == 1, Frist, "die Mahnung kommt nicht an");
        }
        finally { await host.StopAsync(); }
    }

    [Fact]
    public async Task Ist_die_Rechnung_vor_der_Faelligkeit_bezahlt_entscheidet_das_Aggregat_nichts_zu_tun()
    {
        using var host = Cluster("pruef-uhr-bezahlt");
        await host.StartAsync();
        try
        {
            var rechnung = Id<Rechnung>.Neu();
            (await FrageAsync(host, new StelleRechnung(rechnung, Id<Kundenkonto>.Neu(), 10m, ZahlungszielSekunden: 2), Guid.NewGuid()))!.Success.Should().BeTrue();
            (await FrageAsync(host, new BezahleRechnung(rechnung), Guid.NewGuid()))!.Success.Should().BeTrue();
            await Warte(async () => await Zeilen(rechnung.Wert) == 0, Frist, "die Frist feuert nicht");
            (await Log(host, rechnung.Wert)).Should().NotContain(e => e.Payload is ZahlungUeberfaellig, "überholt — kein Storno nötig, das Aggregat entscheidet");
        }
        finally { await host.StopAsync(); }
    }

    /// <summary>Ein Verzeichnis, das beim ersten Austragen „abstürzt“: die Uhr hat gesendet, die Zeile bleibt — der nächste Takt feuert erneut.</summary>
    private sealed class AbsturzBeimAustragen(IFaelligkeiten innen) : IFaelligkeiten
    {
        public int Abstuerze;
        public Task<IReadOnlyList<FaelligeFrist>> FaelligeAsync(int hoechstens, CancellationToken ct) => innen.FaelligeAsync(hoechstens, ct);
        public Task ErledigtAsync(Guid stream, int version, CancellationToken ct) =>
            Interlocked.Increment(ref Abstuerze) == 1 ? throw new InvalidOperationException("Absturz nach dem Senden, vor dem Austragen") : innen.ErledigtAsync(stream, version, ct);
    }

    [Fact]
    public async Task Doppelt_gefeuert_wirkt_die_Frist_genau_einmal()
    {
        AbsturzBeimAustragen? stoerung = null;
        using var host = Cluster("pruef-uhr-doppelt", dienste: s => Dekoriere<IFaelligkeiten>(s, (_, innen) => stoerung = new AbsturzBeimAustragen(innen)));
        await host.StartAsync();
        try
        {
            var rechnung = Id<Rechnung>.Neu();
            (await FrageAsync(host, new StelleRechnung(rechnung, Id<Kundenkonto>.Neu(), 30m, ZahlungszielSekunden: 1), Guid.NewGuid()))!.Success.Should().BeTrue();
            await Warte(async () => await Zeilen(rechnung.Wert) == 0, Frist, "die Frist wird nie ausgetragen");
            stoerung!.Abstuerze.Should().BeGreaterThanOrEqualTo(2, "einmal abgestürzt, dann erneut gefeuert und ausgetragen");
            (await Log(host, rechnung.Wert)).Count(e => e.Payload is ZahlungUeberfaellig).Should().Be(1, "dieselbe Command-Id — die Inbox verwirft das zweite Feuern");
        }
        finally { await host.StopAsync(); }
    }
}
