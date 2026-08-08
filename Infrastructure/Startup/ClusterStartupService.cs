using Microsoft.Extensions.Hosting;
using Proto;
using Proto.Cluster;

namespace Infrastructure.Startup;

/// <summary>
/// Startet den Cluster beim Application-Start.
/// </summary>
public class ClusterStartupService : IHostedService
{
    private readonly ActorSystem _actorSystem;

    public ClusterStartupService(ActorSystem actorSystem)
    {
        _actorSystem = actorSystem;
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