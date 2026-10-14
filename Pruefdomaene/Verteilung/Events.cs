using Abstractions;

namespace Pruefdomaene.Verteilung;

/// <summary>Der Auslöser des <see cref="VerteilProzess"/>: die Breite N steht im Auslöser (Ziele) — kein Zähler im Aggregat.</summary>
public record VerteilungGestartet(Guid VerteilungId, Guid QuelleId, IReadOnlyList<Guid> Ziele, int MengeJeZiel) : IEvent;
public record VerteilungAbgeschlossen(int Zugebucht) : IEvent;

public record VerteilungExistiertBereits(Guid VerteilungId) : ITransientEvent;
public record VerteilungNichtGefunden(Guid VerteilungId) : ITransientEvent;
