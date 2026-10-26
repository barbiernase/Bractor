namespace Abstractions;

/// <summary>
/// Die Nachricht einer QUELLE (Datei erkannt, Webhook eingegangen, Takt, vom Client gesendet …): ein Event von draußen, genau einmal je
/// <see cref="Kennung"/> im Log (Plan §3.1). Das Framework hängt sie als erstes Event eines Streams an, dessen Id deterministisch aus der
/// Kennung folgt — dieselbe Datei zweimal gesehen (oder von zwei Knoten) steht genau einmal im Log; Funktionen hören sie wie jedes Event.
/// </summary>
public interface IQuellNachricht : IEvent
{
    /// <summary>Was diese Nachricht fachlich eindeutig macht (z. B. Pfad + Größe + Zeitstempel einer Datei).</summary>
    string Kennung { get; }
}
