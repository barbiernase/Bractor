namespace Abstractions;

/// <summary>
/// Durabler Stand (Fortschritt) einer Partition einer Projektion bzw. Funktion (docs/04 §4.1). Id-Form
/// <c>"{projektionsId}:{partition}"</c>. Reines POCO ohne Persistenz-Abhängigkeit (analog <see cref="ProjectionCheckpoint"/>); die
/// Marten-Registrierung (numerische Revision auf <see cref="Version"/>) erfolgt in der Host-/Store-Konfiguration.
/// <list type="bullet">
///   <item><see cref="Position"/>: globale Sequenz — jedes Event der Partition mit kleinerer oder gleicher Sequenz ist erledigt
///     (angewandt, übersprungen oder geparkt).</item>
///   <item><see cref="Version"/>: Versionsprüfung beim Commit (G-4). Jeder Commit erwartet die geladene Version; hat eine zweite
///     Aktivierung oder ein Neuaufbau dazwischen geschrieben, fällt der ganze Batch zurück. 0 = noch nie geschrieben.</item>
///   <item><see cref="Geparkt"/>: Streams, deren Dispatch dauerhaft wirft; ihre Events überspringt der Leser, bis ein Einzelversuch
///     durchläuft.</item>
///   <item><see cref="Generation"/>: in welche Generation des Ziels die Projektion schreibt (A4, <see cref="IProjektionsZiel.Beginne"/>); ein
///     Neuaufbau setzt eine neue.</item>
/// </list>
/// </summary>
public class PartitionsCheckpoint
{
    public string Id { get; set; } = default!;
    public string ProjektionsId { get; set; } = default!;
    public int Partition { get; set; }
    public long Position { get; set; }
    public int Version { get; set; }
    public List<GeparkterStream> Geparkt { get; set; } = new();
    public string Generation { get; set; } = Generationen.Erste;
    public DateTimeOffset UpdatedAt { get; set; }

    public static string MakeId(string projektionsId, int partition) => $"{projektionsId}:{partition}";

    /// <summary>Der Anfangsstand einer Partition, die noch nie geschrieben wurde.</summary>
    public static PartitionsCheckpoint Neu(string projektionsId, int partition) => new()
    {
        Id = MakeId(projektionsId, partition),
        ProjektionsId = projektionsId,
        Partition = partition
    };
}

/// <summary>Ein geparkter Stream: <see cref="Version"/> = der Guard vor dem werfenden Event.</summary>
public class GeparkterStream
{
    public Guid StreamId { get; set; }
    public int Version { get; set; }
    public string Grund { get; set; } = "";
    public DateTimeOffset Seit { get; set; }
}

/// <summary>
/// Die Partitions-Funktion des Batch-Konsums (E10): das letzte Byte der UUID in RFC-Reihenfolge modulo P. Der Store rechnet dasselbe in seiner
/// Sprache (Postgres: <c>Infrastructure.Persistence.PartitionsAusdruck</c>),
/// ohne Index und ohne Abhängigkeit von Laufzeit oder PG-Version; für zufällige und gehashte Ids gleichverteilt. P ≤ 256; ein Wechsel von P
/// ist ein Neuaufbau.
/// </summary>
public static class Partitionierung
{
    public const int Hoechstens = 256;

    public static int Von(Guid streamId, int partitionen) => streamId.ToByteArray(bigEndian: true)[15] % partitionen;

}

/// <summary>
/// Ein zweiter Leser oder ein Neuaufbau hat den Partitions-Checkpoint zwischenzeitlich geschrieben (G-4). Nichts aus dem Batch ist durabel;
/// der Leser lädt den Stand neu und liest von dort.
/// </summary>
public sealed class PartitionsKonfliktException : Exception
{
    public PartitionsKonfliktException(string message, Exception inner) : base(message, inner) { }
}
