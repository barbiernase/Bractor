using Abstractions;

namespace Pruefwerk.Katalog.Messung;

/// <summary>Die eine Umsetzung des Zählwerks: der Zustand ist ein Cache, mindestens einmal.</summary>
public sealed class Zaehlwerk(IZustand<Zaehlstand> stand) : IZaehlwerk
{
    public void Zaehle(Guid stelle) => stand.Wert.Je[stelle] = stand.Wert.Je.GetValueOrDefault(stelle) + 1;

    public int Anzahl(Guid stelle) => stand.Wert.Je.GetValueOrDefault(stelle);
}
