namespace Domain.Modell;

/// <summary>
/// Lebenszyklus eines Modell-Artefakts (Konzept konzept-training-und-datensatz §5).
///
///   Registriert ──(SetzeAktiv, mehrfach)──► Registriert   (Aktivierung ist ein Fakt/Event;
///        └─────────────────────────► Archiviert            „welches ist aktiv" hält die Read-Seite)
/// </summary>
public enum ModellStatus
{
    Registriert,
    Archiviert
}
