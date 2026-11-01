using Abstractions;
using Pruefdomaene.Messung;
using Pruefdomaene.Projections.Messung;

namespace Pruefdomaene.Pipeline.Messung;

/// <summary>
/// Schätzt auf jedes Merkmal hin (Hinweis rein, Command raus, docs/04 §4.4). Gelernt wird nicht hier: was das Log über eine Messstelle weiß, hält die
/// Messreihe (eine Projektion, replaybar); die Funktion liest sie per Lese-Fähigkeit. Kennt die Projektion die Messstelle noch nicht (der Hinweis war
/// schneller als sie), schätzt sie den Wert selbst, mit Anzahl 0. Daneben zählt eine gerufene Funktion mit eigenem Zustand die Schätzungen
/// (<see cref="IZaehlwerk"/>) — ein Cache, mindestens einmal.
/// </summary>
public sealed class Schaetzer(IZaehlwerk zaehler) : IFunktion
{
    public async Task<VermerkeSchaetzung> Auf(Merkmal m, IHoleMessreihe reihe)
    {
        var r = await reihe.HoleAsync(m.Stelle);
        zaehler.Zaehle(m.Stelle.Wert);
        return r.Werte.Count > 0 ? new VermerkeSchaetzung(m.Stelle, r.Werte.Average(), r.Werte.Count) : new VermerkeSchaetzung(m.Stelle, m.Wert, 0);
    }
}
