using Abstractions;

namespace Infrastructure.Monitoring;

/// <summary>
/// Momentaufnahme der Backend-Betriebskennzahlen (Monitoring, Scheibe 1: Zähler). Bewusst schmal — die
/// beiden Zahlen, die eine Ops-Sicht zuerst braucht: wie viele Prozesse laufen gerade, und wie voll ist die
/// DLQ. Beide kommen aus der uniformen Maschine (durabler Offen-Index + DLQ-Read-Store), nicht aus einem
/// separaten Zähler-Zustand.
/// </summary>
public sealed record BackendMetrik(int OffeneProzesse, long DlqEinträge);

/// <summary>Liest die aktuellen Backend-Kennzahlen (für HealthCheck + Monitoring-Endpoint).</summary>
public interface IBackendMetrics
{
    Task<BackendMetrik> ErfasseAsync(CancellationToken ct = default);
}

/// <summary>
/// Aggregiert die Kennzahlen aus den bestehenden durablen Quellen: die offenen Prozess-Korrelationen aus dem
/// <see cref="IProzessOffenIndex"/> und die DLQ-Zählung aus dem <see cref="IDeadLetterReadStore"/>. Reiner
/// Read-Pfad, keine Seiteneffekte.
/// </summary>
public sealed class BackendMetrics : IBackendMetrics
{
    private readonly IProzessOffenIndex _offen;
    private readonly IDeadLetterReadStore _dlq;

    public BackendMetrics(IProzessOffenIndex offen, IDeadLetterReadStore dlq)
    {
        _offen = offen;
        _dlq = dlq;
    }

    public async Task<BackendMetrik> ErfasseAsync(CancellationToken ct = default)
    {
        var offene = await _offen.ListeOffeneAsync(ct);
        var dlq = await _dlq.CountAsync(ct);
        return new BackendMetrik(offene.Count, dlq);
    }
}
