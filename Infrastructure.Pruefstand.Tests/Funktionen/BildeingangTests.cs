using System;
using Abstractions;
using Cqrs.Testing;
using Domain.ImagePair;
using Domain.Pipeline.Bildaufbereitung;
using Domain.Pipeline.ImagePair;
using FluentAssertions;
using Xunit;

namespace Infrastructure.Pruefstand.Funktionen;

/// <summary>
/// Ebene 1 — der ECHTE Bildeingang der Domäne: die gleichbleibende Funktion <see cref="Bildeingang"/>
/// deutet eine aufbereitete Datei nach der Aufnahme-Konvention und übergibt sie dem Paar; das ImagePair entscheidet „anlegen oder ergänzen“ in einem
/// Command (<see cref="NimmBildAuf"/>). Bewiesen: erste und zweite Datei eines Paars kommen an, ohne dass eine Ablehnung den Weg trägt; dieselbe
/// Version zweimal ist nur ein Hinweis; ein unbekannter Name ergibt keinen Command.
/// </summary>
public class BildeingangTests
{
    private static readonly IAggregateHandlerFactory Fabrik = new Domain.AggregateHandlerFactory();
    private const string Dc0 = "2025_06_16_17_46_20_293_DC0.tiff", Dc2 = "2025_06_16_17_46_20_293_DC2.tiff";
    private static readonly Guid Paar = ImagePairFileName.ToDeterministicGuid("2025_06_16_17_46_20_293");
    private static readonly DateTimeOffset Jetzt = new(2026, 10, 26, 12, 0, 0, TimeSpan.Zero);

    private static BildAufbereitet Aufbereitet(string name) =>
        new(name, "/in/" + name, 4711, Jetzt, "/vorschau/" + name + ".png", 683, 512, Jetzt.AddSeconds(1));

    private static NimmBildAuf Aufnahme(string name) => (NimmBildAuf)new Bildeingang().Auf(Aufbereitet(name)).Value;

    [Fact]
    public void Eine_aufbereitete_Datei_wird_gedeutet_und_dem_Paar_mit_Vorschau_und_Metadaten_uebergeben()
    {
        Aufnahme(Dc0).Should().BeEquivalentTo(new NimmBildAuf(new(Paar), "2025_06_16_17_46_20_293",
            new DateTimeOffset(2025, 6, 16, 17, 46, 20, 293, TimeSpan.Zero), Jetzt, "/in/" + Dc0, BildVersion.Dc0,
            new BildMeta(Dc0, 4711, 683, 512, Jetzt.AddSeconds(1)), "/vorschau/" + Dc0 + ".png"),
            o => o.Excluding(x => x.ProduziertAm));
        Aufnahme(Dc2).AggregateId.Wert.Should().Be(Paar, "beide Dateien treffen dasselbe Paar (Id aus dem PairKey)");
    }

    [Fact]
    public void Ein_Name_ausserhalb_der_Konvention_ist_nur_ein_Hinweis()
    {
        new Bildeingang().Auf(Aufbereitet("urlaub.jpg")).Value.Should().BeOfType<DateinameUnbekannt>().Which.Dateiname.Should().Be("urlaub.jpg");
    }

    [Fact]
    public void Das_erste_Bild_legt_das_Paar_an_das_zweite_ergaenzt_es_und_macht_es_komplett()
    {
        Szenario.Für<ImagePair>(Fabrik, Paar)
            .Wenn(Aufnahme(Dc0))
            .Dann<ImagePairErstellt>()
            .Dann<BildVerfuegbar>()
            .DannKeineAblehnung()
            .UndZustand(s => !s.IstKomplett, "ein Bild — noch nicht komplett");

        Szenario.Für<ImagePair>(Fabrik, Paar)
            .Vorab(Aufnahme(Dc0))
            .Wenn(Aufnahme(Dc2))
            .Dann<BildVerfuegbar>()
            .Dann<ImagePairKomplett>()
            .DannKeineAblehnung()
            .UndZustand(s => s.IstKomplett, "beide Versionen da");
    }

    [Fact]
    public void Dieselbe_Version_zweimal_ist_nur_ein_Hinweis_und_aendert_nichts()
    {
        Szenario.Für<ImagePair>(Fabrik, Paar)
            .Vorab(Aufnahme(Dc0))
            .Wenn(Aufnahme(Dc0))
            .DannAbgelehnt<BildVersionBereitsVerfuegbar>()
            .UndZustand(s => !s.IstKomplett);
    }
}
