using Abstractions;

namespace Domain.Konto;

/// <summary>
/// Ziel-Aggregat des Überweisungs-Prozesses: Saldo, ein reservierter Teilbetrag und ein Gesperrt-Schalter.
/// REINE Domäne — kein <c>Vorgang</c>, keine Dedup-Menge, kein Prozess-Wissen. Idempotenz (at-least-once
/// gesendete Schritte) sichert die Framework-Inbox über die deterministische CommandId, nicht der Fachcode.
/// Framework-Pflichtfelder (Id, Version) werden generiert.
/// </summary>
public partial class Konto : IState
{
    public decimal Saldo { get; set; }
    public decimal Reserviert { get; set; }
    public bool Gesperrt { get; set; }

    /// <summary>Frei verfügbar = Saldo minus bereits Reserviertes.</summary>
    public decimal Verfuegbar => Saldo - Reserviert;
}
