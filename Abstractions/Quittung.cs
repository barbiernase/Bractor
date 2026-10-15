namespace Abstractions;

/// <summary>Wie ein Command ausging, wie die Inbox ihn festhält (docs/04 §4.4): gewirkt (Effekt oder Noop) oder fachlich abgelehnt.</summary>
public enum KommandoAusgang
{
    Gewirkt = 0,
    Abgelehnt = 1,
}

/// <summary>
/// Die Quittung eines Commands: ein Eintrag der Inbox (eindeutig je <see cref="CommandId"/>), geschrieben in derselben Transaktion wie die
/// Events des Commands. Ein zweiter Eintrag derselben CommandId scheitert am Schlüssel — der Append schreibt dann nichts
/// (<see cref="KommandoSchonEntschiedenException"/>). So wirkt ein Command genau einmal innerhalb des Aufbewahrungs-Horizonts.
/// </summary>
public readonly record struct Quittung(Guid CommandId, KommandoAusgang Ausgang);

/// <summary>
/// Der Append trug eine Quittung, deren CommandId die Inbox schon kennt: nichts ist geschrieben. Den Ausgang des ersten Mals liefert
/// <see cref="IEventStoreRepository.AusgangAsync"/>.
/// </summary>
public sealed class KommandoSchonEntschiedenException(Guid commandId, Exception? innen = null)
    : Exception($"Command {commandId} ist schon entschieden (Inbox)", innen)
{
    public Guid CommandId { get; } = commandId;
}
