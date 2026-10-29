using Abstractions;

namespace Pruefdomaene.Import;

/// <summary>
/// Aggregat <see cref="Importauftrag"/>: ein Massen-Import — gestartet mit der Zahl der Zeilen, der Stand
/// zählt nur vorwärts. Prüfling der Funktion mit Strom-Ausgabe und Fortschritt (<c>Importlauf</c>).
/// </summary>
public partial class Importauftrag : IState
{
    public int Zeilen { get; set; }
    public int Stand { get; set; }

    public bool Existiert => Version > 0;
}
