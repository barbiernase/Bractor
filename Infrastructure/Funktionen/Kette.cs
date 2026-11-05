using Abstractions;
using Proto;

namespace Infrastructure.Funktionen;

// ═══════════════════════════════════════════════════════════════════════════════════════════════════════════════════════
// DIE KETTE (docs/04 §4.4): eine Nachricht am Kopf einer Instanz startet eine Kette. Jeder Schritt (der Parent eines Knotens) nimmt eine Nachricht,
// lässt ein Kind sein Auf rechnen und schlägt für jede Ausgabe in der Wegtabelle nach, an welche Knoten sie geht — an ihre Parents, als neuer
// Aufruf. Niemand führt Buch über die ganze Kette: auf einer GESICHERTEN Kante wartet der Absender auf das „fertig“ des Empfängers und meldet selbst
// erst „fertig“, wenn alle seine gesicherten Ausgaben fertig sind — so reicht die Rückmeldung wie bei verschachtelten Anrufen bis zum Kopf. Eine
// VERLIERBARE Kante bekommt die Nachricht einmal, ohne Antwort. Haltbar ist allein der Kopf: fehlt sein „fertig“, kommt er erneut, die Kette rechnet
// neu, und weil jede Kennung aus der Nachricht folgt, aus der sie entstand, verwirft die Inbox jedes Ziels die Wiederholung.
// ═══════════════════════════════════════════════════════════════════════════════════════════════════════════════════════

/// <summary>Was eine Kette vom Kopf mitbringt (für Herkunft, Ids und Spur): die Kennung des Kopfs und die Herkunft der Kette.</summary>
public sealed record KettenKontext(Guid Kopf, Guid Stream, int Version, DateTimeOffset Zeit, string? Akteur, string? Korrelation, string? Ursache)
{
    /// <summary>Die Herkunft, die das <c>Auf</c> der Funktion <paramref name="funktion"/> sieht (der Wirker ist die Funktion).</summary>
    public Herkunft Herkunft(string funktion) => new(Stream, Version, Zeit, ImAuftrag.IstAkteur(Akteur) ? Akteur : null, WirkerId.FuerFunktion(funktion));
}

/// <summary>
/// Ein Aufruf (an den Parent eines Knotens): Kennung (<see cref="Kettenvertrag.Aufruf"/>), Knoten, Operation (Nummer des <c>Auf</c>), die Nachricht,
/// der Kontext der Kette und wer auf „fertig“ wartet (null: verlierbar — niemand wartet).
/// </summary>
public sealed record KnotenAufruf(Guid Kennung, string Knoten, int Operation, object Nachricht, KettenKontext Kontext, PID? Antwort);

/// <summary>Wie ein Schritt einen Aufruf beantwortet.</summary>
public enum Rueckmeldeart
{
    /// <summary>Gerechnet, und alle gesicherten Ausgaben sind fertig (auch: schon früher fertig geworden).</summary>
    Fertig,
    /// <summary>Jetzt nicht (die Warteschlange ist voll) — der Absender schickt ihn nach einer Pause erneut.</summary>
    Spaeter,
    /// <summary>Gescheitert nach allen Wiederholungen — an diesem oder einem späteren Knoten (<see cref="KnotenRueckmeldung.Knoten"/>).</summary>
    Gescheitert,
}

/// <summary>Die Antwort eines Parents auf einen gesicherten <see cref="KnotenAufruf"/>.</summary>
public sealed record KnotenRueckmeldung(Guid Kennung, Rueckmeldeart Art, string? Knoten = null, string? Grund = null);

/// <summary>
/// Eine Ausgabe eines Aufrufs an Knoten desselben Graphen: ihre Stelle (Kennung der Nachricht), die Nachricht und — kommt sie aus einer Übersetzung,
/// die das Kind inline rechnete — deren Knoten (seine Wege gelten).
/// </summary>
public sealed record KnotenAusgabe(int Stelle, object Nachricht, string? Ueber = null);

/// <summary>An den Graph-Actor einer Quellinstanz: dieser Cluster-Knoten hält sie (die Quelle lauscht hier) bzw. nicht mehr.</summary>
public sealed record QuelleHalten(bool Halten);

/// <summary>An den Parent des Quellknotens: lauschen bzw. aufhören.</summary>
public sealed record Lausche(bool An);

/// <summary>
/// Die Quelle meldet ein Element (Kind des Quellknotens → Graph-Actor): seine laufende Nummer, der Schnappschuss ihres Zustands danach und die Zusage,
/// die der Graph-Actor erfüllt, sobald er die Kette annimmt (Gegendruck: die Quelle meldet das nächste erst danach).
/// </summary>
public sealed record QuellMeldung(object Nachricht, long Nummer, byte[] Zustand, TaskCompletionSource Angenommen);

/// <summary>Die Wege eines Parents: sein Knoten in der Instanz und die Adressen seiner Nachbarn (Parents desselben Graph-Actors).</summary>
public sealed record KnotenWege(GraphInstanz Instanz, string Knoten)
{
    /// <summary>Der Ort dieses Knotens (<c>instanz/knoten</c>).</summary>
    public string Ort => Instanz.Ort(Knoten);

    /// <summary>Die Adresse des Parents eines Knotens derselben Instanz auf diesem Cluster-Knoten (ein Geschwister unter dem Graph-Actor).</summary>
    public static PID Adresse(IContext context, string knoten) => PID.FromAddress(context.Self.Address, context.Parent!.Id + "/" + knoten);
}
