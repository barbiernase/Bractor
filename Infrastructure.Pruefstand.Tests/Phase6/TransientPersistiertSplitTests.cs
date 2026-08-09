using System;
using System.Linq;
using Abstractions;
using FluentAssertions;
using Xunit;

namespace Infrastructure.Pruefstand.Phase6;

/// <summary>
/// P6.2 — die Achse, die entscheidet, welcher Event-Handler einer Pipeline auf Pull wandert und welcher
/// auf dem Push-Broker BLEIBT. Persistierte Events (<see cref="IEvent"/>, im Log) → Pull-Maschine
/// (geordnet, geheilt). Transiente Events (<see cref="ITransientEvent"/>, NICHT im Log) können nicht auf
/// Pull (der Pull-Pfad liest Log-Streams) → sie bleiben per Invariante 6 auf dem verlierbaren Push-Kanal.
/// Dieselbe Klassifikation nutzen der generierte AddGeneratedPipelineEventPulls (persistiert-Filter) und
/// PipelineActorBase.OnStartedAsync (transient-Filter).
/// </summary>
public class TransientPersistiertSplitTests
{
    private sealed record PersistiertesEvent : IEvent;
    private sealed record TransientesEvent : ITransientEvent;

    private static readonly Type[] Abonniert = { typeof(PersistiertesEvent), typeof(TransientesEvent) };

    [Fact]
    public void Persistierte_Events_gehen_auf_Pull()
    {
        var persistiert = Abonniert.Where(t => !typeof(ITransientEvent).IsAssignableFrom(t)).ToList();
        persistiert.Should().ContainSingle().Which.Should().Be(typeof(PersistiertesEvent));
    }

    [Fact]
    public void Transiente_Events_bleiben_auf_Push()
    {
        var transient = Abonniert.Where(t => typeof(ITransientEvent).IsAssignableFrom(t)).ToList();
        transient.Should().ContainSingle().Which.Should().Be(typeof(TransientesEvent));
    }

    [Fact]
    public void Die_beiden_Filter_partitionieren_disjunkt_und_vollstaendig()
    {
        var persistiert = Abonniert.Where(t => !typeof(ITransientEvent).IsAssignableFrom(t));
        var transient = Abonniert.Where(t => typeof(ITransientEvent).IsAssignableFrom(t));
        persistiert.Concat(transient).Should().BeEquivalentTo(Abonniert);
        persistiert.Intersect(transient).Should().BeEmpty();
    }
}
