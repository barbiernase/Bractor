using Domain.DddPatterns.Gemeinsam;

namespace Domain.DddPatterns.Prozess;

/// <summary>Stand der Saga — Teil der Ubiquitous Language.</summary>
public enum SagaStand { Gestartet, Reserviert, Abgeschlossen, Kompensiert }

/// <summary>
/// DDD-Baustein SAGA / PROCESS MANAGER — hält die Konsistenz über MEHRERE Aggregate,
/// die keine gemeinsame Transaktion teilen. Die Saga orchestriert zwei Schritte
/// (Bestand reservieren → Zahlung belasten). Scheitert der zweite, macht sie den ersten
/// per KOMPENSATION rückgängig — das eventual-konsistente Gegenstück zum verteilten
/// Rollback. Sie kennt die Reihenfolge und den Rückabwicklungspfad; die Aggregate bleiben
/// rein und wissen nichts von der Saga.
///
/// (Das Framework fährt dasselbe Muster durable über den Event-Regel-DAG; diese Fassung
/// zeigt das Muster store-frei und damit im Prüfstand prüfbar.)
/// </summary>
public sealed class BestellAbwicklungsSaga
{
    private readonly Lagerbestand _lager;
    private readonly Zahlungsvorgang _zahlung;
    private readonly List<string> _protokoll = new();

    public SagaStand Stand { get; private set; } = SagaStand.Gestartet;
    public IReadOnlyList<string> Protokoll => _protokoll;

    public BestellAbwicklungsSaga(Lagerbestand lager, Zahlungsvorgang zahlung)
    {
        _lager = lager;
        _zahlung = zahlung;
    }

    /// <summary>
    /// Wickelt eine Bestellung ab. Gibt true bei Erfolg, false wenn die Zahlung scheiterte
    /// und deshalb kompensiert wurde. Wirft nur bei einem fachlichen Fehler des ERSTEN
    /// Schritts (dann gibt es nichts zu kompensieren).
    /// </summary>
    public bool Wickle(int menge, Geld preis)
    {
        _lager.Reserviere(menge);                 // Schritt 1
        Stand = SagaStand.Reserviert;
        _protokoll.Add($"reserviert: {menge}");

        try
        {
            _zahlung.Belaste(preis);              // Schritt 2 (kann scheitern)
            Stand = SagaStand.Abgeschlossen;
            _protokoll.Add($"bezahlt: {preis}");
            return true;
        }
        catch (DomaeneException ex)
        {
            _lager.GibFrei(menge);               // KOMPENSATION von Schritt 1
            Stand = SagaStand.Kompensiert;
            _protokoll.Add($"kompensiert: {ex.Message}");
            return false;
        }
    }
}
