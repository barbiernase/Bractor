namespace Infrastructure.Projections;

/// <summary>
/// Die Größen des Batch-Konsums (Umgebung <c>Konsum</c>, docs/04 §4.1): Partitionen je Projektion (E4) und Events je Seite (E11). Dazu die
/// feste Poll-Intervall je Partition. Bis wohin die Position vorrücken darf, sagt der Store (Horizont der Vollständigkeit), keine Uhr.
/// </summary>
public sealed record Konsum(int Partitionen, int BatchGroesse)
{
    /// <summary>E4 und E11 (Plan Garantien und Durchsatz): 8 Partitionen, 500 Events je Seite.</summary>
    public static Konsum Standard { get; } = new(8, 500);

    /// <summary>Der Takt weckt jede Partition in diesem Abstand — das Netz für verlorene Weckrufe.</summary>
    public static readonly TimeSpan PollIntervall = TimeSpan.FromSeconds(30);

    /// <summary>Aus dem Umgebungs-Abschnitt <c>Konsum</c>; Werte außerhalb der Grenzen sind ein Start-Fehler.</summary>
    public static Konsum Aus(Abstractions.KonsumKonfig k)
    {
        if (k.Partitionen is < 1 or > Abstractions.Partitionierung.Hoechstens)
            throw new InvalidOperationException(
                $"Umgebung: Konsum.Partitionen = {k.Partitionen} — erlaubt sind 1 bis {Abstractions.Partitionierung.Hoechstens} (letztes UUID-Byte mod P).");
        if (k.BatchGroesse < 1)
            throw new InvalidOperationException($"Umgebung: Konsum.BatchGroesse = {k.BatchGroesse} — mindestens 1.");
        return new(k.Partitionen, k.BatchGroesse);
    }
}
