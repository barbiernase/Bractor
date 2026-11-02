using Abstractions;

namespace Pruefdomaene.Verteilung;

public record StarteVerteilung(Id<Verteilung> AggregateId, Id<Posten.Posten> QuelleId, IReadOnlyList<Id<Posten.Posten>> Ziele, int MengeJeZiel) : ICommand<Verteilung>, ICreationCommand;

/// <summary>Die Gesamtmenge ist an der Quelle reserviert — die Zubuchungen dürfen los.</summary>
public record VermerkeQuellreservierung(Id<Verteilung> AggregateId) : ICommand<Verteilung>;

/// <summary>Die Quelle reicht nicht: die Verteilung scheitert, nichts wird zugebucht.</summary>
public record VermerkeQuellabsage(Id<Verteilung> AggregateId, int Verfuegbar) : ICommand<Verteilung>;

/// <summary>Ein Ziel ist zugebucht (je Ziel genau einmal: die Command-Id folgt aus der Zubuchung, die Inbox verwirft das Zweite).</summary>
public record ZaehleZubuchung(Id<Verteilung> AggregateId, int Menge) : ICommand<Verteilung>;

/// <summary>
/// Ein Command ohne Ziel aus dem Decide (docs/03 §3.2): die Verteilung ist gestartet — die Abwicklung reserviert die Quelle. Was die Ziele darauf entscheiden, hört sie als
/// Antwort auf ihre Commands (docs/04 §4.4), nicht aus dem Log.
/// </summary>
public record VerteilungAbzuwickeln(Id<Verteilung> VerteilungId, Id<Posten.Posten> QuelleId, IReadOnlyList<Id<Posten.Posten>> Ziele, int MengeJeZiel) : ICommand;

/// <summary>Ein Command ohne Ziel aus dem Decide: die Quelle ist reserviert, jedes Ziel wird zugebucht (FAN-OUT, gesendet von der Verteilabwicklung).</summary>
public record Zubuchen(Id<Verteilung> VerteilungId, IReadOnlyList<Id<Posten.Posten>> Ziele, int MengeJeZiel) : ICommand;
