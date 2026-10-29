using Abstractions;

namespace Infrastructure.Projections;

/// <summary>
/// Der Neuaufbau einer Projektion über eine neue Generation (docs/04 §4.3, docs/10 A4) — „Neuaufbau IST der Batch-Konsum
/// ab Position 0“, kein zweiter Codepfad:
///   1. ein leeres Ziel der neuen Generation vorbereiten (<see cref="IProjektionsZiel.NeueGenerationAsync"/>; gelesen wird weiter die alte),
///   2. den Fortschritt aller Partitionen auf Anfang in der neuen Generation (<see cref="IFortschrittsAblage.FortschrittZuruecksetzenAsync"/>; neue
///      Revision — ein noch laufender Leser verliert seinen nächsten Commit, und was er davor schreibt, landet in der alten Generation),
///   3. je Partition einen <see cref="BatchLeser"/> ab 0 laufen lassen — seitenweise, als Strom (nie der ganze Log als Liste),
///   4. umschalten (<see cref="IProjektionsZiel.UmschaltenAsync"/>): ab jetzt wird die neue Generation gelesen.
/// Für Klasse 1 ist Schritt 1 das Leeren (der Sonderfall aus A4), Schritt 4 nichts.
///
/// Replay-Grenze: nur Projektionen haben einen Neuaufbau. Funktionen (ihr Stand ohne Reset) liegen nie auf diesem Pfad — Ausgänge in die Welt
/// werden nie blind replayt.
/// </summary>
public sealed class ProjectionRebuilder
{
    private readonly IEventStoreRepository _eventStore;
    private readonly Konsum _konsum;
    private readonly Func<DateTimeOffset> _jetzt;

    public ProjectionRebuilder(IEventStoreRepository eventStore, Konsum? konsum = null, Func<DateTimeOffset>? jetzt = null)
    {
        _eventStore = eventStore ?? throw new ArgumentNullException(nameof(eventStore));
        _konsum = konsum ?? Konsum.Standard;
        _jetzt = jetzt ?? (() => DateTimeOffset.UtcNow);
    }

    /// <summary>Vollständiger Neuaufbau in eine neue Generation; liefert sie.</summary>
    /// <param name="typen">Typfilter der Seiten (<see cref="BatchLeser.TypenAus(IEnumerable{Type})"/>); null = ohne.</param>
    public async Task<string> RebuildAsync(
        string projektion,
        Projektionsnaht naht,
        IReadOnlyCollection<string>? typen,
        Func<EventEnvelope, CancellationToken, Task> dispatch,
        CancellationToken ct = default)
    {
        var generation = Generationen.Neu(_jetzt());
        await naht.Ziel.NeueGenerationAsync(generation, ct);                                               // 1. leeres Ziel
        await naht.Ablage.FortschrittZuruecksetzenAsync(projektion, _konsum.Partitionen, generation, ct);  // 2. Fortschritt auf Anfang
        for (var p = 0; p < _konsum.Partitionen; p++)                                                      // 3. ab 0, seitenweise
            await new BatchLeser(_eventStore, naht, projektion, p, _konsum, typen, dispatch).LaufAsync(ct);
        await naht.Ziel.UmschaltenAsync(generation, ct);                                                   // 4. umschalten
        return generation;
    }
}
