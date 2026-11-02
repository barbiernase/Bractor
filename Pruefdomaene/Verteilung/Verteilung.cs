using Abstractions;

namespace Pruefdomaene.Verteilung;

/// <summary>
/// Aggregat <see cref="Verteilung"/> — ein VORGANGS-Aggregat (docs/10 §10.1): die Quelle reserviert die Gesamtmenge, danach wird jedes der N Ziele
/// zugebucht — im Graphen je Ziel ein Unterlauf (JE), gesammelt zu einer Liste (SAMMELN), die die Verteilung abschließt. Die Ziele hält es nur, um die
/// Zubuchung nach der Reservierung anzuordnen; zählen muss es nichts.
/// </summary>
public partial class Verteilung : IState
{
    public IReadOnlyList<Id<Posten.Posten>> Ziele { get; set; } = Array.Empty<Id<Posten.Posten>>();
    public int MengeJeZiel { get; set; }
    public bool QuelleReserviert { get; set; }
    public int Zugebucht { get; set; }
    public bool Abgeschlossen { get; set; }
    public bool Gescheitert { get; set; }

    public bool Existiert => Version > 0;
}
