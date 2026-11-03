using Abstractions;

namespace Pruefdomaene.Verteilung;

/// <summary>
/// Aggregat <see cref="Verteilung"/> — ein VORGANGS-Aggregat (docs/10 §10.1): die Quelle reserviert die Gesamtmenge, danach wird jedes der N Ziele
/// zugebucht — im Graphen je Ziel ein Unterlauf (JE). Jede gelungene Zubuchung meldet sich hier; die Verteilung ZÄHLT mit (Zähl-Aggregat) und schließt
/// nach dem letzten Ziel ab. Das Einsammeln steht so im Aggregat, nicht im Graphen.
/// </summary>
public partial class Verteilung : IState
{
    public IReadOnlyList<Id<Posten.Posten>> Ziele { get; set; } = Array.Empty<Id<Posten.Posten>>();
    public int MengeJeZiel { get; set; }
    public bool QuelleReserviert { get; set; }
    public int ZieleGebucht { get; set; }
    public int Zugebucht { get; set; }
    public bool Abgeschlossen { get; set; }
    public bool Gescheitert { get; set; }

    public bool Existiert => Version > 0;
}
