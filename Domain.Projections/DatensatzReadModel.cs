using Abstractions;
using Domain.Datensatz;
using Domain.ImagePair;   // Klassifikation

namespace Domain.Projections;

// ═══════════════════════════════════════════════════════════════
// DATENSATZ — ein Dokument pro Datensatz (Upsert-Projektion)
// ═══════════════════════════════════════════════════════════════

/// <summary>
/// Materialisierter Zustand eines Datensatzes (Konzept §3.5).
///
/// Die Projektion pflegt die Felder, die sie <em>selbst</em> kennt: Name, Status,
/// Mitglieder-Korb (deduplizierte Union), Ranges (Provenienz), Split und die
/// eingefrorene Version. <see cref="Mitglieder"/> hält die konkreten Bildpaar-Referenzen
/// des Entwurfs — Grundlage der live berechneten Klassenbalance / Menschlabel-Quote,
/// die der Reader (M4) durch einen Join gegen die ImagePair-Read-Models bildet (sie
/// sind dynamisch und werden deshalb bewusst NICHT hier materialisiert).
/// </summary>
public record DatensatzReadModel : IReadModel
{
    public Guid Id { get; init; }
    public string? Name { get; init; }
    public DatensatzStatus Status { get; init; }

    /// <summary>Der Entwurfs-Korb: konkrete Bildpaar-Referenzen, dedupliziert.</summary>
    public List<Guid> Mitglieder { get; init; } = new();

    /// <summary>Materialisierte Größe (== <see cref="Mitglieder"/>.Count) — für die Sidebar.</summary>
    public int AnzahlMitglieder { get; init; }

    /// <summary>Herkunft der aufgenommenen Ranges (Provenienz).</summary>
    public List<RangeHerkunft> Ranges { get; init; } = new();

    /// <summary>Split-Konfiguration (Default 70/15/15 oder überschrieben).</summary>
    public SplitKonfig Split { get; init; } = SplitKonfig.Default;

    /// <summary>Eingefrorene Datensatz-Version (0 = Entwurf, noch nicht eingefroren).</summary>
    public int EingefroreneVersion { get; init; }

    public DateTimeOffset LetzteAktualisierung { get; init; }

    public bool IstEingefroren => Status == DatensatzStatus.Eingefroren;
}

// ═══════════════════════════════════════════════════════════════
// SAMPLE — je eingefrorenem Mitglied EINE Zeile (append, queryierbar)
// ═══════════════════════════════════════════════════════════════

/// <summary>
/// Die queryierbare Trainings-Wahrheit (Konzept §3.5, §6): je eingefrorenem Mitglied eine
/// Zeile mit Label-Stand + Split + Bildpfaden <em>zum Einfrier-Zeitpunkt</em>. Immutabel —
/// bespielt aus <c>DatensatzEingefroren</c>. Grundlage von <c>HoleDatensatzSamples</c> (M4).
///
/// Zusammengesetzte Id <c>{DatensatzId}:{Version}:{ImagePairId}</c> — macht das Bespielen
/// idempotent (Doppelverarbeitung upsertet dieselbe Zeile) und erlaubt Paginierung im DB
/// (Filter auf DatensatzId + Version).
/// </summary>
public record DatensatzSampleReadModel : IReadModel
{
    public string Id { get; init; } = "";
    public Guid DatensatzId { get; init; }
    public int Version { get; init; }
    public Guid ImagePairId { get; init; }
    public string Dc0Pfad { get; init; } = "";
    public string Dc2Pfad { get; init; } = "";
    public Klassifikation Label { get; init; }
    public Split Split { get; init; }

    public static string MakeId(Guid datensatzId, int version, Guid imagePairId) =>
        $"{datensatzId:N}:{version}:{imagePairId:N}";
}
