namespace Infrastructure.Persistence;

/// <summary>
/// Die Partitions-Funktion des Batch-Konsums in Postgres-SQL (<see cref="Abstractions.Partitionierung.Von"/> rechnet dasselbe in .NET): das
/// letzte Byte der UUID in RFC-Reihenfolge, modulo P beim Aufrufer.
/// </summary>
public static class PartitionsAusdruck
{
    /// <summary>Der Ausdruck über die Spalte <paramref name="spalte"/> (uuid).</summary>
    public static string Sql(string spalte) => $"get_byte(uuid_send({spalte}), 15)";
}
