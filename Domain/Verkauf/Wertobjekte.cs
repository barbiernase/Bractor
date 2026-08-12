namespace Domain.Verkauf;

/// <summary>Die unterstützten Währungen.</summary>
public enum Waehrung { EUR, USD, CHF, GBP }

/// <summary>Lebenszyklus eines Verkaufsauftrags.</summary>
public enum Auftragsstatus { Offen, Aufgegeben, Storniert }

/// <summary>
/// ENTITY im Aggregat — eine Auftragsposition, deren Identität die <see cref="ArtikelNr"/>
/// ist (zwei Positionen mit gleicher Artikelnummer sind dieselbe). Nur über die
/// Aggregatwurzel erreichbar und änderbar.
/// </summary>
public record Auftragsposition(string ArtikelNr, string Bezeichnung, int Menge, Geldwert Einzelpreis)
{
    public Geldwert Zeilensumme => Einzelpreis.Mal(Menge);
}
