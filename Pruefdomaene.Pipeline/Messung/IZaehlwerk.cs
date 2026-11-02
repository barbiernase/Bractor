namespace Pruefdomaene.Pipeline.Messung;

/// <summary>Technik mit Zustand (im Konstruktor des <see cref="Schaetzer"/>): zählt je Messstelle die Schätzungen ihres Halters (<see cref="Zaehlwerk"/>).</summary>
public interface IZaehlwerk
{
    void Zaehle(Guid stelle);
    int Anzahl(Guid stelle);
}
