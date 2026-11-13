using Abstractions;

namespace Bildvorschau.Katalog;

/// <summary>
/// Deutet die aufbereitete Datei nach der Aufnahme-Konvention (<see cref="Aufnahmename"/>): die Vorschau trägt die Datei der Quelle mit, so steht hier
/// alles beisammen — das Paar (Guid aus dem Schlüssel), Version, Aufnahmezeit, die Metadaten und der Pfad der Vorschau. Ein Name außerhalb der
/// Konvention ist nur ein Hinweis. Gleichbleibend: rein aus dem Eingang, ohne Technik und Uhr.
/// </summary>
public sealed class Dateinamendeutung : IFunktion
{
    public OneOf<BildGedeutet, DateinameUnbekannt> Auf(VorschauErstellt vorschau)
    {
        if (Aufnahmename.Resolve(vorschau.Dateiname) is not { } paar) return new DateinameUnbekannt(vorschau.Dateiname);
        return new BildGedeutet(paar.Paar, paar.PairKey, paar.ProduziertAm, vorschau.ErkanntAm, vorschau.Pfad, paar.Version,
            vorschau.Dateiname, vorschau.Groesse, vorschau.BreitePixel, vorschau.HoehePixel, vorschau.ErstelltAm, vorschau.Vorschau);
    }
}
