using Abstractions;

namespace Domain.Verkauf;

/// <summary>
/// AGGREGATE ROOT — die Konsistenzgrenze des Verkaufs. Ein ECHTES Framework-Aggregat
/// (Decider/Applier, über die generierte Handler-Factory getrieben, Commands/Events über
/// Proto). Es hält ENTITIES (<see cref="Auftragsposition"/>) und VALUE OBJECTS
/// (<see cref="Geldwert"/>) und erzwingt seine Invarianten im Decider:
///   • Positionen nur ändern, solange offen,
///   • gleiche Artikelnummer verschmilzt Mengen,
///   • Gesamtsumme überschreitet nie das Kreditlimit,
///   • alle Beträge in der Auftragswährung.
///
/// REINE Domäne — kein Cursor, kein Vorgang, keine Prozess-Mechanik. Id/Version werden
/// generiert. Der laufende <see cref="Gesamtsumme"/>-Saldo hält das Kreditlimit-Prüfen O(1).
/// </summary>
public partial class Verkaufsauftrag : IState
{
    public Guid KundeId { get; set; }
    public Geldwert Kreditlimit { get; set; } = new(0m, Waehrung.EUR);
    public Auftragsstatus Status { get; set; } = Auftragsstatus.Offen;
    public List<Auftragsposition> Positionen { get; set; } = new();
    public Geldwert Gesamtsumme { get; set; } = new(0m, Waehrung.EUR);

    public bool IstOffen => Status == Auftragsstatus.Offen;

    public Auftragsposition? Position(string artikelNr) =>
        Positionen.FirstOrDefault(p => p.ArtikelNr == artikelNr);
}
