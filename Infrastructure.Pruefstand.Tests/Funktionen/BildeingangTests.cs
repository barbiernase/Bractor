using System;
using System.Threading.Tasks;
using Abstractions;
using Cqrs.Testing;
using Domain.ImagePair;
using Bildvorschau.Katalog.Aufbereitung;
using FluentAssertions;
using Xunit;

namespace Infrastructure.Pruefstand.Funktionen;

/// <summary>
/// Ebene 1 — der Bildeingang des Paars, getrennt in Katalog und Domäne: die gleichbleibende Katalogfunktion <see cref="Dateinamendeutung"/> deutet eine
/// aufbereitete Datei (die Vorschau trägt die Datei der Quelle mit) nach der Aufnahme-Konvention; der Ablauf übersetzt <see cref="BildGedeutet"/> Feld
/// für Feld in <see cref="NimmBildAuf"/> (nur die Id ausdrücklich), und das ImagePair entscheidet „anlegen oder ergänzen“. Bewiesen: erste und zweite
/// Datei eines Paars kommen an, ohne dass eine Ablehnung den Weg trägt; dieselbe Version zweimal ist nur ein Hinweis; ein unbekannter Name ergibt
/// keinen Command, ebenso eine Version außerhalb der Konvention.
/// </summary>
public class BildeingangTests
{
    private static readonly IAggregateHandlerFactory Fabrik = new Domain.AggregateHandlerFactory();
    private const string Dc0 = "2025_06_16_17_46_20_293_DC0.tiff", Dc2 = "2025_06_16_17_46_20_293_DC2.tiff";
    private static readonly Guid Paar = Aufnahmename.ToDeterministicGuid("2025_06_16_17_46_20_293");
    private static readonly DateTimeOffset Jetzt = new(2026, 10, 26, 12, 0, 0, TimeSpan.Zero);

    private static VorschauErstellt Aufbereitet(string name) =>
        new(name, "/in/" + name, 4711, Jetzt, "/vorschau/" + name + ".png", 683, 512, Jetzt.AddSeconds(1));

    private static async Task<object> Deute(string name) => (await new Dateinamendeutung().Auf(Aufbereitet(name))).Value;
    private static async Task<BildGedeutet> Gedeutet(string name) => (BildGedeutet)await Deute(name);

    // Was die Übersetzung der Domäne rechnet (Domain.Uebersetzungen: BildGedeutet_NimmBildAuf_1, AggregateId ← Paar, der Rest Feld für Feld).
    private static async Task<NimmBildAuf> Aufnahme(string name) => Aufnahme(await Gedeutet(name));
    private static NimmBildAuf Aufnahme(BildGedeutet g) => new(new(g.Paar), g.PairKey, g.ProduziertAm, g.AufgenommenAm, g.UrsprungsPfad, g.Version,
        g.OriginalDateiname, g.DateigroesseBytes, g.BreitePixel, g.HoehePixel, g.ErstelltAm, g.Pfad);

    [Fact]
    public async Task Eine_aufbereitete_Datei_wird_nach_der_Konvention_gedeutet()
    {
        (await Gedeutet(Dc0)).Should().BeEquivalentTo(new BildGedeutet(Paar, "2025_06_16_17_46_20_293",
            new DateTimeOffset(2025, 6, 16, 17, 46, 20, 293, TimeSpan.Zero), Jetzt, "/in/" + Dc0, "DC0",
            Dc0, 4711, 683, 512, Jetzt.AddSeconds(1), "/vorschau/" + Dc0 + ".png"));
        (await Gedeutet(Dc2)).Paar.Should().Be(Paar, "beide Dateien treffen dasselbe Paar (Guid aus dem PairKey)");
    }

    [Fact]
    public async Task Ein_Name_ausserhalb_der_Konvention_ist_nur_ein_Hinweis()
    {
        (await Deute("urlaub.jpg")).Should().BeOfType<DateinameUnbekannt>().Which.Dateiname.Should().Be("urlaub.jpg");
    }

    [Fact]
    public async Task Das_erste_Bild_legt_das_Paar_an_das_zweite_ergaenzt_es_und_macht_es_komplett()
    {
        var dc0 = await Aufnahme(Dc0);
        var dc2 = await Aufnahme(Dc2);
        Szenario.Für<ImagePair>(Fabrik, Paar)
            .Wenn(dc0)
            .Dann<ImagePairErstellt>()
            .Dann<BildVerfuegbar>()
            .DannKeineAblehnung()
            .UndZustand(s => !s.IstKomplett, "ein Bild — noch nicht komplett");

        Szenario.Für<ImagePair>(Fabrik, Paar)
            .Vorab(dc0)
            .Wenn(dc2)
            .Dann<BildVerfuegbar>()
            .Dann<ImagePairKomplett>()
            .DannKeineAblehnung()
            .UndZustand(s => s.IstKomplett, "beide Versionen da");
    }

    [Fact]
    public async Task Dieselbe_Version_zweimal_ist_nur_ein_Hinweis_und_aendert_nichts()
    {
        var dc0 = await Aufnahme(Dc0);
        Szenario.Für<ImagePair>(Fabrik, Paar)
            .Vorab(dc0)
            .Wenn(dc0)
            .DannAbgelehnt<BildVersionBereitsVerfuegbar>()
            .UndZustand(s => !s.IstKomplett);
    }

    [Fact]
    public async Task Eine_Version_ausserhalb_der_Konvention_ist_ein_unbekannter_Name()
    {
        (await Deute("2025_06_16_17_46_20_293_DC7.tiff")).Should().BeOfType<DateinameUnbekannt>();
    }
}
