using Abstractions;

namespace Pruefdomaene.Pipeline.Messung;

/// <summary>Technik mit Zustand (im Konstruktor des <see cref="Schaetzer"/>): zählt je Messstelle die Schätzungen ihres Halters (<see cref="Zaehlwerk"/>).</summary>
public interface IZaehlwerk
{
    void Zaehle(Guid stelle);
    int Anzahl(Guid stelle);
}

/// <summary>Der Zustand des Zählwerks: je Messstelle die Zahl der Schätzungen.</summary>
public sealed class Zaehlstand
{
    public Dictionary<Guid, int> Je { get; set; } = new();
}

/// <summary>
/// Technik mit Zustand (docs/04 §4.4): ihr Zustand gehört der Aktivierung des Halters (<see cref="Schaetzer"/>) — ein Cache, keine
/// Wahrheit: was fachlich zählt, steht im Log und in Projektionen.
/// </summary>
public sealed class Zaehlwerk(IZustand<Zaehlstand> stand) : IZaehlwerk
{
    public void Zaehle(Guid stelle) => stand.Wert.Je[stelle] = stand.Wert.Je.GetValueOrDefault(stelle) + 1;

    public int Anzahl(Guid stelle) => stand.Wert.Je.GetValueOrDefault(stelle);
}
