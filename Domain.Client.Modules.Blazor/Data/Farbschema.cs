using Domain.ImagePair;
namespace Domain.Client.Modules;

/// <summary>
/// Zentrale Farbzuordnung für Klassifikationen — eine Quelle der Wahrheit für
/// Liste, Footer & Co., damit "rot = Anomalie" überall dasselbe bedeutet.
/// Farben identisch zum LabelingFooter (der bei Gelegenheit auch hierher zeigen kann).
/// </summary>
public static class Farbschema
{
    public const string KeinLabel = "#555";

    public static string Fuer(Klassifikation? k) => k switch
    {
        Klassifikation.KeineAnomalie => "#4ade80",   // grün  = OK
        Klassifikation.Questionable  => "#f0c040",   // amber = fraglich
        Klassifikation.Anomalie      => "#f87171",   // rot   = Anomalie
        _                            => KeinLabel    // grau  = (noch) kein Label
    };

    public static string Text(Klassifikation? k) => k switch
    {
        Klassifikation.KeineAnomalie => "OK",
        Klassifikation.Questionable  => "Fraglich",
        Klassifikation.Anomalie      => "Anomalie",
        _                            => "—"
    };
}
