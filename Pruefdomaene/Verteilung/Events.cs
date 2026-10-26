using Abstractions;

namespace Pruefdomaene.Verteilung;

/// <summary>Die Verteilung beginnt: die Breite N steht im Start (Ziele) — das Aggregat zählt nur, wie viele angekommen sind.</summary>
public record VerteilungGestartet(Id<Verteilung> VerteilungId, Id<Posten.Posten> QuelleId, IReadOnlyList<Id<Posten.Posten>> Ziele, int MengeJeZiel) : IEvent;

/// <summary>Die Quelle ist reserviert: jedes Ziel wird zugebucht (FAN-OUT, gesendet von der Verteilabwicklung).</summary>
public record ZubuchungenAngeordnet(IReadOnlyList<Id<Posten.Posten>> Ziele, int MengeJeZiel) : IEvent;

/// <summary>Ein Ziel ist angekommen (COUNT-JOIN: ein Zähler, keine Liste).</summary>
public record ZubuchungGezaehlt(int Menge) : IEvent;

/// <summary>Alle N Ziele sind zugebucht.</summary>
public record VerteilungAbgeschlossen(int Zugebucht) : IEvent;

/// <summary>Die Quelle reichte nicht — nichts wurde reserviert, nichts wird zugebucht.</summary>
public record VerteilungGescheitert(int Verfuegbar) : IEvent;

public record VerteilungExistiertBereits(Id<Verteilung> VerteilungId) : ITransientEvent;
public record VerteilungNichtGefunden(Id<Verteilung> VerteilungId) : ITransientEvent;
