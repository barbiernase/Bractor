namespace Abstractions;

/// <summary>
/// Der Weg eines Log-Eintrags über die Karten des Editors (<c>docs/konzept-werkbank.md</c> §7.3): <see cref="Karten"/> sind die Karten, über
/// die der Schritt lief (Absender → Command → Wirker → Event; Wirker-Ids bzw. <c>rec:Typ</c>), <see cref="Paare"/> die gegangenen Kanten —
/// inklusive Ursache → Absender. Rein aus den Spur-Headern (<see cref="Spur"/>); der Editor schlägt die Ids nur nach.
/// </summary>
public sealed record SpurWeg(IReadOnlyList<string> Karten, IReadOnlyList<string[]> Paare)
{
    /// <summary>Was der Weg eines Folge-Eintrags über seine Ursache wissen muss: Typname, Wirker und Absender (<c>ueber</c>) der Ursache.</summary>
    public sealed record Eintrag(string Name, string? Wirker, string? Ueber);

    /// <summary>
    /// Der Weg: Absender (<c>ueber</c>: Funktion, Uhr, Akteur) → beim Aggregat die Command-Karte → Wirker → die eigene Event-Karte. Gegangen ist
    /// dazu: Ursache (deren Event-Karte) → erste Karte. Einträge des Rahmens (<see cref="IRahmenEvent"/>) sind kein Schritt; der Aufrufer lässt sie aus.
    /// </summary>
    public static SpurWeg Aus(string name, string? wirker, string? ueber, string? ursache, Func<string, Eintrag?> eintrag)
    {
        var karten = new List<string>();
        void Karte(string? id)
        {
            if (!string.IsNullOrEmpty(id) && (karten.Count == 0 || karten[^1] != id)) karten.Add(id);
        }
        Karte(ueber);
        if (wirker?.StartsWith("decide:", StringComparison.Ordinal) == true && wirker.IndexOf('/') is var i and > 0) Karte("rec:" + wirker[(i + 1)..]);
        Karte(wirker);
        Karte("rec:" + name);

        var paare = new List<string[]>();
        if ((ursache != null ? eintrag(ursache) : null) is { } u && karten.Count > 0)
            paare.Add(["rec:" + u.Name, karten[0]]);
        for (var k = 1; k < karten.Count; k++) paare.Add([karten[k - 1], karten[k]]);
        return new SpurWeg(karten, paare);
    }
}
