using System;
using Abstractions;
using Cqrs.Testing;
using Domain.ImagePair;
using Xunit;

namespace Infrastructure.Pruefstand.Aggregate;

/// <summary>
/// <see cref="NimmRohbildAuf"/>: der Eingang der Bildaufbereitung ins Log. Je Version wirkt er genau einmal — eine zweite
/// Meldung derselben Datei (Re-Trigger, Webhook + FileWatch) startet keine zweite Aufbereitung.
/// </summary>
public class ImagePairRohbildTests
{
    private static readonly IAggregateHandlerFactory Fabrik = new Infrastructure.AggregateHandlerFactory();
    private static readonly DateTimeOffset T = new(2026, 10, 7, 12, 0, 0, TimeSpan.Zero);

    private static ImagePairErstellt Erstellt(Guid id) => new(id, "P1", T, T, "/in/a_dc0.tiff");

    [Fact]
    public void Erstes_Rohbild_einer_Version_geht_ins_Log()
    {
        var id = Guid.NewGuid();
        Szenario.Für<ImagePair>(Fabrik, id)
            .Gegeben(Erstellt(id))
            .Wenn(new NimmRohbildAuf(id, BildVersion.Dc0, "/in/a_dc0.tiff", "a_dc0.tiff", 10))
            .Dann<RohbildEingegangen>()
            .UndZustand(s => s.Dc0Eingegangen && !s.Dc2Eingegangen, "nur Dc0 ist eingegangen");
    }

    [Fact]
    public void Dasselbe_Rohbild_zweimal_wirkt_nur_einmal()
    {
        var id = Guid.NewGuid();
        Szenario.Für<ImagePair>(Fabrik, id)
            .Gegeben(Erstellt(id), new RohbildEingegangen(id, BildVersion.Dc0, "/in/a_dc0.tiff", "a_dc0.tiff", 10))
            .Wenn(new NimmRohbildAuf(id, BildVersion.Dc0, "/in/a_dc0.tiff", "a_dc0.tiff", 10))
            .DannAbgelehnt<RohbildBereitsEingegangen>();
    }

    [Fact]
    public void Ohne_Paar_wird_abgelehnt()
    {
        var id = Guid.NewGuid();
        Szenario.Für<ImagePair>(Fabrik, id)
            .Wenn(new NimmRohbildAuf(id, BildVersion.Dc2, "/in/a_dc2.tiff", "a_dc2.tiff", 10))
            .DannAbgelehnt<ImagePairNichtGefunden>();
    }
}
