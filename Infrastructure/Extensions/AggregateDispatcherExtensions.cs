using System;
using Abstractions;
using Infrastructure.Pipeline;   // GeneratedPipelines.CommandAggregateTypes

namespace Infrastructure.Extensions;

/// <summary>
/// Sprechender Einstiegs-Dispatch OHNE Magic String: der Anwender schreibt nur den Command. Woher weiß das
/// Framework, an welches Aggregat er geht? Aus derselben generierten Map, die auch der Prozess-Manager und
/// der <c>HandlerOutputRouter</c> nutzen — <see cref="GeneratedPipelines.CommandAggregateTypes"/> (Command-Typ
/// → Aggregat-Klassenname, aus der Namespace-Konvention abgeleitet). Die Instanz-Id trägt der Command bereits
/// selbst (<see cref="ICommand.AggregateId"/>). So entfällt der bisher stringly-typed
/// <c>AggregateType</c>-Parameter am Aufrufort; ein Rename kann nicht mehr stumm driften.
///
/// Der bestehende <see cref="IAggregateDispatcher.Dispatch(CommandEnvelope)"/> bleibt unangetastet (explizite
/// <c>ExpectedVersion</c>/Korrelation für Fortgeschrittene); diese Überladung baut den Envelope nur bequem.
/// </summary>
public static class AggregateDispatcherExtensions
{
    /// <summary>
    /// Dispatcht einen Command an sein Ziel-Aggregat. <c>AggregateType</c> leitet sich aus
    /// <see cref="GeneratedPipelines.CommandAggregateTypes"/> ab, <c>AggregateId</c> aus dem Command selbst.
    /// </summary>
    /// <param name="expectedVersion">
    /// OCC-Erwartung; Default 0 (leerer Stream — der übliche Fall für einen Einstiegs-/Erzeugungs-Command).
    /// </param>
    public static void Dispatch(this IAggregateDispatcher dispatcher, ICommand command, int expectedVersion = 0)
    {
        dispatcher.Dispatch(new CommandEnvelope
        {
            AggregateId = command.AggregateId,
            AggregateType = ResolveAggregateType(command),
            Modus = new CommandModus.Client(expectedVersion),
            Payload = command,
        });
    }

    /// <summary>
    /// Der Aggregat-Klassenname für einen Command aus der generierten Map. Wirft sprechend, wenn die
    /// Zuordnung fehlt — praktisch immer, weil der Command NICHT im selben Namespace wie sein
    /// <c>IState</c>-Aggregat liegt (die Konvention, aus der der Generator die Map baut).
    /// </summary>
    public static string ResolveAggregateType(ICommand command)
    {
        if (GeneratedPipelines.CommandAggregateTypes.TryGetValue(command.GetType(), out var aggregateType))
            return aggregateType;

        throw new InvalidOperationException(
            $"Kein Ziel-Aggregat für Command '{command.GetType().Name}'. Die Zuordnung Command→Aggregat " +
            $"leitet der Generator aus dem Namespace ab: der Command muss im selben Namespace liegen wie " +
            $"das zugehörige IState-Aggregat.");
    }
}
