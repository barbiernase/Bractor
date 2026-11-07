using Abstractions;
using Domain.ImagePair;
using Bildvorschau.Katalog.Aufbereitung;

namespace Domain.Pipeline.ImagePair;

/// <summary>
/// Der BILDEINGANG des Kamerasystems — der letzte Schritt der Bildaufbereitung: die Vorschau trägt die Datei der Quelle mit (kein Zusammenführen
/// nötig); die Datei wird nach der Aufnahme-Konvention gedeutet ({Zeitstempel}_{Version}.tiff → Paar + Version) und dem Paar
/// übergeben — „anlegen oder ergänzen“ entscheidet das Aggregat (<see cref="NimmBildAuf"/>).
/// Gleichbleibend (R12): rein aus dem Eingang, ohne gerufene Funktion und Uhr — die Funktion hält nichts fest, die Inbox des Paars verwirft Wiederholungen.
/// Ein Name außerhalb der Konvention ist nur ein Hinweis.
/// </summary>
public sealed class Bildeingang : IFunktion
{
    public OneOf<NimmBildAuf, DateinameUnbekannt> Auf(VorschauErstellt vorschau)
    {
        if (ImagePairFileName.Resolve(vorschau.Dateiname) is not { } paar) return new DateinameUnbekannt(vorschau.Dateiname);
        return new NimmBildAuf(paar.AggregateId, paar.PairKey, paar.ProduziertAm, vorschau.ErkanntAm, vorschau.Pfad, paar.Version,
            new BildMeta(vorschau.Dateiname, vorschau.Groesse, vorschau.BreitePixel, vorschau.HoehePixel, vorschau.ErstelltAm), vorschau.Vorschau);
    }
}
