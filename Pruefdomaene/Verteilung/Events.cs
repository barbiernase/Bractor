using Abstractions;

namespace Pruefdomaene.Verteilung;

/// <summary>Die Verteilung beginnt: die Breite N steht im Start (Ziele).</summary>
public record VerteilungGestartet(Id<Verteilung> VerteilungId, Id<Posten.Posten> QuelleId, IReadOnlyList<Id<Posten.Posten>> Ziele, int MengeJeZiel) : IEvent;

/// <summary>Die Quelle ist reserviert: die Verteilung bucht ihre Ziele zu (der Command <see cref="Zubuchen"/> geht an die Verteilabwicklung).</summary>
public record QuelleReserviert() : IEvent;

/// <summary>Alle Ziele sind gebucht — mit der Menge, die tatsächlich zugebucht wurde.</summary>
public record VerteilungAbgeschlossen(int Zugebucht) : IEvent;

/// <summary>Die Quelle reichte nicht — nichts wurde reserviert, nichts wird zugebucht.</summary>
public record VerteilungGescheitert(int Verfuegbar) : IEvent;

public record VerteilungExistiertBereits(Id<Verteilung> VerteilungId) : ITransientEvent;
public record VerteilungNichtGefunden(Id<Verteilung> VerteilungId) : ITransientEvent;
