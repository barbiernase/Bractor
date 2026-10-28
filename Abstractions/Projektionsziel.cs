namespace Abstractions;

// ═════════════════════════════════════════════════════════════════════════════════════════════════════════════════════════════════════
// Die Store-Klassen (Plan Pub/Sub-Neustrukturierung §2.4). Das Framework schreibt keine Datenform vor; von einem Store braucht es nur eine
// von drei Zusagen. Jeder Store implementiert GENAU EINE (R14, Analyzer CQRS109); ob ein Treiber sie einhält, zeigt der allgemeine Store-Test
// (Infrastructure.Projections.Storetest), den jeder Treiber besteht.
// ═════════════════════════════════════════════════════════════════════════════════════════════════════════════════════════════════════

/// <summary>
/// Jeder Store einer Projektion. Ein Batch des Lesers: <see cref="Beginne"/> → je Event <see cref="Stream"/> und das <c>Auf</c> der Projektion (der
/// Store puffert, was seine Fähigkeiten bewirken) → Schreiben (Klasse 1 mit dem Fortschritt, Klasse 2/3 über <see cref="SchreibeAsync"/>, danach
/// hält das Framework den Fortschritt fest). Ein geworfenes <c>Auf</c> schreibt nie; der nächste <see cref="Beginne"/> verwirft den Puffer.
/// </summary>
public interface IProjektionsZiel
{
    /// <summary>Ein Batch beginnt: der Puffer ist leer; geschrieben wird in <paramref name="generation"/> (aus dem Fortschritt der Partition).</summary>
    void Beginne(string generation);

    /// <summary>Der Stream des nächsten Events — eine Schreib-Regel je Dokument kann gegen ihn prüfen.</summary>
    void Stream(Guid stream);

    /// <summary>Klasse 2 und 3: die gepufferten Wirkungen schreiben. Erst danach rückt das Framework die Position vor.</summary>
    Task SchreibeAsync(CancellationToken ct);

    /// <summary>
    /// Neuaufbau (A4): ein leeres Ziel für <paramref name="generation"/> vorbereiten — gelesen wird weiter die bisherige Generation, bis
    /// <see cref="UmschaltenAsync"/>. Ein Leser der alten Generation, der noch schreibt, trifft das neue Ziel nicht. „Leeren“ ist der Sonderfall
    /// eines Stores, dessen Wirkungen nur mit dem Fortschritt gültig werden (Klasse 1).
    /// </summary>
    Task NeueGenerationAsync(string generation, CancellationToken ct);

    /// <summary>Der Neuaufbau ist durch: ab jetzt wird <paramref name="generation"/> gelesen; ältere Generationen darf der Store wegräumen.</summary>
    Task UmschaltenAsync(string generation, CancellationToken ct);
}

/// <summary>
/// Wo der Fortschritt einer Projektion liegt: je Partition Position, Revision, Geparkt und Generation (<see cref="PartitionsCheckpoint"/>), je
/// Stream die höchste angewandte Version (Guard). Bei Klasse 1 der Store selbst, bei Klasse 2 und 3 das Framework.
/// </summary>
public interface IFortschrittsAblage
{
    /// <summary>Der Fortschritt einer Partition; noch nie geschrieben: Position 0, Revision 0, Generation <see cref="Generationen.Erste"/>.</summary>
    Task<PartitionsCheckpoint> LadeFortschrittAsync(string projektion, int partition, CancellationToken ct);

    /// <summary>Die Guards der Streams — höchste angewandte Version je Stream, fehlt einer: -1. Eine Abfrage.</summary>
    Task<IReadOnlyDictionary<Guid, int>> LadeGuardsAsync(string projektion, IReadOnlyCollection<Guid> streams, CancellationToken ct);

    /// <summary>
    /// Neuaufbau: alle <paramref name="partitionen"/> Partitionen auf Anfang (Position 0, nichts geparkt, neue Revision — ein laufender Leser verliert
    /// seinen nächsten Commit) in der Generation <paramref name="generation"/>, alle Guards weg.
    /// </summary>
    Task FortschrittZuruecksetzenAsync(string projektion, int partitionen, string generation, CancellationToken ct);
}

/// <summary>
/// <b>Klasse 1 — atomar:</b> Wirkungen und Fortschritt in EINER Transaktion; der Fortschritt der Partition wird mit erwarteter Revision geschrieben
/// (zwei Aktivierungen: die verlierende scheitert samt ihren Wirkungen). Beispiele: relationale DB, Dokument-DB mit Transaktionen, SQLite beim Client.
/// </summary>
public interface IAtomaresZiel : IProjektionsZiel, IFortschrittsAblage
{
    /// <summary>
    /// Die gepufferten Wirkungen, die <paramref name="guards"/> (Stream → angewandte Version) und den Fortschritt <paramref name="fortschritt"/> mit
    /// erwarteter Revision <c>fortschritt.Version</c> in einer Transaktion. Liefert die neue Revision. Wirft <see cref="PartitionsKonfliktException"/>,
    /// wenn die Partition dazwischen geschrieben wurde, bzw. <see cref="SchreibKonfliktException"/> bei einem geteilten Dokument; dann ist nichts durabel.
    /// </summary>
    Task<int> SchreibeMitFortschrittAsync(PartitionsCheckpoint fortschritt, IReadOnlyDictionary<Guid, int> guards, CancellationToken ct);
}

/// <summary>
/// <b>Klasse 2 — wiederholungsfest:</b> jede Wirkung ergibt wiederholt dasselbe; den Fortschritt hält das Framework und rückt ihn erst nach
/// <see cref="IProjektionsZiel.SchreibeAsync"/> vor. Die Wirkungen tragen die Event-Identität im Schlüssel (eine Datei unter Stream und Version, ein
/// Messpunkt mit Zeitpunkt) oder schreiben bedingt („nur wenn die Version größer ist als die gespeicherte“) — sonst brächte eine zweite Aktivierung
/// alte Werte zurück. Darum nehmen die Schreib-Fähigkeiten eines solchen Stores die <see cref="Herkunft"/> (R14).
/// </summary>
public interface IWiederholungsfestesZiel : IProjektionsZiel { }

/// <summary>
/// <b>Klasse 3 — mindestens einmal:</b> keine Zusage; nach einem Abbruch können Wirkungen doppelt stehen (Metrik-Sinks). Nur für Projektionen mit
/// ausdrücklicher Erlaubnis (<see cref="MindestensEinmalAttribute"/>, R14). Den Fortschritt hält das Framework.
/// </summary>
public interface INurMindestensEinmal : IProjektionsZiel { }

/// <summary>Generationen eines Projektions-Ziels (A4).</summary>
public static class Generationen
{
    /// <summary>Die Generation eines nie neu aufgebauten Ziels.</summary>
    public const string Erste = "g0";

    /// <summary>Eine neue Generation — fortlaufend nach der Zeit (lesbar, nie gleich einer früheren desselben Ziels).</summary>
    public static string Neu(DateTimeOffset jetzt) => "g" + jetzt.UtcTicks.ToString(System.Globalization.CultureInfo.InvariantCulture);
}
