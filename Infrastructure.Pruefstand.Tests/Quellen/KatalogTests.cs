using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Abstractions;
using Core.Katalog;
using FluentAssertions;
using Infrastructure.Funktionen;
using Xunit;

namespace Infrastructure.Pruefstand.Quellen;

/// <summary>
/// Die Quellarten des Frameworks (docs/04 §4.7, <c>Core/Katalog</c>, <c>IQuellart</c>): sie lauschen und melden je Element eine Meldung — der Kern des
/// Dateiwächters (Kennung, Fertig-Erkennung, Gedächtnis im Zustand), der Dateiwächter gegen ein echtes Verzeichnis (sein Zustand trägt über einen
/// Neustart), der Taktgeber und der Webhook (eine Annahme: er lauscht nicht). Store-frei: das Sichern des Zustands nach dem Hineingeben beweisen <c>QuellLaeuferTests</c> und die
/// Integrations-Tests.
/// </summary>
public class KatalogTests
{
    private static readonly DateTimeOffset T0 = new(2026, 10, 12, 8, 0, 0, TimeSpan.Zero);

    private static DateiStand Stand(string rel, long groesse = 10, DateTimeOffset? geaendert = null, bool marker = false)
        => new("/in/" + rel, rel, Path.GetFileName(rel), groesse, geaendert ?? T0, marker);

    private static Dateiwaechterkonfig Konfig(DateiFertigArt art = DateiFertigArt.Stabil, double fensterSek = 1, DateiKennungArt kennung = DateiKennungArt.PfadGroesseZeit)
        => new("/in", Fertig: art, Fenster: TimeSpan.FromSeconds(fensterSek), Kennung: kennung);

    private sealed class Zustand<T> : IZustand<T> where T : class, new() { public T Wert { get; set; } = new(); }

    [Fact]
    public void Kennung_NameGroesse_und_PfadGroesseZeit()
    {
        var d = Stand("sub/a.tiff", 4711, T0);
        DateiScan.Bilde(DateiKennungArt.NameGroesse, d).Should().Be("a.tiff|4711");
        DateiScan.Bilde(DateiKennungArt.PfadGroesseZeit, d).Should().Be("sub/a.tiff|4711|" + T0.ToString("O"));
    }

    [Fact]
    public void Stabil_erst_nach_dem_Fenster_und_nur_wenn_unveraendert()
    {
        var scan = new DateiScan(Konfig(fensterSek: 2), new Gesehen());
        scan.Fertige([Stand("a")], T0).Should().BeEmpty("gerade erst gesehen");
        scan.Fertige([Stand("a")], T0.AddSeconds(1)).Should().BeEmpty("das Fenster ist noch nicht um");
        scan.Fertige([Stand("a", 11)], T0.AddSeconds(3)).Should().BeEmpty("die Größe hat sich geändert → Fenster beginnt neu");
        scan.Fertige([Stand("a", 11)], T0.AddSeconds(5)).Select(d => d.Name).Should().Equal("a");
    }

    [Fact]
    public void Marker_meldet_erst_wenn_die_Marker_Datei_da_ist_und_nie_den_Marker_selbst()
    {
        var scan = new DateiScan(Konfig(DateiFertigArt.Marker), new Gesehen());
        scan.Fertige([Stand("a.tiff")], T0).Should().BeEmpty();
        scan.Fertige([Stand("a.tiff", marker: true), Stand("a.tiff.done")], T0).Select(d => d.Name).Should().Equal("a.tiff");
    }

    [Trait("Kategorie", "Garantie")]
    [Fact]
    public void Gemeldet_steht_im_Gedaechtnis_wird_nicht_wiederholt_eine_ersetzte_Datei_aber_schon()
    {
        var gesehen = new Gesehen();
        var scan = new DateiScan(Konfig(), gesehen);
        scan.Fertige([Stand("a")], T0).Should().BeEmpty();
        var a = scan.Fertige([Stand("a")], T0.AddSeconds(1)).Single();
        scan.Gemeldet(a);
        gesehen.Je.Should().ContainKey("a", "das Gedächtnis des Wächters (im Speicher)");
        new DateiScan(Konfig(), gesehen).Fertige([Stand("a")], T0.AddSeconds(9)).Should().BeEmpty("mit demselben Gedächtnis: schon gemeldet");
        var neu = new DateiScan(Konfig(), gesehen);
        neu.Fertige([Stand("a", 10, T0.AddMinutes(1))], T0.AddSeconds(10)).Should().BeEmpty();
        neu.Fertige([Stand("a", 10, T0.AddMinutes(1))], T0.AddSeconds(11)).Should().ContainSingle("neu geschrieben → neue Kennung");
    }

    [Fact]
    public void Verschwundene_Dateien_fallen_aus_dem_Gedaechtnis()
    {
        var gesehen = new Gesehen();
        gesehen.Je["a"] = DateiScan.Bilde(DateiKennungArt.PfadGroesseZeit, Stand("a"));
        var scan = new DateiScan(Konfig(), gesehen);
        scan.Fertige([Stand("b")], T0).Should().BeEmpty();
        gesehen.Je.Should().NotContainKey("a");
    }

    [Fact]
    public async Task Der_Dateiwaechter_meldet_jede_fertige_Datei_einmal_und_nach_einem_Neustart_alles_erneut_mit_derselben_Kennung()
    {
        var verz = Directory.CreateTempSubdirectory("dateiwaechter-").FullName;
        try
        {
            await File.WriteAllTextAsync(Path.Combine(verz, "a.tiff"), "a");
            await File.WriteAllTextAsync(Path.Combine(verz, "b.txt"), "b");
            var konfig = new Dateiwaechterkonfig(verz, ["*.tiff"], Intervall: TimeSpan.FromMilliseconds(100), Fenster: TimeSpan.FromMilliseconds(200));
            var waechter = new Dateiwaechter(konfig);

            async Task<List<DateiGefunden>> Lausche(Dateiwaechter w, int erwartet, TimeSpan geduld)
            {
                using var stopp = new CancellationTokenSource(geduld);
                var aus = new List<DateiGefunden>();
                try
                {
                    await foreach (var d in w.Lausche(stopp.Token))
                    {
                        aus.Add(d);
                        if (aus.Count == erwartet) break;
                    }
                }
                catch (OperationCanceledException) { }
                return aus;
            }

            var erst = await Lausche(waechter, 1, TimeSpan.FromSeconds(10));
            erst.Select(d => d.Name).Should().Equal("a.tiff");
            await File.WriteAllTextAsync(Path.Combine(verz, "c.tiff"), "c");
            (await Lausche(waechter, 1, TimeSpan.FromSeconds(10))).Select(d => d.Name).Should().Equal(["c.tiff"], "derselbe Wächter merkt sich a.tiff — er meldet nur Neues");
            (await Lausche(waechter, 1, TimeSpan.FromSeconds(1))).Should().BeEmpty();
            // Ein Neustart (ein neuer Wächter) meldet alles erneut — mit derselben Kennung aus dem Inhalt: das Backend verwirft, was schon gewirkt hat.
            var neu = await Lausche(new Dateiwaechter(konfig), 2, TimeSpan.FromSeconds(10));
            neu.Select(d => d.Name).Should().Equal("a.tiff", "c.tiff");
            ((IMeldung)neu[0]).Kennung.Should().Be(((IMeldung)erst[0]).Kennung, "die Kennung kommt aus dem Inhalt, nicht aus dem Lauf");
        }
        finally { Directory.Delete(verz, recursive: true); }
    }

    [Fact]
    public void Der_Intervall_Beginn_ist_auf_jedem_Knoten_gleich()
    {
        var zeit = new DateTimeOffset(2026, 10, 26, 14, 37, 12, TimeSpan.FromHours(2));
        var beginn = Taktgeber.IntervallBeginn(zeit, TimeSpan.FromMinutes(15));
        beginn.Should().Be(new DateTimeOffset(2026, 10, 26, 12, 30, 0, TimeSpan.Zero));
        Taktgeber.IntervallBeginn(zeit.AddMinutes(10), TimeSpan.FromMinutes(15)).Should().Be(beginn.AddMinutes(15), "12:47 liegt im nächsten Intervall");
        new Taktkonfig(TimeSpan.FromMilliseconds(200)).WirksamesIntervall.Should().Be(TimeSpan.FromSeconds(1), "mindestens eine Sekunde");
    }

    [Fact]
    public async Task Der_Taktgeber_meldet_ein_Intervall_einmal_und_nach_einem_Neustart_dasselbe_mit_derselben_Kennung()
    {
        using var stopp = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        await using var e = new Taktgeber(new Taktkonfig(TimeSpan.FromHours(1))).Lausche(stopp.Token).GetAsyncEnumerator(stopp.Token);
        (await e.MoveNextAsync()).Should().BeTrue();
        var erster = e.Current;
        using var kurz = new CancellationTokenSource(TimeSpan.FromMilliseconds(500));
        var weiter = e.MoveNextAsync().AsTask();
        (await Task.WhenAny(weiter, Task.Delay(400))).Should().NotBe(weiter, "im selben Intervall meldet er nichts mehr");
        await using var zweiter = new Taktgeber(new Taktkonfig(TimeSpan.FromHours(1))).Lausche(kurz.Token).GetAsyncEnumerator(kurz.Token);
        (await zweiter.MoveNextAsync()).Should().BeTrue("ein Neustart meldet das laufende Intervall erneut");
        ((IMeldung)zweiter.Current).Kennung.Should().Be(((IMeldung)erster).Kennung, "mit derselben Kennung — das Backend verwirft es");
    }

    [Fact]
    public void Der_Webhook_nimmt_an_statt_zu_lauschen_und_seine_Konfig_nennt_die_Werte_des_Annahmevertrags()
    {
        // Eine Annahme: der Host nimmt ihre Aufrufe an (Annahmen, AnnahmeTests) — kein Postfach, kein Lausche.
        typeof(IAnnahme<Aufruf>).IsAssignableFrom(typeof(Webhook)).Should().BeTrue();
        typeof(IQuellart).IsAssignableFrom(typeof(Webhook)).Should().BeTrue("eine Annahme ist eine Quellart");
        new[] { nameof(Webhookkonfig.Pfad), nameof(Webhookkonfig.Schluessel) }.Should().Equal(Annahmevertrag.Pfad, Annahmevertrag.Schluessel);
        new Webhook(new Webhookkonfig("lieferant")).Konfig.Schluessel.Should().BeNull("ohne Schlüssel nimmt er jeden Aufruf an");
    }

    [Fact]
    public void Das_Zustandsbild_packt_den_eigenen_und_die_Technik_Zustaende_ohne_Reflection()
    {
        var bild = Zustandsbild.Packe(new Dictionary<string, byte[]> { [""] = [1, 2], ["IWertTabelle"] = [3] });
        Zustandsbild.Entpacke(bild).Should().BeEquivalentTo(new Dictionary<string, byte[]> { [""] = [1, 2], ["IWertTabelle"] = [3] });
    }
}
