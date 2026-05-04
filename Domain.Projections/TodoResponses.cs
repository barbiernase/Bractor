using Abstractions;
using Domain.Todo;

namespace Domain.Projections;

public record TodoAntwort(
    Guid Id, string Titel, string? Beschreibung,
    TodoStatus Status, Prioritaet Prioritaet,
    DateTimeOffset? Faelligkeit, DateTimeOffset ErstelltAm,
    DateTimeOffset? ErledigtAm,
    IReadOnlyList<string> Tags) : IQueryResponse;

public record TodoNichtGefundenAntwort(Guid TodoId) : IQueryResponse;

public record TodoListe(IReadOnlyList<TodoAntwort> Items) : IQueryResponse;