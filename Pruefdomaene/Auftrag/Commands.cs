using Abstractions;

namespace Pruefdomaene.Auftrag;

public record StarteAuftrag(Id<Auftrag> AggregateId, Id<Posten.Posten> PostenId, Id<Kontingent.Kontingent> KontingentId, int Menge) : ICommand<Auftrag>, ICreationCommand;

/// <summary>Die Reservierung des Postens für diesen Auftrag ist gelungen (Rückmeldung der Auftragsabwicklung).</summary>
public record VermerkeReservierung(Id<Auftrag> AggregateId) : ICommand<Auftrag>;

/// <summary>Die Belegung des Kontingents für diesen Auftrag ist gelungen.</summary>
public record VermerkeBelegung(Id<Auftrag> AggregateId) : ICommand<Auftrag>;

/// <summary>Ein Zweig ist abgelehnt (Reservierung oder Belegung): der Auftrag scheitert, was gelang, wird zurückgenommen.</summary>
public record VermerkeAbsage(Id<Auftrag> AggregateId, string Grund) : ICommand<Auftrag>;

/// <summary>
/// Ein Command ohne Ziel aus dem Decide (docs/03 §3.2): der Auftrag ist gestartet — die Abwicklung reserviert den Posten und belegt das Kontingent. Was die Ziele darauf
/// entscheiden, hört sie als Antwort auf ihre Commands (docs/04 §4.4), nicht aus dem Log.
/// </summary>
public record AuftragAbzuwickeln(Id<Auftrag> AuftragId, Id<Pruefdomaene.Posten.Posten> PostenId, Id<Pruefdomaene.Kontingent.Kontingent> KontingentId, int Menge) : ICommand;

/// <summary>
/// KOMPENSATION, ausgeschrieben (docs/13 §13.7): der Auftrag ist gescheitert, die Reservierung war gelungen — sie muss zurück. Ein Command ohne Ziel
/// aus dem Decide (genau einmal: nach dem Scheitern entscheidet das Aggregat keine weitere Absage); die Auftragsabwicklung gibt den Posten frei.
/// </summary>
public record PostenFreizugeben(Id<Auftrag> AuftragId, Id<Pruefdomaene.Posten.Posten> PostenId, int Menge) : ICommand;

/// <summary>Gegenzug der gelungenen Belegung eines gescheiterten Auftrags (wie <see cref="PostenFreizugeben"/>).</summary>
public record KontingentZuEntlasten(Id<Auftrag> AuftragId, Id<Pruefdomaene.Kontingent.Kontingent> KontingentId, int Menge) : ICommand;
