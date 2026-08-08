namespace Abstractions;

/// <summary>
/// Durabler Cursor des Poll-Backstops pro Pull-Pfad (Spec 4.8 / 19.1). Er hält die zuletzt
/// gescannte globale High-Water-Mark fest, damit der Poller nach einem Neustart DORT
/// wieder aufsetzt, statt beim Boot die GESAMTE Historie erneut zu scannen.
///
/// Warum durabel und nicht „ab aktueller HWM starten": der Backstop MUSS Events nachholen,
/// die angehängt wurden, WÄHREND der Node unten war (deren Signale gingen verloren). Ein
/// Start bei der aktuellen HWM würde genau diese überspringen. Der persistierte Cursor holt
/// sie nach (Cursor &lt; ihre Sequenz) und vermeidet zugleich das Re-Scannen alter Historie.
///
/// Reines Vertragsinterface — die Ablage (Marten-Dokument, In-Memory) ist Store-Sache.
/// </summary>
public interface IPollCursorStore
{
    /// <summary>Die zuletzt persistierte High-Water-Mark des Pfads; 0, wenn nie gepollt.</summary>
    Task<long> GetAsync(string pollerId, CancellationToken ct);

    /// <summary>Rückt die persistierte High-Water-Mark nach EINEM Poll-Durchlauf vor.</summary>
    Task SetAsync(string pollerId, long highWater, CancellationToken ct);
}

/// <summary>
/// Durables Cursor-Dokument pro Pull-Pfad (Id = pollerId, i. d. R. die SubscriberId).
/// Reines POCO ohne Persistenz-Abhängigkeit (analog <see cref="ProjectionCheckpoint"/>);
/// die Marten-Schema-Registrierung erfolgt in der Host-/Store-Konfiguration.
/// </summary>
public class PollCursor
{
    public string Id { get; set; } = default!;
    public long HighWater { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
}
