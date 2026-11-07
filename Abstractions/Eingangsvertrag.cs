namespace Abstractions;

/// <summary>
/// Die Kennungen der offenen Eingänge einer Funktion (docs/04 §4.4) — abgeleitet, nie zufällig: derselbe Command eines Clients ergibt dieselben
/// Kennungen, ein wiederholter legt keinen zweiten Eingang. Eine Quelle für Sitzung, Zusteller und Tests.
/// </summary>
public static class Eingangsvertrag
{
    /// <summary>Die Kennung des Eingangs, den die Nachricht <paramref name="nachricht"/> am <paramref name="ort"/> öffnet (eine Instanz bzw. <c>instanz/knoten</c>).</summary>
    public static Guid Eingang(Guid nachricht, string ort) => Herkunft.Ableite($"eingang:{ort}:{nachricht:N}");
}
