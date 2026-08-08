using Abstractions;

namespace Domain.Reaktion;

/// <summary>
/// Demo-Empfänger für Phase 3 (Reaktionen): ein Fremd-Aggregat, das einen
/// Reaktions-Command entgegennimmt und ihn GENAU EINMAL wirksam werden lässt.
///
/// Die Deduplizierung liegt bewusst hier beim Empfänger (Spec 9.3): Der Decider
/// erkennt eine bereits verarbeitete Reaktions-Id und produziert dann keine Events
/// (Noop) → ein doppelt gesendeter Command verpufft.
///
/// Hinweis: `VerarbeiteteReaktionen` wächst unbegrenzt — für die Demo bewusst simpel.
/// Produktiv nutzt man ein fachliches Korrelationsfeld bzw. ein begrenztes Fenster.
/// </summary>
public partial class Reaktionsempfaenger : IState
{
    // ── Framework-Pflichtfelder (Id, Version) werden generiert ──

    /// <summary>Anzahl der wirksam gewordenen Reaktionen.</summary>
    public long Wirkungen { get; set; }

    /// <summary>Bereits verarbeitete Reaktions-Ids (aus den Events gefaltet).</summary>
    public HashSet<Guid> VerarbeiteteReaktionen { get; } = new();
}
