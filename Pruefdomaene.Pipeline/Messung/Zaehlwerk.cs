using Abstractions;

namespace Pruefdomaene.Pipeline.Messung;

/// <summary>Der Zustand des Messzählers: je Messstelle die Zahl der gezählten Messwerte.</summary>
public sealed class Zaehlstand
{
    public Dictionary<Guid, int> Je { get; set; } = new();
}

/// <summary>
/// Eine gerufene Funktion mit Zustand (docs/04 §4.4): ihr Zustand gehört der Aktivierung des Halters (<see cref="Schaetzer"/>) und wird mit dessen
/// Position gesichert — jeder Log-Eingang zählt genau einmal, auch nach Absturz und Umzug.
/// </summary>
public sealed class Zaehlwerk(IZustand<Zaehlstand> stand) : IZaehlwerk
{
    public void Zaehle(Guid stelle) => stand.Wert.Je[stelle] = stand.Wert.Je.GetValueOrDefault(stelle) + 1;

    public int Anzahl(Guid stelle) => stand.Wert.Je.GetValueOrDefault(stelle);
}
