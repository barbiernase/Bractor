namespace Domain.DddPatterns.Gemeinsam;

/// <summary>
/// VALUE OBJECT — ein Prozentsatz zwischen 0 und 100. Unveränderlich, selbstvalidierend,
/// gleich per Wert (record struct). Kapselt die „geteilt durch 100"-Rechnung, damit sie
/// nicht als nackter <c>decimal</c> durch die Domäne wandert (Primitive Obsession).
/// </summary>
public readonly record struct Prozent
{
    public decimal Wert { get; }

    private Prozent(decimal wert) => Wert = wert;

    /// <summary>Fabrik mit Invarianten-Prüfung: 0 ≤ Wert ≤ 100.</summary>
    public static Prozent Von(decimal wert)
    {
        DomaeneException.Verlange(wert >= 0m, $"Prozent darf nicht negativ sein: {wert}");
        DomaeneException.Verlange(wert <= 100m, $"Prozent darf 100 nicht überschreiten: {wert}");
        return new Prozent(wert);
    }

    public static readonly Prozent Null = new(0m);

    /// <summary>Der Faktor für Multiplikationen, z. B. 20 % → 0,20.</summary>
    public decimal AlsFaktor => Wert / 100m;

    public override string ToString() => $"{Wert:0.##} %";
}
