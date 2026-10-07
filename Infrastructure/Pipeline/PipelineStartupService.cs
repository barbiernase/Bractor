using Abstractions;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Proto;
using Proto.Cluster;

namespace Infrastructure.Pipeline;

/// <summary>
/// Hält jede Pipeline als GENAU EINE Cluster-Aktivierung am Leben.
///
/// Früher spawnte jeder Node jede Pipeline zusätzlich lokal (<c>Root.Spawn</c>) — neben der virtuellen
/// Cluster-Identität, an die Trigger gehen. Folge: zwei Actors auf demselben Singleton-Handler, und im Multi-Node
/// lief eine trigger-lose Pipeline (FileWatch) auf JEDEM Node → jede Datei N-mal erkannt.
///
/// Jetzt gibt es nur die Cluster-Identität (<see cref="PipelineTriggerSender.Identitaet"/>). Dieser Dienst schickt ihr
/// periodisch <see cref="PipelineAktivieren"/>: die erste Nachricht aktiviert sie (→ <c>Started</c> →
/// <c>Handle(PipelineGestartet)</c>), jede weitere quittiert nur. Fällt der Node der Aktivierung weg, aktiviert der
/// nächste Durchlauf irgendeines Nodes sie neu (Self-Ticks und Bestand des Handlers beginnen dann von vorn —
/// die Wahrheit eines persistierten Event-Pfads liegt ohnehin im Log). Alle Nodes senden — idempotent.
///
/// Startup-Reihenfolge: Cluster → PubSub → Pipelines (hier) → Trigger.
/// </summary>
public class PipelineStartupService : IHostedService
{
    /// <summary>Takt der Wieder-Aktivierung (wie der Poll-Backstop: Sicherheit, nicht Geschwindigkeit).</summary>
    private static readonly TimeSpan Intervall = TimeSpan.FromSeconds(30);
    /// <summary>Nach einem Fehlschlag (Cluster bootet noch, Member fehlt) früher erneut versuchen.</summary>
    private static readonly TimeSpan Wiederholung = TimeSpan.FromSeconds(2);
    private static readonly TimeSpan Frist = TimeSpan.FromSeconds(5);

    private readonly ActorSystem _actorSystem;
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<PipelineStartupService>? _logger;
    private CancellationTokenSource? _cts;
    private Task? _loop;

    public PipelineStartupService(
        ActorSystem actorSystem,
        IServiceProvider serviceProvider,
        ILogger<PipelineStartupService>? logger = null)
    {
        _actorSystem = actorSystem;
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    public Task StartAsync(CancellationToken ct)
    {
        var pipelines = GeneratedPipelines.GetPipelineIds(_serviceProvider).ToList();

        Console.WriteLine();
        Console.WriteLine("=== Pipeline Startup ===");
        foreach (var (name, pipelineId) in pipelines)
            Console.WriteLine($"  ✓ {name} (id: {pipelineId}) — Cluster-Aktivierung");
        Console.WriteLine();

        _cts = new CancellationTokenSource();
        _loop = WachAsync(pipelines.Select(p => p.PipelineId).ToList(), _cts.Token);
        return Task.CompletedTask;
    }

    private async Task WachAsync(IReadOnlyList<string> pipelineIds, CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            var allesAktiv = true;
            foreach (var id in pipelineIds)
                allesAktiv &= await AktiviereAsync(id, ct);

            try { await Task.Delay(allesAktiv ? Intervall : Wiederholung, ct); }
            catch (OperationCanceledException) { break; }
        }
    }

    private async Task<bool> AktiviereAsync(string pipelineId, CancellationToken ct)
    {
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        cts.CancelAfter(Frist);
        try
        {
            var ack = await _actorSystem.Cluster().RequestAsync<PipelineAck>(
                PipelineTriggerSender.Identitaet(pipelineId), new PipelineAktivieren(), cts.Token);
            return ack?.Accepted == true;
        }
        catch (Exception ex) when (!ct.IsCancellationRequested)
        {
            _logger?.LogDebug(ex, "[Pipeline:{PipelineId}] Aktivierung unbestätigt — nächster Durchlauf wiederholt", pipelineId);
            return false;
        }
        catch (OperationCanceledException) { return false; }
    }

    public async Task StopAsync(CancellationToken ct)
    {
        _cts?.Cancel();
        if (_loop != null) { try { await _loop; } catch { /* egal */ } }
        // Die Aktivierungen selbst beendet der Cluster-Shutdown (ClusterStartupService).
        Console.WriteLine("=== Pipeline Shutdown: Wächter gestoppt ===");
    }
}
