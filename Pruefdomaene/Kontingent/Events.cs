using Abstractions;
using Pruefdomaene.Posten;

namespace Pruefdomaene.Kontingent;

public record KontingentEingerichtet(int Plaetze) : IEvent;
public record KontingentBelegt(int Plaetze, Beleg Beleg) : IEvent;
public record KontingentEntlastet(int Plaetze, Beleg Beleg) : IEvent;

/// <summary>Die Belegung für den Beleg ist abgelehnt (zu wenig frei) — ein Fakt, weil der Vorgang des Belegs an ihr hängt.</summary>
public record BelegungAbgelehnt(int Frei, int Angefordert, Beleg Beleg) : IEvent;

public record KontingentExistiertBereits(Id<Kontingent> KontingentId) : ITransientEvent;
public record KontingentNichtGefunden(Id<Kontingent> KontingentId) : ITransientEvent;
