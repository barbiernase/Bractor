namespace Domain.Verkauf;

/// <summary>Die unterstützten Währungen — Teil der Ubiquitous Language.</summary>
public enum Waehrung { EUR, USD, CHF, GBP }

/// <summary>Lebenszyklus des Verkaufsauftrags.</summary>
public enum Auftragsstatus { Offen, Aufgegeben, Storniert }

/// <summary>
/// VALUE OBJECT — ein Geldbetrag in genau einer Währung, als ECHTER Framework-Typ (er
/// reist in Commands/Events über die Proto-/Wire-Grenze). Unveränderlich, gleich per Wert.
///
/// Bewusst ein REINER Daten-Record wie die übrigen Framework-Wertobjekte (BildMeta,
/// RegionPosition): der generierte DTO-Mapper analysiert die Record-Member und verträgt
/// KEINE Instanz-Methoden auf einem gemappten Wertobjekt. Fabrik und Verhalten leben daher
/// in <see cref="Geldwerte"/> (Extension-Methoden) — für den Analyzer unsichtbar, die
/// Aufrufsyntax (<c>preis.Mal(3)</c>) bleibt unverändert.
/// </summary>
public record Geldwert(decimal Betrag, Waehrung Waehrung);

/// <summary>Fabrik + Verhalten für <see cref="Geldwert"/> (außerhalb des Records, damit der DTO-Mapper sauber bleibt).</summary>
public static class Geldwerte
{
    /// <summary>Fabrik mit Invariante: nicht negativ, auf 2 Nachkommastellen gerundet.</summary>
    public static Geldwert Von(decimal betrag, Waehrung waehrung)
    {
        var gerundet = Math.Round(betrag, 2, MidpointRounding.ToEven);
        if (gerundet < 0m) throw new ArgumentOutOfRangeException(nameof(betrag), $"Geldwert darf nicht negativ sein: {gerundet}");
        return new Geldwert(gerundet, waehrung);
    }

    public static bool GleicheWaehrung(this Geldwert a, Geldwert b) => a.Waehrung == b.Waehrung;

    public static Geldwert Plus(this Geldwert a, Geldwert b)
    {
        VerlangeGleicheWaehrung(a, b);
        return a with { Betrag = a.Betrag + b.Betrag };
    }

    public static Geldwert Mal(this Geldwert a, int faktor)
    {
        if (faktor < 0) throw new ArgumentOutOfRangeException(nameof(faktor));
        return a with { Betrag = a.Betrag * faktor };
    }

    public static bool GroesserAls(this Geldwert a, Geldwert b)
    {
        VerlangeGleicheWaehrung(a, b);
        return a.Betrag > b.Betrag;
    }

    private static void VerlangeGleicheWaehrung(Geldwert a, Geldwert b)
    {
        if (a.Waehrung != b.Waehrung)
            throw new InvalidOperationException($"Währungen dürfen nicht gemischt werden: {a.Waehrung} vs. {b.Waehrung}");
    }
}

/// <summary>
/// ENTITY im Aggregat (im Zustand als Wert-Record gehalten): eine Auftragsposition, deren
/// Identität die <see cref="ArtikelNr"/> ist. Nur über die Aggregatwurzel erreichbar.
/// </summary>
public record Auftragsposition(string ArtikelNr, string Bezeichnung, int Menge, Geldwert Einzelpreis)
{
    public Geldwert Zeilensumme => Einzelpreis.Mal(Menge);
}
