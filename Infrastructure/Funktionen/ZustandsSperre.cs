using System.Security.Cryptography;
using System.Text;
using Marten;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Npgsql;

namespace Infrastructure.Funktionen;

/// <summary>
/// Die SPERRE einer Funktion mit Zustand (docs/04 §4.4): ihr <c>IZustand</c> ist ein Cache der Aktivierung — damit er alles sieht, was zur Funktion
/// gehört, rechnet sie im Cluster auf genau einer Aktivierung. Jeder Knoten versucht, je solche Funktion einen Postgres-Advisory-Lock zu nehmen
/// (Sitzungs-Lock auf einer eigenen Verbindung, Schlüssel aus dem Namen); wer ihn hält, meldet „ein Kind“ (<see cref="Beobachte"/>), die Wurzel
/// legt den Parent an und der Knoten hält die Funktion im Gossip-Zustand. Reißt die Verbindung, ist der Lock frei: der Knoten meldet „keins“, ein
/// anderer nimmt ihn. Kurz können zwei Aktivierungen rechnen (bis der Verlust bemerkt ist) — der Zustand ist ein Cache, keine Wahrheit.
/// </summary>
public sealed class ZustandsSperre(IDocumentStore store, IReadOnlyList<string> funktionen, ILogger<ZustandsSperre>? logger = null) : BackgroundService
{
    /// <summary>So oft wird ein freier Lock versucht und ein gehaltener geprüft.</summary>
    public static readonly TimeSpan Takt = TimeSpan.FromSeconds(2);

    private sealed class Halter
    {
        public NpgsqlConnection? Verbindung;
    }

    private readonly Dictionary<string, Halter> _halter = funktionen.ToDictionary(f => f, _ => new Halter(), StringComparer.Ordinal);
    private Action<string, int>? _beobachter;

    /// <summary>Die Funktionen mit Zustand (im Host), um deren Sperre dieser Knoten sich bemüht.</summary>
    public IReadOnlyList<string> Funktionen => funktionen;

    /// <summary>Die Funktionen mit Zustand, deren Sperre dieser Knoten gerade hält.</summary>
    public IReadOnlyList<string> Gehalten => _halter.Where(h => h.Value.Verbindung is not null).Select(h => h.Key).ToList();

    /// <summary>Meldet jeden Wechsel (Funktion, 1 = gehalten, 0 = verloren) — sofort für die schon gehaltenen.</summary>
    public void Beobachte(Action<string, int> beobachter)
    {
        _beobachter = beobachter;
        foreach (var f in Gehalten) beobachter(f, 1);
    }

    /// <summary>Der Schlüssel des Advisory-Locks einer Funktion: aus ihrem Namen (gleich auf jedem Knoten).</summary>
    public static long Schluessel(string funktion) =>
        BitConverter.ToInt64(MD5.HashData(Encoding.UTF8.GetBytes("cqrs-funktion:" + funktion)), 0);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (_halter.Count == 0) return;
        try
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                foreach (var (funktion, h) in _halter) await PruefeAsync(funktion, h, stoppingToken);
                await Task.Delay(Takt, stoppingToken);
            }
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { }
        finally
        {
            foreach (var h in _halter.Values) await GibFreiAsync(h);
        }
    }

    private async Task PruefeAsync(string funktion, Halter h, CancellationToken ct)
    {
        try
        {
            if (h.Verbindung is { } v)
            {
                await using var probe = new NpgsqlCommand("select 1", v);
                await probe.ExecuteScalarAsync(ct);
                return;
            }
            var neu = (NpgsqlConnection)store.Storage.Database.CreateConnection();
            await neu.OpenAsync(ct);
            await using var cmd = new NpgsqlCommand("select pg_try_advisory_lock($1)", neu);
            cmd.Parameters.Add(new NpgsqlParameter { Value = Schluessel(funktion) });
            if (await cmd.ExecuteScalarAsync(ct) is true)
            {
                h.Verbindung = neu;
                logger?.LogInformation("[Funktionen] {Funktion}: Sperre gehalten — die Funktion mit Zustand rechnet auf diesem Knoten", funktion);
                _beobachter?.Invoke(funktion, 1);
            }
            else await neu.DisposeAsync();
        }
        catch (Exception ex) when (!ct.IsCancellationRequested)
        {
            if (h.Verbindung is null) return;
            logger?.LogWarning(ex, "[Funktionen] {Funktion}: Sperre verloren (Verbindung fort)", funktion);
            await GibFreiAsync(h);
            _beobachter?.Invoke(funktion, 0);
        }
    }

    private static async Task GibFreiAsync(Halter h)
    {
        if (h.Verbindung is not { } v) return;
        h.Verbindung = null;
        try { await v.DisposeAsync(); } catch { /* der Lock fällt mit der Sitzung */ }
    }
}
