using Proto;
using Proto.Cluster;

namespace Infrastructure.Pipeline;

/// <summary>
/// Registrierung eines Trigger-Actors.
/// Domain.Infrastructure registriert konkrete Instanzen im DI-Container.
/// TriggerStartupService iteriert und spawnt.
///
/// Lebt in Infrastructure (nicht Abstractions) weil Props und Cluster
/// Proto.Actor-Typen sind. Domain.Pipeline.Infrastructure referenziert
/// Infrastructure und kann TriggerRegistration-Instanzen erzeugen.
/// </summary>
public interface ITriggerRegistration
{
    string Name { get; }
    Props CreateProps(IServiceProvider provider, Cluster cluster);
}

/// <summary>
/// Convenience-Record für einfache Trigger-Registrierungen.
/// </summary>
public record TriggerRegistration(
    string Name,
    Func<IServiceProvider, Cluster, Props> Factory
) : ITriggerRegistration
{
    public Props CreateProps(IServiceProvider provider, Cluster cluster)
        => Factory(provider, cluster);
}