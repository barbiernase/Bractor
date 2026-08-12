using Domain.Spezifikation;

namespace Domain.Verkauf;

/// <summary>Kundenstufe — Teil der Ubiquitous Language.</summary>
public enum Kundenstufe { Neu, Stammkunde, Premium }

/// <summary>
/// Kandidat für die Spezifikationen — ein schlankes Bild des Kunden, über das fachliche
/// Regeln (rabattberechtigt?) entscheiden. Der <see cref="Jahresumsatz"/> ist das Value
/// Object <see cref="Geldwert"/>.
/// </summary>
public sealed record Kunde(Guid Id, Kundenstufe Stufe, Geldwert Jahresumsatz, int OffeneReklamationen);

// ── Atomare, benannte Regeln (SPECIFICATION) ──

public sealed class IstPremium : Spezifikation<Kunde>
{
    public override bool IstErfuellt(Kunde k) => k.Stufe == Kundenstufe.Premium;
}

public sealed class HatMindestumsatz : Spezifikation<Kunde>
{
    private readonly Geldwert _schwelle;
    public HatMindestumsatz(Geldwert schwelle) => _schwelle = schwelle;
    public override bool IstErfuellt(Kunde k) => !k.Jahresumsatz.KleinerAls(_schwelle); // ≥ Schwelle
}

public sealed class OhneOffeneReklamationen : Spezifikation<Kunde>
{
    public override bool IstErfuellt(Kunde k) => k.OffeneReklamationen == 0;
}

/// <summary>
/// Zusammengesetzte Regel als komponierte Spezifikation — die Stärke des Musters:
/// „rabattberechtigt = (Premium ODER ≥ Schwelle Umsatz) UND keine offenen Reklamationen."
/// Liest sich wie die Fachsprache und ist an einer Stelle testbar.
/// </summary>
public static class KundenRegeln
{
    public static Spezifikation<Kunde> Rabattberechtigt(Geldwert umsatzSchwelle) =>
        new IstPremium()
            .Oder(new HatMindestumsatz(umsatzSchwelle))
            .Und(new OhneOffeneReklamationen());
}
