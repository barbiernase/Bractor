namespace Pruefwerk.Katalog.Messung;

/// <summary>Technik mit eigenem Zustand (ohne Marker, im Konstruktor des Schätzers): zählt die Schätzungen je Stelle.</summary>
public interface IZaehlwerk
{
    void Zaehle(Guid stelle);
    int Anzahl(Guid stelle);
}
