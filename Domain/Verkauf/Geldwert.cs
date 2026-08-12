using Abstractions;

namespace Domain.Verkauf;

/// <summary>
/// VALUE OBJECT — ein Geldbetrag in genau einer Währung. Unveränderlich, gleich per Wert.
///
/// Die Daten. Erzeuger und Verhalten liegen als zweite <c>partial</c> auf DEMSELBEN Typ
/// (<c>Geldwert.Verhalten.cs</c>) — kein separater Operationen-Typ. Der Marker
/// <see cref="IWertobjekt"/> macht den Typ compile-time auffindbar.
/// </summary>
public partial record Geldwert(decimal Betrag, Waehrung Waehrung) : IWertobjekt
{
    // Normalisierung (Rundung) ist total — sie kann nie scheitern und sitzt daher in der
    // Konstruktion. So greift sie auch bei `with` und beim Rekonstruieren und hält die
    // Wertgleichheit sauber (10,005 und 10,00 sind nicht plötzlich verschieden).
    public decimal Betrag { get; init; } = Runden(Betrag);

    private static decimal Runden(decimal betrag) => Math.Round(betrag, 2, MidpointRounding.ToEven);
}
