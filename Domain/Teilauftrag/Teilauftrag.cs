using Abstractions;

namespace Domain.Teilauftrag;

/// <summary>
/// Aggregat <see cref="Teilauftrag"/> — EIN aufgefächertes Teil (Worker) eines Sammelvorgangs.
///
/// Es kennt seine <see cref="SammelvorgangId"/> (die Korrelation zurück zur Barriere). Wird es fertig,
/// mappt ein Prozess sein Fertig-Event auf <c>MeldeTeilFertig</c> an der Barriere (Fan-in).
/// Einzelner Datenpunkt, keine Collection. Id/Version generiert.
/// </summary>
public partial class Teilauftrag : IState
{
    public Guid SammelvorgangId { get; set; }
    public bool Fertig { get; set; }

    public bool Existiert => Version > 0;
}
