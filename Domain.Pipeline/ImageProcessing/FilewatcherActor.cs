using Abstractions;
using Microsoft.Extensions.Logging;
using Proto;
using Proto.Cluster;

namespace Domain.Pipeline.ImageProcessing;

/// <summary>
/// Überwacht ein Samba-Share per Polling und sendet DateiErkannt-Trigger an eine Pipeline.
///
/// Nativer Proto.Actor — nicht generiert, nicht über PubSub.
/// Kommuniziert direkt mit dem Pipeline-Actor via Cluster.RequestAsync.
///
/// WICHTIG: Dieser Actor hat KEIN Domain-Wissen!
///   - Kein Dateinamen-Parsing
///   - Keine PairId, keine BildVersion
///   - Keine Kenntnis über die Dateistruktur
///
/// Er erkennt lediglich: "Diesen Dateinamen habe ich noch nicht gesehen"
/// und feuert einen DateiErkannt-Trigger an die Pipeline.
///
/// Design-Entscheidungen:
///   - Kein FileSystemWatcher: inotify funktioniert nicht auf CIFS/Samba-Mounts
///   - Polling jede Sekunde via context.ReenterAfter (mailbox-safe, kein Task.Run)
///   - HashSet statt Fingerprint: Dateinamen sind global eindeutig (kein Überschreiben)
///   - HashSet-Cap 2×Ringpuffergröße: verhindert unbegrenztes Wachstum,
///     behält gelöschte Einträge als Flicker-Schutz
///   - Stabilitäts-Check: Datei muss ein Poll-Intervall lang gleiche Größe haben,
///     damit unvollständige Transfers (SCP, rsync) nicht verarbeitet werden
///   - Kein lock, kein Task.Run: alles läuft sequentiell in der Actor-Mailbox
/// </summary>
public class FileWatcherActor : IActor
{
    private readonly string _watchPath;
    private readonly string _targetPipelineId;
    private readonly Cluster _cluster;
    private readonly ILogger _logger;
    private readonly int _maxSeenEntries;

    private readonly HashSet<string> _seen = new();
    private readonly Dictionary<string, long> _pending = new();
    private bool _stopped;

    /// <summary>Interne Tick-Message — löst einen Verzeichnis-Scan aus.</summary>
    private record PollTick;

    public FileWatcherActor(
        string watchPath,
        string targetPipelineId,
        Cluster cluster,
        ILogger logger,
        int ringBufferSize = 100)
    {
        _watchPath = watchPath;
        _targetPipelineId = targetPipelineId;
        _cluster = cluster;
        _logger = logger;
        _maxSeenEntries = ringBufferSize * 2;
    }

    public async Task ReceiveAsync(IContext context)
    {
        switch (context.Message)
        {
            case Started:
                Start(context);
                break;

            case PollTick:
                await Poll(context);
                ScheduleNextTick(context);
                break;

            case Stopping:
                _stopped = true;
                _logger.LogInformation("FileWatcher stopped ({Path})", _watchPath);
                break;
        }
    }

    // ═══════════════════════════════════════════════════════
    // Started — Seed + ersten Tick schedulen
    // ═══════════════════════════════════════════════════════

    private void Start(IContext context)
    {
        if (!Directory.Exists(_watchPath))
        {
            _logger.LogWarning(
                "FileWatcher: Pfad existiert nicht, wird angelegt: {Path}", _watchPath);
            Directory.CreateDirectory(_watchPath);
        }

        // Bestandsdateien als "schon gesehen" markieren
        try
        {
            foreach (var file in Directory.EnumerateFiles(_watchPath))
            {
                _seen.Add(Path.GetFileName(file));
            }
            _logger.LogInformation(
                "FileWatcher: Seed mit {Count} bestehenden Dateien", _seen.Count);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "FileWatcher: Seed fehlgeschlagen für {Path}", _watchPath);
        }

        _logger.LogInformation(
            "FileWatcher gestartet auf {Path} → Pipeline {PipelineId} (Modus: Polling 1s, Cap: {Cap})",
            _watchPath, _targetPipelineId, _maxSeenEntries);

        // Ersten Tick anstoßen
        ScheduleNextTick(context);
    }

    // ═══════════════════════════════════════════════════════
    // Tick-Scheduling via ReenterAfter
    // ═══════════════════════════════════════════════════════

    private void ScheduleNextTick(IContext context)
    {
        if (_stopped) return;

        context.ReenterAfter(Task.Delay(1000), () =>
        {
            context.Send(context.Self, new PollTick());
        });
    }

    // ═══════════════════════════════════════════════════════
    // PollTick — Verzeichnis scannen, neue Dateien triggern
    // ═══════════════════════════════════════════════════════

    private async Task Poll(IContext context)
    {
        if (!Directory.Exists(_watchPath))
            return;

        try
        {
            var identity = ClusterIdentity.Create(_targetPipelineId, "Pipeline");

            foreach (var fullPath in Directory.EnumerateFiles(_watchPath))
            {
                var name = Path.GetFileName(fullPath);

                if (_seen.Contains(name))
                    continue;

                var currentSize = new FileInfo(fullPath).Length;

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

                _logger.LogInformation(
                    "FileWatcher: Neue Datei erkannt {File}", name);

                var ack = await _cluster.RequestAsync<PipelineAck>(
                    identity,
                    new DateiErkannt(fullPath, name, currentSize),
                    context.CancellationToken);

                if (ack?.Accepted == true)
                    _logger.LogInformation(
                        "Pipeline hat DateiErkannt angenommen: {File}", name);
                else
                    _logger.LogWarning(
                        "Pipeline hat DateiErkannt NICHT angenommen: {File}", name);
            }

            // Eviction: wenn HashSet über Cap, nicht mehr vorhandene Einträge rauswerfen
            if (_seen.Count > _maxSeenEntries)
            {
                var currentFiles = Directory.EnumerateFiles(_watchPath)
                    .Select(Path.GetFileName)
                    .ToHashSet()!;

                _seen.RemoveWhere(name => !currentFiles.Contains(name));

                _logger.LogInformation(
                    "FileWatcher: Eviction durchgeführt, {Count} Einträge verbleiben", _seen.Count);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "FileWatcher: Polling-Fehler auf {Path}", _watchPath);
        }
    }
}