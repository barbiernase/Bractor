using Abstractions;
using Domain.Todo;

namespace Domain.Projections;

/// <summary>
/// ReadModel für Todos.
/// Immutable Record — Updates erzeugen neue Instanzen via with-Expression.
/// </summary>
public record TodoReadModel : IReadModel
{
    public Guid Id { get; init; }
    public string Titel { get; init; } = string.Empty;
    public string? Beschreibung { get; init; }
    public TodoStatus Status { get; init; }
    public Prioritaet Prioritaet { get; init; }
    public DateTimeOffset? Faelligkeit { get; init; }
    public DateTimeOffset ErstelltAm { get; init; }
    public DateTimeOffset? ErledigtAm { get; init; }
    public IReadOnlyList<string> Tags { get; init; } = Array.Empty<string>();
}