using System.Collections.Concurrent;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace Infrastructure.Quellen;

/// <summary>Zustand je Quelle (ok / gestört mit Grund) — für <c>/health</c> und später die Kopfzeile der Werkbank.</summary>
public sealed class QuellenZustand
{
    private readonly ConcurrentDictionary<string, (bool Gestoert, string? Grund, DateTimeOffset Seit)> _je = new(StringComparer.Ordinal);

    public void Ok(string quelle) => _je.AddOrUpdate(quelle, _ => (false, null, DateTimeOffset.UtcNow), (_, alt) => alt.Gestoert ? (false, null, DateTimeOffset.UtcNow) : alt);
    public void Gestoert(string quelle, string grund) => _je.AddOrUpdate(quelle, _ => (true, grund, DateTimeOffset.UtcNow), (_, alt) => alt.Gestoert ? alt with { Grund = grund } : (true, grund, DateTimeOffset.UtcNow));
    public IReadOnlyDictionary<string, (bool Gestoert, string? Grund, DateTimeOffset Seit)> Alle => _je;

    private readonly ConcurrentDictionary<string, long> _meldungen = new(StringComparer.Ordinal);
    /// <summary>Eine Nachricht dieser Quelle steht im Log (Zähler für Puls und Prüfungen).</summary>
    public void Gemeldet(string quelle) => _meldungen.AddOrUpdate(quelle, 1, (_, n) => n + 1);
    /// <summary>Wie viele Nachrichten diese Quelle seit dem Start dieses Knotens gemeldet hat.</summary>
    public long Meldungen(string quelle) => _meldungen.TryGetValue(quelle, out var n) ? n : 0;
}

/// <summary>Eine gestörte Quelle macht den Host „degraded" (nichts geht verloren — der nächste Scan holt alles nach).</summary>
public sealed class QuellenHealthCheck(QuellenZustand zustand) : IHealthCheck
{
    public Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        var gestoert = zustand.Alle.Where(kv => kv.Value.Gestoert).ToList();
        var daten = zustand.Alle.ToDictionary(kv => kv.Key, kv => (object)(kv.Value.Gestoert ? "gestört: " + kv.Value.Grund : "ok"));
        return Task.FromResult(gestoert.Count == 0
            ? HealthCheckResult.Healthy("Quellen ok", daten)
            : HealthCheckResult.Degraded("Quelle gestört: " + string.Join(", ", gestoert.Select(g => g.Key)), data: daten));
    }
}
