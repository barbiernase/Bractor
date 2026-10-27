using Abstractions;
using Pruefdomaene.Messung;

namespace Pruefdomaene.Pipeline.Messung;

/// <summary>Was das Modell je Messstelle gelernt hat.</summary>
public sealed class Lernstand
{
    public int Anzahl { get; set; }
    public double Summe { get; set; }
}

/// <summary>Der Zustand des Schätzers (IZustand, Plan §2.9): je Messstelle Anzahl und Summe der gelernten Werte — kein Domänen-Zustand.</summary>
public sealed class Schaetzmodell
{
    public Dictionary<Guid, Lernstand> Je { get; set; } = new();

    public void Lerne(Guid stelle, double wert)
    {
        if (!Je.TryGetValue(stelle, out var s)) Je[stelle] = s = new Lernstand();
        s.Anzahl++;
        s.Summe += wert;
    }

    public Lernstand? Stand(Guid stelle) => Je.GetValueOrDefault(stelle);
}

/// <summary>
/// Ein Modell, das weiterlernt (Plan §2.9, E6): EIN Zustand für alle Messstellen (<c>[EineAdresse]</c>, A6) — es lernt aus jedem Messwert im Log
/// (genau einmal je Eingang auf dem gesicherten Zustand, auch nach Absturz und Umzug) und schätzt auf jedes Merkmal hin (Hinweis rein, Command raus).
/// </summary>
[EineAdresse]
public sealed class Schaetzer(IZustand<Schaetzmodell> modell) : IFunktion
{
    public void Auf(MesswertErfasst e, Herkunft h) => modell.Wert.Lerne(h.Stream, e.Wert);

    public VermerkeSchaetzung? Auf(Merkmal m) =>
        modell.Wert.Stand(m.Stelle.Wert) is { Anzahl: > 0 } s ? new VermerkeSchaetzung(m.Stelle, s.Summe / s.Anzahl, s.Anzahl) : null;
}
