using Abstractions;

namespace Domain.Pipeline.ImagePair;

/// <summary>Den Dateinamen nach der Aufnahme-Konvention deuten ({Zeitstempel}_{Version}.tiff → Paar + Version).</summary>
public sealed record DeuteDateiname(string Dateiname, string Pfad) : IAuftrag<IDateinameDeutung>;
