using Abstractions;
using Infrastructure.Mapping;
using Infrastructure.Komposition;

namespace Infrastructure.Projections;

/// <summary>
/// Der zustandslose Receiver (docs/04 §4.1) — der Kern, ohne Proto-Mantel.
///
/// Er nimmt ein `StateChangeVia{Event}`-Signal entgegen und weckt den Adapter für
/// dessen Stream: <c>Wake(signal.StreamId)</c>. Mehr trifft er nicht — Verlust,
/// Duplikat oder Wettrennen sind folgenlos, weil die Wahrheit im Log-Read des
/// Adapters liegt (Invariante 2). Reihenfolge/Exactly-once entstehen NICHT hier.
///
/// Diese Klasse ist bewusst transport-frei (kein Proto), damit die Signal→Wake-Logik
/// ohne Cluster testbar ist. Der Proto-Subscriber-Actor (Broker-Subscription) ist ein
/// dünner Mantel darum (2b-cluster).
/// </summary>
public sealed class SignalReceiver
{
    private readonly Func<Guid, CancellationToken, Task> _wake;

    public SignalReceiver(Func<Guid, CancellationToken, Task> wake) => _wake = wake;

    /// <summary>Ein Signal traf ein → wecke den Adapter für seinen Stream.</summary>
    public Task OnSignalAsync(IStateChangeSignal signal, CancellationToken ct = default)
        => _wake(signal.StreamId, ct);

    /// <summary>
    /// Welche Signal-Typen muss ein Receiver für die gegebenen Event-Typen abonnieren?
    /// (Die Events einer Projektion → ihre StateChangeVia{Event}-Signale.)
    /// Event-Typen ohne Signal — etwa transiente — fallen still heraus.
    /// </summary>
    public static IReadOnlyList<Type> SignalTypesFor(IEnumerable<Type> eventTypes)
        => eventTypes
            .Where(t => Anwendung.Signale.EventToSignal.ContainsKey(t))
            .Select(t => Anwendung.Signale.EventToSignal[t])
            .Distinct()
            .ToList();
}
