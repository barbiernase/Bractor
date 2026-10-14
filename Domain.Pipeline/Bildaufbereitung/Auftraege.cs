using Abstractions;

namespace Domain.Pipeline.Bildaufbereitung;

/// <summary>Bild auf eine Zielhöhe verkleinern (Seitenverhältnis bleibt).</summary>
public sealed record VerkleinereBild(string QuellPfad, int Hoehe) : IAuftrag<IBildVerkleinerung>;

/// <summary>Histogramm-Ausgleich (Kontrast) und als Vorschau-PNG ablegen.</summary>
public sealed record GleicheHistogrammAus(string QuellPfad) : IAuftrag<IHistogrammAusgleich>;
