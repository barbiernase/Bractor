using Abstractions;
using Domain.ImagePair;

namespace Domain.Pipeline.ImageProcessing;

/// <summary>
/// Implementierung der Katalog-Funktion <see cref="IDateinameDeutung"/>: die Aufnahme-Konvention ({Zeitstempel}_{Version}.ext) über
/// <see cref="ImagePairFileName.Resolve"/>. Rein und zustandslos — viele Slots gehen. Gebunden im Host (AddFunktion).
/// </summary>
public sealed class ImagePairDateinameDeutung : IDateinameDeutung
{
    public Task<OneOf<ImagePairDateiGedeutet, DateinameUnbekannt>> RufeAsync(DeuteDateiname auftrag, IAusfuehrung x)
    {
        var r = ImagePairFileName.Resolve(auftrag.Dateiname);
        return Task.FromResult<OneOf<ImagePairDateiGedeutet, DateinameUnbekannt>>(r is null
            ? new DateinameUnbekannt(auftrag.Dateiname)
            : new ImagePairDateiGedeutet(r.AggregateId, r.PairKey, r.Version, r.ProduziertAm, auftrag.Pfad));
    }
}
