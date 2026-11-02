using Abstractions;
using Domain.ImagePair;
using Domain.Pipeline.Bildaufbereitung;

namespace Domain.Pipeline.ImagePair;

/// <summary>
/// Der BILDEINGANG des Kamerasystems — der JOIN der Bildaufbereitung: das eingelesene Bild (seine Datei) und seine Vorschau aus dem anderen Zweig
/// kommen im selben Lauf zusammen; die Datei wird nach der Aufnahme-Konvention gedeutet ({Zeitstempel}_{Version}.tiff → Paar + Version) und dem Paar
/// übergeben — „anlegen oder ergänzen“ entscheidet das Aggregat (<see cref="NimmBildAuf"/>).
/// Gleichbleibend (R12): rein aus dem Eingang, ohne gerufene Funktion und Uhr — die Funktion hält nichts fest, die Inbox des Paars verwirft Wiederholungen.
/// Ein Name außerhalb der Konvention ist nur ein Hinweis.
/// </summary>
public sealed class Bildeingang : IFunktion
{
    public OneOf<NimmBildAuf, DateinameUnbekannt> Auf(BildEingelesen bild, VorschauErstellt vorschau)
    {
        if (ImagePairFileName.Resolve(bild.Dateiname) is not { } paar) return new DateinameUnbekannt(bild.Dateiname);
        return new NimmBildAuf(paar.AggregateId, paar.PairKey, paar.ProduziertAm, bild.ErkanntAm, bild.Pfad, paar.Version,
            new BildMeta(bild.Dateiname, bild.Groesse, vorschau.BreitePixel, vorschau.HoehePixel, vorschau.ErstelltAm), vorschau.Vorschau);
    }
}
