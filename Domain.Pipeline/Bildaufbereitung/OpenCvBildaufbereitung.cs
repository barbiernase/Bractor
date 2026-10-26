using Abstractions;
using Domain.ImagePair;
using OpenCvSharp;

namespace Domain.Pipeline.Bildaufbereitung;

/// <summary>
/// OpenCV-Implementierung von <see cref="IBildaufbereitung"/> (Laufort Host): verkleinern auf <see cref="VorschauHoehe"/>, Histogramm ausgleichen,
/// als PNG ablegen. Dienste und Konfig-Record kommen aus der generierten Dienst-Bindung. Der Zielname folgt nur aus der Quelle — eine Wiederholung
/// überschreibt dieselbe Datei, statt eine zweite anzulegen. Mehrere Slots ⇒ zustandslos.
/// </summary>
public sealed class OpenCvBildaufbereitung(IImageResizer resizer, IHistogramEqualizer equalizer, BildaufbereitungKonfig konfig) : IBildaufbereitung
{
    /// <summary>Die Höhe der Vorschau in Pixeln.</summary>
    public const int VorschauHoehe = 512;

    public async Task<OneOf<BildAufbereitet, BildNichtLesbar>> Auf(DateiErkannt e)
    {
        using var original = await Task.Run(() => Cv2.ImRead(e.Pfad, ImreadModes.Color));
        if (original.Empty()) return new BildNichtLesbar(e.Pfad, "Datei nicht lesbar");

        using var klein = resizer.Resize(original, VorschauHoehe);
        using var ausgeglichen = equalizer.Equalize(klein);
        var ziel = Zielpfad.In(konfig, e.Pfad, "vorschau");
        await Task.Run(() => Cv2.ImWrite(ziel, ausgeglichen));
        return new BildAufbereitet(e.Name, e.Pfad, e.Groesse, e.ErkanntAm, ziel, ausgeglichen.Width, ausgeglichen.Height, DateTimeOffset.UtcNow);
    }
}
