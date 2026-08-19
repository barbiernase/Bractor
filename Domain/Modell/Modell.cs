using Abstractions;

namespace Domain.Modell;

/// <summary>
/// Aggregat <see cref="Modell"/> — ein trainiertes Modell-Artefakt (Konzept
/// konzept-training-und-datensatz §5). Schließt den Kreis Datensatz → Training → Modell →
/// Inferenz: „aktiv setzen" macht dieses Modell zum, das die Live-Klassifikation nutzt.
///
/// Aktivierung ist ein EVENT (<see cref="ModellAktiviert"/>), kein Aggregat-Status — welches
/// Modell aktuell aktiv ist, hält die Read-Seite (die jüngste Aktivierung gewinnt). So bleibt
/// das Aggregat unabhängig (kein Cross-Aggregat-Deaktivieren nötig).
///
/// Id/Version ergänzt der StatePropertyGenerator (nicht hier deklarieren).
/// </summary>
public partial class Modell : IState
{
    public Guid TrainingslaufId { get; set; }
    public Guid DatensatzId { get; set; }
    public int DatensatzVersion { get; set; }
    public string? Name { get; set; }
    public string? Pfad { get; set; }
    public ModellMetriken? Metriken { get; set; }

    public ModellStatus Status { get; set; } = ModellStatus.Registriert;

    // ─── Helfer ───
    public bool Existiert => Version > 0;
    public bool IstArchiviert => Status == ModellStatus.Archiviert;
}
