using Abstractions;

namespace Pruefdomaene.Posten;

/// <summary>
/// Aggregat <see cref="Posten"/> — ein Bestand, von dem reserviert, freigegeben und auf den zugebucht wird. Ziel beider Prozesse
/// (Diamant-Zweig A, Fan-out-Quelle und -Ziele), der Reaktion (Nachschub) und Prüfling für Snapshot und Upcasting.
/// </summary>
public partial class Posten : IState
{
    public int Bestand { get; set; }
    public int Reserviert { get; set; }
    public int Mindestbestand { get; set; }

    public int Verfuegbar => Bestand - Reserviert;
    public bool Existiert => Version > 0;
}
