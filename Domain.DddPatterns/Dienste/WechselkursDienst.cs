using Domain.DddPatterns.Gemeinsam;

namespace Domain.DddPatterns.Dienste;

/// <summary>
/// DDD-Baustein DOMAIN SERVICE — eine zustandslose fachliche Operation, die zu KEINER
/// einzelnen Entity und keinem Value Object gehört. Währungsumrechnung ist das Musterbeispiel:
///   • <see cref="Geld"/> darf keine Wechselkurse kennen (die sind extern und zeitabhängig),
///   • kein einzelnes Aggregat „besitzt" den Kurs.
/// Also lebt die Operation in einem Dienst — benannt in der Ubiquitous Language, mit
/// Domänen-Signatur (Geld → Geld), ohne technischen Beigeschmack.
/// </summary>
public sealed class WechselkursDienst
{
    private readonly IReadOnlyDictionary<(Waehrung Von, Waehrung Nach), decimal> _kurse;

    public WechselkursDienst(IReadOnlyDictionary<(Waehrung, Waehrung), decimal> kurse)
        => _kurse = kurse;

    /// <summary>Rechnet einen Betrag in die Zielwährung um. Gleiche Währung → unverändert.</summary>
    public Geld Rechne(Geld betrag, Waehrung ziel)
    {
        if (betrag.Waehrung == ziel) return betrag;

        DomaeneException.Verlange(_kurse.TryGetValue((betrag.Waehrung, ziel), out var kurs),
            $"Kein Wechselkurs {betrag.Waehrung} → {ziel} hinterlegt.");

        return Geld.Von(betrag.Betrag * kurs, ziel);
    }
}
