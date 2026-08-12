using System.Globalization;

namespace Domain.DddPatterns.Gemeinsam;

/// <summary>Die unterstützten Währungen. Teil der Ubiquitous Language.</summary>
public enum Waehrung { EUR, USD, CHF, GBP }

/// <summary>
/// VALUE OBJECT — ein Geldbetrag in genau einer Währung. Das Paradebeispiel gegen
/// Primitive Obsession: statt <c>decimal</c> + irgendwo eine Währung trägt <see cref="Geld"/>
/// beides untrennbar und erzwingt seine Invarianten selbst:
///   • nie negativ (Fabrik prüft),
///   • immer auf 2 Nachkommastellen kaufmännisch gerundet,
///   • Arithmetik NUR innerhalb derselben Währung (Mischung wirft),
///   • gleich per Wert (record struct), unveränderlich.
///
/// Das Rechnen lebt IM Wertobjekt (<see cref="Plus"/>/<see cref="Minus"/>/<see cref="Mal"/>/
/// <see cref="Anteil"/>) — reiches Verhalten statt anämischer Datenhalter.
/// </summary>
public readonly record struct Geld
{
    public decimal Betrag { get; }
    public Waehrung Waehrung { get; }

    private Geld(decimal betrag, Waehrung waehrung)
    {
        Betrag = betrag;
        Waehrung = waehrung;
    }

    /// <summary>Fabrik: rundet kaufmännisch auf 2 Stellen und verlangt Nicht-Negativität.</summary>
    public static Geld Von(decimal betrag, Waehrung waehrung)
    {
        var gerundet = Math.Round(betrag, 2, MidpointRounding.ToEven);
        DomaeneException.Verlange(gerundet >= 0m, $"Geld darf nicht negativ sein: {gerundet} {waehrung}");
        return new Geld(gerundet, waehrung);
    }

    public static Geld Null(Waehrung waehrung) => new(0m, waehrung);

    public bool IstNull => Betrag == 0m;

    public Geld Plus(Geld anderes)
    {
        VerlangeGleicheWaehrung(anderes);
        return Von(Betrag + anderes.Betrag, Waehrung);
    }

    public Geld Minus(Geld anderes)
    {
        VerlangeGleicheWaehrung(anderes);
        DomaeneException.Verlange(Betrag >= anderes.Betrag,
            $"Geld darf nicht unter null fallen: {Betrag} - {anderes.Betrag} {Waehrung}");
        return Von(Betrag - anderes.Betrag, Waehrung);
    }

    /// <summary>Skaliert den Betrag (z. B. Stückpreis × Menge).</summary>
    public Geld Mal(decimal faktor)
    {
        DomaeneException.Verlange(faktor >= 0m, $"Geld-Faktor darf nicht negativ sein: {faktor}");
        return Von(Betrag * faktor, Waehrung);
    }

    /// <summary>Ein prozentualer Anteil, z. B. 19 % MwSt.</summary>
    public Geld Anteil(Prozent prozent) => Von(Betrag * prozent.AlsFaktor, Waehrung);

    public bool GroesserAls(Geld anderes)
    {
        VerlangeGleicheWaehrung(anderes);
        return Betrag > anderes.Betrag;
    }

    public bool KleinerAls(Geld anderes)
    {
        VerlangeGleicheWaehrung(anderes);
        return Betrag < anderes.Betrag;
    }

    private void VerlangeGleicheWaehrung(Geld anderes) =>
        DomaeneException.Verlange(Waehrung == anderes.Waehrung,
            $"Währungen dürfen nicht gemischt werden: {Waehrung} vs. {anderes.Waehrung}");

    public override string ToString() =>
        $"{Betrag.ToString("0.00", CultureInfo.InvariantCulture)} {Waehrung}";
}
