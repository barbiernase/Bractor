using Abstractions;

namespace Pruefdomaene.Kontingent;

public record KontingentEingerichtet(int Plaetze) : IEvent;
public record KontingentBelegt(int Plaetze) : IEvent;
public record KontingentEntlastet(int Plaetze) : IEvent;

public record KontingentExistiertBereits(Id<Kontingent> KontingentId) : ITransientEvent;
public record KontingentNichtGefunden(Id<Kontingent> KontingentId) : ITransientEvent;
public record KontingentErschoepft(Id<Kontingent> KontingentId, int Frei, int Angefordert) : ITransientEvent;
