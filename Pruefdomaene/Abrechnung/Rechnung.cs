using Abstractions;

namespace Pruefdomaene.Abrechnung;

/// <summary>
/// Aggregat <see cref="Rechnung"/> (Plan Pub/Sub-Neustrukturierung Phase 3): gestellt, freigegeben oder gesperrt, bezahlt; eine fällige Prüfung der
/// Zahlung meldet sie überfällig. Prüfling der Funktionen in Baustein-Form (Gutschrift gleichbleibend, Mahnwesen mit Dienst, Freigabe draußen).
/// </summary>
public partial class Rechnung : IState
{
    public Id<Kundenkonto> Konto { get; set; }
    public decimal Betrag { get; set; }
    public bool Bezahlt { get; set; }
    public bool Freigegeben { get; set; }
    public bool Gesperrt { get; set; }
    public int Mahnstufe { get; set; }

    public bool Existiert => Version > 0;
}
