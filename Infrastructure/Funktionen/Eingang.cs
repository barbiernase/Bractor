using Abstractions;

namespace Infrastructure.Funktionen;

// ═══════════════════════════════════════════════════════════════════════════════════════════════════════════════════════
// EINGÄNGE EINER INSTANZ (docs/04 §4.4): Funktionen sind Knoten von Graphen VOR dem Aggregat und lesen nie das Log. Ein Eingang kommt am Kopf einer
// Instanz an — von einem durablen Rand (ein Command ohne Ziel aus einem Decide, eine Quell- oder Client-Nachricht — offen in cqrs_eingaenge, der
// Zusteller bringt ihn), vom Knoten eines anderen Graphen (ein Command ohne Ziel) oder aus dem Hinweis-Netz — und startet einen Lauf. Er trägt eine
// abgeleitete Kennung: der Graph-Actor erkennt einen doppelten Eingang an ihr; die Ids im Lauf folgen aus ihr (Lauf, Pfad, Knoten, Stelle) — die Inbox
// verwirft Wiederholungen. Zwischen Knoten desselben Graphen reist nichts als Eingang: das trägt der Lauf (Lauf.cs).
// ═══════════════════════════════════════════════════════════════════════════════════════════════════════════════════════

/// <summary>Woher ein Eingang kommt und was der Absender zurück erwartet.</summary>
public enum Eingangsart
{
    /// <summary>Aus der Tabelle der offenen Eingänge: „angenommen“ sofort; ist er fertig, trägt der Empfänger ihn selbst aus.</summary>
    Durabel = 0,
    /// <summary>Aus einer Kette: die Antwort kommt, wenn der Eingang fertig ist (seine Commands beantwortet, seine Nachfolger fertig) — oder sofort „später“.</summary>
    Kette = 1,
    /// <summary>Ein Hinweis: höchstens einmal, keine Antwort.</summary>
    Hinweis = 2,
}

/// <summary>
/// Ein Eingang unterwegs zum Kopf einer Instanz eines Graphen (an ihren Graph-Actor auf einem Cluster-Knoten, der sie hält; der Kopf ist der Knoten, der
/// seinen Typ nimmt): Kennung (sie wird die des Laufs), Instanz, die Nachricht und die Herkunft der Kette (Stream, Version, Zeit, Akteur, Korrelation, Ursache). Auf demselben
/// Cluster-Knoten reist die Nachricht als Objekt; zwischen Knoten über ihre Wire-Form (<see cref="Infrastructure.Serialization.NachrichtJsonConverter"/>:
/// ein Event bzw. Hinweis in der Event-Form, sonst die Form der Commands ohne Ziel).
/// </summary>
public sealed record EingangZustellen(Guid Kennung, string Instanz,
    [property: System.Text.Json.Serialization.JsonConverter(typeof(Infrastructure.Serialization.NachrichtJsonConverter))] object Nachricht,
    Eingangsart Art, Guid Stream, int Version, DateTimeOffset Zeit, string? Akteur, string? Korrelation, string? Ursache) : IWireMessage
{
}

/// <summary>Wie ein Parent einen Eingang beantwortet.</summary>
public enum Quittungsart
{
    /// <summary>Angenommen, in Arbeit (nur bei <see cref="Eingangsart.Durabel"/>).</summary>
    Angenommen = 0,
    /// <summary>Fertig: seine Commands sind beantwortet, seine Nachfolger fertig (auch: schon früher fertig geworden).</summary>
    Fertig = 1,
    /// <summary>Jetzt nicht (die Warteschlange ist voll, die Instanz hat hier keinen Ort) — der Absender versucht es später, auch anderswo.</summary>
    Spaeter = 2,
    /// <summary>Gescheitert nach allen Wiederholungen.</summary>
    Gescheitert = 3,
}

/// <summary>Die Antwort eines Parents auf <see cref="EingangZustellen"/>.</summary>
public sealed record EingangQuittung(Guid Kennung, Quittungsart Ausgang) : IWireMessage;

/// <summary>Die abgeleiteten Kennungen in einer Kette — eine Quelle für Kind, Tests und Werkzeuge.</summary>
public static class Kettenvertrag
{
    /// <summary>
    /// Die Kennung des Aufrufs einer Operation (<paramref name="auf"/>) des Knotens am <paramref name="ort"/> (<c>instanz/knoten</c>) im Lauf
    /// <paramref name="lauf"/> auf dem Pfad <paramref name="pfad"/> (Entscheidung 15): ein neuer Lauf vom selben Kopf rechnet dieselben Ids.
    /// </summary>
    public static Guid Aufruf(Guid lauf, string ort, string pfad, int auf) => Herkunft.Ableite($"aufruf:{ort}:{lauf:N}:{pfad}:{auf}");

    /// <summary>Die Kennung der Ausgabe an <paramref name="stelle"/> des Knotens am <paramref name="ort"/> (<c>instanz/knoten</c>) zum Eingang <paramref name="eingang"/>.</summary>
    public static Guid Ausgabe(Guid eingang, string ort, int stelle) => Herkunft.Ableite($"ausgabe:{ort}:{eingang:N}:{stelle}");

    /// <summary>Die Kennung des <paramref name="nummer"/>-ten Events der Antwort auf den Command an <paramref name="stelle"/> zum Eingang <paramref name="eingang"/>.</summary>
    public static Guid Antwort(Guid eingang, string ort, int stelle, int nummer) => Herkunft.Ableite($"antwort:{ort}:{eingang:N}:{stelle}:{nummer}");

    /// <summary>Die Command-Id der Ausgabe an <paramref name="stelle"/> (Eingang, Ort, Stelle, Typ, Ziel): ein erneutes Rechnen trägt dieselbe Id.</summary>
    public static Guid Befehl(Guid eingang, string ort, int stelle, IAggregatCommand befehl) =>
        Herkunft.Ableite($"befehl:{ort}:{eingang:N}:{stelle}:{befehl.GetType().FullName}:{befehl.Ziel:N}");
}
