namespace Abstractions;

/// <summary>
/// Die Quittung eines Client-Commands (docs/08 §8.4): geht gezielt an die Sitzung, die ihn gesendet hat — verlierbar wie
/// <see cref="CommandFailed"/> (Invariante 6). Sie nennt den Ausgang und den Stand des Aggregats danach. So erfährt der Client auch dann, dass
/// sein Command gewirkt hat, wenn er dessen Events nicht hört (ein Noop, Events außerhalb seines Vertrags, eine idempotent beantwortete
/// Wiederholung), und kennt die Version für seinen nächsten Command und für „mindestens S auf v“.
/// </summary>
/// <param name="CommandType">Der Typname des Commands.</param>
/// <param name="Stream">Das Aggregat (die Id seines Streams).</param>
/// <param name="Version">Die Version des Aggregats nach dem Command.</param>
/// <param name="Geschrieben">Hat DIESER Aufruf Events geschrieben? <c>false</c> bei Noop, Ablehnung und erkannter Wiederholung.</param>
/// <param name="Abgelehnt">Fachlich abgelehnt (die Ablehnung selbst geht als eigener Hinweis an die Sitzung).</param>
public record CommandQuittung(string CommandType, Guid Stream, int Version, bool Geschrieben, bool Abgelehnt = false) : ITransientEvent;
