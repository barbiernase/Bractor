using Abstractions;

namespace Pruefdomaene.Pipeline.Messung;

/// <summary>Gerufen (kein Auf), mit Zustand: zählt je Messstelle die Schätzungen ihres Halters (<see cref="Zaehlwerk"/>).</summary>
public interface IZaehlwerk : IFunktion
{
    void Zaehle(Guid stelle);
    int Anzahl(Guid stelle);
}
