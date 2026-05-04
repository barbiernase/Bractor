using Abstractions;

namespace Domain.Todo;

public record ErstelleTodo(
    Guid AggregateId,
    string Titel,
    Prioritaet Prioritaet,
    string? Beschreibung = null,
    DateTimeOffset? Faelligkeit = null) : ICreationCommand;

public record AendereTitel(Guid AggregateId, string NeuerTitel) : ICommand;
public record SetzePrioritaet(Guid AggregateId, Prioritaet NeuePrioritaet) : ICommand;
public record MarkiereAlsErledigt(Guid AggregateId) : ICommand;
public record OeffneWieder(Guid AggregateId) : ICommand;
public record Archiviere(Guid AggregateId) : ICommand;
public record FuegeTagHinzu(Guid AggregateId, string Tag) : ICommand;
public record EntferneTag(Guid AggregateId, string Tag) : ICommand;