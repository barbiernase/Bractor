namespace Abstractions;

/// <summary>
/// Marken-Interface für REPLAYBARE Konsumenten (Projektionen): der durable Effekt ist ein Read-Model,
/// co-committbar mit dem Cursor, Rebuild/Reset erlaubt. Fachlich das heutige
/// <see cref="IProjectionTracker"/> (inkl. <c>Reset*</c>) — hier als benanntes Profil.
///
/// Der Sinn ist der Compile-Zeit-Schnitt (Achse B, P4): ein EMITTIERENDER Konsument
/// (Reaktion/Pipeline-Event/Prozess) bekommt <see cref="IEmittentenCursor"/> — das trägt bewusst
/// KEIN <c>Reset</c>. Zwei getrennte Interfaces statt eines → blindes Replayen geld-bewegender
/// Emittenten ist compile-zeit-unmöglich (CLAUDE.md: „Command-yieldende Handler werden NICHT blind
/// replayt").
/// </summary>
public interface IReplaybarerTracker : IProjectionTracker
{
}
