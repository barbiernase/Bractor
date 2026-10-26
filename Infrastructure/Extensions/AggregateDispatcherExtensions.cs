using System;
using Abstractions;
using Infrastructure.Komposition;
using Infrastructure.Pipeline;   // Anwendung.Pipelines.CommandAggregateTypes

namespace Infrastructure.Extensions;

/// <summary>
/// Sprechender Einstiegs-Dispatch OHNE Magic String: der Anwender schreibt nur den Command. Ziel-Aggregat und Id stehen im Typ des Commands
/// (<see cref="ICommand{TAggregat}"/>, Plan §2.5): den Aggregat-Namen liefert die generierte Routing-Tabelle
/// (<see cref="GeneratedPipelines.CommandAggregateTypes"/>), die Id der Command selbst (<see cref="ICommand.Ziel"/>).
/// </summary>
public static class AggregateDispatcherExtensions
{
    /// <summary>Dispatcht einen Command an sein Ziel-Aggregat.</summary>
    /// <param name="expectedVersion">
    /// OCC-Erwartung; Default 0 (leerer Stream — der übliche Fall für einen Einstiegs-/Erzeugungs-Command).
    /// </param>
    public static void Dispatch(this IAggregateDispatcher dispatcher, ICommand command, int expectedVersion = 0)
    {
        dispatcher.Dispatch(new CommandEnvelope
        {
            AggregateType = ResolveAggregateType(command),
            Modus = new CommandModus.Client(expectedVersion),
            Payload = command,
        });
    }

    /// <summary>
    /// Der Aggregat-Name (Cluster-Kind) für einen Command aus der generierten Routing-Tabelle. Sie enthält jeden Command der Anwendung — das
    /// Ziel steht in <see cref="ICommand{TAggregat}"/> —; fehlt einer, gehört er nicht zu dieser Anwendung.
    /// </summary>
    public static string ResolveAggregateType(ICommand command)
    {
        if (Anwendung.Pipelines.CommandAggregateTypes.TryGetValue(command.GetType(), out var aggregateType))
            return aggregateType;

        throw new InvalidOperationException(
            $"Kein Ziel-Aggregat für Command '{command.GetType().Name}': er gehört nicht zur Routing-Tabelle dieser Anwendung.");
    }
}
