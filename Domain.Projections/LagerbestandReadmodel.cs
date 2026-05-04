using System;
using Abstractions;

namespace Domain.Projections;

/// <summary>
/// ReadModel für Lagerbestände.
/// Immutable Record — Updates erzeugen neue Instanzen via with-Expression.
/// </summary>
public record LagerbestandReadModel : IReadModel
{
    public Guid Id { get; init; }
    public string Name { get; init; } = string.Empty;
    public int Anzahl { get; init; }
    public DateTimeOffset LetzteAktualisierung { get; init; }
}