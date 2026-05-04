using OpenCvSharp;

namespace Domain.Pipeline.ImageProcessing;

/// <summary>
/// Histogrammausgleich via OpenCV.
///
/// Bei Farbbildern: Konvertierung nach YCrCb, Equalisierung nur
/// auf dem Y-Kanal (Helligkeit), dann zurück nach BGR.
/// So bleibt die Farbtreue erhalten, nur der Kontrast wird optimiert.
///
/// Bei Graustufenbildern: direkter Histogrammausgleich.
/// </summary>
internal class HistogramEqualizer : IHistogramEqualizer
{
    public Mat Equalize(Mat input)
    {
        if (input.Empty())
            throw new ArgumentException("Input Mat is empty");

        // Graustufen: direkt equalisieren
        if (input.Channels() == 1)
        {
            var output = new Mat();
            Cv2.EqualizeHist(input, output);
            return output;
        }

        // Farbbild: nur Helligkeitskanal equalisieren
        var ycrcb = new Mat();
        Cv2.CvtColor(input, ycrcb, ColorConversionCodes.BGR2YCrCb);

        var channels = ycrcb.Split();
        Cv2.EqualizeHist(channels[0], channels[0]); // Y-Kanal

        var merged = new Mat();
        Cv2.Merge(channels, merged);

        var result = new Mat();
        Cv2.CvtColor(merged, result, ColorConversionCodes.YCrCb2BGR);

        // Zwischenmatrizen freigeben
        ycrcb.Dispose();
        merged.Dispose();
        foreach (var ch in channels)
            ch.Dispose();

        return result;
    }
}