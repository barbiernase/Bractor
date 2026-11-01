using Abstractions;

namespace Infrastructure.Funktionen;

// ═══════════════════════════════════════════════════════════════════════════════════════════════════════════════════════
// EINGÄNGE EINER FUNKTION (docs/04 §4.4): Funktionen sind Stromlogik VOR dem Aggregat und lesen nie das Log. Ein Eingang kommt von einem durablen
// Rand (die Folge eines Commands, eine Quell- oder Client-Nachricht — offen in cqrs_eingaenge, der Zusteller bringt ihn), aus einer Kette (das
// Zwischenergebnis einer anderen Funktion) oder aus dem Hinweis-Netz. Er trägt eine abgeleitete Kennung: ein Empfänger erkennt einen doppelten
// Eingang an ihr; die Command-Ids der Ausgaben folgen aus ihr (Kennung, Funktion, Stelle) — die Inbox verwirft Wiederholungen.
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
/// Ein Eingang unterwegs zu einer Funktion (an ihren Parent auf einem Knoten, der sie hält): Kennung, Funktion, die Nachricht und die Herkunft der
/// Kette (Stream, Version, Zeit, Akteur, Korrelation, Ursache). Auf demselben Knoten reist die Nachricht als Objekt; zwischen Knoten über ihre
/// Wire-Form (<see cref="Infrastructure.Serialization.NachrichtJsonConverter"/>: ein Event bzw. Hinweis in der Event-Form, sonst die Form der
/// Nachrichten ohne Marker).
/// </summary>
public sealed record EingangZustellen(Guid Kennung, string Funktion,
    [property: System.Text.Json.Serialization.JsonConverter(typeof(Infrastructure.Serialization.NachrichtJsonConverter))] object Nachricht,
    Eingangsart Art, Guid Stream, int Version, DateTimeOffset Zeit, string? Akteur, string? Korrelation, string? Ursache) : IWireMessage
{
    /// <summary>Die Herkunft, die das <c>Auf</c> sieht (der Wirker ist die Funktion).</summary>
    public Herkunft Herkunft() => new(Stream, Version, Zeit, ImAuftrag.IstAkteur(Akteur) ? Akteur : null, WirkerId.FuerFunktion(Funktion));
}

/// <summary>Wie ein Parent einen Eingang beantwortet.</summary>
public enum Quittungsart
{
    /// <summary>Angenommen, in Arbeit (nur bei <see cref="Eingangsart.Durabel"/>).</summary>
    Angenommen = 0,
    /// <summary>Fertig: seine Commands sind beantwortet, seine Nachfolger fertig (auch: schon früher fertig geworden).</summary>
    Fertig = 1,
    /// <summary>Jetzt nicht (die Warteschlange ist voll, die Funktion hält hier keinen Ort) — der Absender versucht es später, auch anderswo.</summary>
    Spaeter = 2,
    /// <summary>Gescheitert nach allen Wiederholungen.</summary>
    Gescheitert = 3,
}

/// <summary>Die Antwort eines Parents auf <see cref="EingangZustellen"/>.</summary>
public sealed record EingangQuittung(Guid Kennung, Quittungsart Ausgang) : IWireMessage;

/// <summary>Die abgeleiteten Kennungen in einer Kette — eine Quelle für Kind, Tests und Werkzeuge.</summary>
public static class Kettenvertrag
{
    /// <summary>Die Kennung der Ausgabe an <paramref name="stelle"/> der Funktion <paramref name="funktion"/> zum Eingang <paramref name="eingang"/>.</summary>
    public static Guid Ausgabe(Guid eingang, string funktion, int stelle) => Herkunft.Ableite($"ausgabe:{funktion}:{eingang:N}:{stelle}");

    /// <summary>Die Kennung des <paramref name="nummer"/>-ten Events der Antwort auf den Command an <paramref name="stelle"/> zum Eingang <paramref name="eingang"/>.</summary>
    public static Guid Antwort(Guid eingang, string funktion, int stelle, int nummer) => Herkunft.Ableite($"antwort:{funktion}:{eingang:N}:{stelle}:{nummer}");

    /// <summary>Die Command-Id der Ausgabe an <paramref name="stelle"/> (Eingang, Funktion, Stelle, Typ, Ziel): ein erneutes Rechnen trägt dieselbe Id.</summary>
    public static Guid Befehl(Guid eingang, string funktion, int stelle, ICommand befehl) =>
        Herkunft.Ableite($"befehl:{funktion}:{eingang:N}:{stelle}:{befehl.GetType().FullName}:{befehl.Ziel:N}");
}
