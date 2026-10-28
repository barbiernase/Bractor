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
}

/// <summary>
/// Das Arbeitsmodell einer Aktivierung — wie Gewichte auf einer GPU: beim Aktivieren aus dem gesicherten Zustand geladen, gerechnet wird nur hier,
/// vor jeder Sicherung wird es in den Zustand zurückgeschrieben. Eine Ressource der Aktivierung, kein Zustand (der liegt in <see cref="Schaetzmodell"/>).
/// </summary>
public sealed class Arbeitsmodell
{
    private readonly Dictionary<Guid, (int Anzahl, double Summe)> _je = new();

    public void Lade(Schaetzmodell m)
    {
        _je.Clear();
        foreach (var (stelle, s) in m.Je) _je[stelle] = (s.Anzahl, s.Summe);
    }

    public Schaetzmodell Abbild() =>
        new() { Je = _je.ToDictionary(x => x.Key, x => new Lernstand { Anzahl = x.Value.Anzahl, Summe = x.Value.Summe }) };

    public void Lerne(Guid stelle, double wert)
    {
        var (n, summe) = _je.GetValueOrDefault(stelle);
        _je[stelle] = (n + 1, summe + wert);
    }

    public (int Anzahl, double Summe) Stand(Guid stelle) => _je.GetValueOrDefault(stelle);
}

/// <summary>
/// Ein Modell, das weiterlernt (Plan §2.9, E6, E10): EIN Zustand für alle Messstellen (<c>[EineAdresse]</c>, A6) — es lernt aus jedem Messwert im Log
/// (genau einmal je Eingang auf dem gesicherten Zustand, auch nach Absturz und Umzug) und schätzt auf jedes Merkmal hin (Hinweis rein, Command raus).
/// Gerechnet wird auf dem Arbeitsmodell der Aktivierung; der Lebenszyklus lädt es aus dem Zustand und schreibt es vor jeder Sicherung zurück.
/// </summary>
[EineAdresse]
public sealed class Schaetzer(IZustand<Schaetzmodell> modell) : IFunktion, ILebenszyklus
{
    private readonly Arbeitsmodell _arbeit = new();

    public Task BeimAktivierenAsync(CancellationToken ct)
    {
        _arbeit.Lade(modell.Wert);
        return Task.CompletedTask;
    }

    public Task VorDemSichernAsync(CancellationToken ct)
    {
        modell.Wert = _arbeit.Abbild();
        return Task.CompletedTask;
    }

    public void Auf(MesswertErfasst e, Herkunft h) => _arbeit.Lerne(h.Stream, e.Wert);

    /// <summary>Auf jedes Merkmal eine Schätzung — hat das Modell die Messstelle noch nicht gelernt (der Hinweis war schneller als das Log), schätzt
    /// es den Wert selbst, mit Anzahl 0.</summary>
    public VermerkeSchaetzung Auf(Merkmal m) =>
        _arbeit.Stand(m.Stelle.Wert) is { Anzahl: > 0 } s ? new VermerkeSchaetzung(m.Stelle, s.Summe / s.Anzahl, s.Anzahl) : new VermerkeSchaetzung(m.Stelle, m.Wert, 0);
}
