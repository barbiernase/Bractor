namespace Infrastructure.Lebenszyklus;

/// <summary>
/// Passivierung der Aggregat-Actors (PF-5, Umgebung <c>Lebenszyklus</c>): erhält ein Actor so lange keine Nachricht, stoppt er sich selbst; der
/// Cluster vergisst die Aktivierung, die nächste Nachricht aktiviert ihn frisch. Der durable Zustand liegt nie im Actor (Log, Snapshot beim
/// Stoppen, Inbox-Tabelle) — deshalb ist das folgenlos für die Garantien und hält den Speicher im Dauerbetrieb flach. <see cref="TimeSpan.Zero"/> =
/// nie passivieren. Die Regel selbst trägt der Rahmen jedes Actors (<see cref="RahmenActor"/>): stoppen nach dem Leerlauf, nie während er
/// beschäftigt ist. Batch-Leser und Funktions-Actors passivieren nicht.
/// </summary>
public sealed record Passivierung(TimeSpan Aggregat)
{
    /// <summary>E7 (Plan Garantien und Durchsatz): Aggregat 10 min.</summary>
    public static Passivierung Standard { get; } = new(TimeSpan.FromMinutes(10));

    /// <summary>Aus dem Umgebungs-Abschnitt <c>Lebenszyklus</c>.</summary>
    public static Passivierung Aus(Abstractions.LebenszyklusKonfig k) => new(TimeSpan.FromSeconds(k.AggregatLeerlaufSekunden));
}
