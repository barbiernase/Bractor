namespace Abstractions;

/// <summary>Die Art eines Trigger-Ingress (wie eine <see cref="IPipelineTrigger"/>-Nachricht von außen entsteht).</summary>
public enum IngressArt
{
    Webhook = 1,
    Timer = 2,
    Datei = 3,
}

/// <summary>
/// Markiert eine Framework-Methode, die einen Trigger-INGRESS registriert (Webhook-Route, Timer, Datei-Beobachtung).
/// Der Vertrag, über den der Domänen-Extractor die Betriebs-Bindungen einer Composition Root erkennt — am Symbol der
/// aufgerufenen Methode statt an der Form des Aufrufs. <see cref="Ort"/> nennt (per <c>nameof</c>) den Parameter, der
/// Route / Intervall / Pfad trägt.
/// </summary>
[AttributeUsage(AttributeTargets.Method, AllowMultiple = false, Inherited = false)]
public sealed class IngressAttribute : Attribute
{
    public IngressAttribute(IngressArt art) => Art = art;

    public IngressArt Art { get; }

    /// <summary>Name des Parameters mit Route (Webhook), Intervall (Timer) bzw. Pfad (Datei).</summary>
    public string? Ort { get; init; }
}
