namespace Abstractions;

/// <summary>
/// Zustell-Marker: „route mich über den PULL-Transport" (geordnet, single-writer, vollständig,
/// vom Poll geheilt) statt über den verlustbehafteten Push-Broker.
///
/// Er wählt den <b>Transport</b>, nicht die Garantie. Was ein Handler <i>ist</i> (Projektion,
/// Reaktion, gemischt), ergibt sich weiterhin allein aus seinen Handle-Rückgabetypen (Spec 8);
/// die <b>Effekt-Garantie</b> sagt das Store-Interface (<see cref="IProjectionTracker"/> →
/// exactly-once; sonst at-least-once). Zwei getrennte Achsen, zwei getrennte Interfaces.
///
/// Migrations-Krücke: im Endzustand (Push abgelöst) tragen alle durablen Handler den Pull-Pfad
/// und der Marker entfällt.
/// </summary>
public interface IPullSubscriber : ISubscriber
{
}
