namespace Cqrs.Vermessung;

/// <summary>
/// Wo die durablen Konsumenten im Log stehen (gelesen über die Lese-Rolle, also nicht mitgezählt): Kopf des Logs, die Poll-Cursor (Prozess-Poll,
/// Emittenten-Polls), die kleinste Partitions-Position der Projektionen, wie viele Emittenten-Cursor sich in den letzten 10 s bewegt haben.
/// Stehen alle Cursor und Positionen am Kopf, ist der Nachlauf der Last vorbei (PF-14): ab dann misst der Leerlauf die Grundlast.
/// </summary>
internal sealed record Konsumentenstand(long Kopf, IReadOnlyList<(string Id, long Marke)> Polls, long Projektion, long Emittenten)
{
    public string Poll => Polls.Count == 0 ? "-" : string.Join('/', Polls.Select(p => $"{p.Id}={p.Marke}"));

    /// <summary>
    /// Alle Poll-Cursor und die kleinste Projektions-Position am Kopf. Ein Poll legt seinen Cursor erst beim ersten Vorrücken an — solange
    /// keiner da ist, gilt nichts als eingeholt (beide vermessenen Anwendungen haben einen Prozess-Poll); Projektionen hat nicht jede.
    /// </summary>
    public bool Eingeholt => Polls.Count > 0 && Polls.All(p => p.Marke >= Kopf) && (Projektion < 0 || Projektion >= Kopf);

    public static async Task<Konsumentenstand> LiesAsync(Datenbank db)
    {
        var s = db.Schema;
        async Task<long> Zahl(string sql) { try { return await db.ZahlAsync(sql); } catch { return -1; } }
        var kopf = await Zahl($"select coalesce(max(seq_id), 0) from {s}.mt_events");
        List<(string, long)> polls;
        try
        {
            polls = await db.LiesAsync($"select id, (data->>'HighWater')::bigint from {s}.mt_doc_pollcursor order by id",
                r => (r.GetString(0), r.GetInt64(1)));
        }
        catch { polls = new(); }
        var projektion = await Zahl($"select coalesce(min((data->>'Position')::bigint), -1) from {s}.mt_doc_partitionscheckpoint");
        var emittenten = await Zahl($"select count(*) from {s}.mt_doc_emittentencursordoc where mt_last_modified > now() - interval '10 seconds'");
        return new Konsumentenstand(kopf, polls, projektion, emittenten);
    }
}
