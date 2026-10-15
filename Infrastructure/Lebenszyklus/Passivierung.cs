using Proto;

namespace Infrastructure.Lebenszyklus;

/// <summary>
/// Passivierung der virtuellen Actors (PF-5, docs/04 §4.1/§4.5, Umgebung <c>Lebenszyklus</c>): erhält ein Actor so lange keine Nachricht, stoppt er
/// sich selbst; der Cluster vergisst die Aktivierung, die nächste Nachricht aktiviert ihn frisch. Der durable Zustand liegt nie im Actor —
/// Aggregat (Log, Snapshot beim Stoppen), Adapter (Marke bzw. Cursor), Prozess-Manager (Log, Marking-Cursor) — deshalb ist das folgenlos für die
/// Garantien und hält den Speicher im Dauerbetrieb flach. <see cref="TimeSpan.Zero"/> = nie passivieren.
/// </summary>
public sealed record Passivierung(TimeSpan Aggregat, TimeSpan Adapter, TimeSpan ProzessManager)
{
    /// <summary>E7 (Plan Garantien und Durchsatz): Aggregat 10 min, Adapter 2 min, Prozess-Manager 10 min.</summary>
    public static Passivierung Standard { get; } = new(TimeSpan.FromMinutes(10), TimeSpan.FromMinutes(2), TimeSpan.FromMinutes(10));

    public static Passivierung Aus(Abstractions.LebenszyklusKonfig k) => new(
        TimeSpan.FromSeconds(k.AggregatLeerlaufSekunden),
        TimeSpan.FromSeconds(k.AdapterLeerlaufSekunden),
        TimeSpan.FromSeconds(k.ProzessManagerLeerlaufSekunden));

    /// <summary>
    /// Die eine Regel für alle drei Actor-Arten: bei <see cref="Started"/> den Leerlauf-Wecker stellen, bei <see cref="ReceiveTimeout"/> stoppen.
    /// Liefert true, wenn die Nachricht damit erledigt ist.
    /// </summary>
    public static bool Behandle(IContext context, TimeSpan leerlauf)
    {
        switch (context.Message)
        {
            case Started when leerlauf > TimeSpan.Zero:
                context.SetReceiveTimeout(leerlauf);
                return false;   // Started behandelt der Actor weiter selbst
            case ReceiveTimeout:
                context.Stop(context.Self);
                return true;
            default:
                return false;
        }
    }
}
