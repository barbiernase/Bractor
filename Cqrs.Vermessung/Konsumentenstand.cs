namespace Cqrs.Vermessung;

/// <summary>
/// Wo die durablen Konsumenten im Log stehen (gelesen über die Lese-Rolle, also nicht mitgezählt): Kopf des Logs, die kleinste
/// Partitions-Position der Projektionen und der Funktionen (beide im Partitions-Checkpoint, die Funktionen mit der Id <c>funktion:…</c>) und wie
/// viele Partitionen ihren Stand in den letzten 10 s geschrieben haben. Stehen alle Positionen am Kopf, ist der Nachlauf der Last vorbei (PF-14):
/// ab dann misst der Leerlauf die Grundlast.
/// </summary>
internal sealed record Konsumentenstand(long Kopf, long Projektion, long Funktion, long Bewegt)
{
    /// <summary>
    /// Die kleinste Position der Projektionen und der Funktionen am Kopf (-1 = diese Art hat noch keinen Stand). Eine Partition legt ihren Stand
    /// erst beim ersten Vorrücken an — solange gar keiner da ist, gilt nichts als eingeholt.
    /// </summary>
    public bool Eingeholt => (Projektion >= 0 || Funktion >= 0) && (Projektion < 0 || Projektion >= Kopf) && (Funktion < 0 || Funktion >= Kopf);

    public static async Task<Konsumentenstand> LiesAsync(Datenbank db)
    {
        var s = db.Schema;
        async Task<long> Zahl(string sql) { try { return await db.ZahlAsync(sql); } catch { return -1; } }
        var kopf = await Zahl($"select coalesce(max(seq_id), 0) from {s}.mt_events");
        var funktion = "data->>'ProjektionsId' like 'funktion:%'";
        var projektion = await Zahl($"select coalesce(min((data->>'Position')::bigint), -1) from {s}.mt_doc_partitionscheckpoint where not ({funktion})");
        var funktionen = await Zahl($"select coalesce(min((data->>'Position')::bigint), -1) from {s}.mt_doc_partitionscheckpoint where {funktion}");
        var bewegt = await Zahl($"select count(*) from {s}.mt_doc_partitionscheckpoint where mt_last_modified > now() - interval '10 seconds'");
        return new Konsumentenstand(kopf, projektion, funktionen, bewegt);
    }
}
