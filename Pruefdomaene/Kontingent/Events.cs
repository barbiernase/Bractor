using Abstractions;

namespace Pruefdomaene.Kontingent;

public record KontingentEingerichtet(int Plaetze) : IEvent;
public record KontingentBelegt(int Plaetze) : IEvent;
public record KontingentEntlastet(int Plaetze) : IEvent;

public record KontingentExistiertBereits(Guid KontingentId) : ITransientEvent;
public record KontingentNichtGefunden(Guid KontingentId) : ITransientEvent;
public record KontingentErschoepft(Guid KontingentId, int Frei, int Angefordert) : ITransientEvent;
