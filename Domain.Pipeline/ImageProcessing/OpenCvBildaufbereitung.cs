using Abstractions;
using Domain.Bildaufbereitung;
using OpenCvSharp;

namespace Domain.Pipeline.ImageProcessing;

// ═══════════════════════════════════════════════════
// OpenCV-Implementierungen der Katalog-Funktionen (Domain.Bildaufbereitung). Gebunden im Host:
//   services.AddFunktion<IBildVerkleinerung, OpenCvBildVerkleinerung>(slots: 2);
//   services.AddFunktion<IHistogrammAusgleich, OpenCvHistogrammAusgleich>(slots: 2);
// Jede schreibt eine Datei in PreprocessingConfig.OutputPath. Der Zielname folgt nur aus der Quelle — eine Wiederholung
// derselben Ausführung überschreibt dieselbe Datei, statt eine zweite anzulegen. Mehrere Slots ⇒ zustandslos.
// ═══════════════════════════════════════════════════

public sealed class OpenCvBildVerkleinerung(IImageResizer resizer, PreprocessingConfig config) : IBildVerkleinerung
{
    public async Task<OneOf<BildVerkleinert, BildNichtLesbar>> RufeAsync(VerkleinereBild auftrag, IAusfuehrung x)
    {
        using var original = await Task.Run(() => Cv2.ImRead(auftrag.QuellPfad, ImreadModes.Color), x.Abbruch);
        if (original.Empty()) return new BildNichtLesbar(auftrag.QuellPfad, "Datei nicht lesbar");

        using var klein = resizer.Resize(original, auftrag.Hoehe);
        var ziel = Zielpfad.In(config, auftrag.QuellPfad, "klein");
        await Task.Run(() => Cv2.ImWrite(ziel, klein), x.Abbruch);
        return new BildVerkleinert(ziel, klein.Width, klein.Height);
    }
}

public sealed class OpenCvHistogrammAusgleich(IHistogramEqualizer equalizer, PreprocessingConfig config) : IHistogrammAusgleich
{
    public async Task<OneOf<HistogrammAusgeglichen, BildNichtLesbar>> RufeAsync(GleicheHistogrammAus auftrag, IAusfuehrung x)
    {
        using var quelle = await Task.Run(() => Cv2.ImRead(auftrag.QuellPfad, ImreadModes.Color), x.Abbruch);
        if (quelle.Empty()) return new BildNichtLesbar(auftrag.QuellPfad, "Datei nicht lesbar");

        using var ausgeglichen = equalizer.Equalize(quelle);
        var ziel = Zielpfad.In(config, auftrag.QuellPfad, "ausgeglichen");
        await Task.Run(() => Cv2.ImWrite(ziel, ausgeglichen), x.Abbruch);
        return new HistogrammAusgeglichen(ziel, ausgeglichen.Width, ausgeglichen.Height, DateTimeOffset.UtcNow);
    }
}

/// <summary>Wohin die Bildfunktionen ihre Ergebnisse schreiben (Verzeichnis).</summary>
public record PreprocessingConfig(string OutputPath);

internal static class Zielpfad
{
    /// <summary><c>{OutputPath}/{Quelle ohne Endung}_{zusatz}.png</c> — das Verzeichnis wird bei Bedarf angelegt.</summary>
    public static string In(PreprocessingConfig config, string quellPfad, string zusatz)
    {
        Directory.CreateDirectory(config.OutputPath);
        return Path.Combine(config.OutputPath, $"{Path.GetFileNameWithoutExtension(quellPfad)}_{zusatz}.png");
    }
}
