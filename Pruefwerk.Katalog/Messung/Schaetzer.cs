using Abstractions;

namespace Pruefwerk.Katalog.Messung;

/// <summary>
/// Schätzt auf jedes Merkmal hin: aus den bisherigen Werten der Stelle (gelesen über eine Fähigkeit des Katalogs, die der Store der Anwendung umsetzt),
/// ohne sie aus dem Wert selbst, mit Anzahl 0. Daneben zählt das Zählwerk (Technik mit eigenem Zustand) die Schätzungen.
/// </summary>
public sealed class Schaetzer(IZaehlwerk zaehler) : IFunktion
{
    public async Task<Geschaetzt> Auf(Merkmal m, IHoleReihe reihe)
    {
        var r = await reihe.HoleAsync(m.Stelle);
        zaehler.Zaehle(m.Stelle);
        return r.Werte.Count > 0 ? new Geschaetzt(m.Stelle, r.Werte.Average(), r.Werte.Count) : new Geschaetzt(m.Stelle, m.Wert, 0);
    }
}
