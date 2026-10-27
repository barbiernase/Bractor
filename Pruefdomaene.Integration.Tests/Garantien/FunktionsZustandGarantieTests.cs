using System.Text.Json;
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
/// Funktionen als Actors mit Zustand und das Hinweis-Netz (Plan Pub/Sub-Neustrukturierung §2.9, §4.2, Phase 4b) gegen echtes Marten, am allgemeinen
/// Fall der Prüfdomäne (Messung): Messwerte im Log → Merkmalsbildung (gleichbleibend) → Merkmal (Hinweis, nie im Log) → Schätzer mit Zustand und einer
/// Adresse bzw. Ausreißerprüfung (Schnittstelle, im Host oder draußen) → Command an die Messstelle. Geprüft wird am Log und am gesicherten Zustand:
/// jeder Log-Eingang wirkt genau einmal auf den gesicherten Zustand — nach Absturz, Umzug und mit zwei Aktivierungen —, Ressourcen schließen beim
/// Stoppen, Commands aus einem Hinweis wirken einmal.
/// </summary>
[Trait("Kategorie", "Garantie")]
public class FunktionsZustandGarantieTests
{
    private static readonly TimeSpan Frist = TimeSpan.FromSeconds(90);
    private static readonly string ZustandsId = Funktionsvertrag.ZustandId(nameof(Schaetzer), Funktionsvertrag.EinzigeIdentitaet);

    private static async Task<IReadOnlyList<EventEnvelope>> Log(IHost host, Guid stream) =>
        await host.Services.GetRequiredService<IEventStoreRepository>().ReadStreamAsync(stream, 0, default);

    private static async Task MissAsync(IHost host, Id<Messstelle> stelle, params double[] werte)
    {
        foreach (var w in werte) (await FrageAsync(host, new ErfasseMesswert(stelle, w), Guid.NewGuid()))!.Success.Should().BeTrue();
    }

    /// <summary>Was der gesicherte Zustand des Schätzers über eine Messstelle weiß (Anzahl gelernter Werte), seine Position und die des Stands.</summary>
    private static async Task<(int Anzahl, long Position, long Stand)> GesichertAsync(IHost host, Id<Messstelle> stelle)
    {
        var staende = host.Services.GetRequiredService<IFunktionsStand>();
        var dok = await staende.LadeZustandAsync(ZustandsId, default);
        var stand = await staende.LadeAsync(Funktionsvertrag.StandId(nameof(Schaetzer)), 0, default);
        if (dok is null) return (0, 0, stand.Position);
        var daten = dok.Daten ?? await File.ReadAllTextAsync(Path.Combine(Verzeichnis(host), dok.Verweis!));
        using var json = JsonDocument.Parse(daten);
        var anzahl = json.RootElement.GetProperty(nameof(Schaetzmodell.Je)).TryGetProperty(stelle.Wert.ToString(), out var je)
            ? je.GetProperty(nameof(Lernstand.Anzahl)).GetInt32() : 0;
        return (anzahl, dok.Position, stand.Position);
    }

    private static readonly Dictionary<IHost, string> Verzeichnisse = new();
    private static string Verzeichnis(IHost host) => Verzeichnisse.GetValueOrDefault(host) ?? "";

    private static Task Gelernt(IHost host, Id<Messstelle> stelle, int anzahl, string meldung) =>
        Warte(async () => await GesichertAsync(host, stelle) is var g && g.Anzahl == anzahl && g.Position == g.Stand, Frist, meldung);

    [Fact]
    public async Task Hinweis_Kette_bis_zum_Command_ohne_Log_Eintrag_des_Hinweises_und_ohne_Festhalten_der_gleichbleibenden_Funktion()
    {
        using var host = Cluster("garantie-hinweiskette");
        await host.StartAsync();
        try
        {
            var a = Id<Messstelle>.Neu();
            var b = Id<Messstelle>.Neu();
            await MissAsync(host, a, 1, 2, 3);
            await MissAsync(host, b, 10, 2_000);   // 2 000 ist ein Ausreißer (Schwellenprüfung im Host)

            await Warte(async () => (await Log(host, a.Wert)).Count(e => e.Payload is SchaetzungVermerkt) == 3
                                    && (await Log(host, b.Wert)).Count(e => e.Payload is SchaetzungVermerkt) == 2
                                    && (await Log(host, b.Wert)).Any(e => e.Payload is AusreisserGemeldet), Frist, "die Hinweis-Kette kommt nicht beim Aggregat an");
            await Task.Delay(TimeSpan.FromSeconds(2));

            (await Log(host, a.Wert)).Select(e => e.Payload.GetType().Name).Should().BeEquivalentTo(
                Enumerable.Repeat(nameof(MesswertErfasst), 3).Concat(Enumerable.Repeat(nameof(SchaetzungVermerkt), 3)), "je Merkmal genau eine Schätzung");
            (await Log(host, b.Wert)).Count(e => e.Payload is AusreisserGemeldet).Should().Be(1);
            await using var conn = new Npgsql.NpgsqlConnection(Verbindung);
            await conn.OpenAsync();
            await using (var cmd = new Npgsql.NpgsqlCommand("select count(*) from pruef_e2e.mt_events where type = 'merkmal'", conn))
                ((long)(await cmd.ExecuteScalarAsync())!).Should().Be(0, "ein Hinweis steht nie im Log");
            foreach (var e in (await Log(host, a.Wert)).Where(e => e.Payload is MesswertErfasst))
                (await Log(host, FunktionsIds.Festhalten(nameof(Merkmalsbildung), a.Wert, e.AggregateVersion))).Should().BeEmpty(
                    "die Merkmalsbildung ist gleichbleibend — sie hält nichts fest");
        }
        finally { await host.StopAsync(); }
    }

    [Fact]
    public async Task Umzug_einer_Aktivierung_der_Zustand_kommt_mit()
    {
        var stelle = Id<Messstelle>.Neu();
        using (var a = Cluster("garantie-zustand-umzug-a"))
        {
            await a.StartAsync();
            try
            {
                await MissAsync(a, stelle, 1, 2, 3, 4);
                await Gelernt(a, stelle, 4, "der Schätzer lernt nicht");
            }
            finally { await a.StopAsync(); }
        }
        using var b = Cluster("garantie-zustand-umzug-b");
        await b.StartAsync();
        try
        {
            await MissAsync(b, stelle, 5, 6);
            await Gelernt(b, stelle, 6, "die neue Aktivierung kennt den Zustand der alten nicht (oder lernt doppelt)");
            await Warte(async () => (await Log(b, stelle.Wert)).Select(e => e.Payload).OfType<SchaetzungVermerkt>().Any(s => s.Anzahl == 6), Frist,
                "die Schätzung nach dem Umzug beruht nicht auf allem Gelernten");
        }
        finally { await b.StopAsync(); }
    }

    /// <summary>Sichert einmal — danach „stürzt“ jede Sicherung ab, nachdem die Datei geschrieben ist, bevor Position und Verweis gelten.</summary>
    private sealed class AbsturzNachDerDatei(IFunktionsStand innen) : IFunktionsStand
    {
        public int Gesichert, Abstuerze;
        public Task<PartitionsCheckpoint> LadeAsync(string id, int partition, CancellationToken ct) => innen.LadeAsync(id, partition, ct);
        public Task<int> SchreibeAsync(PartitionsCheckpoint stand, CancellationToken ct) => innen.SchreibeAsync(stand, ct);
        public Task<FunktionsZustandsDokument?> LadeZustandAsync(string id, CancellationToken ct) => innen.LadeZustandAsync(id, ct);
        public async Task<(int? Stand, int Zustand)> SichereAsync(PartitionsCheckpoint? stand, FunktionsZustandsDokument zustand, CancellationToken ct)
        {
            if (Interlocked.Increment(ref Gesichert) > 1)
            {
                Interlocked.Increment(ref Abstuerze);
                throw new InvalidOperationException("Absturz zwischen Sicherung (Datei) und Position");
            }
            return await innen.SichereAsync(stand, zustand, ct);
        }
    }

    [Fact]
    public async Task Absturz_zwischen_Sicherung_und_Position_jeder_Log_Eingang_wirkt_genau_einmal_auf_den_gesicherten_Zustand()
    {
        var verzeichnis = Path.Combine(Path.GetTempPath(), "cqrs-zustand-" + Guid.NewGuid().ToString("N")[..8]);
        var bindung = new BausteinBindung(nameof(Schaetzer), Zustand: new ZustandsBindung(ZustandsAblage.Datei, verzeichnis, TimeSpan.FromMilliseconds(200)));
        var stelle = Id<Messstelle>.Neu();
        AbsturzNachDerDatei? stoerung = null;
        using (var a = Cluster("garantie-zustand-absturz-a", dienste: s =>
               {
                   s.AddSingleton(bindung);
                   Dekoriere<IFunktionsStand>(s, (_, innen) => stoerung = new AbsturzNachDerDatei(innen));
               }))
        {
            Verzeichnisse[a] = verzeichnis;
            await a.StartAsync();
            try
            {
                await MissAsync(a, stelle, 1, 2, 3);
                await Warte(() => Task.FromResult(Volatile.Read(ref stoerung!.Gesichert) >= 1), Frist, "die erste Sicherung kommt nicht");
                await Task.Delay(TimeSpan.FromMilliseconds(300));   // länger als das Intervall: die nächste Sicherung folgt dem nächsten Eingang
                await MissAsync(a, stelle, 4, 5);
                await Warte(() => Task.FromResult(Volatile.Read(ref stoerung!.Abstuerze) >= 1), Frist, "die Sicherung stürzt nicht ab");
                await Warte(async () => (await Log(a, stelle.Wert)).Count(e => e.Payload is SchaetzungVermerkt) == 5, Frist, "die Aktivierung rechnet nicht weiter");
            }
            finally { await a.StopAsync(); }   // auch die Sicherung beim Stopp „stürzt ab“ — gesichert bleibt der Stand der ersten Sicherung
        }

        using var b = Cluster("garantie-zustand-absturz-b", dienste: s => s.AddSingleton(bindung));
        Verzeichnisse[b] = verzeichnis;
        await b.StartAsync();
        try
        {
            await MissAsync(b, stelle, 6);
            await Gelernt(b, stelle, 6, "nach dem Absturz ist der gesicherte Zustand nicht genau einmal je Eingang (fehlt etwas oder zählt doppelt?)");
            var dok = await b.Services.GetRequiredService<IFunktionsStand>().LadeZustandAsync(ZustandsId, default);
            dok!.Verweis.Should().NotBeNull("großer Zustand liegt als Datei, das Dokument verweist darauf");
            File.Exists(Path.Combine(verzeichnis, dok.Verweis!)).Should().BeTrue();
            Directory.GetFiles(verzeichnis, "*.json", SearchOption.AllDirectories).Should().HaveCount(1,
                "eine Datei je gültiger Sicherung — die verworfenen und die abgelösten sind entfernt");
        }
        finally
        {
            await b.StopAsync();
            try { Directory.Delete(verzeichnis, recursive: true); } catch { /* aufräumen */ }
        }
    }

    [Fact]
    public async Task Zwei_Aktivierungen_derselben_Adresse_der_gesicherte_Zustand_stammt_aus_einer_und_passt_zur_Position()
    {
        var stelle = Id<Messstelle>.Neu();
        using var a = Cluster("garantie-zustand-zwei-a");
        using var b = Cluster("garantie-zustand-zwei-b");
        await a.StartAsync();
        await b.StartAsync();
        try
        {
            // Beide Knoten haben eine Aktivierung derselben Adresse (Schaetzer/0); beide lesen das Log und sichern — eine gewinnt je Version.
            for (var i = 0; i < 6; i++) await MissAsync(a, stelle, i);
            await Gelernt(a, stelle, 6, "zwei Aktivierungen: der gesicherte Zustand zählt nicht genau einmal je Eingang");
            await Task.Delay(TimeSpan.FromSeconds(3));
            var g = await GesichertAsync(a, stelle);
            g.Anzahl.Should().Be(6);
            g.Position.Should().Be(g.Stand, "Zustand und Position aus derselben Sicherung");
        }
        finally
        {
            await b.StopAsync();
            await a.StopAsync();
        }
        (await GesichertAsync(a, stelle)).Anzahl.Should().Be(6, "auch nach dem Stopp beider gilt eine Sicherung, kein Gemisch");
    }

    [Fact]
    public async Task Zwei_Sicherungen_mit_derselben_erwarteten_Version_eine_gewinnt_die_andere_verliert_ganz()
    {
        using var host = Cluster("garantie-zustand-sicherung");
        await host.StartAsync();
        try
        {
            var staende = host.Services.GetRequiredService<IFunktionsStand>();
            var id = "zwei-" + Guid.NewGuid().ToString("N")[..8];
            var stand = PartitionsCheckpoint.Neu("funktion:" + id, 0);
            FunktionsZustandsDokument Dok(string daten) => new()
            {
                Id = Funktionsvertrag.ZustandId(id, "0"), Funktion = id, Adresse = "0", Position = 7, Daten = daten, Version = 0,
            };
            stand.Position = 7;
            var erste = staende.SichereAsync(stand, Dok("""{"von":"a"}"""), default);
            var zweite = staende.SichereAsync(stand, Dok("""{"von":"b"}"""), default);
            var ergebnisse = await Task.WhenAll(erste.ContinueWith(t => t.Exception?.GetBaseException()), zweite.ContinueWith(t => t.Exception?.GetBaseException()));
            ergebnisse.Count(e => e is null).Should().Be(1, "nur eine Aktivierung gewinnt");
            ergebnisse.Count(e => e is PartitionsKonfliktException).Should().Be(1, "die andere verliert ihren ganzen Versuch");
            var gewinner = ergebnisse[0] is null ? "a" : "b";
            (await staende.LadeZustandAsync(Funktionsvertrag.ZustandId(id, "0"), default))!.Daten.Should().Contain(gewinner);
            (await staende.LadeAsync("funktion:" + id, 0, default)).Version.Should().Be(1, "Stand und Zustand in einer Transaktion — vom Gewinner");
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
    public async Task Commands_aus_einem_Hinweis_wirken_einmal_auch_wenn_er_doppelt_ankommt()
    {
        using var host = Cluster("garantie-hinweis-einmal");
        await host.StartAsync();
        try
        {
            var stelle = Id<Messstelle>.Neu();
            await MissAsync(host, stelle, 1);   // die Messstelle gibt es
            var bote = new Bote(host.Services.GetRequiredService<ActorSystem>().Cluster());
            var pruefung = Infrastructure.Komposition.Anwendung.Funktionen.Bausteine.Single(b => b.Name == nameof(IAusreisserpruefung));
            var hinweis = new HinweisZustellen(Guid.NewGuid(), new Merkmal(stelle, 5_000), stelle.Wert, 1, DateTimeOffset.UtcNow, Guid.NewGuid().ToString(), null);
            var ziel = FunktionsActor.IdentitaetFuer(pruefung, stelle.Wert, Infrastructure.Projections.Konsum.Standard);
            bote.Melde(ziel, hinweis);
            bote.Melde(ziel, hinweis);   // derselbe Hinweis noch einmal (ein Absender, der wiederholt): dieselbe Kennung, dieselbe Command-Id
            await Warte(async () => (await Log(host, stelle.Wert)).Any(e => e.Payload is AusreisserGemeldet), Frist, "der Hinweis wirkt nicht");
            await Task.Delay(TimeSpan.FromSeconds(3));
            (await Log(host, stelle.Wert)).Count(e => e.Payload is AusreisserGemeldet).Should().Be(1, "die Inbox verwirft die zweite Command-Id");

            bote.Melde(ziel, hinweis with { Kennung = Guid.NewGuid() });   // ein anderer Hinweis wirkt sehr wohl
            await Warte(async () => (await Log(host, stelle.Wert)).Count(e => e.Payload is AusreisserGemeldet) == 2, Frist, "ein neuer Hinweis wirkt nicht");
        }
        finally { await host.StopAsync(); }
    }

    [Fact]
    public async Task Eine_Funktion_draussen_hoert_einen_Hinweis_ueber_den_Weg_der_Leitung_nichts_wird_festgehalten()
    {
        // Dieselbe Sitzung wie für einen Python-Worker (BausteinSitzung hinter dem Hand-Ausführer): Vermittler, Lease, ArbeitsAuftrag — nur die
        //   Antwort auf einen Hinweis geht mit ihren Ausgaben zum Funktions-Actor, der sie sendet.
        using var host = Cluster("garantie-hinweis-draussen", dienste: s =>
        {
            s.AddSingleton(new BausteinBindung(nameof(IAusreisserpruefung), FunktionsLaufort.Hand));
            s.AddHandAusfuehrer();
        });
        await host.StartAsync();
        try
        {
            var stelle = Id<Messstelle>.Neu();
            await MissAsync(host, stelle, 3_000);
            var hand = host.Services.GetRequiredService<HandAusfuehrer>();
            HandEingang? offen = null;
            await Warte(() => Task.FromResult((offen = hand.OffeneEingaenge.FirstOrDefault(e => e.Eingang.Stream == stelle.Wert)) is not null), Frist,
                "der Hinweis erreicht die Funktion draußen nicht");
            offen!.Eingang.IstHinweis.Should().BeTrue();
            offen.Eingang.Eingang.Should().BeOfType<Merkmal>().Which.Wert.Should().Be(3_000);
            offen.Eingang.Adresse.Should().Be(pruefungsAdresse(stelle), "die Adresse des Funktions-Actors reist mit (A8)");

            (await hand.AntworteAsync(offen.Eingang.Vorgang, new IAusgabe[] { new MeldeAusreisser(stelle, 3_000) }, null, default))
                .Should().Be(ErgebnisAusgang.Geschrieben);
            await Warte(async () => (await Log(host, stelle.Wert)).Any(e => e.Payload is AusreisserGemeldet), Frist, "die Antwort von draußen wirkt nicht");
            (await Log(host, offen.Eingang.Vorgang)).Should().BeEmpty("eine Antwort auf einen Hinweis wird nicht festgehalten");
        }
        finally { await host.StopAsync(); }

        static string pruefungsAdresse(Id<Messstelle> s) =>
            Partitionierung.Von(s.Wert, Infrastructure.Projections.Konsum.Standard.Partitionen).ToString();
    }
}
