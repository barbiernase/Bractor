namespace Abstractions;

/// <summary>
/// Zustell-Marker: „route mich über den PULL-Transport" (geordnet, single-writer, vollständig,
/// vom Poll geheilt) statt über den verlustbehafteten Push-Broker.
///
/// Er wählt den <b>Transport</b>, nicht die Garantie: ein <see cref="ISubscriber"/> ist eine Projektion (docs/04 §4.2), die
/// <b>Effekt-Garantie</b> sagt das Store-Interface (<see cref="IProjectionTracker"/> → exactly-once).
///
/// Migrations-Krücke: im Endzustand (Push abgelöst) tragen alle durablen Handler den Pull-Pfad
/// und der Marker entfällt.
/// </summary>
public interface IPullSubscriber : ISubscriber
{
}
