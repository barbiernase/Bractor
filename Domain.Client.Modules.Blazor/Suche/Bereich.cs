namespace Domain.Client.Modules;

/// <summary>Ein zusammenhängender, halboffener Tagesbereich [Von, Bis).</summary>
public readonly record struct Bereich(DateTimeOffset Von, DateTimeOffset Bis);

public static class Bereiche
{
    /// <summary>
    /// Faltet eine Menge einzelner Tage (auf Tagesgrenze normiert, 00:00) zu
    /// MINIMALEN zusammenhängenden Bereichen [Von, Bis) zusammen: aufeinander
    /// folgende Tage wachsen zu einem Bereich, eine Lücke schließt ihn und
    /// beginnt einen neuen.
    ///
    /// Aus {Jun 2054, Jun 2056} werden ZWEI exakte Bereiche statt eines
    /// Riesenbereichs — und die Anzahl der Bereiche = Anzahl der Queries.
    /// Ein voller zusammenhängender Monat wird EIN Bereich = EINE Query.
    /// </summary>
    public static IReadOnlyList<Bereich> AusTagen(IReadOnlySet<DateTimeOffset> tage)
    {
        if (tage.Count == 0) return Array.Empty<Bereich>();

        var sortiert = tage.OrderBy(t => t).ToList();
        var ergebnis = new List<Bereich>();

        var von = sortiert[0];
        var letzter = sortiert[0];

        for (var i = 1; i < sortiert.Count; i++)
        {
            var t = sortiert[i];
            if (t == letzter.AddDays(1)) { letzter = t; continue; }  // lückenlos → Bereich wächst
            ergebnis.Add(new Bereich(von, letzter.AddDays(1)));      // Lücke → Bereich schließen
            von = t;
            letzter = t;
        }
        ergebnis.Add(new Bereich(von, letzter.AddDays(1)));
        return ergebnis;
    }
}
