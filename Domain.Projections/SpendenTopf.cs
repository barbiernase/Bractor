using Abstractions;
using Core;
using Domain.Spende;

namespace Domain.Projections;

/// <summary>
/// Read-Model der Spenden-Kampagne: der aufsummierte Topf pro Kampagne. Die Summe ist eine
/// AKKUMULIERENDE (nicht-idempotente) Größe — wird EIN Event doppelt verarbeitet, ist die Summe zu
/// hoch. Genau deshalb ist diese Projektion der scharfe Prüfstein für exactly-once vs. at-least-once.
/// </summary>
public class SpendenTopfReadModel
{
    public Guid Id { get; set; }
    public decimal Summe { get; set; }
    public int Anzahl { get; set; }
    public DateTimeOffset Aktualisiert { get; set; }
}

/// <summary>
/// Die Write-Naht der Projektion (was der Entwickler an Store-Vertrag schreibt). Die konkrete
/// Implementierung entscheidet die GARANTIE: co-committend (Effekt + Marke in EINER Transaktion) =
/// exactly-once; getrennt = at-least-once. Der Pull-Pfad wählt sie über die DI-Registrierung.
/// </summary>
public interface ISpendenTopfWriteStore
{
    /// <summary>Addiert eine Spende auf den Topf der Kampagne (read-modify-write, NICHT idempotent).</summary>
    Task AddiereAsync(Guid kampagne, decimal betrag, DateTimeOffset wann);
}

/// <summary>
/// Die Projektion — das ganze fachliche Stück, das der Entwickler schreibt: auf <see cref="SpendeEingegangen"/>
/// den Betrag auf den Kampagnen-Topf addieren. <see cref="IPullSubscriber"/> zieht sie auf die exactly-once-
/// fähige Pull-Maschine; <c>DispatchAsync</c>/<c>SubscribedTypes</c> generiert das Framework aus den
/// <c>Handle</c>-Methoden (reflection-frei). Cursor/Ordnung/Exactly-once tauchen hier NIE auf (Invariante 5).
/// </summary>
public partial class SpendenTopfProjection : ISubscriber, IPullSubscriber
{
    private readonly ISpendenTopfWriteStore _store;

    public SpendenTopfProjection(ISpendenTopfWriteStore store) => _store = store;

    public string SubscriberId => "spendentopf-projection";

    public async Task Handle(SpendeEingegangen evt, IAggregateEnvelope envelope, ProjectionWriter writer)
    {
        await writer.Execute(envelope.AggregateId.ToString(), async ctx =>
        {
            ctx.Track<Kampagne>(envelope.AggregateId);
            await _store.AddiereAsync(envelope.AggregateId, evt.Betrag, envelope.CreatedAtUtc);
        });
    }
}
