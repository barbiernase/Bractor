using Abstractions;

namespace Pruefdomaene.Auftrag;

public record StarteAuftrag(Id<Auftrag> AggregateId, Id<Posten.Posten> PostenId, Id<Kontingent.Kontingent> KontingentId, int Menge) : ICommand<Auftrag>, ICreationCommand;

/// <summary>Die Reservierung des Postens für diesen Auftrag ist gelungen (Rückmeldung der Auftragsabwicklung).</summary>
public record VermerkeReservierung(Id<Auftrag> AggregateId) : ICommand<Auftrag>;

/// <summary>Die Belegung des Kontingents für diesen Auftrag ist gelungen.</summary>
public record VermerkeBelegung(Id<Auftrag> AggregateId) : ICommand<Auftrag>;

/// <summary>Ein Zweig ist abgelehnt (Reservierung oder Belegung): der Auftrag scheitert, was gelang, wird zurückgenommen.</summary>
public record VermerkeAbsage(Id<Auftrag> AggregateId, string Grund) : ICommand<Auftrag>;
