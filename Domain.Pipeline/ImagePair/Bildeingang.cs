using Abstractions;
using Domain.ImagePair;
using Domain.Pipeline.Bildaufbereitung;

namespace Domain.Pipeline.ImagePair;

/// <summary>
/// Der BILDEINGANG des Kamerasystems (Plan Pub/Sub-Neustrukturierung Phase 4): eine aufbereitete Datei wird nach der Aufnahme-Konvention gedeutet
/// ({Zeitstempel}_{Version}.tiff → Paar + Version) und dem Paar übergeben — „anlegen oder ergänzen“ entscheidet das Aggregat (<see cref="NimmBildAuf"/>).
/// Gleichbleibend (R12): rein aus dem Eingang, ohne Dienst und Uhr — die Funktion hält nichts fest, die Inbox des Paars verwirft Wiederholungen.
/// Ein Name außerhalb der Konvention ist nur ein Hinweis.
/// </summary>
public sealed class Bildeingang : IFunktion
{
    public OneOf<NimmBildAuf, DateinameUnbekannt> Auf(BildAufbereitet e)
    {
        if (ImagePairFileName.Resolve(e.Dateiname) is not { } paar) return new DateinameUnbekannt(e.Dateiname);
        return new NimmBildAuf(paar.AggregateId, paar.PairKey, paar.ProduziertAm, e.ErkanntAm, e.Pfad, paar.Version,
            new BildMeta(e.Dateiname, e.Groesse, e.BreitePixel, e.HoehePixel, e.ErstelltAm), e.Vorschau);
    }
}
