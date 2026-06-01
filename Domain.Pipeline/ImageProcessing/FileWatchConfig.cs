namespace Domain.Pipeline.ImageProcessing;

/// <summary>
/// Konfiguration für die FileWatchPipeline.
///
/// WatchPath: Verzeichnis das überwacht wird (z.B. Samba-Share).
/// PollInterval: Wie oft gescannt wird. Default 1 Sekunde.
/// RingBufferSize: HashSet-Cap = 2 × RingBufferSize. Verhindert unbegrenztes
///   Wachstum des _seen-Sets. Default 100.
/// </summary>
public record FileWatchConfig(
    string WatchPath,
    TimeSpan PollInterval,
    int RingBufferSize = 100);
