using OpenCvSharp;

namespace Domain.Pipeline.ImageProcessing;

/// <summary>
/// Skaliert Bilder auf eine Zielhöhe mit proportionaler Breite.
/// Nutzt INTER_AREA für Downscaling — schärfste Ergebnisse bei Verkleinerung.
/// </summary>
internal class ImageResizer : IImageResizer
{
    public Mat Resize(Mat input, int targetHeight)
    {
        if (input.Empty())
            throw new ArgumentException("Input Mat is empty");

        if (input.Height == targetHeight)
            return input.Clone();

        var scale = (double)targetHeight / input.Height;
        var targetWidth = (int)(input.Width * scale);

        var output = new Mat();
        Cv2.Resize(input, output,
            new Size(targetWidth, targetHeight),
            interpolation: scale < 1.0
                ? InterpolationFlags.Area     // Downscale: Area-basiert
                : InterpolationFlags.Cubic);  // Upscale: bikubisch

        return output;
    }
}