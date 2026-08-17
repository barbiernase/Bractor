using Abstractions;

namespace Domain.Datensatz;

/// <summary>
/// Aggregat <see cref="Datensatz"/> — eine kuratierte, einfrierbare, versionierte Menge
/// gelabelter Bildpaare (Vereinigung mehrerer Such-Ranges + manueller Deltas).
///
/// Entwurf: dynamischer, deduplizierter Korb konkreter Bildpaar-Referenzen.
/// Eingefroren: Mitgliedschaft + Label-Stand + Split zum Einfrier-Zeitpunkt
/// festgeschrieben, immutabel — reproduzierbarer Trainings-Input.
///
/// Id/Version werden vom StatePropertyGenerator ergänzt (nicht hier deklarieren).
/// </summary>
public partial class Datensatz : IState
{
    public string? Name { get; set; }

    public DatensatzStatus Status { get; set; } = DatensatzStatus.Entwurf;

    /// <summary>Der Entwurfs-Korb: konkrete Bildpaar-Referenzen, dedupliziert.</summary>
    public HashSet<Guid> DraftMitglieder { get; } = new();

    /// <summary>Herkunft der aufgenommenen Ranges (Provenienz, Konzept §10).</summary>
    public List<RangeHerkunft> Ranges { get; } = new();

    /// <summary>Split-Konfiguration (Default 70/15/15, optional überschrieben).</summary>
    public SplitKonfig Split { get; set; } = SplitKonfig.Default;

    // ─── Eingefroren ───

    /// <summary>Vergebene Datensatz-Version (0 = noch nicht eingefroren).</summary>
    public int EingefroreneVersion { get; set; }

    /// <summary>Der immutable Mitglieder-Snapshot (Label-Stand + Split je Mitglied).</summary>
    public IReadOnlyList<DatensatzMitglied> EingefroreneMitglieder { get; set; }
        = Array.Empty<DatensatzMitglied>();

    // ─── Helfer ───

    /// <summary>Existiert das Aggregat schon? (Stream hat mindestens ein Event.)</summary>
    public bool Existiert => Version > 0;

    public bool IstEingefroren => Status == DatensatzStatus.Eingefroren;

    public bool IstDraftMitglied(Guid imagePairId) => DraftMitglieder.Contains(imagePairId);

    public int AnzahlDraftMitglieder => DraftMitglieder.Count;
}
