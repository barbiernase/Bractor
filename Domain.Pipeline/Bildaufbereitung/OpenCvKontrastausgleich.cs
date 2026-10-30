using OpenCvSharp;

namespace Domain.Pipeline.Bildaufbereitung;

/// <summary>
/// OpenCV-Umsetzung von <see cref="IKontrastausgleich"/> (Laufort Host): das verkleinerte Bild im Histogramm ausgleichen und als Vorschau (PNG)
/// ablegen. Der Zielname folgt nur aus der Quelle — eine Wiederholung überschreibt dieselbe Datei. Zustandslos.
/// </summary>
public sealed class OpenCvKontrastausgleich(IHistogramEqualizer equalizer, BildaufbereitungKonfig konfig) : IKontrastausgleich
{
    public async Task<VorschauErstellt> Auf(BildVerkleinert e)
    {
        using var klein = await Task.Run(() => Cv2.ImRead(e.Verkleinert, ImreadModes.Color));
        if (klein.Empty()) throw new IOException($"{e.Verkleinert} ist nicht mehr lesbar");
        using var ausgeglichen = equalizer.Equalize(klein);
        var ziel = Zielpfad.In(konfig, e.Pfad, "vorschau");
        await Task.Run(() => Cv2.ImWrite(ziel, ausgeglichen));
        return new VorschauErstellt(e.Dateiname, e.Pfad, e.Groesse, e.ErkanntAm, ziel, ausgeglichen.Width, ausgeglichen.Height, DateTimeOffset.UtcNow);
    }
}
