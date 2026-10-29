using Abstractions;
using Infrastructure.Lebenszyklus;
using Infrastructure.Persistence;
using Infrastructure.Projections;
using Microsoft.Extensions.Logging;
using Proto.Cluster;

namespace Infrastructure.Wecken;

// ═══════════════════════════════════════════════════════════════════════════════════════════════════════════════════════
// WECKEN (docs/03 §3.5): es gibt EINE Stelle, an der nach einem Commit geweckt wird — nach dem Commit eines
// Appends (Group-Commit, sein Rückfall auf Einzel-Appends, der Einzel-Append ohne Bündeln). Alle Schreiber gehen durch sie: Aggregat, Quelle,
// festgehaltene Ausgabe einer Funktion. Der generierte Weckplan sagt je Event-Typ, wen der Commit weckt; geweckt wird gezielt, je Commit und Ziel
// einmal, ohne Inhalt — der Weckruf trägt nur sein Ziel und darf verloren, doppelt, ungeordnet sein. Das Ziel liest ab seiner eigenen Position
// im Log; was ein verlorener Weckruf liegen lässt, holt der Takt je Partition (30 s). Kein Broker, keine Abos, keine Signale.
// ═══════════════════════════════════════════════════════════════════════════════════════════════════════════════════════

/// <summary>
/// Ein Ziel des Weckplans: das Cluster-Kind eines Konsumenten (Batch-Leser einer Projektion, Funktions-Actor, Uhr) und ob er EINE Adresse hat
/// (<see cref="Funktionsvertrag.EinzigeIdentitaet"/>) oder je Partition des Streams liest.
/// </summary>
public sealed record Weckziel(string Kind, bool EineAdresse = false)
{
    /// <summary>Die Identität, die die Events des Streams <paramref name="stream"/> liest.</summary>
    public string Identitaet(Guid stream, int partitionen) =>
        EineAdresse ? Funktionsvertrag.EinzigeIdentitaet : Partitionierung.Von(stream, partitionen).ToString();
}

/// <summary>
/// Der Wecker eines Knotens: schlägt je geschriebenem Event den Weckplan nach und weckt jedes Ziel einmal je Commit. Je Ziel ist höchstens ein
/// Weckruf unterwegs; was währenddessen kommt, wird zu genau einem weiteren (<see cref="Weckungen{TKey}"/>) — so staut ein Commit-Strom weder
/// die Mailbox des Ziels noch die offenen Anfragen des Knotens (PF-12). Das Ziel fasst seinerseits zusammen (<see cref="RahmenActor"/>).
/// Gesendet wird über den <see cref="Bote"/> mit Frist: verlierbar wie <c>Melde</c>, nur dass der Wecker das Ende des Weckrufs abwartet, bevor
/// er dasselbe Ziel erneut weckt.
/// </summary>
public sealed class Wecker
{
    /// <summary>Wie lange ein Weckruf höchstens unterwegs ist (Platzierung inklusive), bevor er als verloren gilt.</summary>
    public static readonly TimeSpan Frist = TimeSpan.FromSeconds(10);

    private readonly IReadOnlyDictionary<Type, Weckziel[]> _plan;
    private readonly int _partitionen;
    private readonly Weckungen<(string Kind, string Identitaet)> _weckungen;

    /// <param name="plan">Event-Typ → Ziele (generiert: <c>GeneratedWeckplan.Plan</c>, über <c>Anwendung.Weckplan</c>).</param>
    /// <param name="bote">baut den Boten beim ersten Weckruf (der Cluster steht dann).</param>
    public Wecker(IReadOnlyDictionary<Type, Weckziel[]> plan, Konsum konsum, Func<Bote> bote)
    {
        _plan = plan;
        _partitionen = konsum.Partitionen;
        var b = new Lazy<Bote>(bote);
        _weckungen = new Weckungen<(string Kind, string Identitaet)>(z =>
            b.Value.FrageAsync<WakeAck>(ClusterIdentity.Create(z.Identitaet, z.Kind), new Wake(), Frist, CancellationToken.None));
    }

    /// <summary>Der Weckplan dieses Weckers.</summary>
    public IReadOnlyDictionary<Type, Weckziel[]> Plan => _plan;

    /// <summary>Nach dem Commit eines Group-Commits: jedes Ziel aller Appends einmal.</summary>
    public void Geschrieben(IReadOnlyList<BatchAppend> batch)
    {
        var ziele = new HashSet<(string, string)>();
        foreach (var item in batch) Sammle(item.StreamId, item.Teile, ziele);
        foreach (var z in ziele) _weckungen.Wecke(z);
    }

    /// <summary>Nach dem Commit eines Einzel-Appends.</summary>
    public void Geschrieben(Guid stream, IReadOnlyList<AppendTeil> teile)
    {
        var ziele = new HashSet<(string, string)>();
        Sammle(stream, teile, ziele);
        foreach (var z in ziele) _weckungen.Wecke(z);
    }

    /// <summary>Weckt ein Ziel direkt (der Takt je Partition).</summary>
    public void Wecke(string kind, string identitaet) => _weckungen.Wecke((kind, identitaet));

    /// <summary>Die Ziele, die Events eines Streams wecken (Kind, Identität) — je Ziel einmal.</summary>
    public IReadOnlyCollection<(string Kind, string Identitaet)> Ziele(Guid stream, IEnumerable<IEvent> events)
    {
        var ziele = new HashSet<(string, string)>();
        foreach (var e in events)
            if (_plan.TryGetValue(e.GetType(), out var plan))
                foreach (var z in plan) ziele.Add((z.Kind, z.Identitaet(stream, _partitionen)));
        return ziele;
    }

    private void Sammle(Guid stream, IReadOnlyList<AppendTeil> teile, HashSet<(string, string)> ziele)
    {
        foreach (var teil in teile)
            foreach (var e in teil.Events)
                if (_plan.TryGetValue(e.GetType(), out var plan))
                    foreach (var z in plan) ziele.Add((z.Kind, z.Identitaet(stream, _partitionen)));
    }

    /// <summary>
    /// Die Prüfung des Weckplans beim Start (docs/13 §13.7): jeder registrierte Konsument steht mit jedem Typ, den er hört, im Plan — mit seinem
    /// Kind und seiner Adressierung. Eine Lücke merkte man sonst nicht am Verlust, sondern nur an der Latenz bis zum Takt.
    /// </summary>
    /// <exception cref="Infrastructure.Umgebung.BootFehler">Ein Hörer fehlt im Plan.</exception>
    public static void Pruefe(IReadOnlyDictionary<Type, Weckziel[]> plan, IEnumerable<BatchRegistrierung> registrierungen)
    {
        foreach (var reg in registrierungen)
            foreach (var typ in reg.EventTypes)
                if (!plan.TryGetValue(typ, out var ziele) || !ziele.Contains(new Weckziel(reg.KindName, reg.EineAdresse)))
                    throw new Infrastructure.Umgebung.BootFehler(
                        $"Weckplan: {reg.ProjektionsId} ('{reg.KindName}') hört {typ.Name}, steht damit aber nicht im Weckplan — ein Commit weckte ihn nie, "
                        + "nur der Takt (GR-WECKPLAN).", "weckplan:" + reg.KindName);
    }
}
