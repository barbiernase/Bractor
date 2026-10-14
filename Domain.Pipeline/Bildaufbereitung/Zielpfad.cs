namespace Domain.Pipeline.Bildaufbereitung;

/// <summary>Wohin eine Bildfunktion ihr Ergebnis schreibt.</summary>
internal static class Zielpfad
{
    /// <summary><c>{AusgabePfad}/{Quelle ohne Endung}_{zusatz}.png</c> — das Verzeichnis wird bei Bedarf angelegt.</summary>
    public static string In(BildaufbereitungKonfig konfig, string quellPfad, string zusatz)
    {
        Directory.CreateDirectory(konfig.AusgabePfad);
        return Path.Combine(konfig.AusgabePfad, $"{Path.GetFileNameWithoutExtension(quellPfad)}_{zusatz}.png");
    }
}
