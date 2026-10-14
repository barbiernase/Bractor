using Abstractions;
using OpenCvSharp;

namespace Domain.Pipeline.Bildaufbereitung;

/// <summary>
/// OpenCV-Implementierung von <see cref="IHistogrammAusgleich"/> (Laufort Host, wie <see cref="OpenCvBildVerkleinerung"/>).
/// </summary>
public sealed class OpenCvHistogrammAusgleich(IHistogramEqualizer equalizer, BildaufbereitungKonfig konfig) : IHistogrammAusgleich
{
    public async Task<OneOf<HistogrammAusgeglichen, BildNichtLesbar>> RufeAsync(GleicheHistogrammAus auftrag, IAusfuehrung x)
    {
        using var quelle = await Task.Run(() => Cv2.ImRead(auftrag.QuellPfad, ImreadModes.Color), x.Abbruch);
        if (quelle.Empty()) return new BildNichtLesbar(auftrag.QuellPfad, "Datei nicht lesbar");

        using var ausgeglichen = equalizer.Equalize(quelle);
        var ziel = Zielpfad.In(konfig, auftrag.QuellPfad, "ausgeglichen");
        await Task.Run(() => Cv2.ImWrite(ziel, ausgeglichen), x.Abbruch);
        return new HistogrammAusgeglichen(ziel, ausgeglichen.Width, ausgeglichen.Height, DateTimeOffset.UtcNow);
    }
}
