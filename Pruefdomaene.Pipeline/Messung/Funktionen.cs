using Abstractions;

namespace Pruefdomaene.Pipeline.Messung;

/// <summary>Gerufen (kein Auf), mit Zustand: zählt je Messstelle die Messwerte, die ihr Halter gelernt hat (<see cref="Zaehlwerk"/>).</summary>
public interface IZaehlwerk : IFunktion
{
    void Zaehle(Guid stelle);
    int Anzahl(Guid stelle);
}
