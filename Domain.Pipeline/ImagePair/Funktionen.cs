using Abstractions;

namespace Domain.Pipeline.ImagePair;

/// <summary>Den Dateinamen nach der Aufnahme-Konvention deuten ({Zeitstempel}_{Version}.tiff → Paar + Version).</summary>
public interface IDateinameDeutung : IFunktion
{
    Task<OneOf<ImagePairDateiGedeutet, DateinameUnbekannt>> RufeAsync(DeuteDateiname auftrag, IAusfuehrung x);
}
