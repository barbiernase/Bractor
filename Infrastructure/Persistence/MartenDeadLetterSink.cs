using Abstractions;
using Marten;
using Microsoft.Extensions.Logging;

namespace Infrastructure.Persistence;

/// <summary>
/// Marten-basierte <see cref="IDeadLetterSink"/>: ein durables <see cref="DeadLetter"/>-Dokument je toter
/// Command-Zustellung (Schema „dlq"). Beobachtbar (Query) und replay-bar (der Betreiber kann den Command aus
/// dem Eintrag neu anstoßen). Best-effort: ein Fehler beim Schreiben wird nur geloggt, nie in den Aufrufer
/// (den Pipeline-Actor) zurückgeworfen — die DLQ ist Beobachtung, kein kritischer Pfad.
///
/// Marten-Registrierung nötig (in der StoreOptions-Konfiguration):
///   opts.Schema.For&lt;DeadLetter&gt;().Identity(x =&gt; x.Id);
/// </summary>
public sealed class MartenDeadLetterSink : IDeadLetterSink
{
    private readonly IDocumentStore _store;
    private readonly ILogger<MartenDeadLetterSink> _logger;

    public MartenDeadLetterSink(IDocumentStore store, ILogger<MartenDeadLetterSink> logger)
    {
        _store = store ?? throw new ArgumentNullException(nameof(store));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task WriteAsync(DeadLetter eintrag, CancellationToken ct = default)
    {
        try
        {
            await using var session = _store.LightweightSession();
            session.Store(eintrag);
            await session.SaveChangesAsync(ct);
            _logger.LogWarning(
                "[DLQ] {CommandType} für {AggregateId} tot: {Grund} (Quelle {Quelle}, {Versuche} Versuche)",
                eintrag.CommandType, eintrag.AggregateId, eintrag.Grund, eintrag.Quelle, eintrag.Versuche);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[DLQ] Konnte Dead-Letter für {CommandType} nicht schreiben.", eintrag.CommandType);
        }
    }
}
