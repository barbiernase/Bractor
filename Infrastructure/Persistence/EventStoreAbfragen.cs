namespace Infrastructure.Persistence;

/// <summary>
/// Die SQL-Texte der häufigen Abfragen des <see cref="MartenEventStore"/> an einer Stelle — der Store schickt genau diese, und der Plan-Test
/// (<c>AbfragePlanGarantieTests</c>) prüft genau diese: jede liest über einen Index nur eine Handvoll Blöcke, mit dem auf die Werte zugeschnittenen
/// Plan ebenso wie mit dem allgemeinen, den Postgres einer vorbereiteten Anweisung nach einigen Aufrufen gibt (PF-20) — unabhängig davon, wie viele
/// Events die Tabelle hält (PF-17).
/// </summary>
public static class EventStoreAbfragen
{
    /// <summary>
    /// „Steht hinter der Position $1 ein Event?“ — als die kleinste Sequenz dahinter, nicht als <c>exists</c>: das <c>exists</c> durchsuchte die
    /// Tabelle von vorn, wann immer Postgres sie für klein hielt (veraltete Statistik einer wachsenden Tabelle) oder den Wert nicht kannte
    /// (allgemeiner Plan); „sortiert, nur die erste“ geht für jeden Plan am billigsten über den Index.
    /// </summary>
    public static string Dahinter(string schema) => $"(select seq_id from {schema}.mt_events where seq_id > $1 order by seq_id limit 1) is not null";


    /// <summary>
    /// Der Stand des Sequenzzählers für eine Probe des Horizonts (0, solange nie gezogen) — über <c>pg_sequence_last_value</c>, das die Seite der
    /// Sequenz unter ihrer Puffersperre liest. Ein gewöhnliches <c>select last_value from …_sequence</c> liest ohne Sperre: schreibt ein
    /// <c>nextval</c> gerade seinen WAL-Eintrag (jeder 32.), steht in der Seite für einen Moment der vorausgeloggte Wert, bis zu 32 über dem
    /// zuletzt vergebenen. Eine Probe mit diesem Stand gab Sequenzen frei, die noch niemand gezogen hatte — ihre Transaktionen bekamen danach
    /// höhere Nummern, als die Probe abwartete, und Leser rückten über ihre Events (G-17).
    /// </summary>
    public static string Zaehlerstand(string schema) => $"select coalesce(pg_sequence_last_value('{schema}.mt_events_sequence'), 0)";

    /// <summary>Die Spalten eines gelesenen Events: Daten, Typ, Identität, die Metadaten nach der Konfiguration des Stores, die Sequenz.</summary>
    public static string Events(string schema, string metadatenSpalten, string bedingung, string ordnung)
        => $"select data, type, id, stream_id, version, timestamp, {metadatenSpalten}, seq_id from {schema}.mt_events "
           + $"where {bedingung} and is_archived = false {ordnung}";


    /// <summary>Die Seite einer Partition (Batch-Konsum): jenseits $1, Partition $3 von $2, ohne die erledigten $4, optional nur die Typen $6.</summary>
    public static string PartitionsBedingung(bool mitTypen)
        => $"seq_id > $1 and {PartitionsAusdruck.Sql("stream_id")} % $2 = $3 and not (seq_id = any($4))" + (mitTypen ? " and type = any($6)" : "");

    public const string PartitionsOrdnung = "order by seq_id limit $5";
}
