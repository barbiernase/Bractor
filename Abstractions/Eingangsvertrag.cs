namespace Abstractions;

/// <summary>
/// Die Kennungen der offenen Eingänge einer Funktion (docs/04 §4.4) — abgeleitet, nie zufällig: dieselbe Entscheidung ergibt dieselben Kennungen, ein
/// wiederholter Command legt keinen zweiten Eingang. Eine Quelle für Aggregat-Actor, Zusteller und Tests.
/// </summary>
public static class Eingangsvertrag
{
    /// <summary>Die Kennung des Commands ohne Ziel an <paramref name="stelle"/> (unter ihnen) der Entscheidung über den Command <paramref name="commandId"/>.</summary>
    public static Guid AusEntscheidung(Guid commandId, int stelle) => Herkunft.Ableite($"folge:{commandId:N}:{stelle}");

    /// <summary>Die Kennung des Eingangs, den die Nachricht <paramref name="nachricht"/> am <paramref name="ort"/> öffnet (eine Instanz bzw. <c>instanz/knoten</c>).</summary>
    public static Guid Eingang(Guid nachricht, string ort) => Herkunft.Ableite($"eingang:{ort}:{nachricht:N}");

    /// <summary>Die Kennung des Hinweises, den ein Aggregat auf den Command <paramref name="commandId"/> hin ausgab (eine Ablehnung, docs/04 §4.4).</summary>
    public static Guid HinweisAusCommand(Guid commandId) => Herkunft.Ableite($"hinweis:command:{commandId:N}");
}
