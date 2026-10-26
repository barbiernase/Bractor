using Abstractions;

namespace Pruefdomaene.Auftrag;

/// <summary>Der Auftrag beginnt: die Auftragsabwicklung reserviert den Posten und belegt das Kontingent (zwei unabhängige Zweige).</summary>
public record AuftragGestartet(Id<Auftrag> AuftragId, Id<Posten.Posten> PostenId, Id<Kontingent.Kontingent> KontingentId, int Menge) : IEvent;

public record ReservierungVermerkt() : IEvent;
public record BelegungVermerkt() : IEvent;

/// <summary>Beide Zweige sind gelungen, keiner abgelehnt.</summary>
public record AuftragBestaetigt() : IEvent;

/// <summary>Der erste abgelehnte Zweig: der Auftrag wird nie bestätigt.</summary>
public record AuftragGescheitert(string Grund) : IEvent;

/// <summary>
/// KOMPENSATION, ausgeschrieben (Plan Risiko 4): der Auftrag ist gescheitert, die Reservierung war gelungen — sie muss zurück. Das Aggregat entscheidet
/// den Gegenzug genau einmal (sein Zustand merkt es sich), die Auftragsabwicklung sendet ihn.
/// </summary>
public record PostenFreigabeAngeordnet(Id<Posten.Posten> PostenId, int Menge) : IEvent;

/// <summary>Gegenzug der gelungenen Belegung eines gescheiterten Auftrags (wie <see cref="PostenFreigabeAngeordnet"/>).</summary>
public record KontingentEntlastungAngeordnet(Id<Kontingent.Kontingent> KontingentId, int Menge) : IEvent;

public record AuftragExistiertBereits(Id<Auftrag> AuftragId) : ITransientEvent;
public record AuftragNichtGefunden(Id<Auftrag> AuftragId) : ITransientEvent;
