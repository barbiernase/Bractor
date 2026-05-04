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
        
        try
        {
            using var timeoutCts = new CancellationTokenSource(TimeSpan.FromSeconds(30));
            using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(ct, timeoutCts.Token);
            
            await _actorSystem.Cluster().StartMemberAsync();
            
            var members = _actorSystem.Cluster().MemberList.GetAllMembers();
            Console.WriteLine($"  ✓ Cluster gestartet ({members.Length} Member)");
        }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested)
        {
            Console.WriteLine("  ✗ TIMEOUT: Cluster-Start >30s");
            throw new TimeoutException("Cluster startup timed out");
        }
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