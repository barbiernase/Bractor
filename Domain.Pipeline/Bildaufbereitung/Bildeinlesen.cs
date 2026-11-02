using Domain.Bildaufbereitung;
using Abstractions;
using Domain.ImagePair;
using OpenCvSharp;

namespace Domain.Pipeline.Bildaufbereitung;

/// <summary>
/// Schritt 1 der Bildaufbereitung — eine gefundene Datei als Farbbild lesen und ihre Maße nennen; ist sie nicht lesbar, endet der Lauf hier
/// (<see cref="BildNichtLesbar"/>, ein Hinweis an die Akteure, die ihn hören). Zustandslos.
/// </summary>
public sealed class Bildeinlesen : IFunktion
{
    public async Task<OneOf<BildEingelesen, BildNichtLesbar>> Auf(DateiGefunden e)
    {
        using var bild = await Task.Run(() => Cv2.ImRead(e.Pfad, ImreadModes.Color));
        if (bild.Empty()) return new BildNichtLesbar(e.Pfad, "Datei nicht lesbar");
        return new BildEingelesen(e.Name, e.Pfad, e.Groesse, e.ErkanntAm, bild.Width, bild.Height);
    }
}
