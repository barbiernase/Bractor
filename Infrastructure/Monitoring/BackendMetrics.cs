using Abstractions;

namespace Infrastructure.Monitoring;

/// <summary>
/// Momentaufnahme der Backend-Betriebskennzahlen (Monitoring, Scheibe 1: Zähler). Bewusst schmal: wie voll ist die DLQ, wie viele Actors
/// sind aktiv, wie weit liegt der Konsum zurück — aus den durablen Quellen (DLQ-Read-Store, Partitions-Positionen), nicht aus einem
/// separaten Zähler-Zustand.
/// </summary>
/// <param name="AktiveActors">Lokal aktive Actors dieses Knotens (Aggregate, Leser, Funktionen, Infrastruktur) — fällt nach der
/// Passivierung (PF-5) zurück.</param>
/// <param name="KonsumRueckstand">Batch-Konsum: relevante Events jenseits der Positionen aller Partitionen aller Projektionen.</param>
/// <param name="GeparkteStreams">Batch-Konsum: geparkte Streams (Dispatch wirft dauerhaft) über alle Projektionen.</param>
public sealed record BackendMetrik(long DlqEinträge, int AktiveActors = 0, long KonsumRueckstand = 0, int GeparkteStreams = 0);

/// <summary>Liest die aktuellen Backend-Kennzahlen (für HealthCheck + Monitoring-Endpoint).</summary>
public interface IBackendMetrics
{
    Task<BackendMetrik> ErfasseAsync(CancellationToken ct = default);
}

/// <summary>
/// Aggregiert die Kennzahlen aus den bestehenden durablen Quellen: die DLQ-Zählung aus dem <see cref="IDeadLetterReadStore"/> und den
/// Rückstand des Batch-Konsums. Reiner Read-Pfad, keine Seiteneffekte.
/// </summary>
public sealed class BackendMetrics : IBackendMetrics
{
    private readonly IDeadLetterReadStore _dlq;
    private readonly Proto.ActorSystem? _system;
    private readonly Projections.IKonsumRueckstand? _konsum;

    public BackendMetrics(IDeadLetterReadStore dlq, Proto.ActorSystem? system = null, Projections.IKonsumRueckstand? konsum = null)
    {
        _dlq = dlq;
        _system = system;
        _konsum = konsum;
    }

    public async Task<BackendMetrik> ErfasseAsync(CancellationToken ct = default)
    {
        var dlq = await _dlq.CountAsync(ct);
        var konsum = _konsum is null ? [] : await _konsum.ErfasseAsync(ct);
        return new BackendMetrik(dlq, _system?.ProcessRegistry.ProcessCount ?? 0,
            konsum.Sum(k => k.Rueckstand), konsum.Sum(k => k.Geparkt));
    }
}
