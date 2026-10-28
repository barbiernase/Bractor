using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Proto;
using Proto.Cluster;

namespace Infrastructure.Startup;

/// <summary>
/// Startet den Cluster beim Application-Start und wartet, bis er bereit ist (<see cref="Bereitschaft"/>): erst dann starten die übrigen Dienste
/// (Takt, Eingänge) — nach dem harten Ende eines Knotens legt der Cluster sonst Aktivierungen auf den toten.
/// </summary>
public class ClusterStartupService : IHostedService
{
    private readonly ActorSystem _actorSystem;
    private readonly ILogger<ClusterStartupService>? _logger;

    public ClusterStartupService(ActorSystem actorSystem, ILogger<ClusterStartupService>? logger = null)
    {
        _actorSystem = actorSystem;
        _logger = logger;
    }

    public async Task StartAsync(CancellationToken ct)
    {
        Console.WriteLine();
        Console.WriteLine("=== Cluster Startup ===");
        Console.WriteLine("  → Starte Cluster-Member...");
        
        // ★ Der 30s-Schutz wird jetzt WIRKLICH erzwungen: früher wurde ein linkedCts gebaut, aber
        //   StartMemberAsync() bekam das Token nie übergeben → ein hängender Join blockierte ewig.
        //   Task.WhenAny ist API-agnostisch (unabhängig davon, ob StartMemberAsync ein Token nimmt).
        var startTask = _actorSystem.Cluster().StartMemberAsync();
        var completed = await Task.WhenAny(startTask, Task.Delay(TimeSpan.FromSeconds(30), ct));
        if (completed != startTask)
        {
            Console.WriteLine("  ✗ TIMEOUT: Cluster-Start >30s");
            throw new TimeoutException("Cluster startup timed out");
        }
        await startTask;   // Exceptions des Starts beobachten

        var members = _actorSystem.Cluster().MemberList.GetAllMembers();
        Console.WriteLine($"  ✓ Cluster gestartet ({members.Length} Member)");
        if (await Bereitschaft.WarteAsync(_actorSystem.Cluster(), _logger, ct))
            Console.WriteLine("  ✓ Cluster bereit (jede Probe antwortet)");
        Console.WriteLine();
    }

    public async Task StopAsync(CancellationToken ct)
    {
        Console.WriteLine();
        Console.WriteLine("=== Cluster Shutdown ===");
        await _actorSystem.Cluster().ShutdownAsync();
        Console.WriteLine("  ✓ Cluster gestoppt");
        Console.WriteLine();
    }
}