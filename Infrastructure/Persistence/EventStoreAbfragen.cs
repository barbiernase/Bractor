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

    /// <summary>Die nach $1 geänderten Streams in Sequenz-Reihenfolge, höchstens $2 Events; mit Typen nur die in $3 (Poll-Backstop).</summary>
    public static string GeaenderteStreams(string schema, bool mitTypen)
        => $"select seq_id, stream_id from {schema}.mt_events where seq_id > $1 and "
           + (mitTypen ? "type = any($3)" : "type <> 'tombstone'") + " order by seq_id limit $2";

    /// <summary>Die Spalten eines gelesenen Events: Daten, Typ, Identität, die Metadaten nach der Konfiguration des Stores, die Sequenz.</summary>
    public static string Events(string schema, string metadatenSpalten, string bedingung, string ordnung)
        => $"select data, type, id, stream_id, version, timestamp, {metadatenSpalten}, seq_id from {schema}.mt_events "
           + $"where {bedingung} and is_archived = false {ordnung}";

    /// <summary>
    /// Die eigenen Events eines Vorgangs im Stream $1 (Korrelation $3) ab Version $2, in einer Anweisung (eine Momentaufnahme) mit dem Kopf des
    /// Streams: die erste Zeile trägt nur den Kopf und die Version, ab der gelesen wurde (Spalten 1 und 2), die übrigen die Events (ab Spalte 3,
    /// wie <see cref="Events"/>). Ist $2 = 0 (kein Cursor), beginnt der Read hinter dem letzten Event, das vor dem Start des Vorgangs geschrieben
    /// wurde: vor dem ersten Event seines Logs $4 (kleinere Sequenz) gibt es nichts Eigenes. Diese Grenze sucht Postgres rückwärts vom Kopf über
    /// den Stream-Index; das kostet nur die Events seit dem Start, nie die Geschichte davor. Ohne Event vor dem Start (oder ohne Log) ab 1.
    ///
    /// So gebaut, dass jeder Plan den Stream-Index nur im nötigen Bereich liest, auch mit veralteter Statistik (Plan-Test): der Kopf als
    /// „sortiert, nur der erste“ (ein <c>max(version)</c> lief dann als Aggregat über den ganzen Stream), die Untergrenze als Ausdruck in der
    /// Bedingung der Events (ein lateraler Join machte sie zum Join-Filter und las den ganzen Stream). Zwei einfache Teile statt Hilfstabellen
    /// (CTE): halb so teuer in der Ausführung, ein Viertel im Planen.
    /// </summary>
    public static string Korreliert(string schema, string metadatenSpalten)
    {
        var ab = "greatest($2::bigint, case when $2::bigint > 0 then 0 else coalesce(("
                 + $"select v.version from {schema}.mt_events v where v.stream_id = $1::uuid and v.seq_id < ("
                 + $"select b.seq_id from {schema}.mt_events b where b.stream_id = $4::uuid order by b.version limit 1) "
                 + "order by v.version desc limit 1), 0) + 1 end)";
        // Kopfzeile: Kopf, Ab und so viele leere Spalten wie eine Event-Zeile (Daten, Typ, Id, Stream, Version, Zeit, acht Metadaten, Sequenz).
        var leer = string.Join(", ", Enumerable.Repeat("null", 15));
        return $"select coalesce((select k.version from {schema}.mt_events k where k.stream_id = $1::uuid order by k.version desc limit 1), 0), "
               + $"{ab}, {leer} union all ("
               + $"select null, null, data, type, id, stream_id, version, timestamp, {metadatenSpalten}, seq_id from {schema}.mt_events "
               + $"where stream_id = $1::uuid and correlation_id = $3::text and is_archived = false and version >= {ab}) "
               + "order by 7 nulls first";
    }

    /// <summary>Die Seite einer Partition (Batch-Konsum): jenseits $1, Partition $3 von $2, ohne die erledigten $4, optional nur die Typen $6.</summary>
    public static string PartitionsBedingung(bool mitTypen)
        => $"seq_id > $1 and {PartitionsAusdruck.Sql("stream_id")} % $2 = $3 and not (seq_id = any($4))" + (mitTypen ? " and type = any($6)" : "");

    public const string PartitionsOrdnung = "order by seq_id limit $5";
}
