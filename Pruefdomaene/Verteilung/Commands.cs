using Abstractions;

namespace Pruefdomaene.Verteilung;

public record StarteVerteilung(Id<Verteilung> AggregateId, Id<Posten.Posten> QuelleId, IReadOnlyList<Id<Posten.Posten>> Ziele, int MengeJeZiel) : ICommand<Verteilung>, ICreationCommand;

/// <summary>Die Gesamtmenge ist an der Quelle reserviert — die Zubuchungen dürfen los.</summary>
public record VermerkeQuellreservierung(Id<Verteilung> AggregateId) : ICommand<Verteilung>;

/// <summary>Die Quelle reicht nicht: die Verteilung scheitert, nichts wird zugebucht.</summary>
public record VermerkeQuellabsage(Id<Verteilung> AggregateId, int Verfuegbar) : ICommand<Verteilung>;

/// <summary>Ein Ziel ist gebucht: die Verteilung zählt mit und schließt nach dem letzten ab (je Ziel genau einmal: die Command-Id folgt aus der Kette).</summary>
public record VermerkeZubuchung(Id<Verteilung> AggregateId, int Menge) : ICommand<Verteilung>;
