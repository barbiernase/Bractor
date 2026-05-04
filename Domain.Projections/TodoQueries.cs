using Abstractions;

namespace Domain.Projections;

public record GetTodo(Guid TodoId) : IQuery;
public record GetAlleTodos() : IQuery;