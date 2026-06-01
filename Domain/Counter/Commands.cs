using Abstractions;

namespace Domain.Counter;

// ─── LIFECYCLE ───
// ICreationCommand: Client setzt ExpectedVersion = 0,
// erzeugt einen neuen Stream im EventStore.

public record ErstelleCounter(
    Guid AggregateId,
    string Name,
    long? MaxGrenze,
    long? MinGrenze,
    long StartWert
) : ICreationCommand;

// ─── OPERATIONS ───
// Regulärer ICommand: Client setzt ExpectedVersion auf die zuletzt
// bekannte Version. Das Framework prüft Concurrency.

public record Incrementiere(Guid AggregateId, long Delta) : ICommand;
public record Dekrementiere(Guid AggregateId, long Delta) : ICommand;
public record Reset(Guid AggregateId) : ICommand;
public record SetzeMaxGrenze(Guid AggregateId, long? MaxGrenze) : ICommand;
public record SetzeMinGrenze(Guid AggregateId, long? MinGrenze) : ICommand;
public record LoescheCounter(Guid AggregateId) : ICommand;
