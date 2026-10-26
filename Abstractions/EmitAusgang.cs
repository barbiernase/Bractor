namespace Abstractions;

/// <summary>
/// Was ein Emit über die Wirkung weiß (docs/11 §11.3). Der Ausgang ist eine Aussage über den Empfänger, nicht über den Draht:
/// <list type="bullet">
///   <item><see cref="Zugestellt"/> — das Ziel hat den Command verarbeitet (gewirkt oder als Wiederholung erkannt).</item>
///   <item><see cref="Abgelehnt"/> — das Ziel hat fachlich abgelehnt; die Ablehnung ist durabel (Marke <c>KommandoAbgelehnt</c>),
///     eine Wiederholung bringt dieselbe. Auch das ist entschieden.</item>
///   <item><see cref="Unbekannt"/> — keine Antwort in der Frist, ein technischer Fehler oder kein Ziel: vielleicht gewirkt, vielleicht
///     nicht. Nur eine Wiederholung mit derselben CommandId klärt es (die Inbox des Ziels verwirft ein Duplikat).</item>
/// </list>
/// </summary>
public enum EmitAusgang
{
    Zugestellt,
    Abgelehnt,
    Unbekannt,
}

public static class EmitAusgangErweiterung
{
    /// <summary>Zugestellt oder abgelehnt: der Command muss nicht wiederholt werden.</summary>
    public static bool IstEntschieden(this EmitAusgang a) => a != EmitAusgang.Unbekannt;
}
