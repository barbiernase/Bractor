using Domain.Projections;
namespace Domain.Client.Modules.Suche;

// Abgeleitete Baum-Struktur (Anzeige-Modell, kein State). Neueste zuerst.
public sealed record TagKnoten(DateTimeOffset Datum, int Anzahl);
public sealed record MonatKnoten(int Jahr, int Monat, int Anzahl, IReadOnlyList<TagKnoten> Tage)
{
    // Stabiler Schlüssel für die Monats-Auswahl (Häkchen) und AktiveMonate.
    public int Schluessel => Jahr * 100 + Monat;
}
public sealed record JahrKnoten(int Jahr, int Anzahl, IReadOnlyList<MonatKnoten> Monate);

/// <summary>
/// Pure Ableitung: aus der flachen ProduktionsTage-Liste wird die volle
/// Jahr→Monat→Tag-Hierarchie. Kein Grenzen-Filter mehr — welche Monate ihre
/// Tage tatsächlich anzeigen, entscheidet das Panel über AktiveMonate beim
/// Rendern. Rein funktional; das Panel memoized über die Listen-Referenz.
/// </summary>
public static class ProduktionsBaum
{
    // ── Einzige Stelle für die Zählung ──
    // Default: "Anzahl Produktionstage". Trägt ProduktionsTag eine Paarzahl,
    // hier auf  t => t.<Feld>  umstellen.
    private static int Zaehle(ProduktionsTag t) => 1;

    public static IReadOnlyList<JahrKnoten> Baue(IReadOnlyList<ProduktionsTag> tage)
        => tage
            .GroupBy(t => t.Datum.Year)
            .OrderByDescending(j => j.Key)
            .Select(j => new JahrKnoten(
                Jahr: j.Key,
                Anzahl: j.Sum(Zaehle),
                Monate: j.GroupBy(t => t.Datum.Month)
                    .OrderByDescending(m => m.Key)
                    .Select(m => new MonatKnoten(
                        Jahr: j.Key,
                        Monat: m.Key,
                        Anzahl: m.Sum(Zaehle),
                        Tage: m.OrderByDescending(t => t.Datum)
                            .Select(t => new TagKnoten(t.Datum, Zaehle(t)))
                            .ToList()))
                    .ToList()))
            .ToList();
}
