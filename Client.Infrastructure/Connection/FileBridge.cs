using Client.Infrastructure.Abstractions;
using Client.Infrastructure.Bus;

namespace Client.Infrastructure.Connection;

/// <summary>
/// Brücke zwischen Bus-LoadFile-Requests und dem Host-Dateisystem.
///
/// Dritte Brücke neben ConnectionModule (Commands) und QueryBridge (Queries).
/// Alle drei folgen identischem Pattern:
///   bus.SubscribeAsync → Verarbeitung → bus.PostToSyncContext(Publish)
///
/// Die FileBridge lädt keine Dateiinhalte selbst. Sie delegiert die
/// Pfadauflösung an einen injizierten IFilePathResolver. Der Resolver
/// ist host-spezifisch — er entscheidet, wie der Client die Datei
/// konsumiert (URL, lokaler Pfad, Data-URI).
///
/// Kein Reflection — subscribt auf konkreten Typ LoadFile.
/// Kein Domain-Wissen — kennt keine Bilder, keine ImagePairs, keine Dateitypen.
/// </summary>
public class FileBridge : IAsyncDisposable
{
    private readonly IFilePathResolver _resolver;
    private readonly List<IDisposable> _subscriptions = new();
    private readonly HashSet<string> _inflight = new();
    private ClientBus? _bus;
    private CancellationTokenSource? _cts;

    public FileBridge(IFilePathResolver resolver)
    {
        _resolver = resolver;
    }

    // ═══════════════════════════════════════════════════════════
    // ACTIVATE — subscribt auf LoadFile (kein Reflection)
    // ═══════════════════════════════════════════════════════════

    /// <summary>
    /// Aktiviert die FileBridge: subscribt async auf LoadFile.
    ///
    /// Wird vom ClientStartupService nach subscribeAll und registerQueries,
    /// aber vor ConnectAsync aufgerufen.
    /// </summary>
    public void Activate(ClientBus bus)
    {
        _bus = bus;
        _cts = new CancellationTokenSource();

        var sub = bus.SubscribeAsync<LoadFile>(
            async (request, ctx) =>
        {
            await HandleLoadAsync(request);
        });
        _subscriptions.Add(sub);
    }

    // ═══════════════════════════════════════════════════════════
    // HANDLER — Pfadauflösung via IFilePathResolver
    // ═══════════════════════════════════════════════════════════

    /// <summary>
    /// Verarbeitet einen LoadFile-Request.
    ///
    /// Duplikat-Schutz via _inflight-Set: Wenn derselbe Pfad bereits
    /// aufgelöst wird (z.B. mehrere UI-Komponenten fragen gleichzeitig),
    /// wird der zweite Request ignoriert. Das erste Ergebnis wird über
    /// den Bus an alle Subscriber verteilt.
    /// </summary>
    private async Task HandleLoadAsync(LoadFile request)
    {
        Console.WriteLine($"[FileBridge] LoadFile received: '{request.Path}'");
    
        lock (_inflight)
        {
            if (!_inflight.Add(request.Path))
            {
                Console.WriteLine($"[FileBridge] SKIP (inflight): '{request.Path}'");
                return;
            }
        }

        try
        {
            var resolved = await _resolver.ResolveAsync(
                request.Path, _cts!.Token);

            Console.WriteLine($"[FileBridge] ✔ Resolved: '{request.Path}' → '{resolved.AccessUrl}'");
        
            _bus!.PostToSyncContext(() =>
                _bus.Publish(new FileLoaded(
                    request.Path,
                    resolved.AccessUrl,
                    resolved.ContentType,
                    resolved.SizeBytes)));
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[FileBridge] ✗ FAILED: '{request.Path}' → {ex.Message}");
        
            _bus!.PostToSyncContext(() =>
                _bus.Publish(new FileLoadFailed(
                    request.Path, ex.Message)));
        }
        finally
        {
            lock (_inflight)
            {
                _inflight.Remove(request.Path);
            }
        }
    }

    // ═══════════════════════════════════════════════════════════
    // DISPOSE
    // ═══════════════════════════════════════════════════════════

    public ValueTask DisposeAsync()
    {
        _cts?.Cancel();
        _cts?.Dispose();
        foreach (var sub in _subscriptions)
            sub.Dispose();
        _subscriptions.Clear();
        return ValueTask.CompletedTask;
    }
}