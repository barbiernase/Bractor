using Abstractions;

namespace Pruefdomaene.Verteilung;

/// <summary>
/// Aggregat <see cref="Verteilung"/> — ein VORGANGS- und ZÄHL-Aggregat (docs/10 §10.1): die Quelle reserviert die Gesamtmenge,
/// danach wird jedes der N Ziele zugebucht (Fan-out über die Verteilabwicklung), und erst wenn alle N gezählt sind, schließt die Verteilung ab.
/// Die Ziele hält es nur, um den Fan-out nach der Reservierung anzuordnen; der Fan-in ist eine Zahl.
/// </summary>
public partial class Verteilung : IState
{
    public IReadOnlyList<Id<Posten.Posten>> Ziele { get; set; } = Array.Empty<Id<Posten.Posten>>();
    public int MengeJeZiel { get; set; }
    public bool Angeordnet { get; set; }
    public int Gezaehlt { get; set; }
    public int Zugebucht { get; set; }
    public bool Abgeschlossen { get; set; }
    public bool Gescheitert { get; set; }

    public bool Existiert => Version > 0;
}
