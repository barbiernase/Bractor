namespace Domain.Trainingslauf;

/// <summary>
/// Lebenszyklus eines Trainingslaufs (Konzept §4.1).
///
///   Angefordert → Laeuft → (Fortschritt je Epoche) → Abgeschlossen
///        └────────► Gescheitert / Abgebrochen / Haengengeblieben
/// </summary>
public enum TrainingsStatus
{
    Angefordert,
    Laeuft,
    Abgeschlossen,
    Gescheitert,
    Abgebrochen,
    Haengengeblieben
}
