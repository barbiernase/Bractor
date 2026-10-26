using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Abstractions;
using Domain.ImagePair;
using FluentAssertions;
using Google.Protobuf;
using Infrastructure.Quellen;
using Infrastructure.Quellen.Treiber.Datei;
using Infrastructure.Serialization;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Infrastructure.Pruefstand.Quellen;

/// <summary>
/// Quellen = Treiber + Konfig (docs/konzept-werkbank.md §3), store-frei bewiesen: der Scan-Kern (Kennung, Fertig-Erkennung, Gedächtnis),
/// die Lease von „EinmalImCluster", die Start-Prüfungen der Konfig, der Treiber gegen ein echtes Temp-Verzeichnis (Log als Funktion)
/// und der Proto-Round-trip eines Quellen-Typs mit geerbten required-init-Feldern.
/// </summary>
public class DateiTreiberTests
{
    private static readonly DateTimeOffset T0 = new(2026, 10, 12, 8, 0, 0, TimeSpan.Zero);

    private static DateiStand Stand(string rel, long groesse = 10, DateTimeOffset? geaendert = null, bool marker = false)
        => new("/in/" + rel, rel, Path.GetFileName(rel), groesse, geaendert ?? T0, marker);

    private static DateiQuellKonfig Konfig(DateiFertigArt art = DateiFertigArt.Stabil, double fensterSek = 1, DateiKennungArt kennung = DateiKennungArt.PfadGroesseZeit)
        => new() { Pfad = "/in", Fertig = new DateiFertig { Art = art, Fenster = TimeSpan.FromSeconds(fensterSek) }, Kennung = kennung };

    // ── Kennung ──────────────────────────────────────────────────────────────────────────────────────────────────────

    [Fact]
    public void Kennung_NameGroesse_und_PfadGroesseZeit()
    {
        var d = Stand("sub/a.tiff", 4711, T0);
        DateiScan.Bilde(DateiKennungArt.NameGroesse, d).Should().Be("a.tiff|4711");
        DateiScan.Bilde(DateiKennungArt.PfadGroesseZeit, d).Should().Be("sub/a.tiff|4711|" + T0.ToString("O"));
    }

    [Trait("Kategorie", "Garantie")]
    [Fact]
    public void Vorgangs_Id_folgt_aus_Typ_und_Kennung()
    {
        var a = QuellVorgang.StreamFür(typeof(DateiErkannt), "a.tiff|1");
        QuellVorgang.StreamFür(typeof(DateiErkannt), "a.tiff|1").Should().Be(a, "dieselbe Datei → derselbe Vorgang");
        QuellVorgang.StreamFür(typeof(DateiErkannt), "a.tiff|2").Should().NotBe(a);
    }

    // ── Fertig-Erkennung ─────────────────────────────────────────────────────────────────────────────────────────────

    [Fact]
    public void Stabil_erst_nach_dem_Fenster_und_nur_wenn_unveraendert()
    {
        var scan = new DateiScan(Konfig(fensterSek: 2));
        scan.Fertige([Stand("a")], T0).Should().BeEmpty("gerade erst gesehen");
        scan.Fertige([Stand("a")], T0.AddSeconds(1)).Should().BeEmpty("das Fenster ist noch nicht um");
        scan.Fertige([Stand("a", 11)], T0.AddSeconds(3)).Should().BeEmpty("die Größe hat sich geändert → Fenster beginnt neu");
        scan.Fertige([Stand("a", 11)], T0.AddSeconds(5)).Select(d => d.Name).Should().Equal("a");
    }

    [Fact]
    public void Marker_meldet_erst_wenn_die_Marker_Datei_da_ist_und_nie_den_Marker_selbst()
    {
        var scan = new DateiScan(Konfig(DateiFertigArt.Marker));
        scan.Fertige([Stand("a.tiff")], T0).Should().BeEmpty();
        scan.Fertige([Stand("a.tiff", marker: true), Stand("a.tiff.done")], T0).Select(d => d.Name).Should().Equal("a.tiff");
    }

    [Trait("Kategorie", "Garantie")]
    [Fact]
    public void Gemeldet_wird_nicht_wiederholt_eine_ersetzte_Datei_aber_schon()
    {
        var scan = new DateiScan(Konfig(fensterSek: 0));
        var a = scan.Fertige([Stand("a")], T0).Single();
        scan.Gemeldet(a);
        scan.Fertige([Stand("a")], T0.AddSeconds(5)).Should().BeEmpty("schon gemeldet");
        scan.Fertige([Stand("a", 10, T0.AddMinutes(1))], T0.AddSeconds(6)).Should().ContainSingle("neu geschrieben → neue Kennung → neuer Vorgang");
    }

    [Fact]
    public void NameGroesse_erkennt_eine_gleich_grosse_Ersetzung_nicht()
    {
        var scan = new DateiScan(Konfig(fensterSek: 0, kennung: DateiKennungArt.NameGroesse));
        scan.Gemeldet(scan.Fertige([Stand("a")], T0).Single());
        scan.Fertige([Stand("a", 10, T0.AddMinutes(1))], T0.AddSeconds(5)).Should().BeEmpty();
    }

    [Trait("Kategorie", "Garantie")]
    [Fact]
    public void Schon_im_Log_bekannte_Dateien_werden_nicht_gemeldet_verschwundene_fallen_aus_dem_Gedaechtnis()
    {
        var scan = new DateiScan(Konfig(fensterSek: 0));
        scan.SchonBekannt(Stand("a"));
        scan.Fertige([Stand("a"), Stand("b")], T0).Select(d => d.Name).Should().Equal("b");
        scan.Fertige([], T0.AddSeconds(1)).Should().BeEmpty();
        scan.Fertige([Stand("a")], T0.AddSeconds(2)).Should().ContainSingle("verschwunden und wieder da: das Gedächtnis ist nur ein Spar-Cache, die Wahrheit ist der Log");
    }

    // ── Lease (EinmalImCluster) ──────────────────────────────────────────────────────────────────────────────────────

    [Trait("Kategorie", "Garantie")]
    [Fact]
    public void Lease_hat_genau_einen_Halter_bis_sie_ablaeuft()
    {
        var lease = new QuellenLease(TimeSpan.FromSeconds(15));
        lease.Halte("k1", T0).Haelt.Should().BeTrue();
        lease.Halte("k2", T0.AddSeconds(1)).Should().Be(new QuelleGehalten(false, "k1"));
        lease.Halte("k1", T0.AddSeconds(10)).Haelt.Should().BeTrue("der Halter verlängert");
        lease.Halte("k2", T0.AddSeconds(20)).Haelt.Should().BeFalse("verlängert bis T0+25");
        lease.Halte("k2", T0.AddSeconds(26)).Haelt.Should().BeTrue("abgelaufen → der nächste Fragende übernimmt");
    }

    // ── Konfig: Start-Prüfungen ──────────────────────────────────────────────────────────────────────────────────────

    private static readonly QuellTyp Typ = new(typeof(DateiErkannt), "Datei", "DateiErkannt", "quelle:DateiErkannt",
        d => new DateiErkannt { Name = d.Name, Pfad = d.Pfad, RelativerPfad = d.RelativerPfad, Groesse = d.Groesse,
            GeaendertAm = d.GeaendertAm, ErkanntAm = d.ErkanntAm, Kennung = d.Kennung });

    private static IServiceCollection Binde(Dictionary<string, string?> werte)
        => new ServiceCollection().AddQuellen(new ConfigurationBuilder().AddInMemoryCollection(werte).Build(), [Typ]);

    [Fact]
    public void Konfig_bindet_alle_Felder()
    {
        var s = Binde(new()
        {
            ["Quellen:DateiErkannt:Pfad"] = "/mnt/kamera",
            ["Quellen:DateiErkannt:Muster:0"] = "*.tiff",
            ["Quellen:DateiErkannt:Unterordner"] = "true",
            ["Quellen:DateiErkannt:Erkennung"] = "EreignisUndAbfrage",
            ["Quellen:DateiErkannt:Intervall"] = "00:00:05",
            ["Quellen:DateiErkannt:Fertig:Art"] = "Marker",
            ["Quellen:DateiErkannt:Fertig:Endung"] = ".ok",
            ["Quellen:DateiErkannt:Kennung"] = "NameGroesse",
            ["Quellen:DateiErkannt:Laufmodus"] = "JederKnoten",
        });
        var k = (DateiQuellKonfig)s.BuildServiceProvider().GetRequiredService<KonfigurierteQuelle>().Konfig;
        k.Pfad.Should().Be("/mnt/kamera");
        k.WirksameMuster.Should().Equal("*.tiff");
        k.Unterordner.Should().BeTrue();
        k.Erkennung.Should().Be(DateiErkennung.EreignisUndAbfrage);
        k.Intervall.Should().Be(TimeSpan.FromSeconds(5));
        k.Fertig.Art.Should().Be(DateiFertigArt.Marker);
        k.Fertig.Endung.Should().Be(".ok");
        k.Kennung.Should().Be(DateiKennungArt.NameGroesse);
        k.Laufmodus.Should().Be(QuellLaufmodus.JederKnoten);
    }

    // ── Webhook als Konfig (konzept-werkbank §12.3 E12) ──

    [Fact]
    public void Konfig_Webhook_ist_eine_Route()
    {
        Binde(new() { ["Quellen:DateiErkannt:Pfad"] = "/x", ["Quellen:DateiErkannt:Webhook"] = "/webhook/datei" })
            .BuildServiceProvider().GetRequiredService<KonfigurierteQuelle>().Konfig.Webhook.Should().Be("/webhook/datei");
        FluentActions.Invoking(() => Binde(new() { ["Quellen:DateiErkannt:Pfad"] = "/x", ["Quellen:DateiErkannt:Webhook"] = "webhook/datei" }))
            .Should().Throw<Infrastructure.Umgebung.BootFehler>().Where(e => e.Karte == "quelle:DateiErkannt").WithMessage("*keine Route*");
    }

    [Fact]
    public void Webhook_ohne_Einblick_Port_bricht_den_Start()
    {
        var b = Microsoft.AspNetCore.Builder.WebApplication.CreateBuilder();
        b.Services.AddSingleton(new KonfigurierteQuelle(Typ, new DateiQuellKonfig { Pfad = "/x", Webhook = "/webhook/datei" }));
        using var app = b.Build();
        FluentActions.Invoking(() => app.MapQuellWebhooks(new UmgebungKonfig { Transport = new TransportKonfig { EinblickPort = 0 } }))
            .Should().Throw<Infrastructure.Umgebung.BootFehler>().WithMessage("*braucht einen Einblick-Port*");
    }

    [Fact]
    public void Konfig_unbekanntes_Feld_ist_ein_Fehler()
        => FluentActions.Invoking(() => Binde(new() { ["Quellen:DateiErkannt:Pfad"] = "/x", ["Quellen:DateiErkannt:Pfaad"] = "/y" }))
            .Should().Throw<InvalidOperationException>().WithMessage("*Pfaad*");

    [Fact]
    public void Konfig_Abschnitt_ohne_Typ_ist_ein_Fehler()
        => FluentActions.Invoking(() => Binde(new() { ["Quellen:DateiErkannt:Pfad"] = "/x", ["Quellen:Kamera2:Pfad"] = "/y" }))
            .Should().Throw<InvalidOperationException>().WithMessage("*ohne Quellen-Typ*Kamera2*");

    [Fact]
    public void Konfig_Quelle_ohne_Abschnitt_ist_ein_Fehler()
        => FluentActions.Invoking(() => Binde(new()))
            .Should().Throw<InvalidOperationException>().WithMessage("*ohne Abschnitt*DateiErkannt*");

    // ── Treiber gegen ein echtes Verzeichnis (der Log ist eine Funktion) ────────────────────────────────────────────

    [Trait("Kategorie", "Garantie")]
    [Fact]
    public async Task Treiber_meldet_neue_Dateien_genau_einmal_und_ueberspringt_was_der_Log_schon_kennt()
    {
        var dir = Directory.CreateTempSubdirectory("quelle-").FullName;
        try
        {
            File.WriteAllText(Path.Combine(dir, "alt.tiff"), "x");
            File.WriteAllText(Path.Combine(dir, "neu.tiff"), "yy");
            File.WriteAllText(Path.Combine(dir, "egal.txt"), "z");
            var konfig = new DateiQuellKonfig
            {
                Pfad = dir, Muster = ["*.tiff"], Intervall = TimeSpan.FromMilliseconds(50),
                Fertig = new DateiFertig { Fenster = TimeSpan.Zero }, Kennung = DateiKennungArt.NameGroesse,
            };
            var gemeldet = new ConcurrentQueue<DateiErkannt>();
            var imLog = new HashSet<Guid> { QuellVorgang.StreamFür(typeof(DateiErkannt), "alt.tiff|1") };
            var zustand = new QuellenZustand();
            var treiber = new DateiTreiber(Typ, konfig, (n, _) => { gemeldet.Enqueue((DateiErkannt)n); return Task.CompletedTask; },
                (ids, _) => Task.FromResult<IReadOnlySet<Guid>>(ids.Where(imLog.Contains).ToHashSet()), zustand);

            using var cts = new CancellationTokenSource();
            var lauf = treiber.LaufeAsync(cts.Token);
            await Warte(() => gemeldet.Count >= 1);
            File.WriteAllText(Path.Combine(dir, "spaeter.tiff"), "zzz");
            await Warte(() => gemeldet.Count >= 2);
            await Task.Delay(300);
            cts.Cancel();
            await lauf;

            gemeldet.Select(d => d.Name).Should().Equal("neu.tiff", "spaeter.tiff");
            var neu = gemeldet.First();
            neu.Groesse.Should().Be(2);
            neu.RelativerPfad.Should().Be("neu.tiff");
            neu.Pfad.Should().Be(Path.Combine(dir, "neu.tiff"));
            neu.Kennung.Should().Be("neu.tiff|2");
            zustand.Alle["DateiErkannt"].Gestoert.Should().BeFalse();
            File.Exists(Path.Combine(dir, "neu.tiff")).Should().BeTrue("der Treiber liest nur — er verschiebt und löscht nichts");
        }
        finally { Directory.Delete(dir, true); }
    }

    [Fact]
    public async Task Fehlendes_Verzeichnis_macht_die_Quelle_gestoert_und_der_Treiber_erholt_sich()
    {
        var dir = Path.Combine(Path.GetTempPath(), "quelle-fehlt-" + Guid.NewGuid().ToString("N"));
        var konfig = new DateiQuellKonfig { Pfad = dir, Intervall = TimeSpan.FromMilliseconds(50), Fertig = new DateiFertig { Fenster = TimeSpan.Zero } };
        var gemeldet = new ConcurrentQueue<IQuellNachricht>();
        var zustand = new QuellenZustand();
        var treiber = new DateiTreiber(Typ, konfig, (n, _) => { gemeldet.Enqueue(n); return Task.CompletedTask; },
            (_, _) => Task.FromResult<IReadOnlySet<Guid>>(new HashSet<Guid>()), zustand);
        using var cts = new CancellationTokenSource();
        var lauf = treiber.LaufeAsync(cts.Token);
        try
        {
            await Warte(() => zustand.Alle.TryGetValue("DateiErkannt", out var z) && z.Gestoert);
            Directory.Exists(dir).Should().BeFalse("der Treiber legt kein Verzeichnis an");
            Directory.CreateDirectory(dir);
            File.WriteAllText(Path.Combine(dir, "a.tiff"), "x");
            await Warte(() => gemeldet.Count == 1, sekunden: 10);
            // Der Treiber meldet im selben Durchlauf, in dem er die Störung aufhebt — erst warten, dann prüfen (kein Wettlauf).
            await Warte(() => !zustand.Alle["DateiErkannt"].Gestoert);
            zustand.Alle["DateiErkannt"].Gestoert.Should().BeFalse();
        }
        finally
        {
            cts.Cancel();
            await lauf;
            if (Directory.Exists(dir)) Directory.Delete(dir, true);
        }
    }

    /// <summary>Garantie (docs/11 §11.8): Rückstau — die nächste Datei wird erst gemeldet, wenn die vorige im Log steht.</summary>
    [Trait("Kategorie", "Garantie")]
    [Fact]
    public async Task Rueckstau_die_naechste_Datei_erst_wenn_die_vorige_im_Log_steht()
    {
        var dir = Directory.CreateTempSubdirectory("quelle-").FullName;
        try
        {
            foreach (var n in new[] { "a.tiff", "b.tiff", "c.tiff" }) File.WriteAllText(Path.Combine(dir, n), n);
            var konfig = new DateiQuellKonfig
            {
                Pfad = dir, Muster = ["*.tiff"], Intervall = TimeSpan.FromMilliseconds(50),
                Fertig = new DateiFertig { Fenster = TimeSpan.Zero }, Kennung = DateiKennungArt.NameGroesse,
            };
            var gleichzeitig = 0;
            var hoechstens = 0;
            var gemeldet = new ConcurrentQueue<string>();
            var treiber = new DateiTreiber(Typ, konfig, async (n, _) =>
                {
                    hoechstens = Math.Max(hoechstens, Interlocked.Increment(ref gleichzeitig));
                    await Task.Delay(50);   // der Log-Append dauert
                    gemeldet.Enqueue(((DateiErkannt)n).Name);
                    Interlocked.Decrement(ref gleichzeitig);
                },
                (ids, _) => Task.FromResult<IReadOnlySet<Guid>>(new HashSet<Guid>()), new QuellenZustand());

            using var cts = new CancellationTokenSource();
            var lauf = treiber.LaufeAsync(cts.Token);
            await Warte(() => gemeldet.Count >= 3);
            cts.Cancel();
            await lauf;

            hoechstens.Should().Be(1, "gemeldet wird nacheinander");
            gemeldet.Should().Equal("a.tiff", "b.tiff", "c.tiff");
        }
        finally { Directory.Delete(dir, recursive: true); }
    }

    private static async Task Warte(Func<bool> bedingung, int sekunden = 5)
    {
        var bis = DateTime.UtcNow.AddSeconds(sekunden);
        while (!bedingung())
        {
            if (DateTime.UtcNow > bis) throw new TimeoutException("Bedingung nicht erreicht");
            await Task.Delay(20);
        }
    }

    // ── Proto: geerbte required-init-Felder überleben den Draht ─────────────────────────────────────────────────────

    [Fact]
    public void Quellen_Typ_mit_geerbten_Feldern_round_trippt_ueber_Proto_Bytes()
    {
        var datei = new DateiErkannt
        {
            Name = "a.tiff", Pfad = "/in/sub/a.tiff", RelativerPfad = "sub/a.tiff", Groesse = 4711,
            GeaendertAm = T0, ErkanntAm = T0.AddSeconds(2), Kennung = "sub/a.tiff|4711|x",
        };
        var mapper = new ProtoMessageMapper(Domain.Leitung.GeneratedNutzlast.Instanz);
        var dto = mapper.MapToDto(new EventEnvelope { AggregateId = Guid.NewGuid(), Payload = datei, AggregateType = "Quelle:DateiErkannt" });
        var wire = Cqrs.Leitung.EventEnvelopeDto.Parser.ParseFrom(dto.ToByteArray());
        mapper.MapToDomain(wire).Payload.Should().Be(datei);
    }
}
