using Abstractions;

namespace Pruefdomaene.Verteilung;

/// <summary>Der Auslöser des <see cref="VerteilProzess"/>: die Breite N steht im Auslöser (Ziele) — kein Zähler im Aggregat.</summary>
public record VerteilungGestartet(Id<Verteilung> VerteilungId, Id<Posten.Posten> QuelleId, IReadOnlyList<Id<Posten.Posten>> Ziele, int MengeJeZiel) : IEvent;
public record VerteilungAbgeschlossen(int Zugebucht) : IEvent;

public record VerteilungExistiertBereits(Id<Verteilung> VerteilungId) : ITransientEvent;
public record VerteilungNichtGefunden(Id<Verteilung> VerteilungId) : ITransientEvent;
