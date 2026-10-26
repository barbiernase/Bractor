using Abstractions;

namespace Pruefdomaene.Posten;

/// <summary>
/// Aggregat <see cref="Posten"/> — ein Bestand, von dem reserviert, freigegeben und auf den zugebucht wird. Ziel des Auftrags
/// (Reservierung, Gegenzug), der Verteilung (Quelle und Ziele) und des Nachschubs; Prüfling für Snapshot und Upcasting.
/// </summary>
public partial class Posten : IState
{
    public int Bestand { get; set; }
    public int Reserviert { get; set; }
    public int Mindestbestand { get; set; }

    public int Verfuegbar => Bestand - Reserviert;
    public bool Existiert => Version > 0;
}
