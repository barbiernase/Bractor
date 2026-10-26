using Abstractions;

namespace Pruefdomaene.Auftrag;

/// <summary>
/// Aggregat <see cref="Auftrag"/> — ein VORGANGS-Aggregat (Plan Pub/Sub-Neustrukturierung §2.2 „Warten gehört in ein Aggregat“): zwei Zweige laufen
/// unabhängig (Posten reservieren, Kontingent belegen) und melden sich hier zurück, in beliebiger Reihenfolge. Bestätigt wird erst, wenn BEIDE gelangen;
/// scheitert einer, wird der gelungene genau einmal zurückgenommen — auch wenn seine Rückmeldung erst nach der Absage kommt. Die Kanten Event →
/// Command trägt die Funktion <c>Auftragsabwicklung</c>; den Zustand über mehrere Ereignisse hält nur dieses Aggregat.
/// </summary>
public partial class Auftrag : IState
{
    public Id<Posten.Posten> PostenId { get; set; }
    public Id<Kontingent.Kontingent> KontingentId { get; set; }
    public int Menge { get; set; }
    public bool Reserviert { get; set; }
    public bool Belegt { get; set; }
    public bool Bestaetigt { get; set; }
    public bool Gescheitert { get; set; }
    public bool PostenZurueck { get; set; }
    public bool KontingentZurueck { get; set; }

    public bool Existiert => Version > 0;
}
