using Abstractions;
using Domain.Modell;

namespace Domain.Projections;

// ═══════════════════════════════════════════════════════════════
// MODELL — ein Dokument pro registriertem Modell (Upsert-Projektion)
// ═══════════════════════════════════════════════════════════════

/// <summary>
/// Materialisierter Zustand eines registrierten Modells (Konzept konzept-training-und-datensatz §5).
/// Provenienz (aus welchem Lauf / welcher eingefrorenen Datensatz-Version), Pfad, Metriken, Status.
/// „Welches Modell aktiv ist" hält der Singleton <see cref="AktivesModellReadModel"/>.
/// </summary>
public record ModellReadModel : IReadModel
{
    public Guid Id { get; init; }
    public string? Name { get; init; }
    public string? Pfad { get; init; }

    public Guid TrainingslaufId { get; init; }
    public Guid DatensatzId { get; init; }
    public int DatensatzVersion { get; init; }

    public double Loss { get; init; }
    public double Genauigkeit { get; init; }

    public ModellStatus Status { get; init; }
    public DateTimeOffset RegistriertAm { get; init; }
}

// ═══════════════════════════════════════════════════════════════
// AKTIVES MODELL — Singleton-Zeiger (last-writer-wins)
// ═══════════════════════════════════════════════════════════════

/// <summary>
/// Der Singleton-Zeiger auf das aktuell aktive Modell (Konzept §5: „genau ein aktives Modell").
/// Wird bei jedem <c>ModellAktiviert</c> überschrieben (die jüngste Aktivierung gewinnt). Der
/// Inferenz-Worker/Klassifikator liest hier, welches Modell er nutzen soll.
/// </summary>
public record AktivesModellReadModel : IReadModel
{
    /// <summary>Feste Singleton-Id (ein globaler „Zweck").</summary>
    public const string Singleton = "aktiv";

    public string Id { get; init; } = Singleton;
    public Guid ModellId { get; init; }
    public string? Name { get; init; }
    public string? Pfad { get; init; }
    public DateTimeOffset AktiviertAm { get; init; }
}
