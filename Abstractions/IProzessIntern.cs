namespace Abstractions;

/// <summary>
/// Marker für die INTERNEN Typen der Prozess-Maschinerie (generierte <c>{Prozess}_StarteProzess</c>,
/// <c>{Prozess}_Melde…</c>-Quittungen und Prozess-Events). Sie laufen — wie Signale
/// (<see cref="IStateChangeSignal"/>) — nur intern (single-node in-process bzw. Event-Store-JSON),
/// NICHT über die externe gRPC/Proto-Grenze. Der <c>DtoMapperGenerator</c> schließt sie deshalb aus.
///
/// Wichtig: der Ausschluss betrifft NUR das Proto-Mapping. Für Marten/Replay bleiben die (jetzt pro
/// Prozess EINDEUTIG benannten) Event-Typen in der Typ-Registry — deshalb funktioniert die
/// namensbasierte Event-Speicherung auch mit mehreren Prozessen.
/// (Cross-node-Serialisierung der Prozess-Typen ist ein separates, ohnehin offenes Thema — Phase 4c/4d.)
/// </summary>
public interface IProzessIntern { }
