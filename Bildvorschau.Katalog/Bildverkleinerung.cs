using Abstractions;
using OpenCvSharp;

namespace Bildvorschau.Katalog;

/// <summary>
/// Schritt 2 der Bildaufbereitung — das eingelesene Bild auf <see cref="VorschauHoehe"/> verkleinern und als PNG ablegen (OpenCV über die Technik
/// <see cref="IImageResizer"/>). Der Zielname folgt nur aus der Quelle — eine Wiederholung überschreibt dieselbe Datei. Zustandslos.
/// </summary>
public sealed class Bildverkleinerung(IImageResizer resizer, BildaufbereitungKonfig konfig) : IFunktion
{
    /// <summary>Die Höhe der Vorschau in Pixeln.</summary>
    public const int VorschauHoehe = 512;

    public async Task<BildVerkleinert> Auf(BildEingelesen e)
    {
        using var original = await Task.Run(() => Cv2.ImRead(e.Pfad, ImreadModes.Color));
        if (original.Empty()) throw new IOException($"{e.Pfad} ist nicht mehr lesbar");
        using var klein = resizer.Resize(original, VorschauHoehe);
        var ziel = Zielpfad.In(konfig, e.Pfad, "klein");
        await Task.Run(() => Cv2.ImWrite(ziel, klein));
        return new BildVerkleinert(e.Dateiname, e.Pfad, e.Groesse, e.ErkanntAm, ziel, klein.Width, klein.Height);
    }
}
