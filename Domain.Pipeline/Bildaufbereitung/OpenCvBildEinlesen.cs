using Abstractions;
using Domain.ImagePair;
using OpenCvSharp;

namespace Domain.Pipeline.Bildaufbereitung;

/// <summary>OpenCV-Umsetzung von <see cref="IBildEinlesen"/> (Laufort Host): die Datei als Farbbild lesen und ihre Maße nennen. Zustandslos.</summary>
public sealed class OpenCvBildEinlesen : IBildEinlesen
{
    public async Task<OneOf<BildEingelesen, BildNichtLesbar>> Auf(DateiErkannt e)
    {
        using var bild = await Task.Run(() => Cv2.ImRead(e.Pfad, ImreadModes.Color));
        if (bild.Empty()) return new BildNichtLesbar(e.Pfad, "Datei nicht lesbar");
        return new BildEingelesen(e.Name, e.Pfad, e.Groesse, e.ErkanntAm, bild.Width, bild.Height);
    }
}
