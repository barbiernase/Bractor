namespace Domain.Verkauf;

/// <summary>Die unterstützten Währungen.</summary>
public enum Waehrung { EUR, USD, CHF, GBP }

/// <summary>Lebenszyklus eines Verkaufsauftrags.</summary>
public enum Auftragsstatus { Offen, Aufgegeben, Storniert }

/// <summary>
/// VALUE OBJECT — ein Geldbetrag in genau einer Währung. Unveränderlich, gleich per Wert,
/// selbstvalidierend (nie negativ, immer 2 Nachkommastellen) und mit dem Rechnen IM Objekt:
/// Arithmetik nur innerhalb derselben Währung, das Mischen wird verweigert. Ein nackter
/// <c>decimal</c> könnte davon nichts garantieren.
/// </summary>
public record Geldwert(decimal Betrag, Waehrung Waehrung)
{
    public decimal Betrag { get; init; } = Gerundet(Betrag);

    private static decimal Gerundet(decimal betrag)
    {
        var gerundet = Math.Round(betrag, 2, MidpointRounding.ToEven);
        if (gerundet < 0m)
            throw new ArgumentOutOfRangeException(nameof(betrag), $"Geldwert darf nicht negativ sein: {gerundet}");
        return gerundet;
    }

    public bool GleicheWaehrung(Geldwert anderer) => Waehrung == anderer.Waehrung;

    public Geldwert Plus(Geldwert anderer)
    {
        VerlangeGleicheWaehrung(anderer);
        return this with { Betrag = Betrag + anderer.Betrag };
    }

    public Geldwert Mal(int faktor)
    {
        if (faktor < 0) throw new ArgumentOutOfRangeException(nameof(faktor), "Faktor darf nicht negativ sein.");
        return this with { Betrag = Betrag * faktor };
    }

    public bool GroesserAls(Geldwert anderer)
    {
        VerlangeGleicheWaehrung(anderer);
        return Betrag > anderer.Betrag;
    }

    private void VerlangeGleicheWaehrung(Geldwert anderer)
    {
        if (Waehrung != anderer.Waehrung)
            throw new InvalidOperationException($"Währungen dürfen nicht gemischt werden: {Waehrung} vs. {anderer.Waehrung}");
    }

    public override string ToString() => $"{Betrag:0.00} {Waehrung}";
}

/// <summary>
/// ENTITY im Aggregat — eine Auftragsposition, deren Identität die <see cref="ArtikelNr"/>
/// ist (zwei Positionen mit gleicher Artikelnummer sind dieselbe). Nur über die
/// Aggregatwurzel erreichbar und änderbar.
/// </summary>
public record Auftragsposition(string ArtikelNr, string Bezeichnung, int Menge, Geldwert Einzelpreis)
{
    public Geldwert Zeilensumme => Einzelpreis.Mal(Menge);
}
