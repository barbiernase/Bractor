using Abstractions;
using FluentAssertions;
using Infrastructure.Funktionen;
using Infrastructure.Lebenszyklus;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Proto;
using Proto.Cluster;
using Pruefdomaene.Messung;
using Pruefdomaene.Pipeline.Messung;
using static Pruefdomaene.Integration.Tests.Pruefstand;

namespace Pruefdomaene.Integration.Tests.Garantien;

/// <summary>
/// Funktionen eines Graphen mit Zustand und Ressourcen (docs/04 §4.4, docs/11) gegen echtes Marten, am allgemeinen Fall der Prüfdomäne (Messung): ein
/// Messwert → Merkmalsbildung (gleichbleibend) → Merkmal (ein Command zwischen Knoten, nie im Log) gabelt an den Schätzer (liest die Messreihe, eine
/// Projektion) und die Ausreißerprüfung (Schnittstelle, im Host oder draußen) → Command an die Messstelle. Geprüft wird am Log: Ressourcen schließen beim
/// Stoppen, die Schätzung beruht auf dem, was die Projektion gelernt hat, ein Knoten mit Zustand rechnet auf genau einem Cluster-Knoten.
/// </summary>
[Trait("Kategorie", "Garantie")]
public class FunktionsZustandGarantieTests
{
    private static readonly TimeSpan Frist = TimeSpan.FromSeconds(90);

    private static async Task<IReadOnlyList<EventEnvelope>> Log(IHost host, Guid stream) =>
        await host.Services.GetRequiredService<IEventStoreRepository>().ReadStreamAsync(stream, 0, default);

    private static async Task MissAsync(IHost host, Id<Messstelle> stelle, params double[] werte)
    {
        foreach (var w in werte) (await FrageAsync(host, new ErfasseMesswert(stelle, w), Guid.NewGuid()))!.Success.Should().BeTrue();
    }

    [Fact]
    public async Task Fork_bis_zum_Command_ohne_Log_Eintrag_des_Merkmals_und_ohne_Festhalten_der_gleichbleibenden_Funktion()
    {
        using var host = Cluster("garantie-merkmalfork");
        await host.StartAsync();
        try
        {
            var a = Id<Messstelle>.Neu();
            var b = Id<Messstelle>.Neu();
            await MissAsync(host, a, 1, 2, 3);
            await MissAsync(host, b, 10, 2_000);   // 2 000 ist ein Ausreißer (Schwellenprüfung im Host)

            await Warte(async () => (await Log(host, a.Wert)).Count(e => e.Payload is SchaetzungVermerkt) == 3
                                    && (await Log(host, b.Wert)).Count(e => e.Payload is SchaetzungVermerkt) == 2
                                    && (await Log(host, b.Wert)).Any(e => e.Payload is AusreisserGemeldet), Frist, "der Fork kommt nicht beim Aggregat an");
            await Task.Delay(TimeSpan.FromSeconds(2));

            (await Log(host, a.Wert)).Select(e => e.Payload.GetType().Name).Should().BeEquivalentTo(
                Enumerable.Repeat(nameof(MesswertErfasst), 3).Concat(Enumerable.Repeat(nameof(SchaetzungVermerkt), 3)), "je Merkmal genau eine Schätzung");
            (await Log(host, b.Wert)).Count(e => e.Payload is AusreisserGemeldet).Should().Be(1);
            await using var conn = new Npgsql.NpgsqlConnection(Verbindung);
            await conn.OpenAsync();
            await using (var cmd = new Npgsql.NpgsqlCommand("select count(*) from pruef_e2e.mt_events where type = 'merkmal'", conn))
                ((long)(await cmd.ExecuteScalarAsync())!).Should().Be(0, "ein Command zwischen Knoten steht nie im Log");
        }
        finally { await host.StopAsync(); }
    }

    [Fact]
    public async Task Die_Schaetzung_beruht_auf_dem_was_die_Projektion_gelernt_hat()
    {
        using var host = Cluster("garantie-schaetzung-projektion");
        await host.StartAsync();
        try
        {
            var stelle = Id<Messstelle>.Neu();
            await MissAsync(host, stelle, 1, 2, 3);
            var queries = host.Services.GetRequiredService<IQueryDienst>();
            await Warte(async () => ((Pruefdomaene.Projections.Messung.Messreihe)(await queries.ExecuteAsync(
                new Pruefdomaene.Projections.Messung.HoleMessreihe(stelle))).Data).Werte.Count == 3, Frist, "die Messreihe lernt nicht");
            await Warte(async () => (await Log(host, stelle.Wert)).Count(e => e.Payload is SchaetzungVermerkt) == 3, Frist, "die Schätzungen fehlen");

            await MissAsync(host, stelle, 6);
            await Warte(async () => (await Log(host, stelle.Wert)).Count(e => e.Payload is SchaetzungVermerkt) == 4, Frist, "die vierte Schätzung fehlt");
            var vierte = (await Log(host, stelle.Wert)).Select(e => e.Payload).OfType<SchaetzungVermerkt>().Last();
            vierte.Anzahl.Should().BeGreaterThanOrEqualTo(3, "die Funktion liest die Messreihe, die schon drei Werte kennt");
            vierte.Erwartet.Should().Be(vierte.Anzahl == 3 ? 2 : 3, "der Mittelwert dessen, was die Projektion kennt");
        }
        finally { await host.StopAsync(); }
    }

    /// <summary>Ein Versand, der seine Verbindungen zählt: gebaut je Aktivierung, geschlossen beim Stoppen.</summary>
    private sealed class Verbindungen
    {
        public int Offen, Geschlossen;
    }

    private sealed class Versand(Verbindungen v) : Pruefdomaene.Pipeline.Abrechnung.IMahnversand, IAsyncDisposable
    {
        private readonly int _ = Interlocked.Increment(ref v.Offen);
        public Task<string> VersendeAsync(Id<Pruefdomaene.Abrechnung.Kundenkonto> konto, Id<Pruefdomaene.Abrechnung.Rechnung> rechnung, int stufe) =>
            Task.FromResult("brief-" + Guid.NewGuid().ToString("N"));
        public ValueTask DisposeAsync() { Interlocked.Increment(ref v.Geschlossen); return ValueTask.CompletedTask; }
    }

    [Fact]
    public async Task Eine_Ressource_der_Aktivierung_wird_beim_Stoppen_geschlossen()
    {
        var v = new Verbindungen();
        using var host = Cluster("garantie-ressource", dienste: s => s.AddScoped<Pruefdomaene.Pipeline.Abrechnung.IMahnversand>(_ => new Versand(v)));
        await host.StartAsync();
        try
        {
            var rechnung = Id<Pruefdomaene.Abrechnung.Rechnung>.Neu();
            var konto = Id<Pruefdomaene.Abrechnung.Kundenkonto>.Neu();
            (await FrageAsync(host, new Pruefdomaene.Abrechnung.StelleRechnung(rechnung, konto, 50m), Guid.NewGuid()))!.Success.Should().BeTrue();
            (await FrageAsync(host, new Pruefdomaene.Abrechnung.PruefeZahlung(rechnung), Guid.NewGuid()))!.Success.Should().BeTrue();
            await Warte(async () => (await Zustand<Pruefdomaene.Abrechnung.Kundenkonto>(host.Services, konto.Wert)).Mahnungen == 1, Frist, "die Mahnung kommt nicht an");
            Volatile.Read(ref v.Offen).Should().BeGreaterThan(0);
            Volatile.Read(ref v.Geschlossen).Should().Be(0, "solange die Aktivierung lebt, bleibt ihre Verbindung offen");
        }
        finally { await host.StopAsync(); }
        Volatile.Read(ref v.Geschlossen).Should().Be(Volatile.Read(ref v.Offen), "jede Aktivierung schließt beim Stoppen ihre Verbindung");
    }

    [Fact]
    public async Task Ein_Knoten_draussen_nimmt_das_Merkmal_ueber_den_Weg_der_Leitung_nichts_wird_festgehalten()
    {
        // Derselbe Weg wie für einen Python-Worker: der Hand-Ausführer ist ein Arbeiter in der Werkstatt, ein Kind des Knotens gibt ihm das Merkmal
        //   und sendet die Ausgaben seiner Antwort — nichts wird festgehalten.
        using var host = Cluster("garantie-merkmal-draussen", dienste: s =>
        {
            s.AddSingleton(global::Infrastructure.Funktionen.GraphInstanzen.JeGraphEine(global::Infrastructure.Komposition.Anwendung.Funktionen.Graphen,
                new Dictionary<string, KnotenKonfig> { [nameof(IAusreisserpruefung)] = new() { Laufort = FunktionsLaufort.Hand } }));
            s.AddHandAusfuehrer();
        });
        await host.StartAsync();
        try
        {
            var stelle = Id<Messstelle>.Neu();
            await MissAsync(host, stelle, 3_000);
            var hand = host.Services.GetRequiredService<HandAusfuehrer>();
            HandEingang? offen = null;
            await Warte(() => Task.FromResult((offen = hand.OffeneEingaenge.FirstOrDefault(e => e.Stream == stelle.Wert)) is not null), Frist,
                "das Merkmal erreicht die Funktion draußen nicht");
            offen!.Eingang.Should().BeOfType<Merkmal>().Which.Wert.Should().Be(3_000);

            hand.Antworte(offen.Vorgang, new IAusgabe[] { new MeldeAusreisser(stelle, 3_000) }, null)
                .Should().Be(Werkstattausgang.Angenommen);
            await Warte(async () => (await Log(host, stelle.Wert)).Any(e => e.Payload is AusreisserGemeldet), Frist, "die Antwort von draußen wirkt nicht");
            (await Log(host, offen.Vorgang)).Should().BeEmpty("eine Antwort von draußen wird nicht festgehalten");
        }
        finally { await host.StopAsync(); }
    }

    [Fact]
    public async Task Eine_Funktion_mit_Zustand_rechnet_auf_genau_einem_Knoten_der_ihre_Sperre_haelt()
    {
        // Zwei Hosts auf derselben Datenbank: der Schätzer trägt über sein Zählwerk Zustand — nur einer hält die Sperre seiner Instanz (Advisory-Lock);
        //   endet er, nimmt der andere sie.
        var a = Cluster("garantie-sperre-a");
        var b = Cluster("garantie-sperre-b");
        await a.StartAsync();
        await b.StartAsync();
        var gestoppt = new HashSet<IHost>();
        try
        {
            bool Haelt(IHost h) => h.Services.GetRequiredService<ZustandsSperre>().Gehalten.Contains(nameof(Merkmalbildung));
            await Warte(() => Task.FromResult(Haelt(a) || Haelt(b)), Frist, "niemand nimmt die Sperre des Schätzers");
            await Task.Delay(TimeSpan.FromSeconds(3));
            (Haelt(a) && Haelt(b)).Should().BeFalse("genau ein Knoten hält die Sperre");
            var (halter, anderer) = Haelt(a) ? (a, b) : (b, a);
            halter.Services.GetRequiredService<FunktionsOrte>().HaeltSelbst(nameof(Merkmalbildung)).Should().BeTrue("der Halter hält die Funktion im Gossip-Zustand");
            anderer.Services.GetRequiredService<FunktionsOrte>().HaeltSelbst(nameof(Merkmalbildung)).Should().BeFalse();

            await halter.StopAsync();
            gestoppt.Add(halter);
            await Warte(() => Task.FromResult(Haelt(anderer)), Frist, "der andere Knoten übernimmt die Sperre nicht");
            await Warte(() => Task.FromResult(anderer.Services.GetRequiredService<FunktionsOrte>().HaeltSelbst(nameof(Merkmalbildung))), Frist,
                "der andere Knoten hält die Funktion nicht");
        }
        finally
        {
            foreach (var h in new[] { a, b }.Where(h => !gestoppt.Contains(h))) await h.StopAsync();
            a.Dispose();
            b.Dispose();
        }
    }
}
