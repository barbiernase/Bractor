using Abstractions;

namespace Domain.Counter;

/// <summary>
/// Aggregat-Zustand. Innerhalb starke Konsistenz, dazwischen
/// nur Eventually Consistency. Die nested partial classes
/// Decider und Applier leben in eigenen Dateien.
/// </summary>
public partial class Counter : IState
{
    // ── Framework-Pflichtfelder
    /*
    public Guid Id { get; set; }
    public int Version { get; set; }
    */

    // ── Fachliche Felder
    public string Name { get; set; } = "";
    public long Wert { get; set; }
    public long? MaxGrenze { get; set; }
    public long? MinGrenze { get; set; }
    public bool IstGeloescht { get; set; }

    public DateTimeOffset ErstelltAm { get; set; }
    public DateTimeOffset? LetzteAenderungAm { get; set; }
}
