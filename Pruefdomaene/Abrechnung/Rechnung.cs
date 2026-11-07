using Abstractions;

namespace Pruefdomaene.Abrechnung;

/// <summary>
/// Aggregat <see cref="Rechnung"/>: gestellt, freigegeben oder gesperrt (eine große dem Rechnungsprüfer vorgelegt), bezahlt; eine fällige Prüfung
/// der Zahlung meldet sie überfällig. Was danach geschieht (Freigabe, Gutschrift, Mahnung), stößt ein Client an, der auf die Fakten reagiert.
/// </summary>
public partial class Rechnung : IState
{
    public Id<Kundenkonto> Konto { get; set; }
    public decimal Betrag { get; set; }
    public bool Bezahlt { get; set; }
    public bool Freigegeben { get; set; }
    public bool Gesperrt { get; set; }
    public bool Vorgelegt { get; set; }
    public int Mahnstufe { get; set; }

    public bool Existiert => Version > 0;
}
