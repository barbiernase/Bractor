namespace Infrastructure.Lebenszyklus;

/// <summary>
/// Passivierung der virtuellen Actors (PF-5, docs/04 §4.1/§4.5, Umgebung <c>Lebenszyklus</c>): erhält ein Actor so lange keine Nachricht, stoppt er
/// sich selbst; der Cluster vergisst die Aktivierung, die nächste Nachricht aktiviert ihn frisch. Der durable Zustand liegt nie im Actor —
/// Aggregat (Log, Snapshot beim Stoppen), Adapter (Marke bzw. Cursor), Prozess-Manager (Log, Marking-Cursor) — deshalb ist das folgenlos für die
/// Garantien und hält den Speicher im Dauerbetrieb flach. <see cref="TimeSpan.Zero"/> = nie passivieren. Die Regel selbst trägt der Rahmen
/// jedes Actors (<see cref="RahmenActor"/>): stoppen nach dem Leerlauf, nie während er beschäftigt ist.
/// </summary>
public sealed record Passivierung(TimeSpan Aggregat, TimeSpan Adapter, TimeSpan ProzessManager)
{
    /// <summary>E7 (Plan Garantien und Durchsatz): Aggregat 10 min, Adapter 2 min, Prozess-Manager 10 min.</summary>
    public static Passivierung Standard { get; } = new(TimeSpan.FromMinutes(10), TimeSpan.FromMinutes(2), TimeSpan.FromMinutes(10));

    /// <summary>
    /// Kürzester erlaubter Leerlauf des Prozess-Managers: 4 × das Backstop-Intervall (15 s). Der Backstop weckt jeden offenen Vorgang
    /// regelmäßig; passivierte der Manager dazwischen, faltete jede Backstop-Weckung kalt — unter Last eine Todesspirale (gemessen 2026-10-15
    /// mit 10 s: Störungslauf, 3 500 Vorgänge blieben liegen).
    /// </summary>
    public static readonly TimeSpan MindestProzessManager = TimeSpan.FromSeconds(60);

    /// <summary>Aus dem Umgebungs-Abschnitt <c>Lebenszyklus</c>; ein zu kurzer Leerlauf des Prozess-Managers ist ein Start-Fehler.</summary>
    public static Passivierung Aus(Abstractions.LebenszyklusKonfig k)
    {
        var manager = TimeSpan.FromSeconds(k.ProzessManagerLeerlaufSekunden);
        if (manager > TimeSpan.Zero && manager < MindestProzessManager)
            throw new InvalidOperationException(
                $"Umgebung: Lebenszyklus.ProzessManagerLeerlaufSekunden = {k.ProzessManagerLeerlaufSekunden} ist kürzer als "
                + $"{MindestProzessManager.TotalSeconds:0} s (4 × Backstop) — offene Vorgänge würden zwischen zwei Backstop-Weckungen passiviert "
                + "und bei jeder Weckung kalt gefaltet. 0 (nie) oder mindestens 60.");
        return new(TimeSpan.FromSeconds(k.AggregatLeerlaufSekunden), TimeSpan.FromSeconds(k.AdapterLeerlaufSekunden), manager);
    }
}
