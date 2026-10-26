using Abstractions;

namespace Pruefdomaene.Abrechnung;

/// <summary>Aggregat <see cref="Kundenkonto"/>: Gutschriften und vermerkte Mahnungen je Rechnung — das Ziel der Funktionen der Abrechnung.</summary>
public partial class Kundenkonto : IState
{
    public decimal Guthaben { get; set; }
    public int Gutschriften { get; set; }
    public int Mahnungen { get; set; }
}
