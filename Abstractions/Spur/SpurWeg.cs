namespace Abstractions;

/// <summary>
/// Der Weg eines Log-Eintrags über die Karten des Editors (<c>docs/konzept-werkbank.md</c> §7.3): <see cref="Karten"/> sind die Karten, über
/// die der Schritt lief (Absender → Command → Wirker → Event; Wirker-Ids bzw. <c>rec:Typ</c>), <see cref="Paare"/> die gegangenen Kanten —
/// inklusive Ursache → Absender und Draht Fluss-Knoten → Fluss-Knoten. Rein aus den Spur-Headern (<see cref="Spur"/>) und, für den ersten
/// Draht eines Flusses, dem Quell-Knoten aus dem gelesenen Code; der Editor schlägt die Ids nur nach.
/// </summary>
public sealed record SpurWeg(IReadOnlyList<string> Karten, IReadOnlyList<string[]> Paare)
{
    /// <summary>Was der Weg eines Folge-Eintrags über seine Ursache wissen muss: Typname, Wirker und Absender (<c>ueber</c>) der Ursache.</summary>
    public sealed record Eintrag(string Name, string? Wirker, string? Ueber);

    /// <summary>
    /// Der Weg: Absender (<c>ueber</c>: Fluss-Knoten, Regel, Reaktion, Akteur) → beim Aggregat die Command-Karte → Wirker → die eigene
    /// Event-Karte (nicht bei internen Marken wie <c>KommandoAbgelehnt</c>). Gegangen sind dazu: Ursache (deren Event-Karte) → erste Karte,
    /// und — war die Ursache von einem Fluss-Knoten bzw. einer Regel desselben Ablaufs abgeschickt oder kam sie aus der Quelle des Flusses
    /// (<paramref name="quellKnoten"/>: Pipeline, Quell-Typ → Knoten) — der Draht zwischen beiden.
    /// </summary>
    public static SpurWeg Aus(string name, string? wirker, string? ueber, string? ursache, bool intern,
        Func<string, Eintrag?> eintrag, Func<string, string, string?>? quellKnoten = null)
    {
        var karten = new List<string>();
        void Karte(string? id)
        {
            if (!string.IsNullOrEmpty(id) && (karten.Count == 0 || karten[^1] != id)) karten.Add(id);
        }
        Karte(ueber);
        if (wirker?.StartsWith("decide:", StringComparison.Ordinal) == true && wirker.IndexOf('/') is var i and > 0) Karte("rec:" + wirker[(i + 1)..]);
        Karte(wirker);
        if (!intern) Karte("rec:" + name);

        var paare = new List<string[]>();
        if ((ursache != null ? eintrag(ursache) : null) is { } u && karten.Count > 0)
        {
            paare.Add(["rec:" + u.Name, karten[0]]);
            var vorher = u.Ueber;
            if (vorher is null && u.Wirker?.StartsWith("quelle:", StringComparison.Ordinal) == true && ueber is { } j
                && j.StartsWith("fluss:", StringComparison.Ordinal) && j.IndexOf('#') is var h and > 6)
                vorher = quellKnoten?.Invoke(j[6..h], u.Wirker[7..]);
            if (vorher is { } v && ueber is { } jetzt && v != jetzt && Ablauf(v) is { } a && a == Ablauf(jetzt)) paare.Add([v, jetzt]);
        }
        for (var k = 1; k < karten.Count; k++) paare.Add([karten[k - 1], karten[k]]);
        return new SpurWeg(karten, paare);
    }

    // Der Ablauf eines Fluss-Knotens bzw. einer Regel: „fluss:P#k“ → „fluss:P“.
    private static string? Ablauf(string id)
        => (id.StartsWith("fluss:", StringComparison.Ordinal) || id.StartsWith("prozess:", StringComparison.Ordinal)) && id.IndexOf('#') is var i and > 0 ? id[..i] : null;
}
