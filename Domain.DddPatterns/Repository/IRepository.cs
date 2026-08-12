using Abstractions;

namespace Domain.DddPatterns.Repository;

/// <summary>
/// Ein Aggregat, das seine seit dem Laden erzeugten Ereignisse herausgibt und deren
/// Übernahme quittiert. Der Vertrag, den ein event-sourced <see cref="IRepository{T}"/>
/// braucht, um zu wissen, WAS zu persistieren ist — ohne das Aggregat zu kennen.
/// </summary>
public interface IEreignisGespeist<out TEreignis>
{
    IReadOnlyList<TEreignis> NeueEreignisse { get; }
    void EreignisseUebernommen();
}

/// <summary>
/// DDD-Baustein REPOSITORY — die Illusion einer In-Memory-Sammlung von Aggregaten. Der
/// Fachcode sagt „lade Bestellung X" / „speichere Bestellung X"; wie das auf einen
/// Event-Stream + Replay abbildet, bleibt verborgen. Genau die Trennung, die auch das
/// echte Framework macht (dort ist der Marten-Event-Store das Repository).
/// </summary>
public interface IRepository<TAggregat> where TAggregat : IState
{
    Task<TAggregat?> LadeAsync(Guid id, CancellationToken ct = default);
    Task SpeichereAsync(TAggregat aggregat, CancellationToken ct = default);
}
