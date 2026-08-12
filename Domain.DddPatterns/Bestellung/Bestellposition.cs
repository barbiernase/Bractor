using Domain.DddPatterns.Gemeinsam;

namespace Domain.DddPatterns.Bestellung;

/// <summary>
/// DDD-Baustein ENTITY — eine Bestellposition INNERHALB des Aggregats. Der Unterschied
/// zum Value Object ist die IDENTITÄT: zwei Positionen sind „dieselbe", wenn ihre
/// <see cref="ArtikelNr"/> gleich ist — auch wenn Menge oder Preis sich ändern. Deshalb
/// eine Klasse mit identitätsbasierter Gleichheit, kein record mit Wertgleichheit.
///
/// Die Position ist NUR über die Aggregatwurzel (<see cref="Bestellung"/>) erreichbar und
/// änderbar — sie hat keine eigene Repository-/Lebenszyklus-Hoheit (Aggregat-Grenze).
/// </summary>
public sealed class Bestellposition
{
    public string ArtikelNr { get; }
    public string Bezeichnung { get; }
    public int Menge { get; private set; }
    public Geld Stueckpreis { get; }

    internal Bestellposition(string artikelNr, string bezeichnung, int menge, Geld stueckpreis)
    {
        DomaeneException.Verlange(!string.IsNullOrWhiteSpace(artikelNr), "Artikelnummer darf nicht leer sein.");
        DomaeneException.Verlange(menge > 0, $"Menge muss positiv sein: {menge}");
        ArtikelNr = artikelNr;
        Bezeichnung = bezeichnung;
        Menge = menge;
        Stueckpreis = stueckpreis;
    }

    /// <summary>Zeilensumme = Stückpreis × Menge — Verhalten IN der Entity.</summary>
    public Geld Zeilensumme => Stueckpreis.Mal(Menge);

    internal void SetzeMenge(int neueMenge)
    {
        DomaeneException.Verlange(neueMenge > 0, $"Menge muss positiv sein: {neueMenge}");
        Menge = neueMenge;
    }

    // Identität, nicht Wert: Gleichheit hängt allein an der ArtikelNr.
    public override bool Equals(object? obj) => obj is Bestellposition p && p.ArtikelNr == ArtikelNr;
    public override int GetHashCode() => ArtikelNr.GetHashCode();
}
