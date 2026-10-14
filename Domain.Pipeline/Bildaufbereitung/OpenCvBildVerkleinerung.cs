using Abstractions;
using OpenCvSharp;

namespace Domain.Pipeline.Bildaufbereitung;

/// <summary>
/// OpenCV-Implementierung von <see cref="IBildVerkleinerung"/>. Gebunden nach der Umgebung (Funktionen:IBildVerkleinerung:Laufort = Host);
/// Dienst und Konfig-Record kommen aus der generierten Dienst-Bindung (GeneratedDienste). Der Zielname folgt nur aus der Quelle — eine
/// Wiederholung derselben Ausführung überschreibt dieselbe Datei, statt eine zweite anzulegen. Mehrere Slots ⇒ zustandslos.
/// </summary>
public sealed class OpenCvBildVerkleinerung(IImageResizer resizer, BildaufbereitungKonfig konfig) : IBildVerkleinerung
{
    public async Task<OneOf<BildVerkleinert, BildNichtLesbar>> RufeAsync(VerkleinereBild auftrag, IAusfuehrung x)
    {
        using var original = await Task.Run(() => Cv2.ImRead(auftrag.QuellPfad, ImreadModes.Color), x.Abbruch);
        if (original.Empty()) return new BildNichtLesbar(auftrag.QuellPfad, "Datei nicht lesbar");

        using var klein = resizer.Resize(original, auftrag.Hoehe);
        var ziel = Zielpfad.In(konfig, auftrag.QuellPfad, "klein");
        await Task.Run(() => Cv2.ImWrite(ziel, klein), x.Abbruch);
        return new BildVerkleinert(ziel, klein.Width, klein.Height);
    }
}
