namespace Domain.Verkauf;

/// <summary>
/// DDD-Baustein DOMAIN SERVICE — eine zustandslose fachliche Operation, die zu KEINEM
/// einzelnen Wertobjekt und keinem Aggregat gehört. Währungsumrechnung ist das
/// Musterbeispiel: <see cref="Geldwert"/> darf keine Wechselkurse kennen (die sind extern
/// und zeitabhängig), und kein Aggregat „besitzt" den Kurs. Also lebt die Operation in
/// einem Dienst — benannt in der Ubiquitous Language, mit Domänen-Signatur (Geldwert → Geldwert).
/// </summary>
public sealed class WechselkursDienst
{
    private readonly IReadOnlyDictionary<(Waehrung Von, Waehrung Nach), decimal> _kurse;

    public WechselkursDienst(IReadOnlyDictionary<(Waehrung, Waehrung), decimal> kurse)
        => _kurse = kurse;

    /// <summary>Rechnet einen Betrag in die Zielwährung um. Gleiche Währung → unverändert.</summary>
    public Geldwert Rechne(Geldwert betrag, Waehrung ziel)
    {
        if (betrag.Waehrung == ziel) return betrag;
        if (!_kurse.TryGetValue((betrag.Waehrung, ziel), out var kurs))
            throw new InvalidOperationException($"Kein Wechselkurs {betrag.Waehrung} → {ziel} hinterlegt.");
        return Geldwert.Von(betrag.Betrag * kurs, ziel);
    }
}
