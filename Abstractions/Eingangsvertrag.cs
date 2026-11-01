namespace Abstractions;

/// <summary>
/// Die Kennungen der offenen Eingänge einer Funktion (docs/04 §4.4) — abgeleitet, nie zufällig: dieselbe Entscheidung ergibt dieselben Kennungen, ein
/// wiederholter Command legt keinen zweiten Eingang. Eine Quelle für Aggregat-Actor, Zusteller und Tests.
/// </summary>
public static class Eingangsvertrag
{
    /// <summary>Die Kennung der Folge an <paramref name="stelle"/> (unter den Folgen) der Entscheidung über den Command <paramref name="commandId"/>.</summary>
    public static Guid Folge(Guid commandId, int stelle) => Herkunft.Ableite($"folge:{commandId:N}:{stelle}");

    /// <summary>Die Kennung des Eingangs, den die Nachricht <paramref name="nachricht"/> bei der Funktion <paramref name="funktion"/> öffnet.</summary>
    public static Guid Eingang(Guid nachricht, string funktion) => Herkunft.Ableite($"eingang:{funktion}:{nachricht:N}");
}
