using OpenCvSharp;

namespace Domain.Pipeline.ImageProcessing;

/// <summary>
/// Skaliert ein Bild auf eine Zielhöhe (Breite proportional).
/// Arbeitet auf OpenCV Mat — kein Disk-I/O, reine Speicher-Operation.
/// </summary>
public interface IImageResizer
{
    /// <summary>
    /// Skaliert das Bild so, dass die Höhe targetHeight Pixel beträgt.
    /// Breite wird proportional angepasst.
    /// </summary>
    Mat Resize(Mat input, int targetHeight);
}