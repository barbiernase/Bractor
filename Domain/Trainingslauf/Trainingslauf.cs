using Abstractions;

namespace Domain.Trainingslauf;

/// <summary>
/// Aggregat <see cref="Trainingslauf"/> — ein Trainings-Job (Konzept §4): angefordert → läuft →
/// (Fortschritt je Epoche) → abgeschlossen/gescheitert/abgebrochen/hängengeblieben.
///
/// Referenziert eine EINGEFRORENE Datensatz-Version (immutabler Input → reproduzierbar). Der
/// Fortschritt fließt event-sourced zurück (Python meldet, der Decider foldet) → die Live-Kurve
/// streamt von selbst ins Dashboard.
///
/// Id/Version werden vom StatePropertyGenerator ergänzt (nicht hier deklarieren).
/// </summary>
public partial class Trainingslauf : IState
{
    public Guid DatensatzId { get; set; }
    public int DatensatzVersion { get; set; }
    public Hyperparameter? Hyperparameter { get; set; }

    public TrainingsStatus Status { get; set; } = TrainingsStatus.Angefordert;

    public int AktuelleEpoche { get; set; }
    public List<EpochenMetrik> MetrikHistorie { get; } = new();

    public string? ModellPfad { get; set; }
    public Endmetriken? Endmetriken { get; set; }
    public string? Fehlergrund { get; set; }

    // ─── Helfer ───

    public bool Existiert => Version > 0;

    /// <summary>Angefordert oder Läuft — ein noch nicht terminaler Lauf.</summary>
    public bool IstAktiv => Status is TrainingsStatus.Angefordert or TrainingsStatus.Laeuft;

    /// <summary>Ein terminaler Zustand — keine weiteren Übergänge.</summary>
    public bool IstBeendet => Status is
        TrainingsStatus.Abgeschlossen or
        TrainingsStatus.Gescheitert or
        TrainingsStatus.Abgebrochen or
        TrainingsStatus.Haengengeblieben;
}
