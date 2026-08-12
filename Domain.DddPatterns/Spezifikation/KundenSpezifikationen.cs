using Domain.DddPatterns.Gemeinsam;

namespace Domain.DddPatterns.Spezifikation;

/// <summary>Kundenstufe — Teil der Ubiquitous Language.</summary>
public enum Kundenstufe { Neu, Stammkunde, Premium }

/// <summary>
/// Kandidat für die Spezifikationen. Ein schlankes Read-Modell des Kunden, über das
/// fachliche Regeln (versandkostenfrei? rabattberechtigt?) entscheiden.
/// </summary>
public sealed record Kunde(
    Guid Id,
    Kundenstufe Stufe,
    Geld Jahresumsatz,
    int OffeneReklamationen);

// ── Atomare, benannte Regeln ──

public sealed class IstPremium : Spezifikation<Kunde>
{
    public override bool IstErfuellt(Kunde k) => k.Stufe == Kundenstufe.Premium;
}

public sealed class HatMindestumsatz : Spezifikation<Kunde>
{
    private readonly Geld _schwelle;
    public HatMindestumsatz(Geld schwelle) => _schwelle = schwelle;
    public override bool IstErfuellt(Kunde k) =>
        !k.Jahresumsatz.KleinerAls(_schwelle); // ≥ Schwelle
}

public sealed class OhneOffeneReklamationen : Spezifikation<Kunde>
{
    public override bool IstErfuellt(Kunde k) => k.OffeneReklamationen == 0;
}

/// <summary>
/// Zusammengesetzte Regel als komponierte Spezifikation — die eigentliche Stärke des
/// Musters: „rabattberechtigt = (Premium ODER ≥ 10.000 € Umsatz) UND keine offenen
/// Reklamationen." Liest sich wie die Fachsprache und ist an einer Stelle testbar.
/// </summary>
public static class KundenRegeln
{
    public static Spezifikation<Kunde> Rabattberechtigt(Geld umsatzSchwelle) =>
        new IstPremium()
            .Oder(new HatMindestumsatz(umsatzSchwelle))
            .Und(new OhneOffeneReklamationen());
}
