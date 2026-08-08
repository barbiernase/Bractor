namespace Abstractions;

/// <summary>
/// Implementiert vom Store/Ziel einer Projektion. Erlaubt dem Rebuild-Koordinator,
/// das Ziel vor dem Neuaufbau zu leeren.
///
/// Per Spec 7.7 ist das Zurücksetzen/Leeren eine STORE-Operation, kein neues
/// Framework-Konzept. Dieses Interface benennt sie nur, damit der Koordinator sie
/// einheitlich auslösen kann — es schreibt nicht vor, WIE geleert wird.
///
/// Grundsatz: Es wird NIE in einen befüllten Zustand replayt. Vor jedem Rebuild
/// gilt: erst leeren (hier), dann Marken zurücksetzen (IProjectionTracker.Reset…),
/// dann ab -1 neu lesen (Adapter, Phase 2).
/// </summary>
public interface IRebuildableProjection
{
    /// <summary>Leert das gesamte Ziel dieser Projektion (vollständiger Rebuild).</summary>
    Task ClearAsync(CancellationToken ct);

    /// <summary>Leert das Ziel für genau einen Stream (Einzel-Stream-Rebuild).</summary>
    Task ClearAsync(Guid streamId, CancellationToken ct);
}

/// <summary>
/// Koordiniert den vollständigen Replay einer Projektion. Die IMPLEMENTIERUNG
/// landet in Phase 2, weil sie die Read-und-Dispatch-Schleife des Adapters
/// wiederverwendet — Replay IST der Adapter-Pfad mit Fortschritt = -1.
///
/// Ablauf von RebuildAsync (dokumentiert, damit die Mechanik jetzt vollständig
/// festliegt und in Phase 2 nur noch verdrahtet wird):
///   1. IRebuildableProjection.ClearAsync()         — Ziel leeren
///   2. IProjectionTracker.ResetAllAsync()          — Marken auf -1
///   3. Für jeden Stream der Projektion: ab Version 0 lesen (ReadStreamAsync)
///      und jedes Event über den generierten Dispatch anwenden — exakt der
///      normale Adapter-Pfad, nur mit garantiert leerem Ausgangszustand.
///
/// WICHTIG — Replay-Grenze (Architektur-Entscheidung, festgeklopft in Phase 3):
/// Replay ist PROJEKTIONS-lokal. Handler, die Commands yielden (Reaktionen) oder
/// Prozesse starten, werden NICHT blind replayt — sonst würden geldbewegende
/// Commands neu gefeuert bzw. historische Prozesse neu angetrieben. Der Rebuilder
/// re-dispatcht ausschließlich die Repo-Write-Effekte einer Projektion; reaktive
/// Ausgänge werden beim Replay unterdrückt.
/// </summary>
public interface IProjectionRebuilder
{
    /// <summary>Vollständiger Neuaufbau einer Projektion aus dem Log.</summary>
    Task RebuildAsync(string projectionId, CancellationToken ct);

    /// <summary>Neuaufbau eines einzelnen Streams innerhalb einer Projektion.</summary>
    Task RebuildStreamAsync(string projectionId, Guid streamId, CancellationToken ct);
}
