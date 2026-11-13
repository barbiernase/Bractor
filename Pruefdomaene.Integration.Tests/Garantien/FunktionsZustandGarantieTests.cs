using Abstractions;
using FluentAssertions;
using Infrastructure.Funktionen;
using Infrastructure.Lebenszyklus;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Proto;
using Proto.Cluster;
using Pruefdomaene.Messung;
using Pruefwerk.Katalog;
using static Pruefdomaene.Integration.Tests.Pruefstand;

namespace Pruefdomaene.Integration.Tests.Garantien;

/// <summary>
/// Funktionen eines Graphen mit Zustand und Ressourcen (docs/04 §4.4, docs/11) gegen echtes Marten, am allgemeinen Fall der Prüfdomäne (Messung): ein
/// Messwert → Merkmalsbildung (gleichbleibend) → Merkmal (ein Command zwischen Knoten, nie im Log) gabelt an den Schätzer (liest die Messreihe, eine
/// Projektion) und die Ausreißerprüfung → Command an die Messstelle. Geprüft wird am Log: Ressourcen schließen beim
/// Stoppen, die Schätzung beruht auf dem, was die Projektion gelernt hat, ein Knoten mit Zustand rechnet nur auf seinem Rechner.
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

    private sealed class Versand(Verbindungen v) : Pruefwerk.Katalog.IMahnversand, IAsyncDisposable
    {
        private readonly int _ = Interlocked.Increment(ref v.Offen);
        public Task<string> VersendeAsync(Guid empfaenger, Guid vorgang, int stufe) =>
            Task.FromResult("brief-" + Guid.NewGuid().ToString("N"));
        public ValueTask DisposeAsync() { Interlocked.Increment(ref v.Geschlossen); return ValueTask.CompletedTask; }
    }

    [Fact]
    public async Task Eine_Ressource_der_Aktivierung_wird_beim_Stoppen_geschlossen()
    {
        var v = new Verbindungen();
        using var host = Cluster("garantie-ressource", dienste: s => s.AddScoped<Pruefwerk.Katalog.IMahnversand>(_ => new Versand(v)));
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

    /// <summary>Je Graph eine Instanz, die Merkmalbildung (Zustand im Zählwerk) auf dem Rechner <paramref name="rechner"/> festgelegt.</summary>
    private static void Festgelegt(IServiceCollection s, string rechner) =>
        s.Replace(ServiceDescriptor.Singleton(new GraphInstanzen(GraphInstanzen.JeGraphEine(global::Infrastructure.Komposition.Anwendung.Funktionen.Graphen).Alle
            .Select(i => i.Graph.Name == nameof(Merkmalbildung) ? i with { Rechner = rechner } : i).ToList())));

    private static bool Haelt(IHost h) => h.Services.GetRequiredService<FunktionsOrte>().HaeltSelbst(nameof(Merkmalbildung));

    [Fact]
    public async Task Eine_Funktion_mit_Zustand_rechnet_nur_auf_ihrem_Rechner_und_zieht_bei_Ausfall_nicht_um()
    {
        // Zwei Hosts auf derselben Datenbank: der Schätzer trägt über sein Zählwerk Zustand, seine Instanz nennt den Rechner „a“ — nur dieser Host
        //   legt sie an; endet er, übernimmt der andere sie nicht (die Maschine hat, was die Instanz braucht).
        var a = Cluster("garantie-rechner-a", rechner: "a", dienste: s => Festgelegt(s, "a"));
        var b = Cluster("garantie-rechner-b", rechner: "b", dienste: s => Festgelegt(s, "a"));
        await a.StartAsync();
        await b.StartAsync();
        var gestoppt = new HashSet<IHost>();
        try
        {
            await Warte(() => Task.FromResult(Haelt(a)), Frist, "der Rechner a hält die Merkmalbildung nicht");
            Haelt(b).Should().BeFalse("die Instanz nennt den Rechner a");

            await a.StopAsync();
            gestoppt.Add(a);
            await Task.Delay(TimeSpan.FromSeconds(5));
            Haelt(b).Should().BeFalse("fällt der Rechner aus, zieht nichts um");
        }
        finally
        {
            foreach (var h in new[] { a, b }.Where(h => !gestoppt.Contains(h))) await h.StopAsync();
            a.Dispose();
            b.Dispose();
        }
    }

    [Fact]
    public async Task Tragen_zwei_Hosts_denselben_Rechnernamen_haelt_nur_einer_die_festgelegte_Instanz()
    {
        // Ein Konfigurationsfehler: zwei Hosts im selben Cluster nennen sich „r“. Vorrang hat der mit der kleineren Mitglieds-Id; der andere hält seine
        //   festgelegten Instanzen nicht (und meldet den Fehler). Endet der erste, hält der zweite sie.
        var cluster = "garantie-doppelt-" + Guid.NewGuid().ToString("N")[..8];
        var a = Cluster("garantie-doppelt", rechner: "r", clusterName: cluster, dienste: s => Festgelegt(s, "r"));
        var b = Cluster("garantie-doppelt", rechner: "r", clusterName: cluster, dienste: s => Festgelegt(s, "r"));
        await a.StartAsync();
        await b.StartAsync();
        var gestoppt = new HashSet<IHost>();
        try
        {
            bool Verdraengt(IHost h) => h.Services.GetRequiredService<FunktionsOrte>().Verdraengt;
            await Warte(() => Task.FromResult(Verdraengt(a) || Verdraengt(b)), Frist, "keiner erkennt den doppelten Rechnernamen");
            await Warte(() => Task.FromResult(Haelt(a) != Haelt(b)), Frist, "nicht genau einer hält die festgelegte Instanz");
            var (halter, anderer) = Haelt(a) ? (a, b) : (b, a);
            Verdraengt(halter).Should().BeFalse("der Halter hat Vorrang");
            Verdraengt(anderer).Should().BeTrue();

            await halter.StopAsync();
            gestoppt.Add(halter);
            await Warte(() => Task.FromResult(Haelt(anderer)), Frist, "ist der erste fort, hält der zweite die Instanz");
        }
        finally
        {
            foreach (var h in new[] { a, b }.Where(h => !gestoppt.Contains(h))) await h.StopAsync();
            a.Dispose();
            b.Dispose();
        }
    }
}
