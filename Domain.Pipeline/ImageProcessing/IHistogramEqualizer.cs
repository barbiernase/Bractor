using OpenCvSharp;

namespace Domain.Pipeline.ImageProcessing;

/// <summary>
/// Führt einen Histogrammausgleich auf einem Bild durch.
/// Verbessert den Kontrast — besonders bei schlecht belichteten Aufnahmen.
/// Arbeitet auf OpenCV Mat — kein Disk-I/O, reine Speicher-Operation.
/// </summary>
public interface IHistogramEqualizer
{
    /// <summary>
    /// Histogrammausgleich. Bei Farbbildern wird nur der
    /// Helligkeitskanal (Y/V) equalisiert, nicht die Farbkanäle.
    /// </summary>
    Mat Equalize(Mat input);
}