using Abstractions;
using Microsoft.Extensions.Logging;

namespace Domain.Pipeline.ImageProcessing;

/// <summary>
/// Überwacht ein Samba-Share per Polling und yieldet DateiErkannt-Trigger.
///
/// Ersetzt den nativen FileWatcherActor. Läuft jetzt als Pipeline:
///   - Kein eigener Actor-Code — der PipelineActorGenerator erzeugt den Actor
///   - Self-Messaging als typisierter Ausgang Selbst&lt;PollTick&gt; (mailbox-safe, ReenterAfter im Actor)
///   - Output ist ein IPipelineTrigger (DateiErkannt), wird vom Generator
///     über TriggerToPipelineId an die ImageProcessingPipeline geroutet
///
/// WICHTIG: Dieser Handler hat KEIN Domain-Wissen!
///   - Kein Dateinamen-Parsing
///   - Keine PairId, keine BildVersion
///   - Keine Kenntnis über die Dateistruktur
///
/// Er erkennt lediglich: "Diesen Dateinamen habe ich noch nicht gesehen"
/// und yieldet einen DateiErkannt-Trigger.
///
/// Design-Entscheidungen (unverändert zum alten FileWatcherActor):
///   - Kein FileSystemWatcher: inotify funktioniert nicht auf CIFS/Samba-Mounts
///   - Polling per Self-Tick (der erste über Handle(PipelineGestartet), danach je Tick der nächste)
///   - HashSet statt Fingerprint: Dateinamen sind global eindeutig
///   - HashSet-Cap 2×Ringpuffergröße: verhindert unbegrenztes Wachstum,
///     behält gelöschte Einträge als Flicker-Schutz
///   - Stabilitäts-Check: Datei muss ein Poll-Intervall lang gleiche Größe haben
///   - Kein lock: alles läuft sequentiell in der Actor-Mailbox
/// </summary>
public partial class FileWatchPipeline : IPipelineHandler
{
    /// <summary>
    /// Interne Tick-Message — löst einen Verzeichnis-Scan aus.
    /// Public weil die Handle-Methode public ist (vom Generator gefordert).
    /// Wird nur über einen Selbst&lt;PollTick&gt;-Ausgang geplant — nie von außen.
    /// </summary>
    public record PollTick : IPipelineSelfMessage;

    private readonly FileWatchConfig _config;
    private readonly ILogger<FileWatchPipeline> _logger;
    private readonly HashSet<string> _seen = new();
    private readonly Dictionary<string, long> _pending = new();
    private readonly int _maxSeenEntries;

    public FileWatchPipeline(FileWatchConfig config, ILogger<FileWatchPipeline> logger)
    {
        _config = config;
        _logger = logger;
        _maxSeenEntries = config.RingBufferSize * 2;
    }

    public string PipelineId => "file-watch";

    /// <summary>Start: Bestand als „schon gesehen" markieren und den ersten Tick planen.</summary>
    public IEnumerable<OneOf<Selbst<PollTick>>> Handle(PipelineGestartet start, PipelineContext ctx)
    {
        if (!Directory.Exists(_config.WatchPath))
        {
            _logger.LogWarning(
                "FileWatchPipeline: Pfad existiert nicht, wird angelegt: {Path}", _config.WatchPath);
            Directory.CreateDirectory(_config.WatchPath);
        }

        // Bestandsdateien als "schon gesehen" markieren
        try
        {
            foreach (var file in Directory.EnumerateFiles(_config.WatchPath))
                _seen.Add(Path.GetFileName(file));

            _logger.LogInformation(
                "FileWatchPipeline gestartet auf {Path} (Seed {Count}, Cap {Cap})",
                _config.WatchPath, _seen.Count, _maxSeenEntries);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex,
                "FileWatchPipeline: Seed fehlgeschlagen für {Path}", _config.WatchPath);
        }

        // Ersten Tick anstoßen
        yield return NaechsterTick();
    }

    private Selbst<PollTick> NaechsterTick() => Selbst.In(new PollTick(), _config.PollInterval);

    public async IAsyncEnumerable<OneOf<DateiErkannt, Selbst<PollTick>>> Handle(PollTick _, PipelineContext ctx)
    {
        if (!Directory.Exists(_config.WatchPath))
        {
            yield return NaechsterTick();
            yield break;
        }

        string[] files;
        try
        {
            files = Directory.GetFiles(_config.WatchPath);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "FileWatchPipeline: Polling-Fehler auf {Path}", _config.WatchPath);
            files = null!;
        }
        if (files == null)
        {
            yield return NaechsterTick();
            yield break;
        }

        foreach (var fullPath in files)
        {
            var name = Path.GetFileName(fullPath);

            if (_seen.Contains(name))
                continue;

            long currentSize;
            try
            {
                currentSize = new FileInfo(fullPath).Length;
            }
            catch
            {
                // Datei zwischenzeitlich gelöscht/gesperrt — nächster Tick
                continue;
            }

            // Neu erkannt → Größe merken, beim nächsten Tick prüfen
            if (!_pending.TryGetValue(name, out var lastSize))
            {
                _pending[name] = currentSize;
                continue;
            }

            // Größe hat sich geändert → Datei wird noch geschrieben
            if (currentSize != lastSize)
            {
                _pending[name] = currentSize;
                continue;
            }

            // Größe stabil seit letztem Tick → Datei ist komplett
            _pending.Remove(name);
            _seen.Add(name);

            _logger.LogInformation("FileWatchPipeline: Neue Datei erkannt {File}", name);
            yield return new DateiErkannt(fullPath, name, currentSize);
        }

        // Eviction: wenn HashSet über Cap, nicht mehr vorhandene Einträge rauswerfen
        if (_seen.Count > _maxSeenEntries)
        {
            var currentFiles = Directory.GetFiles(_config.WatchPath)
                .Select(Path.GetFileName)
                .ToHashSet()!;

            _seen.RemoveWhere(n => !currentFiles.Contains(n));

            _logger.LogInformation(
                "FileWatchPipeline: Eviction durchgeführt, {Count} Einträge verbleiben", _seen.Count);
        }

        // Nächsten Tick einplanen
        yield return NaechsterTick();

        await Task.CompletedTask;
    }
}
